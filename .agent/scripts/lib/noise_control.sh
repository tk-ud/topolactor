#!/usr/bin/env bash
# Compact success / verbose failure helpers for .agent/tests check-*.sh gates.
# Policy: .agent/tests/README.md (ok log short; error detailed replay on stderr).

noise_run() {
  local label="$1"; shift
  local tmp
  tmp="$(mktemp)"
  set +e
  "$@" >"$tmp" 2>&1
  local code=$?
  set -e
  if [ "$code" -ne 0 ]; then
    echo "FAIL ${label} exit=${code} command=$*" >&2
    cat "$tmp" >&2
    rm -f "$tmp"
    return "$code"
  fi
  rm -f "$tmp"
  [ "${NOISE_QUIET_SUCCESS:-0}" = "1" ] || echo "PASS ${label}"
}

noise_run_bash() {
  local label="$1"
  local cmd="$2"
  local tmp
  tmp="$(mktemp)"
  set +e
  bash -c "$cmd" >"$tmp" 2>&1
  local code=$?
  set -e
  if [ "$code" -ne 0 ]; then
    echo "FAIL ${label} exit=${code} command=${cmd}" >&2
    cat "$tmp" >&2
    rm -f "$tmp"
    return "$code"
  fi
  rm -f "$tmp"
  [ "${NOISE_QUIET_SUCCESS:-0}" = "1" ] || echo "PASS ${label}"
}

# Multi-lane gate: suppress per-lane PASS; caller emits one summary line.
noise_run_lane() {
  NOISE_QUIET_SUCCESS=1 noise_run "$@"
}
