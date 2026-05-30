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

# ─── FUNCTION_BOUNDARY ────────────────────────────────────────────────────────

echo ""
echo "=== [FUNCTION_BOUNDARY] Runtime function unit tests ==="
echo "    Scope: RuntimeExecutor.ExecuteAsync, ManifestDispatcher.DispatchAsync,"
echo "           ManifestDispatcher.BuildLegacyHookRequest,"
echo "           TargetDispatchOverride.ValidateRequest, TargetDispatchOverride.TryHandleAsync,"
echo "           OperationVectorResolver.Resolve, RuntimeGuard.Validate,"
echo "           ContextRouteRecommendationResolver.ResolveAsync"

if dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj \
    --nologo --verbosity minimal; then
  echo "OK  [FUNCTION_BOUNDARY] runtime function unit tests passed"
else
  fail "[FUNCTION_BOUNDARY] runtime function unit tests failed"
fi

# ─── RUNTIME_INTEGRATION ──────────────────────────────────────────────────────

echo ""
echo "=== [RUNTIME_INTEGRATION] Integration boundary tests ==="
echo "    Scope: DefaultEntitySearchIntegrationTests"
echo "           full dispatch path: dispatcher / executor / override"

if dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj \
    --nologo --verbosity minimal; then
  echo "OK  [RUNTIME_INTEGRATION] integration boundary tests passed"
else
  fail "[RUNTIME_INTEGRATION] integration boundary tests failed"
fi

# ─── FRONTEND_CONTRACT ────────────────────────────────────────────────────────

echo ""
echo "=== [FRONTEND_CONTRACT] Frontend API proxy and dispatch contract tests ==="
echo "    Scope: adminApi.test.ts, defaultEntitySearch.test.ts, pipelineContinuity.test.ts"

if deno test \
    frontend/tests/adminApi.test.ts \
    frontend/tests/defaultEntitySearch.test.ts \
    frontend/tests/pipelineContinuity.test.ts \
    --allow-read; then
  echo "OK  [FRONTEND_CONTRACT] frontend contract tests passed"
else
  fail "[FRONTEND_CONTRACT] frontend contract tests failed"
fi


# ─── SSOT_VOCABULARY_CONTRACT ───────────────────────────────────────────────

echo ""
echo "=== [SSOT_VOCABULARY_CONTRACT] SSOT vocabulary subset contract checks ==="
echo "    Scope: component catalog / seed runtime destination / pipeline prohibited+required identity vocabulary"

if bash .agent/tests/check-ssot-vocabulary-contract.sh; then
  echo "OK  [SSOT_VOCABULARY_CONTRACT] vocabulary contract checks passed"
else
  fail "[SSOT_VOCABULARY_CONTRACT] vocabulary contract checks failed"
fi

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
  echo "=== ${FAILURES} unified test gate failure(s) ===" >&2
  exit 1
fi
