#!/usr/bin/env bash
set -euo pipefail

FAILURES=0

run_check() {
  local label="$1"
  local cmd="$2"
  local required_mode="${3:-required}"

  echo ""
  echo "=== [LOCAL_CI] ${label} ==="

  local out
  set +e
  out="$(bash -c "$cmd" 2>&1)"
  code=$?
  set -e
  echo "$out"

  if [ "$code" -eq 0 ]; then
    echo "RESULT [${label}] PASS"
    return
  fi

  if [ "$label" = "RUNTIME_ENVIRONMENT" ] && echo "$out" | grep -qE "docker.sock|docker API|daemon is running"; then
    echo "RESULT [${label}] REQUIRED_NOT_EXECUTED (docker runtime unavailable; not PASS)" >&2
    FAILURES=$((FAILURES + 1))
    return
  fi

  if [ "$code" -eq 127 ]; then
    if [ "$required_mode" = "required" ]; then
      echo "RESULT [${label}] REQUIRED_NOT_EXECUTED (missing tool/command; not PASS)" >&2
    else
      echo "RESULT [${label}] NOT_EXECUTED (missing tool/command; not PASS)" >&2
    fi
  else
    echo "RESULT [${label}] FAIL (exit=${code})" >&2
  fi
  FAILURES=$((FAILURES + 1))
}

run_check "UNIFIED_TEST_GATE" "bash .agent/tests/check-unified-test-gate.sh" "required"
run_check "RUNTIME_ENVIRONMENT" "bash .agent/tests/check-runtime-environment.sh" "required"
run_check "EXTERNAL_PORT_COMPAT_ABSORPTION" "bash .agent/tests/check-external-port-compat-absorption.sh" "required"
run_check "EXTERNAL_PORT_PARENT_COMPLETION" "bash .agent/tests/check-external-port-parent-completion.sh" "required"
run_check "WEBHOOK_INBOX_PORT_CONSUMER" "bash .agent/tests/check-webhook-inbox-port-consumer.sh" "required"
# check-structure.sh must run last.
run_check "STRUCTURE_CHECK_LAST" "bash .agent/tests/check-structure.sh" "required"

echo ""
if [ "${FAILURES}" -eq 0 ]; then
  echo "=== [LOCAL_CI] PASS (all checks passed) ==="
  exit 0
fi

echo "=== [LOCAL_CI] FAIL (${FAILURES} check(s) failed or not executed) ===" >&2
exit 1
