#!/usr/bin/env bash
# check-admin-uibuilder-wiring.sh — /admin/ui-builder UI structure/wiring SSOT proof surface
#
# Executable structure gate for docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml:
#   1. SSOT declares the owning contracts (mode boundary, persistence authority,
#      trigger vocabulary, lifecycle / high-frequency / drag-drop policies).
#   2. Implementation surfaces exist and map to the SSOT (wiring projection lib,
#      wiring graph panel, event settings panel, island mode toggle).
#   3. Negative boundaries hold: wiring graph is projection-only (no persistence
#      write from the panel), no provider/bundle hardcoded candidate lists,
#      no plaintext credential fields in the authoring surface.
# Structural connection check only; NOT semantic completion judgment.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

SSOT="docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml"
LIB="frontend/lib/uiBuilderWiringProjection.ts"
PANEL="frontend/components/WiringGraphPanel.tsx"
EVENT_PANEL="frontend/components/NodeEventAuthoringPanel.tsx"
ISLAND="frontend/islands/UiBuilderAdmin.tsx"
TEST="frontend/tests/uiBuilderWiringProjection.test.ts"
RUNNER="frontend/runtime/uiEventEffectRunner.ts"
RUNNER_TEST="frontend/tests/uiEventEffectRunner.test.ts"
SCHEDULER="frontend/runtime/frontendScheduler.ts"
PROJECTION_SHELL="frontend/islands/ProjectionShell.tsx"
COMPONENT_FACTORY="frontend/runtime/runtimeComponentFactory.ts"
RENDER_EMISSION="frontend/runtime/renderEmission.ts"
ADAPTER="frontend/runtime/runtimeComponentAdapter.ts"
SCENARIO_TEST="frontend/tests/runtimeUiInteractionScenario.test.ts"
EXECUTION_LANE_TEST="frontend/tests/adminWiringExecutionLane.test.ts"

FAILURES=0
fail() { echo "FAIL: $1" >&2; FAILURES=$((FAILURES + 1)); }

require_file() {
  [ -f "$1" ] || fail "missing required surface: $1"
}

require_grep() {
  local pattern="$1" file="$2" why="$3"
  grep -Eq "$pattern" "$file" || fail "$file missing '$pattern' ($why)"
}

forbid_grep() {
  local pattern="$1" file="$2" why="$3"
  if grep -Eq "$pattern" "$file"; then
    fail "$file contains forbidden '$pattern' ($why)"
  fi
}

# ─── 1. SSOT contract keys ────────────────────────────────────────────────────
require_file "$SSOT"
for key in lineage_boundary layout_mode wiring_mode persistence_authority \
  trigger_vocabulary lifecycle_policy high_frequency_policy drag_drop_wiring_edit \
  side_effect_cycle_policy component_runtime_state_effect_boundary \
  ui_event_settings setting_category_taxonomy external_event_candidates proof_surface; do
  require_grep "^  ${key}:|^    ${key}:|^      ${key}:" "$SSOT" "SSOT contract key"
done
require_grep "runtimeInteractions" "$SSOT" "persistence authority vocabulary"
require_grep "ui_builder_preset_ecosystem_ssot_as_owning_ssot_for_this_surface" "$SSOT" "ownership negative boundary"

# lane_storage_boundary (top_ssot_alignment): the five formalized lanes and the
# manifest.topology refs-only boundary statement.
require_grep "^  lane_storage_boundary:" "$SSOT" "lane/storage boundary top-SSOT-alignment section"
for lane in package_internal_api_wiring_lane runtime_interactions_lane external_api_lane external_instance_lane local_ui_state_lane; do
  require_grep "$lane:" "$SSOT" "lane_storage_boundary lane"
done
require_grep "manifest_topology_boundary:" "$SSOT" "manifest.topology refs-only boundary statement"
require_grep "manifest_topology_array_element_embedding_layout_or_wiring_payload_directly" "$SSOT" "manifest.topology UI-payload-embedding negative boundary"
# package authority boundary: manifest.packageIds must reference
# topology.components_package_design (DB design authority), never
# topology.ui_component_package (a distinct tensor-FK-only identity) -- see
# docs/design/db-schema.yaml packages/components_package_design.manifest_reference.
require_grep "topology.components_package_design" "$SSOT" "manifest-facing package authority table"
require_grep "packageIds_referencing_topology_ui_component_package_instead_of_topology_components_package_design" "$SSOT" "package authority negative boundary"
# package_internal_api_wiring_lane idempotency applicability must stay an
# explicit known_gap, not silently asserted resolved.
if ! grep -Pzo "(?s)package_internal_api_wiring_lane:.{0,2000}?idempotency_applicability: known_gap" "$SSOT" >/dev/null 2>&1; then
  fail "$SSOT package_internal_api_wiring_lane must declare idempotency_applicability: known_gap (not silently assumed included)"
fi

# Canonical report vocabulary (frontend-ui-audit-bundle-semantic-frame.md).
require_grep "initial_mount" "$SSOT" "canonical lifecycle trigger"
for term in "UI監視割当" "UI状態更新" "副作用設定" "内部API" "外部API連携" "外部インスタンス連携"; do
  require_grep "$term" "$SSOT" "canonical setting category vocabulary"
done
# load / loaded / DOM onLoad must appear only as prohibited vocabulary, never in groups.
require_grep "prohibited_vocabulary" "$SSOT" "lifecycle prohibited vocabulary block"
if grep -Eq 'lifecycle: \[.*\bload\b' "$SSOT"; then
  fail "$SSOT lifecycle trigger group contains 'load' (prohibited vocabulary)"
fi

# ─── 2. Implementation surfaces map to SSOT ──────────────────────────────────
require_file "$LIB"
require_file "$PANEL"
require_file "$EVENT_PANEL"
require_file "$ISLAND"
require_file "$TEST"

require_grep "admin-uibuilder-ui-structure-wiring-ssot" "$LIB" "SSOT reference"
require_grep "initial_mount" "$LIB" "canonical lifecycle trigger"
require_grep "debounceMs" "$LIB" "high-frequency policy field"
require_grep "lifecycleDispatchConfirmed" "$LIB" "lifecycle policy field"
require_grep "idempotencyPolicy" "$LIB" "lifecycle idempotency policy field"
require_grep "buildWiringGraphProjection" "$LIB" "wiring projection entry point"
require_grep "applyWiringDropEdit" "$LIB" "drag-drop wiring edit entry point"
require_grep "findSideEffectCycleErrors" "$LIB" "side-effect cycle policy entry point"
require_grep "selectableWriteTargets" "$LIB" "dependency not-in write target filter"
require_grep "deriveUiWatchBindings" "$LIB" "UI監視割当 declaration derivation"
# load must not be part of the trigger vocabulary constants.
if grep -Eq '"load"|"loaded"' "$LIB"; then
  fail "$LIB contains prohibited lifecycle vocabulary (load/loaded)"
fi
# No implementation catch-all in the taxonomy.
if grep -Eq '"legacy"' "$LIB"; then
  fail "$LIB taxonomy contains catch-all 'legacy' category"
fi

require_grep "buildWiringGraphProjection" "$PANEL" "panel derives projection from runtimeInteractions"
require_grep "buildWiringGraphProjection|WiringGraphPanel" "$ISLAND" "island wires wiring mode"
require_grep "uiBuilderWiringProjection" "$TEST" "proof test targets projection lib"

# Runtime state/effect runner boundary (component_runtime_state_effect_boundary).
require_file "$RUNNER"
require_file "$RUNNER_TEST"
require_grep "admin-uibuilder-ui-structure-wiring-ssot" "$RUNNER" "SSOT reference"
require_grep "createUiEventEffectRunner" "$RUNNER" "effect runner entry point"
require_grep "createRuntimeStateDispatcher" "$RUNNER" "runtime state dispatcher"
require_grep "initial_mount" "$RUNNER" "runtime synthetic lifecycle emission"
require_grep "findSideEffectCycleErrors" "$RUNNER" "runtime loop guard"
require_grep "enqueueInstanceOperationDispatchCommand" "$SCHEDULER" "外部インスタンス連携 runtime dispatch lane"
require_grep "createUiEventEffectRunner" "$PROJECTION_SHELL" "runner owned by mount surface (not per-component useEffect)"
require_grep "uiEventEffectRunner" "$RUNNER_TEST" "runner proof test"
# state update -> projection rerender contract: store notification wired to specs re-render.
require_grep "NotifyingRuntimeLocalStateStore" "$RUNNER" "store change notification type"
require_grep "subscribe" "$RUNNER" "store change subscription"
require_grep "subscribe" "$PROJECTION_SHELL" "store notification -> projection specs re-render hook"
require_grep "rendered runtimeSpec props|runtimeSpec" "$RUNNER_TEST" "rendered-props reflection proof (not store Map only)"

# Mutation authority unification: event-triggered UI状態更新 goes through the same
# guarded dispatcher as the lifecycle path — no direct raw-store write in emitBoundEvent.
require_file "$COMPONENT_FACTORY"
require_file "$RENDER_EMISSION"
require_file "$ADAPTER"
require_file "$SCENARIO_TEST"
require_file "$EXECUTION_LANE_TEST"
require_grep "applyGuardedLocalStateMutation" "$RUNNER" "shared guarded mutation entry point"
require_grep "resolveUiStateUpdateMutation" "$RUNNER" "shared UI状態更新 target resolution"
require_grep "predeclareProjectionState|createProjectionStateDispatcher" "$RUNNER" "predeclare-before-mutate entry point"
require_grep "RuntimeGuardedStateStore" "$ADAPTER" "guarded state store type (set returns ok/error, not void)"
require_grep "applyGuardedLocalStateMutation" "$COMPONENT_FACTORY" \
  "emitBoundEvent must route localStateMutation through the shared guarded dispatcher"
require_grep "resolveUiStateUpdateMutation" "$RENDER_EMISSION" \
  "event binding builder must reuse the shared target/action/value resolution"
require_grep "createProjectionStateDispatcher" "$PROJECTION_SHELL" \
  "single shared dispatcher created before first render (not per-path stores)"
require_grep "RUNTIME_STATE_SLOT_NOT_DECLARED" "$SCENARIO_TEST" \
  "event-triggered undeclared-target fail-close proof"
require_grep "createProjectionStateDispatcher|createRuntimeStateDispatcher" "$SCENARIO_TEST" \
  "scenario test must exercise the guarded dispatcher, not a raw ad-hoc store"

# Event path must not bypass the guard with a direct store write.
forbid_grep '\.localStateStore\.set\(' "$COMPONENT_FACTORY" \
  "event-triggered mutation must call applyGuardedLocalStateMutation, not spec.localStateStore.set() directly"

# Runtime fail-close boundary (Round 4 re-audit): predeclare excludes stale/deleted
# targets, the runner reconciles its node list on SSE refresh, and emitBoundEvent
# enforces high_frequency_policy at dispatch time, not only authoring/apply time.
require_grep "isValidDebounceMs" "$LIB" \
  "shared high_frequency_policy debounceMs validity check"
require_grep "isValidDebounceMs" "$COMPONENT_FACTORY" \
  "emitBoundEvent must reuse the shared debounceMs validity check"
require_grep "HIGH_FREQUENCY_DISPATCH_REQUIRES_DEBOUNCE" "$COMPONENT_FACTORY" \
  "runtime dispatch guard must fail close before the external/instance dispatch lane"
require_grep "nodeIds\.has\(resolved\.targetNodeId\)" "$RUNNER" \
  "predeclare must not declare a UI状態更新 target absent from the current node list"
require_grep "updateNodes" "$RUNNER" \
  "runner must expose node-list reconciliation for SSE refresh"
require_grep "updateNodes" "$PROJECTION_SHELL" \
  "mount surface must reconcile the runner's node list on SSE refresh"
require_grep "updateNodes" "$RUNNER_TEST" \
  "SSE refresh reconciliation proof (existing node idempotent, new node fires once)"
require_grep "HIGH_FREQUENCY_DISPATCH_REQUIRES_DEBOUNCE" "$EXECUTION_LANE_TEST" \
  "runtime dispatch guard proof for both external and instance high-frequency lanes"

# Retry-safe dispatch idempotency (Round 5 re-audit): deterministic idempotency_key
# generation, threaded from binding-build time through event-time dispatch, and
# from the lifecycle path — the frontend fired-registry alone does not survive
# reload/reconnect/runner recreation. Backend ledger proof is covered by
# backend/tests/Topolactor.Runtime.Tests (ExternalPortDispatchRuntimeTests.cs /
# InstancePortRuntimeTests.cs), outside this frontend-scoped structure gate.
require_grep "computeDispatchIdempotencyKey" "$LIB" \
  "deterministic identity-based idempotency key generation"
require_grep "appendResolvedPayloadToIdempotencyKey" "$LIB" \
  "event-time resolved-payload extension of the identity-only base key"
require_grep "computeDispatchIdempotencyKey" "$RENDER_EMISSION" \
  "binding builder must compute the identity-only idempotencyKeyBase"
require_grep "idempotencyKeyBase" "$COMPONENT_FACTORY" \
  "emitBoundEvent must extend the binding's idempotencyKeyBase with the resolved payload"
require_grep "computeDispatchIdempotencyKey" "$RUNNER" \
  "lifecycle path must compute idempotencyKey for dispatchExternalPort/dispatchInstanceOperation"
require_grep "idempotencyKey" "$SCHEDULER" \
  "frontendScheduler must forward idempotencyKey as idempotency_key on the dispatch payload"
require_grep "idempotencyKey" "$RUNNER_TEST" \
  "proof that idempotencyKey is stable across separately-created runner instances (reload/reconnect)"

# ─── 3. Negative boundaries ──────────────────────────────────────────────────
# Wiring graph panel is projection-only: no direct persistence dispatch from the panel.
forbid_grep "dispatchAdminOp|queueAdminClientCommand|layout_patch:apply" "$PANEL" \
  "wiring graph must be view/edit projection, not persistence authority"

# No catch-all category label rounding in the wiring panel (legacy = 状態設定 prohibited).
forbid_grep "legacy:" "$PANEL" "catch-all category rounding prohibited"

# No provider/bundle fixed candidate lists in wiring/event authoring surfaces.
forbid_grep "email_bundle|export_sftp_bundle|stripe_bundle|webhook_inbox_bundle|Stripe|Gemini" "$PANEL" \
  "provider/bundle hardcode prohibited"
forbid_grep "email_bundle|export_sftp_bundle|stripe_bundle|webhook_inbox_bundle|Stripe|Gemini" "$LIB" \
  "provider/bundle hardcode prohibited"

# No plaintext credential fields in authoring surfaces.
forbid_grep "password|secret|private_key|access_token|refresh_token" "$PANEL" \
  "plaintext credential vocabulary prohibited"
forbid_grep "password|secret|private_key|access_token|refresh_token" "$LIB" \
  "plaintext credential vocabulary prohibited"

if [ "$FAILURES" -gt 0 ]; then
  echo "=== [ADMIN_UIBUILDER_WIRING] FAIL (${FAILURES} failure(s)) ===" >&2
  exit 1
fi

echo "PASS check-admin-uibuilder-wiring"
