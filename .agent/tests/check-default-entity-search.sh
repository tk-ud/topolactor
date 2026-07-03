#!/usr/bin/env bash
# check-default-entity-search.sh — local CI gate for default:entity:search integration test
# Runs backend integration tests and frontend Deno contract tests (entity search, admin API, pipeline continuity).
# Fails loudly on missing tools or test failures.
# No DB credentials, no production HTTP host, no real business data required.

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
FAILURES=0
run_lane(){ if ! noise_run "$1" ${@:2}; then FAILURES=$((FAILURES+1)); fi; }
run_lane "default_entity_search lane=backend_integration" dotnet test backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj --nologo --verbosity minimal
run_lane "default_entity_search lane=frontend_default_entity" deno test frontend/tests/defaultEntitySearch.test.ts --allow-read
run_lane "default_entity_search lane=frontend_admin_api" deno test frontend/tests/adminApi.test.ts --allow-read
run_lane "default_entity_search lane=frontend_pipeline_continuity" deno test frontend/tests/pipelineContinuity.test.ts --allow-read
[ "$FAILURES" -eq 0 ] && { echo "PASS default-entity-search lanes=4"; exit 0; }
echo "FAIL default-entity-search failures=$FAILURES" >&2; exit 1
