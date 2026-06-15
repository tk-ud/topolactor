#!/usr/bin/env bash
# check-design-ssot-progress-terms.sh
# Verifies that design SSOTs (docs/design/ and .agent/docs/) outside of
# docs/system-roadmap.yaml and .agent/tasks/todo.md do not contain progress
# management terms that belong exclusively in the Roadmap.
#
# NOTE: Document-level metadata like "status: ssot_design_document" or
# "status: subordinate_design_document" is NOT forbidden — these are
# file-type classifiers, not progress trackers.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

check_absent() {
  local file="$REPO_ROOT/$1"
  local term="$2"
  if [ ! -f "$file" ]; then
    return
  fi
  if grep -qF -- "$term" "$file"; then
    fail "Progress term found in $1: \"$term\""
  else
    echo "OK  [absent] $1 ∌ \"$term\""
  fi
}

FORBIDDEN_PROGRESS_TERMS=(
  "status: not_started"
  "status: partial"
  "status: implemented"
  "status: production_ready"
  "status: future_bundle_candidate"
  "status: acceptance_pending"
  "status: planned"
  "status: skeleton"
  "owner_status:"
  "core_bundle_status:"
  "implementation_status:"
  "future_implementation_requirements:"
  "assigned_to_design_ssot"
  "covered_by_this_pr"
)

DESIGN_SSOTS=(
  "docs/design/admin-console-workflow-ssot.yaml"
  "docs/design/admin-master-roster-management-ssot.yaml"
  "docs/design/auth-db-session-credential-ssot.yaml"
  "docs/design/cli-mcp-port-implementation-ssot.yaml"
  "docs/design/cli-model-context-protocols-port-ssot.yaml"
  "docs/design/commit-inference-engine.yaml"
  "docs/design/css-dictionary-ssot.yaml"
  "docs/design/enum-dictionary-ssot.yaml"
  "docs/design/extended-runtime-bundle-registry-ssot.yaml"
  "docs/design/external-port-substrate-ssot.yaml"
  "docs/design/mock-preset-intake-compiler-ssot.yaml"
  "docs/design/pipeline-continuity-ssot.yaml"
  "docs/design/runtime-bundle-audit-approval-ssot.yaml"
  "docs/design/runtime-bundle-email-ssot.yaml"
  "docs/design/runtime-bundle-export-sftp-ssot.yaml"
  "docs/design/runtime-bundle-file-storage-ssot.yaml"
  "docs/design/runtime-bundle-job-scheduler-ssot.yaml"
  "docs/design/runtime-bundle-secret-credential-ssot.yaml"
  "docs/design/runtime-bundle-stripe-ssot.yaml"
  "docs/design/runtime-bundle-webhook-inbox-ssot.yaml"
  "docs/design/runtime-orchestration-ssot.yaml"
  "docs/design/sql-attention-logs-ssot.yaml"
  "docs/design/team-markdown-dashboard-saved-view-ssot.yaml"
  "docs/design/ui-builder-preset-ecosystem-ssot.yaml"
  "docs/design/user-facing-helper-manual-ssot.yaml"
)

AGENT_SSOTS=(
  ".agent/docs/ssot-map.yaml"
  ".agent/docs/required-paths.yaml"
  ".agent/docs/structure-map.yaml"
)

echo ""
echo "=== Design SSOTs: no progress management terms ==="

for f in "${DESIGN_SSOTS[@]}"; do
  for term in "${FORBIDDEN_PROGRESS_TERMS[@]}"; do
    check_absent "$f" "$term"
  done
done

echo ""
echo "=== Agent SSOT docs: no progress management terms ==="

for f in "${AGENT_SSOTS[@]}"; do
  for term in "${FORBIDDEN_PROGRESS_TERMS[@]}"; do
    check_absent "$f" "$term"
  done
done

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "=== check-design-ssot-progress-terms.sh: all checks passed ==="
  exit 0
else
  echo "=== check-design-ssot-progress-terms.sh: $FAILURES check(s) failed ===" >&2
  exit 1
fi
