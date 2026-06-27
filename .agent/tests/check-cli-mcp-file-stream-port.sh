#!/usr/bin/env bash
set -euo pipefail

# Guard for SubBundle `cli-mcp-file-stream-port` under parent bundle
# `cli-mcp-dispatch-secured-port`. Scope: authorized export-job file stream
# (download_export_file / topolactor://exports/{id}/file), checksum verification
# against the canonical export_jobs / export_manifests ledger, fail-close on
# unauthorized / disabled / checksum-mismatch, and success/failure runtime evidence.
# It must NOT introduce a new export job, re-read rows, import structured output,
# create draft/commit candidates, execute approval, or project credentials.

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
SEED="db/seed_empty.sql"
TEST="backend/tests/Topolactor.Runtime.Tests/AuthorizedCliReaderPortRuntimeTests.cs"
TODO=".agent/tasks/todo.md"
SSOT="docs/design/cli-model-context-protocols-port-ssot.yaml"
IMPL_SSOT="docs/design/cli-mcp-port-implementation-ssot.yaml"
FILE_SSOT="docs/design/runtime-bundle-file-storage-ssot.yaml"

for f in "$RUNTIME" "$SCHEMA" "$REPO" "$NPGSQL" "$DB" "$SEED" "$TEST" "$TODO" "$SSOT" "$IMPL_SSOT" "$FILE_SSOT"; do
  [ -f "$f" ] || { echo "missing $f" >&2; exit 1; }
done

# SSOT boundary anchors
check "$SSOT" "download_export_file"
check "$SSOT" "topolactor://exports/\{export_job_id\}/file"
check "$IMPL_SSOT" "file_stream_contract"
check "$FILE_SSOT" "cli_mcp_file_stream_policy"

# Runtime: dispatch-secured file stream lane through cli_reader_port_runtime
check "$RUNTIME" "download_export_file"
check "$RUNTIME" "manifestId is null"
check "$RUNTIME" "CLI_READER_DISPATCH_REQUIRED"
check "$RUNTIME" "DownloadExportFileAsync"
check "$RUNTIME" "VerifyExportChecksum"
check "$RUNTIME" "BuildExportResourceResponse"
check "$RUNTIME" "LoadAuthorizedExportFileAsync"
check "$RUNTIME" "RecordExportDownloadEvidenceAsync"
check "$RUNTIME" "RecordExportDownloadFailureEvidenceAsync"
check "$RUNTIME" "config.FileStreamEnabled"
check "$RUNTIME" "CLI_READER_FILE_STREAM_DISABLED"
check "$RUNTIME" "CLI_READER_FILE_JOB_UNAUTHORIZED"
check "$RUNTIME" "CLI_READER_FILE_JOB_NOT_READY"
check "$RUNTIME" "CLI_READER_FILE_APPROVAL_REQUIRED"
check "$RUNTIME" "CLI_READER_FILE_CHECKSUM_MISMATCH"
check "$RUNTIME" "CLI_READER_FILE_SOURCE_IDS_MISMATCH"
check "$RUNTIME" "CLI_READER_FILE_MANIFEST_MISSING"
check "$RUNTIME" "CLI_READER_FILE_ARTIFACT_MISSING"
check "$RUNTIME" "CLI_READER_FILE_CHECKSUM_VERIFIED"
check "$RUNTIME" "CLI_READER_FILE_DOWNLOAD_COMPLETED"
check "$RUNTIME" "topolactor://exports/"
# approval is read-only, never executed
check "$RUNTIME" "ApprovalRequired"

# Schema contracts
check "$SCHEMA" "LoadAuthorizedExportFileQuery"
check "$SCHEMA" "AuthorizedExportFile"
check "$SCHEMA" "FileStreamEnabled"
check "$SCHEMA" "ManifestSourceRecordIds"

# Repository abstraction
check "$REPO" "LoadAuthorizedExportFileAsync"
check "$REPO" "RecordExportDownloadEvidenceAsync"
check "$REPO" "RecordExportDownloadFailureEvidenceAsync"

# Npgsql repository: read from canonical ledger only, port + user authorization,
# runtime_event_log evidence, no new export job creation in the file stream path.
check "$NPGSQL" "LoadAuthorizedExportFileAsync"
check "$NPGSQL" "RecordExportDownloadEvidenceAsync"
check "$NPGSQL" "RecordExportDownloadFailureEvidenceAsync"
check "$NPGSQL" "file_stream_enabled"
check "$NPGSQL" "topology.export_jobs"
check "$NPGSQL" "topology.export_manifests"
check "$NPGSQL" "j.port_id = @port_id"
check "$NPGSQL" "j.requested_by = @requested_by"
check "$NPGSQL" "topology.runtime_event_log"
check "$NPGSQL" "cli_mcp_file_download_completed"
check "$NPGSQL" "cli_mcp_file_checksum_verified"
check "$NPGSQL" "cli_mcp_file_checksum_mismatch_rejected"
check "$NPGSQL" "cli_mcp_file_source_ids_mismatch_rejected"
check "$NPGSQL" "cli_mcp_file_manifest_missing_rejected"
check "$NPGSQL" "cli_mcp_file_artifact_missing_rejected"
check "$NPGSQL" "ExtractManifestSourceRecordIds"

# DB schema: file stream scope field + runtime event operation support
check "$DB" "file_stream_enabled BOOLEAN NOT NULL DEFAULT FALSE"
check "$DB" "ADD COLUMN IF NOT EXISTS file_stream_enabled"
check "$DB" "download_export_file"
check "$SEED" "file_stream_enabled"

# Tests
check "$TEST" "Download_export_file_streams_authorized_job_with_checksum_and_download_evidence"
check "$TEST" "Download_export_file_fail_close_on_checksum_mismatch"
check "$TEST" "Download_export_file_fail_close_on_source_record_ids_mismatch"
check "$TEST" "Download_export_file_records_failure_evidence_for_manifest_and_artifact_mismatch"
check "$TEST" "DownloadFailureEvidence"
check "$TEST" "Download_export_file_fail_close_when_job_not_authorized"
check "$TEST" "Download_export_file_fail_close_when_file_stream_disabled"
check "$TEST" "Download_export_file_rejects_non_dispatch_resolved_request"
check "$TEST" "Download_export_file_response_excludes_credential_bucket_endpoint_and_signed_url"

# TODO bundle-level evidence
check "$TODO" "cli-mcp-file-stream-port"
check "$TODO" "Implemented evidence update"

# Out-of-scope axes must not appear in the file stream surfaces.
absent "$RUNTIME" "import_structured_output|create_draft_operation|create_commit_candidate|approval_execute|ApproveExport|ExecuteApproval"
absent "$NPGSQL" "import_structured_output|create_draft_operation|create_commit_candidate|approval_execute|ApproveExport"
# File stream read path must not project storage secrets / signed URLs.
absent "$NPGSQL" "signed_url|bucket_name|storage_endpoint|access_key|secret_key"
# The file stream path keys off an existing export_job_id; it must not call create_export_job
# from inside DownloadExportFileAsync (the no-re-create invariant is asserted by runtime tests).
absent "$NPGSQL" "DownloadExportFileAsync"

echo "cli-mcp-file-stream-port guard passed"
