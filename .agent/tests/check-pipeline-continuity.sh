#!/usr/bin/env bash
# check-pipeline-continuity.sh — pipeline continuity CI gate
#
# Structure (see docs/design/pipeline-continuity-ssot.yaml for node/gap definitions):
#
#   1. PIPELINE BODY   — data-driven vertical slice fixture execution.
#                        Reference only: body runs in default-entity-search.yml via
#                        check-default-entity-search.sh (requires dotnet + deno).
#                        This script confirms the delegation target is present.
#   2. HARDCODE GUARD  — detects dispatcher bypass and hardcoded routing anti-patterns
#                        in topology_transform_runtime (RuntimeExecutor).
#   3. GAP STATUS      — enumerates known not-yet-implemented nodes from SSOT
#                        gap_summary. Not failures.
#
# Pipeline body is NOT vocabulary grep. This script does not re-define node lists —
# node/required_identity/status are defined in docs/design/pipeline-continuity-ssot.yaml.
# Hardcode guard patterns correspond to pipeline-continuity-ssot.yaml hardcode_guard.checks.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
SSOT="$REPO_ROOT/docs/design/pipeline-continuity-ssot.yaml"

FAILURES=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

# ─── 1. PIPELINE BODY — delegation reference ──────────────────────────────────
# Data-driven vertical slice: fixture flows through full dispatch path end-to-end.
# Implemented in check-default-entity-search.sh (dotnet + deno) running in the
# default-entity-search.yml workflow. This check confirms the script is present.

echo ""
echo "=== [pipeline.body] data-driven vertical slice: default:entity:search ==="
echo "    Body test: .agent/tests/check-default-entity-search.sh (default-entity-search.yml)"
echo "    Fixture:   EndpointRequestDto{target=default,layer=entity,action=Search}"
echo "    Verifies:  structureMapId / packageId / schemaId / componentIds survive full dispatch"

BODY_SCRIPT="$SCRIPT_DIR/check-default-entity-search.sh"
if [ -f "$BODY_SCRIPT" ]; then
  echo "OK  [pipeline.body] check-default-entity-search.sh present"
else
  fail "[pipeline.body] check-default-entity-search.sh not found — pipeline body test missing"
fi

# ─── 2. HARDCODE GUARD — dispatcher bypass and fallback detection ──────────────
# Guards that topology_transform_runtime (RuntimeExecutor) does not accumulate
# hardcoded target/layer/action dispatch branching that replaces data-driven
# manifest_dispatcher routing.
#
# Allowed count and exceptions defined in:
#   docs/design/pipeline-continuity-ssot.yaml hardcode_guard.checks.target_dispatch_branching

echo ""
echo "=== [pipeline.hardcode_guard] dispatcher bypass and hardcode detection ==="

RUNTIME_EXEC="$REPO_ROOT/backend/runtime/RuntimeExecutor.cs"

if [ ! -f "$RUNTIME_EXEC" ]; then
  fail "[hardcode.guard] RuntimeExecutor.cs not found"
else
  # Count target string literal comparisons in RuntimeExecutor.
  # Allowed: up to 3 legacy/isolated branches; 0 is preferred (complete isolation achieved).
  # When branches exist they must be documented exceptions in pipeline-continuity-ssot.yaml.
  # Any count above 3 indicates a new hardcoded dispatch target being added.
  # || true: grep -c exits 1 on 0 matches; treat 0 matches as 0, not a script failure.
  DIRECT_EQ=$(grep -c 'vector\.Target ==' "$RUNTIME_EXEC" || true)
  STRING_EQ=$(grep -c 'string\.Equals(vector\.Target' "$RUNTIME_EXEC" || true)
  TOTAL=$((DIRECT_EQ + STRING_EQ))
  ALLOWED=3
  if [ "$TOTAL" -gt "$ALLOWED" ]; then
    fail "[hardcode.guard] $TOTAL target-dispatch branches in RuntimeExecutor.cs (allowed: up to $ALLOWED; excess indicates hardcoded routing replacing manifest_dispatcher)"
  else
    echo "OK  [hardcode.guard] target-dispatch branches: $TOTAL (allowed: up to $ALLOWED; 0 is preferred)"
  fi

  # Check for silent fallback to default target.
  if grep -q '?? "default"' "$RUNTIME_EXEC"; then
    fail "[hardcode.guard] silent fallback '?? \"default\"' found in RuntimeExecutor.cs"
  else
    echo "OK  [hardcode.guard] no silent fallback to default target"
  fi

  # Check that fixed topology ID literals do not appear in production runtime paths.
  # These IDs are test fixture values and must remain isolated under tests/.
  FIXED_ID=$(grep -rn \
    '"00000000-0000-0000-0000-000000000001"\|"00000000-0000-0000-0000-000000000002"\|"00000000-0000-0000-0000-000000000003"\|"00000000-0000-0000-0000-000000000004"' \
    "$REPO_ROOT/backend/runtime" \
    "$REPO_ROOT/backend/endpoint" \
    "$REPO_ROOT/backend/mapper" \
    2>/dev/null || true)
  if [ -n "$FIXED_ID" ]; then
    fail "[hardcode.guard] fixed topology fixture ID found in production runtime/endpoint/mapper (must stay in tests only): $FIXED_ID"
  else
    echo "OK  [hardcode.guard] no fixed topology fixture IDs in production runtime"
  fi
fi

# ─── 3. GAP STATUS — read from SSOT gap_summary ───────────────────────────────
# Known gaps from pipeline-continuity-ssot.yaml gap_summary.
# Not failures — these enumerate nodes that are not yet implemented.
# To implement a node: add file to SSOT files[], update status, add pipeline body test.

echo ""
echo "=== [pipeline.gap_status] known gaps (from pipeline-continuity-ssot.yaml) ==="
if [ -f "$SSOT" ]; then
  sed -n '/^  gap_summary:/,$ { /^ *- "/ p }' "$SSOT" \
    | sed 's/^ *- "//;s/"$//' \
    | while IFS= read -r line; do
        echo "GAP  $line"
      done
else
  fail "[pipeline.gap_status] pipeline-continuity-ssot.yaml not found"
fi

# ─── Result ───────────────────────────────────────────────────────────────────

echo ""
if [ "$FAILURES" -gt 0 ]; then
  echo "=== $FAILURES pipeline continuity failure(s) ===" >&2
  exit 1
fi

echo "=== Pipeline continuity: hardcode guard OK | see gap_status for not-implemented nodes ==="
