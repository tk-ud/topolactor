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
echo "=== [BACKEND_SEED_COLLAPSE] seed manifest -> dispatcher -> runtime -> screen data -> db_notify -> SSE ==="
dotnet test "${REPO_ROOT}/backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj" --filter "FullyQualifiedName~ProjectionLaneSeedCollapse" --nologo --verbosity minimal
echo "OK  [BACKEND_SEED_COLLAPSE] backend seed projection collapse test passed"
echo "=== [FRONTEND_LANE] SSE receiver / dispatcher / projection runtime / render helper ==="
deno test "${REPO_ROOT}/frontend/tests/sseLane.test.ts" --allow-read
echo "OK  [FRONTEND_LANE] frontend projection lane tests passed"
echo "=== [FRONTEND_USER_FACING_RENDER] /demo ProjectionView / UserDemoStepper / UserDemoResultCard projectionDefinition consumption ==="
deno test "${REPO_ROOT}/frontend/tests/uiRenderedInteraction.test.ts" --allow-read --filter "projectionDefinition"
echo "OK  [FRONTEND_USER_FACING_RENDER] user-facing render projectionDefinition tests passed"
echo "=== [SEED_CONTRACT] static seed mapping parse ==="
echo "=== [SEED_TO_LANE_INTEGRATION] seed static parse -> lane harness -> projection assertion ==="
deno test "${REPO_ROOT}/frontend/tests/projectionLaneSeedHarness.test.ts" --allow-read
echo "OK  [SEED_CONTRACT] seed contract tests passed"
echo "OK  [SEED_TO_LANE_INTEGRATION] seed-to-lane projection assertions passed"
