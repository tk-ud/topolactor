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
DISPATCH_AUTH="backend/endpoint/DispatchAuthContext.cs"
SSOT="docs/design/runtime-orchestration-ssot.yaml"

for f in "$RUNTIME" "$REPO" "$NPGSQL" "$DB" "$SEED" "$TEST" "$DISPATCH_AUTH" "$SSOT"; do [ -f "$f" ] || fail "missing $f"; done

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
check "$RUNTIME" "ReadContextSet"
check "$RUNTIME" "authenticated_user_id"
check "$RUNTIME" "CLI_READER_BYPASS_OR_SECRET_FIELD"
check "$RUNTIME" "AppendRuntimeEventAsync"
check "$PROGRAM" "DispatchAuthContext.ApplyJwtAuthority"
check "$PROGRAM" "TryGetCapabilities"
check "$DISPATCH_AUTH" "ReservedAuthContextKeys"
check "$DISPATCH_AUTH" "authenticated_user_id"
check "$PROGRAM" "cli_reader_port_runtime"
check "$SSOT" "cli_reader_port_runtime"
python3 - <<'PY'
from pathlib import Path

ssot = Path("docs/design/runtime-orchestration-ssot.yaml").read_text().splitlines()
program = Path("backend/Program.cs").read_text()

def read_yaml_list(key: str) -> list[str]:
    for idx, line in enumerate(ssot):
        if line.strip() == f"{key}:":
            base_indent = len(line) - len(line.lstrip())
            items: list[str] = []
            for candidate in ssot[idx + 1:]:
                stripped = candidate.strip()
                if not stripped or stripped.startswith("#"):
                    continue
                indent = len(candidate) - len(candidate.lstrip())
                if indent <= base_indent:
                    break
                if stripped.startswith("- "):
                    items.append(stripped[2:].strip())
            return items
    raise SystemExit(f"missing {key} in runtime-orchestration SSOT")

backend_runtime_destinations = set(read_yaml_list("backend_runtime_destinations"))
backend_dispatchable_kinds = set(read_yaml_list("backend_dispatchable_kinds"))
expected_dispatchable = backend_runtime_destinations - {"auth_runtime"}

if backend_dispatchable_kinds != expected_dispatchable:
    missing = sorted(expected_dispatchable - backend_dispatchable_kinds)
    extra = sorted(backend_dispatchable_kinds - expected_dispatchable)
    raise SystemExit(
        "backend_dispatchable_kinds must match backend_runtime_destinations minus auth_runtime; "
        f"missing={missing} extra={extra}"
    )

for runtime_destination in sorted(backend_dispatchable_kinds):
    needle = f'["{runtime_destination}"]'
    if needle not in program:
        raise SystemExit(f"Program.cs handler registry missing {runtime_destination}")
PY
check "$DB" "topology.cli_reader_ports"
check "$DB" "topology.cli_reader_port_runtime_events"
check "$DB" "no_plaintext_secret_value"
check "$SEED" "cli_reader_port.default"
check "$SEED" "secret_projection"
check "$TEST" "Rejects_non_dispatch_resolved_request"
check "$TEST" "Rejects_direct_sql_db_core_api_and_plaintext_credential_bypass_fields"
check "$TEST" "Success_operations_require_dispatch_and_authorized_scope"
check "$TEST" "Rejects_client_supplied_user_role_and_capability_authority_in_payload"
check "backend/tests/Topolactor.Runtime.Tests/DispatchRoleAuthorityTests.cs" "ForgedClientContext_DoesNotSatisfyCliReaderCapability_ButJwtResolvedContextDoes"
absent "$RUNTIME" "CreateExportJob|FileStream|import_structured_output|create_commit_candidate|create_draft_operation"
absent "$NPGSQL" "authorized:|placeholder"

echo "cli-mcp read scope port guard passed"
