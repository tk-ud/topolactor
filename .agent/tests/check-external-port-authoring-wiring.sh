#!/usr/bin/env bash
set -euo pipefail

rg -n 'list_external_port_authoring_candidates|ExternalPortAuthoringCandidateDto' backend frontend >/dev/null
rg -n 'external_port' frontend/lib/packageWiringOptions.ts frontend/islands/UiBuilderAdmin.tsx >/dev/null
rg -n 'media/audio_player|media/video_player|AudioPlayer|VideoPlayer' docs/design/ui-ux-primitive-catalog-ssot.yaml frontend/components/catalog.ts frontend/runtime/runtimeComponentFactory.ts >/dev/null

# Guard against provider/bundle fixed option lists in the Design Inspector / UI Builder surface.
if rg -n 'email_bundle|export_sftp_bundle|stripe_bundle|webhook_inbox_bundle|Gemini|SFTP|Stripe' frontend/islands/UiBuilderAdmin.tsx frontend/components/catalog.ts frontend/runtime/runtimeComponentFactory.ts; then
  echo "provider or bundle fixed candidate leaked into frontend authoring surface" >&2
  exit 1
fi

# Plaintext-looking credential fields must not be projected by the candidate action.
if rg -n 'password|secret|private_key|access_token|refresh_token' backend/repository/NpgsqlUiTopologyRepository.cs frontend/islands/UiBuilderAdmin.tsx; then
  echo "plaintext credential field leaked into external port authoring candidates" >&2
  exit 1
fi
