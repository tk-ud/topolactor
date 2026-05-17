#!/usr/bin/env bash
# check-default-entity-search.sh — local CI gate for default:entity:search integration test
# Runs backend integration tests and frontend Deno contract test.
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

echo ""
echo "=== Backend integration test: default:entity:search ==="
if dotnet test "${REPO_ROOT}/backend/tests/Topolactor.Integration.Tests/Topolactor.Integration.Tests.csproj" \
    --nologo --verbosity minimal; then
  echo "OK  [backend] integration tests passed"
else
  echo "FAIL: backend integration tests failed" >&2
  FAILURES=$((FAILURES + 1))
fi

echo ""
echo "=== Frontend contract test: default:entity:search ==="
if deno test "${REPO_ROOT}/frontend/tests/defaultEntitySearch.test.ts" --allow-read; then
  echo "OK  [frontend] contract test passed"
else
  echo "FAIL: frontend contract test failed" >&2
  FAILURES=$((FAILURES + 1))
fi

echo ""
if [ "${FAILURES}" -eq 0 ]; then
  echo "=== All default:entity:search checks passed ==="
  exit 0
else
  echo "=== ${FAILURES} check(s) failed ===" >&2
  exit 1
fi
