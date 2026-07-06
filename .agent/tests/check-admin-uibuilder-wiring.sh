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
for key in layout_mode wiring_mode persistence_authority trigger_vocabulary \
  lifecycle_policy high_frequency_policy drag_drop_wiring_edit \
  ui_event_settings external_event_candidates proof_surface; do
  require_grep "^  ${key}:|^    ${key}:" "$SSOT" "SSOT contract key"
done
require_grep "runtimeInteractions" "$SSOT" "persistence authority vocabulary"
require_grep "ui_builder_preset_ecosystem_ssot_as_owning_ssot_for_this_surface" "$SSOT" "ownership negative boundary"

# ─── 2. Implementation surfaces map to SSOT ──────────────────────────────────
require_file "$LIB"
require_file "$PANEL"
require_file "$EVENT_PANEL"
require_file "$ISLAND"
require_file "$TEST"

require_grep "admin-uibuilder-ui-structure-wiring-ssot" "$LIB" "SSOT reference"
require_grep "lifecycle" "$LIB" "lifecycle trigger group"
require_grep "debounceMs" "$LIB" "high-frequency policy field"
require_grep "lifecycleDispatchConfirmed" "$LIB" "lifecycle policy field"
require_grep "buildWiringGraphProjection" "$LIB" "wiring projection entry point"
require_grep "applyWiringDropEdit" "$LIB" "drag-drop wiring edit entry point"

require_grep "buildWiringGraphProjection" "$PANEL" "panel derives projection from runtimeInteractions"
require_grep "buildWiringGraphProjection|WiringGraphPanel" "$ISLAND" "island wires wiring mode"
require_grep "uiBuilderWiringProjection" "$TEST" "proof test targets projection lib"

# ─── 3. Negative boundaries ──────────────────────────────────────────────────
# Wiring graph panel is projection-only: no direct persistence dispatch from the panel.
forbid_grep "dispatchAdminOp|queueAdminClientCommand|layout_patch:apply" "$PANEL" \
  "wiring graph must be view/edit projection, not persistence authority"

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
