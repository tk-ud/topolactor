#!/usr/bin/env bash
set -euo pipefail

repo="backend/repository/NpgsqlUiTopologyRepository.cs"
ui="frontend/islands/UiBuilderAdmin.tsx"

rg -n 'list_external_port_authoring_candidates|ExternalPortAuthoringCandidateDto' backend frontend >/dev/null
rg -n 'external_port' frontend/lib/packageWiringOptions.ts "$ui" >/dev/null
rg -n 'media/audio_player|media/video_player|AudioPlayer|VideoPlayer' docs/design/ui-ux-primitive-catalog-ssot.yaml frontend/components/catalog.ts frontend/runtime/runtimeComponentFactory.ts >/dev/null

for table in external_access_ports external_response_ports external_hook_ports; do
  rg -n "FROM topology\.$table" "$repo" >/dev/null
  python3 - "$repo" "$table" <<'PY'
import sys
from pathlib import Path
src = Path(sys.argv[1]).read_text()
table = sys.argv[2]
idx = src.index(f"FROM topology.{table}")
window = src[idx:idx+120]
if "WHERE active = true" not in window:
    raise SystemExit(f"{table} is not active-filtered")
PY
done

if rg -n 'required_by_bundle\s+AS\s+consumer_bundle_binding' "$repo"; then
  echo "required_by_bundle must not be aliased as consumer_bundle_binding" >&2
  exit 1
fi
rg -n 'NULL::text AS consumer_bundle_binding|consumer_bundle_binding' "$repo" >/dev/null
rg -n 'external-port:\{portKind\}:\{portId\}|external-port:\$\{portKind\}:\$\{portId\}' "$repo" >/dev/null
rg -n 'externalPortCandidates\.some\(\(c\) => c\.targetRef === targetRef\.trim\(\)' "$ui" >/dev/null
rg -n 'update_package_wiring' "$ui" >/dev/null

# Guard against provider/bundle fixed option lists in the Design Inspector / UI Builder surface.
if rg -n 'email_bundle|export_sftp_bundle|stripe_bundle|webhook_inbox_bundle|Gemini|SFTP|Stripe' frontend/islands/UiBuilderAdmin.tsx frontend/components/catalog.ts frontend/runtime/runtimeComponentFactory.ts; then
  echo "provider or bundle fixed candidate leaked into frontend authoring surface" >&2
  exit 1
fi

# Plaintext-looking credential fields must not be projected by the candidate action.
if rg -n 'password|secret|private_key|access_token|refresh_token' "$repo" "$ui"; then
  echo "plaintext credential field leaked into external port authoring candidates" >&2
  exit 1
fi
