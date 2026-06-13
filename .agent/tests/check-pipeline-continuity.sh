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


PROJECTION_TEST="$REPO_ROOT/frontend/tests/projectionConstructor.test.ts"
if [ -f "$PROJECTION_TEST" ]; then
  echo "OK  [pipeline.body] projection constructor test file present"
else
  fail "[pipeline.body] frontend/tests/projectionConstructor.test.ts not found — projection constructor lane body test missing"
fi

if [ -f "$SSOT" ]; then
  if grep -q '^    frontend_projection_constructor_lane:' "$SSOT"; then
    echo "OK  [pipeline.body] frontend_projection_constructor_lane present in SSOT"
  else
    fail "[pipeline.body] frontend_projection_constructor_lane missing in SSOT"
  fi

  PROJECTION_LANE_BLOCK="$(awk '
    /^    frontend_projection_constructor_lane:/ { in_lane=1 }
    in_lane {
      if (/^    [a-z0-9_]+:/ && $0 !~ /^    frontend_projection_constructor_lane:/) {
        exit
      }
      print
    }
  ' "$SSOT")"

  if [ -z "$PROJECTION_LANE_BLOCK" ]; then
    fail "[pipeline.body] failed to extract frontend_projection_constructor_lane block from SSOT"
  else
    echo "OK  [pipeline.body] extracted frontend_projection_constructor_lane block"
  fi

  for identity_key in componentId component_kind parameter_schema event_binding; do
    if printf '%s\n' "$PROJECTION_LANE_BLOCK" | grep -Eq "^[[:space:]]*-[[:space:]]${identity_key}$"; then
      :
    else
      fail "[pipeline.body] frontend_projection_constructor_lane.required_identity missing key: ${identity_key}"
    fi
  done
  echo "OK  [pipeline.body] lane-scoped required_identity minimum keys present"

  for prohibited_key in \
    componentId_drift \
    silent_fallback_for_malformed_component_definition_payload \
    malformed_schema_silent_pass \
    unsupported_schema_type_silent_pass \
    invalid_event_binding_fallback_to_empty_object \
    boolean_coercion; do
    if printf '%s\n' "$PROJECTION_LANE_BLOCK" | grep -Eq "^[[:space:]]*-[[:space:]]${prohibited_key}$"; then
      :
    else
      fail "[pipeline.body] frontend_projection_constructor_lane.prohibited missing key: ${prohibited_key}"
    fi
  done
  echo "OK  [pipeline.body] lane-scoped prohibited guard keys present"

  if printf '%s\n' "$PROJECTION_LANE_BLOCK" | grep -q '^[[:space:]]*pipeline_body_test:'; then
    echo "OK  [pipeline.body] lane-scoped pipeline_body_test section present"
  else
    fail "[pipeline.body] frontend_projection_constructor_lane.pipeline_body_test missing"
  fi

  if printf '%s\n' "$PROJECTION_LANE_BLOCK" | grep -q '^[[:space:]]*kind:[[:space:]]*unit_boundary_contract$'; then
    echo "OK  [pipeline.body] lane-scoped pipeline_body_test.kind is unit_boundary_contract"
  else
    fail "[pipeline.body] frontend_projection_constructor_lane.pipeline_body_test.kind must be unit_boundary_contract"
  fi

  if printf '%s\n' "$PROJECTION_LANE_BLOCK" | grep -q '^[[:space:]]*-[[:space:]]frontend/tests/projectionConstructor.test.ts$'; then
    echo "OK  [pipeline.body] lane-scoped pipeline_body_test.test_files includes projectionConstructor test"
  else
    fail "[pipeline.body] frontend_projection_constructor_lane.pipeline_body_test.test_files missing frontend/tests/projectionConstructor.test.ts"
  fi
else
  fail "[pipeline.body] pipeline-continuity-ssot.yaml not found"
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


# ─── 3. RUNTIME UI INTERACTION TIER 2 REPRESENTATIVE HARNESS ────────────────
# Tier 2 does not force every API through a full product loop, but runtime UI
# interaction changes must keep the representative canonical fixture and backend
# fail-close validation tests present.

echo ""
echo "=== [pipeline.runtime_ui_interaction] Tier 2 representative harness presence ==="

RUNTIME_UI_SCENARIO="$REPO_ROOT/frontend/tests/runtimeUiInteractionScenario.test.ts"
RUNTIME_UI_VALIDATION="$REPO_ROOT/backend/tests/Topolactor.Runtime.Tests/NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs"

if [ -f "$RUNTIME_UI_SCENARIO" ]; then
  echo "OK  [pipeline.runtime_ui_interaction] runtimeUiInteractionScenario.test.ts present"
  if grep -q "runtimeInteractions" "$RUNTIME_UI_SCENARIO"; then
    echo "OK  [pipeline.runtime_ui_interaction] canonical runtimeInteractions fixture present"
  else
    fail "[pipeline.runtime_ui_interaction] runtimeUiInteractionScenario.test.ts must exercise canonical runtimeInteractions"
  fi
  if grep -q "previewMode inert" "$RUNTIME_UI_SCENARIO"; then
    echo "OK  [pipeline.runtime_ui_interaction] preview inert vs production runtime assertion present"
  else
    fail "[pipeline.runtime_ui_interaction] runtimeUiInteractionScenario.test.ts must assert previewMode inert behavior"
  fi
else
  fail "[pipeline.runtime_ui_interaction] frontend/tests/runtimeUiInteractionScenario.test.ts not found — Tier 2 runtime UI scenario missing"
fi

if [ -f "$RUNTIME_UI_VALIDATION" ]; then
  echo "OK  [pipeline.runtime_ui_interaction] backend layout patch validation test file present"
  for test_name in \
    "RuntimeInteractionMissingTarget_FailsClose" \
    "RuntimeInteractionUnsupportedStatePath_FailsClose" \
    "RuntimeInteractionUnsupportedAction_FailsClose" \
    "RuntimeInteractionTargetKindMismatch_FailsClose"; do
    if grep -q "$test_name" "$RUNTIME_UI_VALIDATION"; then
      echo "OK  [pipeline.runtime_ui_interaction] backend validation test present: $test_name"
    else
      fail "[pipeline.runtime_ui_interaction] backend validation test missing: $test_name"
    fi
  done
else
  fail "[pipeline.runtime_ui_interaction] backend layout patch validation test file missing"
fi

if [ -f "$SSOT" ] && grep -q "button_click_modal_open_close" "$SSOT"; then
  echo "OK  [pipeline.runtime_ui_interaction] SSOT representative scenario registered"
else
  fail "[pipeline.runtime_ui_interaction] SSOT missing button_click_modal_open_close representative scenario"
fi

# ─── 3. GAP STATUS — read from SSOT gap_summary ───────────────────────────────
# Known gaps from pipeline-continuity-ssot.yaml gap_summary.
# Not failures — these enumerate nodes that are not yet implemented.
# To implement a node: add file to SSOT files[], update status, add pipeline body test.

echo ""

# frontend_component_event_log_lane minimal gate: presence/CI registration/SSOT references only
if ! grep -q "frontend_component_event_log_lane" "$SSOT"; then
  fail "[pipeline.component_event_lane] lane definition missing"
fi
if ! grep -q "FrontendComponentEventLogLaneTests.cs" "$SSOT"; then
  fail "[pipeline.component_event_lane] lane test file reference missing"
fi
if ! grep -q "frontend/tests/frontendComponentEventRuntime.test.ts" "$SSOT"; then
  fail "[pipeline.component_event_lane] frontend runtime test reference missing"
fi
if ! grep -q "check-pipeline-continuity.sh" "$SSOT"; then
  fail "[pipeline.component_event_lane] ci_script reference missing"
fi

# ─── 5. SSOT WIRING AUDIT CI LANES — presence check ──────────────────────────
# Verifies that the 5 SSOT wiring audit CI lane test files are present.
# Each lane audits a specific SSOT contract boundary (not execution semantics).
# Defined in docs/system-roadmap.yaml: system_ci.dotnet_ssot_wiring_audit_tests.
# Tests must exist in the dotnet test project to satisfy the completion conditions.

echo ""
echo "=== [ssot.wiring_audit] SSOT wiring audit CI lane presence check ==="

TEST_DIR="$REPO_ROOT/backend/tests/Topolactor.Runtime.Tests"

for lane_file in \
  "SsotWiringAuditTopologyRegistrationTests.cs" \
  "SsotWiringAuditHubRegistrationTests.cs" \
  "SsotWiringAuditSchedulerRuntimeTests.cs" \
  "SsotWiringAuditComponentRegistrationTests.cs" \
  "SsotWiringAuditDiagnosticsEligibilityTests.cs"; do
  if [ -f "$TEST_DIR/$lane_file" ]; then
    echo "OK  [ssot.wiring_audit] $lane_file present"
  else
    fail "[ssot.wiring_audit] $lane_file not found — SSOT wiring audit CI lane missing"
  fi
done

# Verify roadmap reflects the completed CI lanes.
ROADMAP="$REPO_ROOT/docs/system-roadmap.yaml"
if [ -f "$ROADMAP" ]; then
  for lane_key in \
    "system_ci.topology_registration" \
    "system_ci.hub_registration" \
    "system_ci.scheduler_runtime" \
    "system_ci.component_registration"; do
    if grep -q "^    ${lane_key}:" "$ROADMAP"; then
      echo "OK  [ssot.wiring_audit] roadmap entry present: $lane_key"
    else
      fail "[ssot.wiring_audit] roadmap entry missing: $lane_key"
    fi
  done
else
  fail "[ssot.wiring_audit] docs/system-roadmap.yaml not found"
fi

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
