#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

ok() {
  echo "OK  : $1"
}

must_have() {
  local file="$1"
  local term="$2"
  if rg -n -F -- "$term" "$REPO_ROOT/$file" >/dev/null; then
    ok "$file -> $term"
  else
    fail "$file missing term: $term"
  fi
}

check_file() {
  local file="$1"
  if [ -f "$REPO_ROOT/$file" ]; then
    ok "file exists: $file"
  else
    fail "file missing: $file"
  fi
}

echo "=== system CI admin runtime callable SSOT wiring check ==="

TARGET_SSOT="docs/design/system-ci-admin-runtime-callable-surface.yaml"
check_file "$TARGET_SSOT"

# ssot-map discovery
must_have ".agent/docs/ssot-map.yaml" "work_type: system_ci_admin_runtime_callable_surface"
must_have ".agent/docs/ssot-map.yaml" "$TARGET_SSOT"

# parent SSOT links in target SSOT
must_have "$TARGET_SSOT" "docs/design/ci-contract-ssot.yaml"
must_have "$TARGET_SSOT" "docs/design/pipeline-continuity-ssot.yaml"
must_have "$TARGET_SSOT" "docs/design/runtime-orchestration-ssot.yaml"
must_have "$TARGET_SSOT" "docs/design/topology-recommendation-ci-runtime.md"
must_have "$TARGET_SSOT" "docs/design/topology-recommendation-ci-runtime.yaml"
must_have "$TARGET_SSOT" "docs/framework-policy.yaml"

# ci-contract reference
must_have "docs/design/ci-contract-ssot.yaml" "CI_ADMIN_RUNTIME_DIAGNOSTIC_SURFACE"
must_have "docs/design/ci-contract-ssot.yaml" "system-ci-admin-runtime-callable-surface.yaml"

# pipeline continuity reference
must_have "docs/design/pipeline-continuity-ssot.yaml" "system-ci-admin-runtime-callable-surface.yaml"
must_have "docs/design/pipeline-continuity-ssot.yaml" "AdminRuntime"
must_have "docs/design/pipeline-continuity-ssot.yaml" "diagnostic"

# runtime orchestration reference
must_have "docs/design/runtime-orchestration-ssot.yaml" "system-ci-admin-runtime-callable-surface.yaml"
must_have "docs/design/runtime-orchestration-ssot.yaml" "AdminRuntime"
must_have "docs/design/runtime-orchestration-ssot.yaml" "SystemOperationCiRuntime"

# topology recommendation references
must_have "docs/design/topology-recommendation-ci-runtime.md" "system-ci-admin-runtime-callable-surface.yaml"
must_have "docs/design/topology-recommendation-ci-runtime.md" "System Operation CI"
must_have "docs/design/topology-recommendation-ci-runtime.md" "Admin"

must_have "docs/design/topology-recommendation-ci-runtime.yaml" "system-ci-admin-runtime-callable-surface.yaml"
must_have "docs/design/topology-recommendation-ci-runtime.yaml" "admin_callable_surface_design"

# operation vocabulary
must_have "$TARGET_SSOT" "system_ci:list_targets"
must_have "$TARGET_SSOT" "system_ci:inspect"

# target vocabulary
must_have "$TARGET_SSOT" "hub_attention_continuity"
must_have "$TARGET_SSOT" "current_rebuildability"
must_have "$TARGET_SSOT" "registry_continuity"

# output contract vocabulary
must_have "$TARGET_SSOT" "SystemCiDiagnosticResult"
must_have "$TARGET_SSOT" "SystemCiStatus"
must_have "$TARGET_SSOT" "Pass"
must_have "$TARGET_SSOT" "Gap"
must_have "$TARGET_SSOT" "Blocking"

# prohibited / required boundary vocabulary
must_have "$TARGET_SSOT" "read_only_diagnostic_surface"
must_have "$TARGET_SSOT" "diagnostics_evidence_eligibility_only"
must_have "$TARGET_SSOT" "DB_write_from_diagnostic_surface"
must_have "$TARGET_SSOT" "registry_mutation_from_diagnostic_surface"
must_have "$TARGET_SSOT" "automatic_repair_from_diagnostic_surface"
must_have "$TARGET_SSOT" "automatic_promotion_from_diagnostic_surface"
must_have "$TARGET_SSOT" "silent_fallback_to_pass"

if [ "$FAILURES" -eq 0 ]; then
  echo "=== system CI admin runtime callable SSOT wiring check passed ==="
  exit 0
fi

echo "=== system CI admin runtime callable SSOT wiring check failed: $FAILURES ===" >&2
exit 1
