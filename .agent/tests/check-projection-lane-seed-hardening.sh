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
NOISE_QUIET_SUCCESS=1 noise_run "projection_lane_seed lane=backend_lane" dotnet test "${REPO_ROOT}/backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj" --filter "FullyQualifiedName~ManifestDispatcherManifestDrivenTests|FullyQualifiedName~ScreenDataShapeQueryRuntimeTests" --nologo --verbosity minimal
NOISE_QUIET_SUCCESS=1 noise_run "projection_lane_seed lane=frontend_sse" deno test "${REPO_ROOT}/frontend/tests/sseLane.test.ts" --allow-read
NOISE_QUIET_SUCCESS=1 noise_run "projection_lane_seed lane=frontend_render" deno test "${REPO_ROOT}/frontend/tests/uiRenderedInteraction.test.ts" --allow-read --filter "projectionDefinition"
NOISE_QUIET_SUCCESS=1 noise_run "projection_lane_seed lane=seed_contract" deno test "${REPO_ROOT}/frontend/tests/projectionLaneSeedHarness.test.ts" --allow-read --filter "seed contract"
NOISE_QUIET_SUCCESS=1 noise_run "projection_lane_seed lane=seed_to_lane" deno test "${REPO_ROOT}/frontend/tests/projectionLaneSeedHarness.test.ts" --allow-read --filter "seed integration|NG"
echo "PASS projection-lane-seed-hardening lanes=5"
