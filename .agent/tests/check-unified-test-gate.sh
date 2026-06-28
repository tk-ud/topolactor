#!/usr/bin/env bash
# check-unified-test-gate.sh — unified test gate for function, runtime, integration, and frontend contract layers.
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
FAILED_LAYERS=()
FAILED_COMMANDS=()

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

record_failure() {
  local layer="$1"
  local command_text="$2"
  local message="$3"

  FAILED_LAYERS+=("$layer")
  FAILED_COMMANDS+=("$command_text")
  fail "[$layer] $message"
}

run_layer_check() {
  local layer="$1"
  local command_text="$2"
  local success_message="$3"
  local failure_message="$4"
  shift 4

  echo "COMMAND: $command_text"
  if "$@"; then
    echo "RESULT: PASS [$layer]"
    echo "OK  [$layer] $success_message"
  else
    echo "RESULT: FAIL [$layer]" >&2
    record_failure "$layer" "$command_text" "$failure_message"
  fi
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

# ─── FUNCTION_BOUNDARY ────────────────────────────────────────────────────────

echo ""
echo "=== [FUNCTION_BOUNDARY] Runtime function unit tests ==="
echo "    Scope: RuntimeExecutor.ExecuteAsync, ManifestDispatcher.DispatchAsync,"
echo "           ManifestDispatcher.BuildLegacyHookRequest,"
echo "           TargetDispatchOverride.ValidateRequest, TargetDispatchOverride.TryHandleAsync,"
echo "           OperationVectorResolver.Resolve, RuntimeGuard.Validate,"
echo "           ContextRouteRecommendationResolver.ResolveAsync"

run_layer_check \
  "FUNCTION_BOUNDARY" \
  "dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj --nologo --verbosity minimal" \
  "runtime function unit tests passed" \
  "runtime function unit tests failed" \
  dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj \
    --nologo --verbosity minimal

# ─── RUNTIME_INTEGRATION ──────────────────────────────────────────────────────

echo ""
echo "=== [RUNTIME_INTEGRATION] Integration boundary tests ==="
echo "    Scope: DefaultEntitySearchIntegrationTests"
echo "           full dispatch path: dispatcher / executor / override"

run_layer_check \
  "RUNTIME_INTEGRATION" \
  "dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj --nologo --verbosity minimal" \
  "integration boundary tests passed" \
  "integration boundary tests failed" \
  dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj \
    --nologo --verbosity minimal

# ─── FRONTEND_CONTRACT ────────────────────────────────────────────────────────

echo ""
echo "=== [FRONTEND_CONTRACT] Frontend API proxy and dispatch contract tests ==="
echo "    Scope: adminApi.test.ts, adminImport.test.ts, adminUxGuard.test.ts,"
echo "           defaultEntitySearch.test.ts, pipelineContinuity.test.ts,"
echo "           emissionUserFacingResult.test.ts, uiBuilderStepper.test.ts"

run_layer_check \
  "FRONTEND_CONTRACT" \
  "deno test frontend/tests/adminApi.test.ts frontend/tests/adminImport.test.ts frontend/tests/adminUxGuard.test.ts frontend/tests/defaultEntitySearch.test.ts frontend/tests/pipelineContinuity.test.ts frontend/tests/emissionUserFacingResult.test.ts frontend/tests/uiBuilderStepper.test.ts --allow-read" \
  "frontend contract tests passed" \
  "frontend contract tests failed" \
  deno test \
    frontend/tests/adminApi.test.ts \
    frontend/tests/adminImport.test.ts \
    frontend/tests/adminUxGuard.test.ts \
    frontend/tests/defaultEntitySearch.test.ts \
    frontend/tests/pipelineContinuity.test.ts \
    frontend/tests/emissionUserFacingResult.test.ts \
    frontend/tests/uiBuilderStepper.test.ts \
    --allow-read

# ─── CLIENT_COMMAND_LANE ──────────────────────────────────────────────────────

echo ""
echo "=== [CLIENT_COMMAND_LANE] Frontend scheduler and source guard tests ==="
echo "    Scope: frontendScheduler.test.ts"
echo "    Note: --allow-read required for source guard (scans frontend/**/*.ts)"

run_layer_check \
  "CLIENT_COMMAND_LANE" \
  "deno test frontend/tests/frontendScheduler.test.ts --allow-read" \
  "client command lane tests passed" \
  "client command lane tests failed" \
  deno test \
    frontend/tests/frontendScheduler.test.ts \
    --allow-read

# ─── CATALOG_SSOT_ALIGNMENT ───────────────────────────────────────────────────

echo ""
echo "=== [CATALOG_SSOT_ALIGNMENT] Frontend catalog / seed / SSOT drift detection tests ==="
echo "    Scope: componentCatalogSeedRegistration.test.ts, documentCanvasCatalogCheck.test.ts,"
echo "           adminDispatchManifestSeed.test.ts, ssotReaderCatalog.test.ts"
echo "    Note: requires --allow-read (reads db/seed_empty.sql, docs/design/*.yaml)"

run_layer_check \
  "CATALOG_SSOT_ALIGNMENT" \
  "deno test frontend/tests/componentCatalogSeedRegistration.test.ts frontend/tests/documentCanvasCatalogCheck.test.ts frontend/tests/adminDispatchManifestSeed.test.ts frontend/tests/ssotReaderCatalog.test.ts --allow-read" \
  "catalog/seed/SSOT alignment tests passed" \
  "catalog/seed/SSOT alignment tests failed" \
  deno test \
    frontend/tests/componentCatalogSeedRegistration.test.ts \
    frontend/tests/documentCanvasCatalogCheck.test.ts \
    frontend/tests/adminDispatchManifestSeed.test.ts \
    frontend/tests/ssotReaderCatalog.test.ts \
    --allow-read


# ─── SSOT_VOCABULARY_CONTRACT ───────────────────────────────────────────────

echo ""
echo "=== [SSOT_VOCABULARY_CONTRACT] SSOT vocabulary subset contract checks ==="
echo "    Scope: component catalog / seed runtime destination / pipeline prohibited+required identity vocabulary"

run_layer_check \
  "SSOT_VOCABULARY_CONTRACT" \
  "bash .agent/tests/check-ssot-vocabulary-contract.sh" \
  "vocabulary contract checks passed" \
  "vocabulary contract checks failed" \
  bash .agent/tests/check-ssot-vocabulary-contract.sh

echo ""
echo "=== [RECOMMENDATION_PRESSURE_LANE] hub-local vs SQL Attention lane boundary SSOT ==="
run_layer_check \
  "RECOMMENDATION_PRESSURE_LANE" \
  "bash .agent/tests/check-recommendation-pressure-lane-boundary.sh" \
  "lane boundary checks passed" \
  "lane boundary checks failed" \
  bash .agent/tests/check-recommendation-pressure-lane-boundary.sh

# ─── FRONTEND_ALL_TESTS ───────────────────────────────────────────────────────

echo ""
echo "=== [FRONTEND_ALL_TESTS] All frontend tests (full suite) ==="
echo "    Scope: all tests in frontend/tests/ (includes UI handler behavior, event SSOT,"
echo "           lane distinction, and any new tests added to frontend/tests/)"
echo "    Note: --allow-read required for source guard and SSOT readers"

run_layer_check \
  "FRONTEND_ALL_TESTS" \
  "bash .agent/tests/check-frontend-all-tests.sh" \
  "full frontend test suite passed" \
  "full frontend test suite failed" \
  bash .agent/tests/check-frontend-all-tests.sh


# ─── INSTANCE_PORT_SUBSTRATE_DESIGN ──────────────────────────────────────────

echo ""
echo "=== [INSTANCE_PORT_SUBSTRATE_DESIGN] Credential-backed instance port SSOT wiring ==="
echo "    Scope: design-only guard for instance_port_substrate / call_instance_postgres_function"

run_layer_check \
  "INSTANCE_PORT_SUBSTRATE_DESIGN" \
  "bash .agent/tests/check-instance-port-substrate.sh" \
  "instance port substrate design guard passed" \
  "instance port substrate design guard failed" \
  bash .agent/tests/check-instance-port-substrate.sh

# ─── NOT_COVERED ──────────────────────────────────────────────────────────────

echo ""
echo "=== [NOT_COVERED] Functions without direct test coverage (remaining todo) ==="
echo "REMAINING_TODO  runtime-environment-gate covers docker-compose/DB/migration; env / volume / live API-route E2E is still not included"

# ─── Result ───────────────────────────────────────────────────────────────────

echo ""
if [ "${FAILURES}" -eq 0 ]; then
  echo "=== Unified test gate passed ==="
  exit 0
else
  echo "=== Unified test gate failed ===" >&2
  for i in "${!FAILED_LAYERS[@]}"; do
    echo "FAILED_LAYER: ${FAILED_LAYERS[$i]}" >&2
    echo "COMMAND: ${FAILED_COMMANDS[$i]}" >&2
    echo "" >&2
  done
  echo "NEXT:" >&2
  echo "Run the listed command locally and fix only the failed layer." >&2
  echo "=== ${FAILURES} unified test gate failure(s) ===" >&2
  exit 1
fi
