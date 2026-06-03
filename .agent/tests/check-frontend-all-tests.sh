#!/usr/bin/env bash
# check-frontend-all-tests.sh — run all frontend tests in frontend/tests/*.test.ts

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

if ! command -v deno &>/dev/null; then
  echo "ERROR: deno is required. Install Deno and retry." >&2
  echo "This check was NOT executed — missing tool is not a pass." >&2
  exit 1
fi

cd "${REPO_ROOT}"

echo "=== [FRONTEND_ALL_TESTS] Running all frontend tests in frontend/tests/ ==="
deno test frontend/tests/ --allow-read --allow-env

echo ""
echo "=== [FRONTEND_ALL_TESTS] PASSED ==="
