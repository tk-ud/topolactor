#!/usr/bin/env bash
# check-recommendation-pressure-lane-boundary.sh — SSOT lane boundary contract (deno, no ruby)
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

require_tool deno

cd "$REPO_ROOT"
deno test frontend/tests/recommendationPressureLaneGuard.test.ts --allow-read

echo "=== Recommendation pressure lane boundary check passed ==="
