#!/usr/bin/env bash
set -euo pipefail

check() {
  local file="$1" pattern="$2"
  if ! grep -Eq "$pattern" "$file"; then
    echo "missing pattern in $file: $pattern" >&2
    exit 1
  fi
}
absent() {
  local file="$1" pattern="$2"
  if grep -Eq "$pattern" "$file"; then
    echo "forbidden pattern in $file: $pattern" >&2
    exit 1
  fi
}

RUNTIME="backend/runtime/AuthorizedCliReaderPortRuntime.cs"
SCHEMA="backend/schema/CliReaderPortContracts.cs"
REPO="backend/repository/CliReaderPortRepository.cs"
NPGSQL="backend/repository/NpgsqlCliReaderPortRepository.cs"
DB="db/topology_tables.sql"
TEST="backend/tests/Topolactor.Runtime.Tests/AuthorizedCliReaderPortRuntimeTests.cs"
TODO=".agent/tasks/todo.md"
SSOT="docs/design/cli-model-context-protocols-port-ssot.yaml"
IMPL_SSOT="docs/design/cli-mcp-port-implementation-ssot.yaml"

check "$SSOT" "export_job"
check "$IMPL_SSOT" "export_job_schema_contract"
check "$RUNTIME" "create_export_job"
check "$RUNTIME" "CLI_READER_EXPORT_IDEMPOTENCY_KEY_REQUIRED"
check "$RUNTIME" "config.PortId"
check "$RUNTIME" "CLI_READER_EXPORT_SOURCE_RECORD_IDS_REQUIRED"
check "$RUNTIME" "ExtractSourceRecordIds"
check "$RUNTIME" "CreateExportJobAsync"
check "$SCHEMA" "CreateCliReaderExportJobCommand"
check "$SCHEMA" "CliReaderExportJobResult"
check "$REPO" "CreateExportJobAsync"
check "$SCHEMA" "Guid PortId"
check "$NPGSQL" "@port_id"
check "$NPGSQL" "topolactor://exports/\{exportJobId\}/manifest.json"
check "$NPGSQL" "ON CONFLICT \(idempotency_key\) DO NOTHING"
check "$NPGSQL" "true AS inserted"
check "$NPGSQL" "if \(inserted\)"
check "$NPGSQL" "topology.export_jobs"
check "$NPGSQL" "topology.export_manifests"
check "$NPGSQL" "topology.runtime_event_log"
check "$NPGSQL" "generated_files"
check "$NPGSQL" "source_record_ids"
check "$NPGSQL" "checksum"
check "$NPGSQL" "manifest_path"
check "$NPGSQL" "cli_mcp_export_job_created"
check "$DB" "port_id UUID NOT NULL UNIQUE"
check "$DB" "ADD COLUMN IF NOT EXISTS port_id"
check "$DB" "cli_reader_port_runtime_events_operation_check"
check "$DB" "create_export_job"
check "db/seed_empty.sql" "00000000-0000-0000-0000-00000000c100"
check "$DB" "ADD COLUMN IF NOT EXISTS generated_files"
check "$DB" "export_jobs_status_check"
check "$DB" "generated_files"
check "$DB" "approval_required"
check "$DB" "source_record_ids"
check "$TEST" "Create_export_job_records_manifest_checksum_source_ids_and_runtime_event"
check "$TEST" "Create_export_job_uses_config_port_id_not_payload_port_id"
check "$TEST" "Create_export_job_rejects_missing_source_record_ids"
check "$TODO" "cli-mcp-export-job-port"
check "$TODO" "Implemented evidence update"

absent "$RUNTIME" "ReadString\(payload, \"port_id\"\)|ReadString\(payload, \"portId\"\)|CLI_READER_EXPORT_PORT_ID_REQUIRED|download_export_file|FileStream|import_structured_output|create_commit_candidate|create_draft_operation|approval_execute|ApproveExport"
absent "$NPGSQL" "topolactor://exports/\{command.IdempotencyKey\}/manifest.json|download_export_file|FileStream|import_structured_output|create_commit_candidate|create_draft_operation|approval_execute|ApproveExport"

echo "cli-mcp-export-job-port guard passed"
