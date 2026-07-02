#!/usr/bin/env bash
# check-unified-test-gate.sh — unified test gate (.agent/docs/test-bundles.yaml runner surface) for function, runtime, integration, and frontend contract layers.
#
# Classification:
#   FUNCTION_BOUNDARY   — RuntimeExecutor.ExecuteAsync, ManifestDispatcher.DispatchAsync,
#                         ManifestDispatcher.BuildLegacyHookRequest,
#                         TargetDispatchOverride.ValidateRequest / TryHandleAsync,
#                         OperationVectorResolver.Resolve, RuntimeGuard.Validate,
#                         ContextRouteRecommendationResolver.ResolveAsync
#   RUNTIME_INTEGRATION — DefaultEntitySearchIntegrationTests (full dispatch path via
#                         dispatcher / executor / override)
#   FRONTEND_CONTRACT   — adminApi.test.ts, defaultEntitySearch.test.ts, pipelineContinuity.test.ts
#
# NOT_COVERED: OutputLaneRouter.RouteAsync / AdminRuntime.ExecuteDataAsync / db_notify output lane.
#   docker-compose / DB / migration verification is covered in check-runtime-environment.sh.
# Missing tool is an explicit failure, not a pass.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

FAILURES=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

require_tool() {
  if ! command -v "$1" &>/dev/null; then
    echo "ERROR: required tool not found: $1" >&2
    echo "This check was NOT executed — missing tool is not a pass." >&2
    exit 1
  fi
}

require_tool dotnet
require_tool deno

cd "${REPO_ROOT}"
source .agent/scripts/lib/noise_control.sh

run_lane() {
  local label="$1"
  local cmd="$2"
  echo "processing unified-test-gate lane=${label}"
  if noise_run_bash "unified_test_gate lane=${label}" "$cmd"; then
    return 0
  fi
  fail "[${label}] lane failed"
  return 0
}


# Compact lane execution; failure replay is handled by noise_run_bash.
run_lane "TEST_PROOF_MANIFEST_INTEGRITY" "bash .agent/tests/check-test-proof-manifest-integrity.sh"
run_lane "FUNCTION_BOUNDARY" "dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj --nologo --verbosity minimal"
run_lane "RUNTIME_INTEGRATION" "dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj --nologo --verbosity minimal"
run_lane "FRONTEND_CONTRACT" "deno test frontend/tests/adminApi.test.ts frontend/tests/adminImport.test.ts frontend/tests/adminUxGuard.test.ts frontend/tests/defaultEntitySearch.test.ts frontend/tests/pipelineContinuity.test.ts frontend/tests/userDemoStepper.test.ts frontend/tests/uiBuilderStepper.test.ts --allow-read"
run_lane "CLIENT_COMMAND_LANE" "deno test frontend/tests/frontendScheduler.test.ts --allow-read"
run_lane "CATALOG_SSOT_ALIGNMENT" "deno test frontend/tests/componentCatalogSeedRegistration.test.ts frontend/tests/documentCanvasCatalogCheck.test.ts frontend/tests/adminDispatchManifestSeed.test.ts frontend/tests/ssotReaderCatalog.test.ts --allow-read"
run_lane "SSOT_VOCABULARY_CONTRACT" "bash .agent/tests/check-ssot-vocabulary-contract.sh"
run_lane "RECOMMENDATION_PRESSURE_LANE" "bash .agent/tests/check-recommendation-pressure-lane-boundary.sh"
run_lane "FRONTEND_ALL_TESTS" "bash .agent/tests/check-frontend-all-tests.sh"

echo "REMAINING_TODO runtime-environment-gate covers docker-compose/DB/migration; env / volume / live API-route E2E is still not included"

if [ "${FAILURES}" -eq 0 ]; then
  echo "PASS unified-test-gate lanes=9 remaining_todo=1"
  exit 0
fi

echo "FAIL unified-test-gate failures=${FAILURES}" >&2
exit 1
