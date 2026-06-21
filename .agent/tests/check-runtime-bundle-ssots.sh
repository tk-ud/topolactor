#!/usr/bin/env bash
# check-runtime-bundle-ssots.sh
# Verifies core runtime bundle SSOT files exist, contain required sections,
# and that extended-runtime-bundle-registry cross-references are consistent.
# Also checks that secret/credential patterns are not present in public SSOTs.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0

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
echo "=== Runtime Bundle SSOT file existence ==="

BUNDLE_SSOTS=(
  "docs/design/runtime-bundle-email-ssot.yaml"
  "docs/design/runtime-bundle-email-ssot.md"
  "docs/design/runtime-bundle-stripe-ssot.yaml"
  "docs/design/runtime-bundle-stripe-ssot.md"
  "docs/design/runtime-bundle-file-storage-ssot.yaml"
  "docs/design/runtime-bundle-file-storage-ssot.md"
  "docs/design/runtime-bundle-export-sftp-ssot.yaml"
  "docs/design/runtime-bundle-export-sftp-ssot.md"
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml"
  "docs/design/runtime-bundle-webhook-inbox-ssot.md"
  "docs/design/runtime-bundle-job-scheduler-ssot.yaml"
  "docs/design/runtime-bundle-job-scheduler-ssot.md"
  "docs/design/runtime-bundle-audit-approval-ssot.yaml"
  "docs/design/runtime-bundle-audit-approval-ssot.md"
  "docs/design/runtime-bundle-secret-credential-ssot.yaml"
  "docs/design/runtime-bundle-secret-credential-ssot.md"
)

for f in "${BUNDLE_SSOTS[@]}"; do
  check_file "$f"
done

echo ""
echo "=== Required sections in bundle SSOT YAMLs ==="

REQUIRED_KEYS=(
  "purpose"
  "authority_boundary"
  "trigger_kind"
  "allowed_side_effects"
  "prohibited_operations"
  "public_safe_config_policy"
  "secret_credential_boundary"
  "approval_boundary"
  "scheduler_boundary"
  "audit_log_boundary"
  "failure_policy"
  "idempotency_policy"
  "relation_to_extended_runtime_bundle_registry"
  "relation_to_cli_mcp_port"
  "explicitly_out_of_scope"
  "implementation_boundary_requirements"
)

for yaml in \
  "docs/design/runtime-bundle-email-ssot.yaml" \
  "docs/design/runtime-bundle-stripe-ssot.yaml" \
  "docs/design/runtime-bundle-file-storage-ssot.yaml" \
  "docs/design/runtime-bundle-export-sftp-ssot.yaml" \
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml" \
  "docs/design/runtime-bundle-job-scheduler-ssot.yaml" \
  "docs/design/runtime-bundle-audit-approval-ssot.yaml" \
  "docs/design/runtime-bundle-secret-credential-ssot.yaml"; do
  for key in "${REQUIRED_KEYS[@]}"; do
    check_content "$yaml" "$key"
  done
done

echo ""
echo "=== Email bundle: CLI/MCP send boundary ==="

check_content "docs/design/runtime-bundle-email-ssot.yaml" "email_send_from_cli_or_mcp"
check_content "docs/design/runtime-bundle-email-ssot.yaml" "email_send_is_explicitly_out_of_scope_for_cli_mcp_port"
check_content "docs/design/runtime-bundle-email-ssot.yaml" "ui_approval_confirmed"

echo ""
echo "=== Stripe bundle: webhook verification required ==="

check_content "docs/design/runtime-bundle-stripe-ssot.yaml" "webhook_verification_required: true"
check_content "docs/design/runtime-bundle-stripe-ssot.yaml" "paid_state_from_unverified_webhook"
check_content "docs/design/runtime-bundle-stripe-ssot.yaml" "verified_webhook_event_only"

echo ""
echo "=== Extended registry: core bundle owner assignments ==="

REGISTRY="docs/design/extended-runtime-bundle-registry-ssot.yaml"
check_content "$REGISTRY" "runtime-bundle-email-ssot"
check_content "$REGISTRY" "runtime-bundle-stripe-ssot"
check_content "$REGISTRY" "runtime-bundle-file-storage-ssot"
check_content "$REGISTRY" "runtime-bundle-export-sftp-ssot"

echo ""
echo "=== Extended registry: future bundle owner unresolved ==="

check_content "$REGISTRY" "future_bundle_owner_unassigned"
check_content "$REGISTRY" "unresolved_by_design"
check_content "$REGISTRY" "requires_future_specific_bundle_ssot"
check_content "$REGISTRY" "implementation_gate"

echo ""
echo "=== Extended registry: no progress management terms ==="

check_absent "$REGISTRY" "status: not_started"
check_absent "$REGISTRY" "status: partial"
check_absent "$REGISTRY" "status: implemented"
check_absent "$REGISTRY" "owner_status:"
check_absent "$REGISTRY" "core_bundle_status:"
check_absent "$REGISTRY" "implementation_status:"
check_absent "$REGISTRY" "assigned_to_design_ssot"

echo ""
echo "=== Webhook inbox bundle: scheduler boundary required ==="

check_content "docs/design/runtime-bundle-webhook-inbox-ssot.yaml" "webhook_direct_runtime_execution_without_scheduler"
check_content "docs/design/runtime-bundle-webhook-inbox-ssot.yaml" "scheduler_then_runtime_route_only"
check_content "docs/design/runtime-bundle-webhook-inbox-ssot.yaml" "signature_verification"

echo ""
echo "=== Job/Scheduler bundle: runtime destination selection not owned ==="

check_content "docs/design/runtime-bundle-job-scheduler-ssot.yaml" "runtime_destination_selection_by_scheduler"
check_content "docs/design/runtime-bundle-job-scheduler-ssot.yaml" "trigger_alignment_and_runtime_queue_only"
check_content "docs/design/runtime-bundle-job-scheduler-ssot.yaml" "manifest_dispatcher"

echo ""
echo "=== Audit/Approval bundle: CLI/MCP boundary preserved ==="

check_content "docs/design/runtime-bundle-audit-approval-ssot.yaml" "cli_mcp_read_export_boundary_violation"
check_content "docs/design/runtime-bundle-audit-approval-ssot.yaml" "ui_human_explicit_action_only"
check_content "docs/design/runtime-bundle-audit-approval-ssot.yaml" "export_job_approval_boundary"

echo ""
echo "=== Secret/Credential bundle: no real credential in SSOT ==="

check_content "docs/design/runtime-bundle-secret-credential-ssot.yaml" "db_guarded_vault_or_runtime_reference_not_public_ssot"
check_content "docs/design/runtime-bundle-secret-credential-ssot.yaml" "real_credential_in_public_ssot"
check_content "docs/design/runtime-bundle-secret-credential-ssot.yaml" "credential_in_audit_log_plaintext"

echo ""
echo "=== Extended registry: core bundle owner assignments (all 8) ==="

REGISTRY="docs/design/extended-runtime-bundle-registry-ssot.yaml"
check_content "$REGISTRY" "runtime-bundle-webhook-inbox-ssot"
check_content "$REGISTRY" "runtime-bundle-job-scheduler-ssot"
check_content "$REGISTRY" "runtime-bundle-audit-approval-ssot"
check_content "$REGISTRY" "runtime-bundle-secret-credential-ssot"

echo ""
echo "=== Secret/credential boundary declared in each SSOT ==="

for yaml in \
  "docs/design/runtime-bundle-email-ssot.yaml" \
  "docs/design/runtime-bundle-stripe-ssot.yaml" \
  "docs/design/runtime-bundle-file-storage-ssot.yaml" \
  "docs/design/runtime-bundle-export-sftp-ssot.yaml" \
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml" \
  "docs/design/runtime-bundle-job-scheduler-ssot.yaml" \
  "docs/design/runtime-bundle-audit-approval-ssot.yaml" \
  "docs/design/runtime-bundle-secret-credential-ssot.yaml"; do
  check_content "$yaml" "secret_credential_boundary"
  check_content "$yaml" "prohibited_in_public_ssot"
  if [[ "$yaml" == "docs/design/runtime-bundle-secret-credential-ssot.yaml" ]]; then
    check_content "$yaml" "db_guarded_vault_or_runtime_reference_not_public_ssot"
  else
    check_content "$yaml" "runtime_secret_store_not_in_public_ssot"
  fi
done

echo ""
echo "=== No real credential patterns in public SSOT files ==="

# Negative checks: detect patterns suggesting real credentials leaked into SSOTs
FORBIDDEN_PATTERNS=(
  "smtp.gmail.com"
  "smtp.sendgrid.net"
  "api.stripe.com"
  "sk_live_"
  "sk_test_"
  "pk_live_"
  "pk_test_"
  "whsec_"
  "s3.amazonaws.com"
  "storage.googleapis.com"
)

for yaml in \
  "docs/design/runtime-bundle-email-ssot.yaml" \
  "docs/design/runtime-bundle-stripe-ssot.yaml" \
  "docs/design/runtime-bundle-file-storage-ssot.yaml" \
  "docs/design/runtime-bundle-export-sftp-ssot.yaml" \
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml" \
  "docs/design/runtime-bundle-job-scheduler-ssot.yaml" \
  "docs/design/runtime-bundle-audit-approval-ssot.yaml" \
  "docs/design/runtime-bundle-secret-credential-ssot.yaml"; do
  for pat in "${FORBIDDEN_PATTERNS[@]}"; do
    check_absent "$yaml" "$pat"
  done
done

echo ""
echo "=== External port substrate SSOT existence ==="

check_file "docs/design/external-port-substrate-ssot.yaml"
check_content "docs/design/external-port-substrate-ssot.yaml" "external_port_substrate_ssot"
check_content "docs/design/external-port-substrate-ssot.yaml" "access_port"
check_content "docs/design/external-port-substrate-ssot.yaml" "response_port"
check_content "docs/design/external-port-substrate-ssot.yaml" "hook_port"
check_content "docs/design/external-port-substrate-ssot.yaml" "credential_requirement"
check_content "docs/design/external-port-substrate-ssot.yaml" "credential_kind"
check_content "docs/design/external-port-substrate-ssot.yaml" "reclassified_as_credential_requirement_substrate"

echo ""
echo "=== Secret/Credential bundle: reclassified as port substrate component ==="

check_content "docs/design/runtime-bundle-secret-credential-ssot.yaml" "reclassified_as_port_substrate_component"
check_content "docs/design/runtime-bundle-secret-credential-ssot.yaml" "port_substrate_relation"
check_absent "docs/design/runtime-bundle-secret-credential-ssot.yaml" "credential_rotation_service_design"
check_absent "docs/design/runtime-bundle-secret-credential-ssot.yaml" "credential_registration_ui_design"
check_absent "docs/design/runtime-bundle-secret-credential-ssot.yaml" "credential_validation_service_design"
check_absent "docs/design/runtime-bundle-secret-credential-ssot.yaml" "SecretCredentialBundleRuntime"

echo ""
echo "=== port_substrate_relation declared in each consumer bundle SSOT ==="

for yaml in \
  "docs/design/runtime-bundle-email-ssot.yaml" \
  "docs/design/runtime-bundle-stripe-ssot.yaml" \
  "docs/design/runtime-bundle-file-storage-ssot.yaml" \
  "docs/design/runtime-bundle-export-sftp-ssot.yaml" \
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml" \
  "docs/design/runtime-bundle-job-scheduler-ssot.yaml" \
  "docs/design/runtime-bundle-audit-approval-ssot.yaml" \
  "docs/design/runtime-bundle-secret-credential-ssot.yaml"; do
  check_content "$yaml" "port_substrate_relation"
  check_content "$yaml" "external-port-substrate-ssot.yaml"
done

echo ""
echo "=== Roadmap: all 8 consumer bundle entries present ==="

ROADMAP="docs/system-roadmap.yaml"
check_content "$ROADMAP" "product.external_port_substrate"
check_content "$ROADMAP" "product.email_port_consumer"
check_content "$ROADMAP" "product.stripe_port_consumer"
check_content "$ROADMAP" "product.file_storage_port_consumer"
check_content "$ROADMAP" "product.export_sftp_port_consumer"
check_content "$ROADMAP" "product.webhook_inbox_port_consumer"
check_content "$ROADMAP" "product.job_scheduler_port_consumer"
check_content "$ROADMAP" "product.audit_approval_port_consumer"

check_content "$ROADMAP" "external-port-substrate-implementation"
check_content "$ROADMAP" "external_port_policy_read_repository_and_consumer_runtime_wiring_implemented"
check_content "$ROADMAP" "consumer_bundle_physical_table_manifest_evidence_projection_runtime_connected"
check_content "$ROADMAP" "hook_port_receive_scheduler_enqueue_runtime_event_and_projection_guarded"
check_content "$ROADMAP" "consumer bundle physical table / manifest binding / evidence projection surfaces"
check_content "$ROADMAP" "email_physical_table_and_manifest_binding_not_connected"
check_content "$ROADMAP" "file_storage_physical_table_and_manifest_binding_not_connected"
check_content "$ROADMAP" "file_storage_checksum_and_manifest_runtime_not_connected"
check_content "$ROADMAP" "export_sftp_physical_table_and_manifest_binding_not_connected"
check_content "$ROADMAP" "export_sftp_checksum_and_manifest_verification_boundary_not_connected"
check_content "$ROADMAP" "stripe_physical_table_and_manifest_binding_not_connected"
check_absent "$ROADMAP" "external-port-substrate-design"
check_absent "$ROADMAP" "physical_table_ddl_design_not_started"
check_absent "$ROADMAP" "admin_role_port_write_ui_design_not_started"
check_absent "$ROADMAP" "consumer_bundle_port_connection_design_not_started"
check_absent '.agent/tasks/todo.md' '## Bundle `external-port-substrate-implementation`'
check_absent '.agent/tasks/todo.md' '`external-port-substrate-implementation` | external_port_substrate'
check_content ".agent/tasks/todo.md" "接続実装"
check_absent ".agent/tasks/todo.md" "external-port-substrate-design"
check_absent ".agent/tasks/todo.md" "接続設計"
check_content ".agent/tasks/external-port-substrate-implementation-todo.md" "external_port_substrate"

echo ""
echo "=== Design SSOTs: no progress management terms ==="

for yaml in \
  "docs/design/runtime-bundle-email-ssot.yaml" \
  "docs/design/runtime-bundle-stripe-ssot.yaml" \
  "docs/design/runtime-bundle-file-storage-ssot.yaml" \
  "docs/design/runtime-bundle-export-sftp-ssot.yaml" \
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml" \
  "docs/design/runtime-bundle-job-scheduler-ssot.yaml" \
  "docs/design/runtime-bundle-audit-approval-ssot.yaml" \
  "docs/design/runtime-bundle-secret-credential-ssot.yaml"; do
  check_absent "$yaml" "owner_status:"
  check_absent "$yaml" "core_bundle_status:"
  check_absent "$yaml" "future_implementation_requirements:"
done

echo ""
echo "=== secure_consumer_dispatch_lane_ref declared in each consumer bundle SSOT ==="

for yaml in \
  "docs/design/runtime-bundle-email-ssot.yaml" \
  "docs/design/runtime-bundle-stripe-ssot.yaml" \
  "docs/design/runtime-bundle-file-storage-ssot.yaml" \
  "docs/design/runtime-bundle-export-sftp-ssot.yaml" \
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml" \
  "docs/design/runtime-bundle-job-scheduler-ssot.yaml" \
  "docs/design/runtime-bundle-audit-approval-ssot.yaml"; do
  check_content "$yaml" "secure_consumer_dispatch_lane_ref"
  check_content "$yaml" "docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane"
  check_content "$yaml" "seed_binding_completion: pr460"
done

echo ""
echo "=== ssot-map.yaml: external_port_substrate wiring present ==="

check_content ".agent/docs/ssot-map.yaml" "external_port_substrate_ssot"
check_content ".agent/docs/ssot-map.yaml" "docs/design/external-port-substrate-ssot.yaml"

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "=== check-runtime-bundle-ssots.sh: all checks passed ==="
  exit 0
else
  echo "=== check-runtime-bundle-ssots.sh: $FAILURES check(s) failed ===" >&2
  exit 1
fi
