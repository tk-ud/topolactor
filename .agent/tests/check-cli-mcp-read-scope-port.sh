#!/usr/bin/env bash
set -euo pipefail

fail() { echo "ERROR: $*" >&2; exit 1; }
check() { rg -n "$2" "$1" >/dev/null || fail "missing '$2' in $1"; }
absent() { ! rg -n "$2" "$1" >/dev/null || fail "forbidden '$2' in $1"; }

RUNTIME="backend/runtime/AuthorizedCliReaderPortRuntime.cs"
REPO="backend/repository/CliReaderPortRepository.cs"
NPGSQL="backend/repository/NpgsqlCliReaderPortRepository.cs"
DB="db/topology_tables.sql"
SEED="db/seed_empty.sql"
TEST="backend/tests/Topolactor.Runtime.Tests/AuthorizedCliReaderPortRuntimeTests.cs"
PROGRAM="backend/Program.cs"

for f in "$RUNTIME" "$REPO" "$NPGSQL" "$DB" "$SEED" "$TEST"; do [ -f "$f" ] || fail "missing $f"; done

check "$RUNTIME" "manifestId is null"
check "$RUNTIME" "CLI_READER_DISPATCH_REQUIRED"
check "$RUNTIME" "read\", \"search\", \"aggregate\", \"analyze\", \"validate"
check "$RUNTIME" "CLI_READER_AUTH_REQUIRED"
check "$RUNTIME" "CLI_READER_ROLE_DENIED"
check "$RUNTIME" "CLI_READER_USER_DENIED"
check "$RUNTIME" "CLI_READER_TABLE_DENIED"
check "$RUNTIME" "CLI_READER_COLUMN_DENIED"
check "$RUNTIME" "CLI_READER_FILTER_DENIED"
check "$RUNTIME" "CLI_READER_PERIOD_DENIED"
check "$RUNTIME" "CLI_READER_ROW_SCOPE_UNRESOLVED"
check "$RUNTIME" "CLI_READER_CAPABILITY_UNRESOLVED"
check "$RUNTIME" "CLI_READER_BYPASS_OR_SECRET_FIELD"
check "$RUNTIME" "AppendRuntimeEventAsync"
check "$PROGRAM" "cli_reader_port_runtime"
check "$DB" "topology.cli_reader_ports"
check "$DB" "topology.cli_reader_port_runtime_events"
check "$DB" "no_plaintext_secret"
check "$SEED" "cli_reader_port.default"
check "$SEED" "secret_projection"
check "$TEST" "Rejects_non_dispatch_resolved_request"
check "$TEST" "Rejects_direct_sql_db_core_api_and_plaintext_credential_bypass_fields"
check "$TEST" "Success_operations_require_dispatch_and_authorized_scope"
absent "$RUNTIME" "CreateExportJob|FileStream|import_structured_output|create_commit_candidate|create_draft_operation"

echo "cli-mcp read scope port guard passed"
