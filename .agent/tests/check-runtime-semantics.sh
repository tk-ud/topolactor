#!/usr/bin/env bash
# check-runtime-semantics.sh — runtime meaning checks
# Validates auth/dispatch/recommendation/admin-proxy semantics via unit/integration slices.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

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
noise_run "runtime_semantics lane=backend_runtime" dotnet test backend/tests/Topolactor.Runtime.Tests/Topolactor.Runtime.Tests.csproj --nologo --verbosity minimal
noise_run "runtime_semantics lane=backend_integration" dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj --nologo --verbosity minimal
noise_run "runtime_semantics lane=frontend_api" deno test frontend/tests/adminApi.test.ts frontend/tests/defaultEntitySearch.test.ts frontend/tests/pipelineContinuity.test.ts --allow-read
echo "PASS runtime-semantics lanes=3"
