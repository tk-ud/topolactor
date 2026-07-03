#!/usr/bin/env bash
# Shared helpers for compact success output with full replay on failure.

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
