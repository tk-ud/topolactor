#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
if ! command -v deno &>/dev/null; then
  echo "ERROR: deno is required for projection lane seed hardening tests." >&2
  exit 1
fi
if ! command -v dotnet &>/dev/null; then
  echo "ERROR: dotnet is required for projection lane seed hardening tests." >&2
  exit 1
fi
cd "${REPO_ROOT}"
source .agent/scripts/lib/noise_control.sh
noise_run "projection_lane_seed lane=backend_seed_collapse" dotnet test "${REPO_ROOT}/backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj" --filter "FullyQualifiedName~ProjectionLaneSeedCollapse" --nologo --verbosity minimal
noise_run "projection_lane_seed lane=frontend_sse" deno test "${REPO_ROOT}/frontend/tests/sseLane.test.ts" --allow-read
noise_run "projection_lane_seed lane=frontend_render" deno test "${REPO_ROOT}/frontend/tests/uiRenderedInteraction.test.ts" --allow-read --filter "projectionDefinition"
noise_run "projection_lane_seed lane=seed_to_lane" deno test "${REPO_ROOT}/frontend/tests/projectionLaneSeedHarness.test.ts" --allow-read
echo "PASS projection-lane-seed-hardening lanes=4"
