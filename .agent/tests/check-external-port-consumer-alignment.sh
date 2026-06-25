#!/usr/bin/env bash
# check-external-port-consumer-alignment.sh
# Guards against misimplementation of consumer bundles relative to
# external_port_substrate secure_consumer_dispatch_lane invariant.
#
# Detects:
# 1. consumer bundle SSOT not referencing secure_consumer_dispatch_lane
# 2. TODO still has pre-PR#460 seed-binding-unstarted language
# 3. provider-specific runtime / client / bundle-specific runtime additions
# 4. frontend credential exposure
# 5. dispatch payload containing credential / vault value
# 6. portTargetRef lane bypass
# 7. file/export bundles missing checksum / manifest
# 8. evidence-required bundles missing physical table manifest / evidence / runtime_event_log
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

pass() {
  echo "OK  : $1"
}

check_content() {
  local file="$REPO_ROOT/$1"
  local term="$2"
  if [ ! -f "$file" ]; then
    fail "Content check skipped (file missing): $1 — expected term: $term"
    return
  fi
  if grep -qF -- "$term" "$file"; then
    pass "$1 → \"$term\""
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
    pass "$1 does not contain \"$term\""
  fi
}

echo ""
echo "=== 1. Consumer bundle SSOTs reference secure_consumer_dispatch_lane ==="

CONSUMER_SSOTS=(
  "docs/design/runtime-bundle-file-storage-ssot.yaml"
  "docs/design/runtime-bundle-email-ssot.yaml"
  "docs/design/runtime-bundle-stripe-ssot.yaml"
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml"
  "docs/design/runtime-bundle-audit-approval-ssot.yaml"
  "docs/design/runtime-bundle-export-sftp-ssot.yaml"
)

for ssot in "${CONSUMER_SSOTS[@]}"; do
  check_content "$ssot" "secure_consumer_dispatch_lane_ref"
  check_content "$ssot" "docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane"
  check_content "$ssot" "seed_binding_completion: pr460"
done

echo ""
echo "=== 1b. external-port-substrate-ssot.yaml has secure_consumer_dispatch_lane ==="

check_content "docs/design/external-port-substrate-ssot.yaml" "secure_consumer_dispatch_lane:"
check_content "docs/design/external-port-substrate-ssot.yaml" "prohibited_at_consumer_bundle"
check_content "docs/design/external-port-substrate-ssot.yaml" "provider_specific_runtime_implementation"
check_content "docs/design/external-port-substrate-ssot.yaml" "portTargetRef_bypass_via_new_payload_lane"
check_content "docs/design/external-port-substrate-ssot.yaml" "frontend_credential_exposure"

echo ""
echo "=== 2. TODO does not have pre-PR#460 seed-binding-unstarted language ==="

# These phrases imply seed binding is not yet started — forbidden after PR#460
FORBIDDEN_TODO_PHRASES=(
  "seed / DB record / projection 接続を実装する"
  "credential_kind を external として port record に付属させる実装を追加する"
  "consumer bundle seed binding を追加する"
)

TODO_FILE=".agent/tasks/todo.md"
for phrase in "${FORBIDDEN_TODO_PHRASES[@]}"; do
  if grep -qF -- "$phrase" "$REPO_ROOT/$TODO_FILE"; then
    fail "Pre-PR#460 seed-binding-unstarted language found in $TODO_FILE: \"$phrase\""
  else
    pass "$TODO_FILE does not contain forbidden phrase: \"$phrase\""
  fi
done

echo ""
echo "=== 3. No provider-specific runtime / client class additions in backend ==="

# Detect provider-specific C# runtime/service/client classes
PROVIDER_CLASS_PATTERNS=(
  "class SmtpEmailService"
  "class SmtpEmailClient"
  "class StripeWebhookService"
  "class StripeWebhookClient"
  "class SftpTransferService"
  "class SftpTransferClient"
  "class S3StorageService"
  "class S3StorageClient"
  "class SendGridService"
  "class SendGridClient"
  "class WebhookInboxProviderService"
  "class ExternalSchedulerProviderService"
  "class ExternalSchedulerReceiver"
  "class ExternalSchedulerClient"
  "class AuditApprovalNotificationService"
  "class SmtpBundle"
  "class StripeBundle"
  "class SftpBundle"
  "class S3Bundle"
)

if [ -d "$REPO_ROOT/backend/runtime" ]; then
  for pattern in "${PROVIDER_CLASS_PATTERNS[@]}"; do
    if grep -rl "$pattern" "$REPO_ROOT/backend/runtime" 2>/dev/null | grep -q .; then
      fail "Provider-specific runtime class found: \"$pattern\" in backend/runtime"
    else
      pass "No \"$pattern\" in backend/runtime"
    fi
  done
fi

echo ""
echo "=== 4. No frontend credential exposure ==="

# Detect plaintext credential fields being assigned in frontend
FRONTEND_DIRS=()
[ -d "$REPO_ROOT/frontend" ] && FRONTEND_DIRS+=("$REPO_ROOT/frontend")

if [ ${#FRONTEND_DIRS[@]} -gt 0 ]; then
  FRONTEND_CREDENTIAL_PATTERNS=(
    "smtp_password"
    "stripe_secret_key"
    "webhook_signing_key_value"
    "sftp_private_key_value"
    "s3_secret_access_key"
    "client_secret_value"
    "access_token_plaintext"
    "refresh_token_plaintext"
  )
  for pat in "${FRONTEND_CREDENTIAL_PATTERNS[@]}"; do
    if grep -rl "$pat" "${FRONTEND_DIRS[@]}" 2>/dev/null | grep -q .; then
      fail "Possible frontend credential exposure: \"$pat\" found in frontend/"
    else
      pass "No \"$pat\" in frontend/"
    fi
  done
fi

echo ""
echo "=== 5. Dispatch payload does not contain credential / vault value ==="

# Detect if dispatch payloads are including credential fields directly
CREDENTIAL_PAYLOAD_PATTERNS=(
  "\"credential\":"
  "\"vault_payload\":"
  "\"reference_key\":"
  "\"encrypted_payload\":"
  "\"smtp_password\":"
  "\"sftp_key\":"
  "\"stripe_secret\":"
  "credential_plaintext"
)

if [ -d "$REPO_ROOT/backend/runtime" ]; then
  for pat in "${CREDENTIAL_PAYLOAD_PATTERNS[@]}"; do
    if grep -rl "$pat" "$REPO_ROOT/backend/runtime" 2>/dev/null | grep -q .; then
      fail "Credential value in dispatch payload: \"$pat\" found in backend/runtime"
    else
      pass "No \"$pat\" in backend/runtime dispatch"
    fi
  done
fi

echo ""
echo "=== 6. portTargetRef lane not bypassed ==="

# The consumer dispatch path must use port_target_ref / portTargetRef
# Detect canonical_binding_ which was the bypassed pattern (cleaned up post PR#458/#459)
if [ -d "$REPO_ROOT/backend/runtime" ]; then
  if grep -rl "canonical_binding_" "$REPO_ROOT/backend/runtime" 2>/dev/null | grep -q .; then
    fail "canonical_binding_ found in backend/runtime — consumer dispatch must use port_target_ref lane only"
  else
    pass "No canonical_binding_ in backend/runtime"
  fi
fi

echo ""
echo "=== 7. File/export bundles: checksum and manifest declared in SSOT ==="

check_content "docs/design/runtime-bundle-file-storage-ssot.yaml" "checksum"
check_content "docs/design/runtime-bundle-file-storage-ssot.yaml" "manifest"
check_content "docs/design/runtime-bundle-file-storage-ssot.yaml" "file_write_without_checksum"
check_content "docs/design/runtime-bundle-export-sftp-ssot.yaml" "checksum"
check_content "docs/design/runtime-bundle-export-sftp-ssot.yaml" "manifest"
check_content "docs/design/runtime-bundle-export-sftp-ssot.yaml" "sftp_push_without_checksum"
check_content "docs/design/runtime-bundle-export-sftp-ssot.yaml" "sftp_push_without_manifest"

# file/export TODO must include checksum/manifest remaining items
check_content ".agent/tasks/todo.md" "checksum"
check_content ".agent/tasks/todo.md" "manifest"

echo ""
echo "=== 8. Evidence-required bundles: physical table manifest / evidence / runtime_event_log declared ==="

EVIDENCE_REQUIRED_SSOTS=(
  "docs/design/runtime-bundle-email-ssot.yaml"
  "docs/design/runtime-bundle-audit-approval-ssot.yaml"
  "docs/design/runtime-bundle-stripe-ssot.yaml"
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml"
)

for ssot in "${EVIDENCE_REQUIRED_SSOTS[@]}"; do
  check_content "$ssot" "runtime_event_log"
  check_content "$ssot" "audit_log_boundary"
done

# secure_consumer_dispatch_lane_ref remaining_scope must include physical_table and evidence
# job_scheduler is excluded from physical_table check: no new DB table is created (in-memory queue only)
for ssot in "${CONSUMER_SSOTS[@]}"; do
  if [[ "$ssot" != "docs/design/runtime-bundle-job-scheduler-ssot.yaml" ]]; then
    check_content "$ssot" "physical_table"
  fi
  check_content "$ssot" "evidence"
done

echo ""
echo "=== 9. job_scheduler built-in scheduler independence ==="

ROADMAP="docs/system-roadmap.yaml"
check_content "docs/design/runtime-bundle-job-scheduler-ssot.yaml" "built_in_scheduler_independence"
check_content "docs/design/runtime-bundle-job-scheduler-ssot.yaml" "topolactor 内蔵 scheduler"
check_content "docs/design/runtime-bundle-job-scheduler-ssot.yaml" "RuntimeTimelineScheduler must not depend on external_port_substrate port records"
check_content "docs/design/runtime-bundle-job-scheduler-ssot.yaml" "generic hook_port intake"
check_content "$ROADMAP" "standalone_external_scheduler_port_consumer_removed"

echo ""
echo "=== 10. Roadmap closed_gap_ref for seed binding ==="

check_content "$ROADMAP" "consumer_bundle_seed_binding_implemented_per_pr460_all_7_bundles"
check_content "$ROADMAP" "email_seed_binding_and_credential_requirement_implemented_per_pr460"
check_content "$ROADMAP" "stripe_seed_binding_and_credential_requirement_implemented_per_pr460"
check_content "$ROADMAP" "file_storage_seed_binding_and_credential_requirement_implemented_per_pr460"
check_content "$ROADMAP" "export_sftp_seed_binding_and_credential_requirement_implemented_per_pr460"
check_content "$ROADMAP" "webhook_inbox_seed_binding_and_credential_requirement_implemented_per_pr460"
check_content "$ROADMAP" "audit_approval_seed_binding_and_credential_requirement_implemented_per_pr460"

echo ""
echo "=== 11. Forbidden vocabulary guard (job_queue physical table / 8 Bundle 共通基盤 / external 系 8 Bundle) ==="

SSOT_AND_TASK_FILES=(
  "docs/design/runtime-bundle-job-scheduler-ssot.md"
  "docs/design/external-port-substrate-ssot.yaml"
  "docs/design/extended-runtime-bundle-registry-ssot.yaml"
  ".agent/tasks/todo.md"
  ".agent/tasks/external-port-substrate-implementation-todo.md"
  "docs/system-roadmap.yaml"
)

FORBIDDEN_VOCAB=(
  "job_queue_schema_design"
  "job_queue physical table"
  "job queue physical table"
  "external 系 8 Bundle"
  "8 Bundle 共通基盤"
)

for f in "${SSOT_AND_TASK_FILES[@]}"; do
  for term in "${FORBIDDEN_VOCAB[@]}"; do
    check_absent "$f" "$term"
  done
done

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "=== check-external-port-consumer-alignment.sh: all checks passed ==="
  exit 0
else
  echo "=== check-external-port-consumer-alignment.sh: $FAILURES check(s) failed ===" >&2
  exit 1
fi
