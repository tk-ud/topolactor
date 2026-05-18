#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_FILE="$REPO_ROOT/.agent/tmp/tmp.txt"

if [ -f "$TMP_FILE" ]; then
  rm -f "$TMP_FILE"
  echo "deleted: .agent/tmp/tmp.txt"
else
  echo "already absent: .agent/tmp/tmp.txt"
fi
