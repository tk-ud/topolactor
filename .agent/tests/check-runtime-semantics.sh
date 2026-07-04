#!/usr/bin/env bash
# check-runtime-semantics.sh — runtime meaning checks
# Validates auth/dispatch/recommendation/admin-proxy semantics via unit/integration slices.
# Output policy: .agent/tests/README.md

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

FAILURES=0

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
  if ! noise_run_lane "$1" "${@:2}"; then
    FAILURES=$((FAILURES + 1))
  fi
}

run_lane "runtime_semantics lane=backend_runtime" dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj --nologo --verbosity minimal
run_lane "runtime_semantics lane=backend_integration" dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj --nologo --verbosity minimal
run_lane "runtime_semantics lane=frontend_api" deno test frontend/tests/adminApi.test.ts frontend/tests/defaultEntitySearch.test.ts frontend/tests/pipelineContinuity.test.ts --allow-read

if [ "${FAILURES}" -eq 0 ]; then
  echo "PASS runtime-semantics lanes=3"
  exit 0
fi

echo "FAIL runtime-semantics failures=${FAILURES}" >&2
exit 1
