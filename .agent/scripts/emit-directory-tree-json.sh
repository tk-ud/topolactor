#!/usr/bin/env bash
# emit-directory-tree-json.sh — thin CI-entrypoint wrapper around the general
# directory-JSON-observation mechanism (emit-directory-tree-json.py).
#
# Bash is used here only as an orchestration/CI-entrypoint shim; structured
# JSON emission itself is Python3 stdlib (see emit-directory-tree-json.py).
#
# Usage:
#   bash .agent/scripts/emit-directory-tree-json.sh [--root <dir>] [--depth <n>] [--output <file>]
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec python3 "$SCRIPT_DIR/emit-directory-tree-json.py" "$@"
