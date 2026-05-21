#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TARGET="$REPO_ROOT/.agent/protocols/reports-and-todos.md"
FAILURES=0

fail(){ echo "FAIL: $1" >&2; FAILURES=$((FAILURES+1)); }
pass(){ echo "OK  : $1"; }

check_term(){
  local term="$1"
  if grep -qF -- "$term" "$TARGET"; then
    pass "reports-and-todos.md -> $term"
  else
    fail "reports-and-todos.md missing: $term"
  fi
}

echo "=== Completion Summary Template Evidence Check ==="
check_term "### Existing PR update follow-up evidence"
check_term "- Existing PR update: YES / NO"
check_term "- Target PR:"
check_term "- Head commit:"
check_term "- Follow-up PR comment evidence:"
check_term "POSTED / PR_COMMENT_NOT_POSTED / NOT_REQUIRED"
check_term "- Posted comment verification:"
check_term "VERIFIED / NOT_EXECUTED / UNAVAILABLE"
check_term "- Paste-ready comment body when not posted:"

if [ "$FAILURES" -eq 0 ]; then
  echo "=== Completion summary template evidence checks passed ==="
  exit 0
else
  echo "=== $FAILURES completion summary template evidence check(s) failed ===" >&2
  exit 1
fi
