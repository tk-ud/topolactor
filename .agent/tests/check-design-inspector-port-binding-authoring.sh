#!/usr/bin/env bash
# check-design-inspector-port-binding-authoring.sh
# Guards for Design Inspector component event / payloadFrom / outputProp / portTargetRef authoring bundle.
# Bundle: ui-builder-design-inspector-port-binding-authoring
# SSOT: external-port-substrate-ssot.yaml admin_setting_projection + ui-builder-preset-ecosystem-ssot.yaml payloadFrom_resolver_contract
set -euo pipefail

ui="frontend/islands/UiBuilderAdmin.tsx"
utils="frontend/runtime/visualLayoutUtils.ts"

# dispatchExternalPort is present in COMPONENT_ACTION_TYPES as an authoring action kind.
rg -n 'dispatchExternalPort' "$ui" >/dev/null

# New ComponentEventWiring fields are authored in the Design Inspector wiring tab.
rg -n 'payloadFrom\?' "$ui" >/dev/null
rg -n 'outputProp\?' "$ui" >/dev/null
rg -n 'portTargetRef\?' "$ui" >/dev/null

# payloadFrom authoring validated via parsePayloadFromSource — no ad-hoc regex, no silent acceptance.
rg -n 'parsePayloadFromSource' "$ui" >/dev/null
rg -n 'validatePayloadFromEntries' "$ui" >/dev/null
rg -n 'PAYLOAD_FROM_UNRESOLVED_REF' "$ui" >/dev/null

# useExternalPortAuthoringCandidates hook is present (reusable abstraction; no per-component one-off fetch).
rg -n 'useExternalPortAuthoringCandidates' "$ui" >/dev/null

# portTargetRef selection in Design Inspector is constrained to DB-derived active candidates only.
rg -n 'externalPortCandidates' "$ui" >/dev/null

# VisualNodePayload runtimeInteractions type includes new authoring fields for round-trip.
rg -n 'payloadFrom\?' "$utils" >/dev/null
rg -n 'outputProp\?' "$utils" >/dev/null
rg -n 'portTargetRef\?' "$utils" >/dev/null

# Guard: no provider or bundle fixed candidate list introduced in Design Inspector surface.
if rg -n 'email_bundle|export_sftp_bundle|stripe_bundle|webhook_inbox_bundle|Gemini|SFTP|Stripe' "$ui" frontend/components/catalog.ts frontend/runtime/runtimeComponentFactory.ts; then
  echo "FAIL: provider or bundle fixed candidate leaked into Design Inspector authoring surface" >&2
  exit 1
fi

# Guard: no plaintext credential fields in component event wiring authoring fields.
if rg -n 'password|private_key|access_token|refresh_token' "$ui"; then
  echo "FAIL: plaintext credential field found in Design Inspector authoring surface" >&2
  exit 1
fi

# Guard: runtime execution of dispatchExternalPort is NOT wired (remains authoring-only this bundle).
# renderEmission.ts must not handle 'dispatchExternalPort' case in its switch.
if rg -n 'dispatchExternalPort' frontend/runtime/renderEmission.ts 2>/dev/null; then
  echo "FAIL: dispatchExternalPort runtime execution wired in renderEmission — out of scope for this bundle" >&2
  exit 1
fi

echo "=== Design Inspector port binding authoring checks passed ==="
