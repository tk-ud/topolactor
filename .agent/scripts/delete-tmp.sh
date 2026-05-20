#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$REPO_ROOT/.agent/tmp"

cleanup() {
  local target="$1"
  if [ -e "$target" ]; then
    rm -rf "$target"
    echo "deleted: ${target#$REPO_ROOT/}"
  else
    echo "already absent: ${target#$REPO_ROOT/}"
  fi
}

cleanup "$TMP_DIR/tmp.txt"
cleanup "$TMP_DIR/workflow-state.env"
cleanup "$TMP_DIR/checklists"
