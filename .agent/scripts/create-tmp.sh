#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$REPO_ROOT/.agent/tmp"
TMP_FILE="$TMP_DIR/tmp.txt"

mkdir -p "$TMP_DIR"

if [ -f "$TMP_FILE" ]; then
  echo "tmp already exists: .agent/tmp/tmp.txt"
  exit 0
fi

cat > "$TMP_FILE" <<'EOF'
# Temporary Planning Surface
# Keep this short: initial constraints/policy only.
# Do NOT use as reasoning log, PR summary, or implementation log.
#
# Before Policy Judgment Checklist:
# 1) Compare this memo with: git diff main...HEAD
# 2) Resolve scope drift or document intentional scope change in completion/PR summary
# 3) Delete this file via: bash .agent/scripts/delete-tmp.sh
EOF

echo "created: .agent/tmp/tmp.txt"
