#!/usr/bin/env bash
set -euo pipefail

FAILURES=0

run_check() {
  local label="$1"
  local cmd="$2"

  echo ""
  echo "=== [LOCAL_CI] ${label} ==="

  if bash -c "$cmd"; then
    echo "RESULT [${label}] PASS"
  else
    local code=$?
    if [ "$code" -eq 127 ]; then
      echo "RESULT [${label}] NOT_EXECUTED (missing tool or command; exit=${code})" >&2
    else
      echo "RESULT [${label}] FAIL (exit=${code})" >&2
    fi
    FAILURES=$((FAILURES + 1))
  fi
}

run_check "UNIFIED_TEST_GATE" "bash .agent/tests/check-unified-test-gate.sh"
run_check "RUNTIME_ENVIRONMENT" "bash .agent/tests/check-runtime-environment.sh"
# check-structure.sh must run last.
run_check "STRUCTURE_CHECK_LAST" "bash .agent/tests/check-structure.sh"

echo ""
if [ "${FAILURES}" -eq 0 ]; then
  echo "=== [LOCAL_CI] PASS (all checks passed) ==="
  exit 0
fi

echo "=== [LOCAL_CI] FAIL (${FAILURES} check(s) failed or not executed) ===" >&2
exit 1
