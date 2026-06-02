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
  "future_implementation_requirements"
)

for yaml in \
  "docs/design/runtime-bundle-email-ssot.yaml" \
  "docs/design/runtime-bundle-stripe-ssot.yaml" \
  "docs/design/runtime-bundle-file-storage-ssot.yaml" \
  "docs/design/runtime-bundle-export-sftp-ssot.yaml"; do
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
check_content "$REGISTRY" "assigned_to_design_ssot"

echo ""
echo "=== Extended registry: future bundle owner unresolved ==="

check_content "$REGISTRY" "future_bundle_owner_unassigned"
check_content "$REGISTRY" "unresolved_by_design"
check_content "$REGISTRY" "requires_future_specific_bundle_ssot"
check_content "$REGISTRY" "implementation_gate"

echo ""
echo "=== Secret/credential boundary declared in each SSOT ==="

for yaml in \
  "docs/design/runtime-bundle-email-ssot.yaml" \
  "docs/design/runtime-bundle-stripe-ssot.yaml" \
  "docs/design/runtime-bundle-file-storage-ssot.yaml" \
  "docs/design/runtime-bundle-export-sftp-ssot.yaml"; do
  check_content "$yaml" "secret_credential_boundary"
  check_content "$yaml" "prohibited_in_public_ssot"
  check_content "$yaml" "runtime_secret_store_not_in_public_ssot"
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
  "docs/design/runtime-bundle-export-sftp-ssot.yaml"; do
  for pat in "${FORBIDDEN_PATTERNS[@]}"; do
    check_absent "$yaml" "$pat"
  done
done

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "=== check-runtime-bundle-ssots.sh: all checks passed ==="
  exit 0
else
  echo "=== check-runtime-bundle-ssots.sh: $FAILURES check(s) failed ===" >&2
  exit 1
fi
