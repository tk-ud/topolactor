#!/usr/bin/env bash
# check-cli-mcp-port-implementation-ssot.sh
# Verifies CLI/MCP Port implementation SSOT boundary constraints.
# Checks: prohibited operations, export_job required fields, MCP tool/resource names,
# audit_log required fields, approval boundary, bundle relations.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0
SSOT_YAML="docs/design/cli-mcp-port-implementation-ssot.yaml"
SSOT_MD="docs/design/cli-mcp-port-implementation-ssot.md"

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

check_file() {
  local f="$REPO_ROOT/$1"
  if [ -f "$f" ]; then
    echo "OK  [file] $1"
  else
    fail "File missing: $1"
  fi
}

check_content() {
  local file="$REPO_ROOT/$1"
  local term="$2"
  if [ ! -f "$file" ]; then
    fail "Content check skipped (file missing): $1 — expected term: $term"
    return
  fi
  if grep -qF -- "$term" "$file"; then
    echo "OK  [term] $1 → \"$term\""
  else
    fail "Term not found in $1: \"$term\""
  fi
}

check_absent() {
  local file="$REPO_ROOT/$1"
  local term="$2"
  if [ ! -f "$file" ]; then
    return
  fi
  if grep -qF -- "$term" "$file"; then
    fail "Forbidden term found in $1: \"$term\""
  else
    echo "OK  [absent] $1 does not contain \"$term\""
  fi
}

echo ""
echo "=== CLI/MCP Port implementation SSOT file existence ==="
check_file "$SSOT_YAML"
check_file "$SSOT_MD"

echo ""
echo "=== Required sections in implementation SSOT YAML ==="
REQUIRED_SECTIONS=(
  "purpose"
  "authority_boundary"
  "implementation_scope"
  "data_reader_contract"
  "context_api_contract"
  "export_job_schema_contract"
  "file_stream_contract"
  "mcp_tool_contract"
  "mcp_resource_contract"
  "audit_log_contract"
  "approval_boundary"
  "secret_credential_boundary"
  "prohibited_operations"
  "failure_policy"
  "idempotency_policy"
  "relation_to_cli_model_context_protocols_port_ssot"
  "relation_to_file_storage_bundle"
  "relation_to_export_sftp_bundle"
  "relation_to_audit_approval_bundle"
  "relation_to_secret_credential_bundle"
  "implementation_boundary_requirements"
)

for section in "${REQUIRED_SECTIONS[@]}"; do
  check_content "$SSOT_YAML" "$section"
done

echo ""
echo "=== Prohibited operations: direct DB connection ==="
check_content "$SSOT_YAML" "direct_db_connection"
check_content "$SSOT_MD" "direct DB connection"

echo ""
echo "=== Prohibited operations: direct SQL execution ==="
check_content "$SSOT_YAML" "direct_sql_execution"
check_content "$SSOT_MD" "direct SQL execution"

echo ""
echo "=== Prohibited operations: CLI/MCP approval ==="
check_content "$SSOT_YAML" "cli_mcp_approval_trigger"
check_content "$SSOT_MD" "approval execution"

echo ""
echo "=== Prohibited operations: credential read ==="
check_content "$SSOT_YAML" "cli_mcp_credential_read"
check_content "$SSOT_MD" "credential read"

echo ""
echo "=== export_job required fields ==="
EXPORT_JOB_FIELDS=(
  "export_job_id"
  "port_id"
  "requested_by"
  "requested_at"
  "period"
  "target_scope"
  "export_format"
  "status"
  "source_record_ids"
  "generated_files"
  "checksum"
  "manifest_path"
  "completed_at"
  "idempotency_key"
  "approval_required"
  "approval_status"
)

for field in "${EXPORT_JOB_FIELDS[@]}"; do
  check_content "$SSOT_YAML" "$field"
done

echo ""
echo "=== MCP tool names ==="
MCP_TOOLS=(
  "get_monthly_context"
  "search_records"
  "aggregate_records"
  "validate_export"
  "create_export_job"
  "download_export_file"
  "get_export_status"
)

for tool in "${MCP_TOOLS[@]}"; do
  check_content "$SSOT_YAML" "$tool"
done

echo ""
echo "=== MCP resource URIs ==="
check_content "$SSOT_YAML" "topolactor://monthly/{period}/context.json"
check_content "$SSOT_YAML" "topolactor://exports/{export_job_id}/manifest.json"
check_content "$SSOT_YAML" "topolactor://exports/{export_job_id}/file"

echo ""
echo "=== audit_log required fields ==="
AUDIT_FIELDS=(
  "user_id"
  "role"
  "client_type"
  "tool_name"
  "resource_uri"
  "query_hash"
  "period"
  "target_table"
  "target_scope"
  "result_count"
  "export_job_id"
  "executed_at"
  "ip_address"
  "user_agent"
)

for field in "${AUDIT_FIELDS[@]}"; do
  check_content "$SSOT_YAML" "$field"
done

echo ""
echo "=== approval boundary: UI/Human only ==="
check_content "$SSOT_YAML" "ui_human_only_operations"
check_content "$SSOT_YAML" "approval_boundary"

echo ""
echo "=== Bundle relations declared ==="
check_content "$SSOT_YAML" "relation_to_file_storage_bundle"
check_content "$SSOT_YAML" "relation_to_export_sftp_bundle"
check_content "$SSOT_YAML" "relation_to_audit_approval_bundle"
check_content "$SSOT_YAML" "relation_to_secret_credential_bundle"

echo ""
echo "=== out_of_scope declared in MD ==="
check_content "$SSOT_MD" "Out of Scope"

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "=== check-cli-mcp-port-implementation-ssot.sh: all checks passed ==="
  exit 0
else
  echo "=== check-cli-mcp-port-implementation-ssot.sh: $FAILURES check(s) failed ===" >&2
  exit 1
fi
