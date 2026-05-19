#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FIXTURES_DIR="${SCRIPT_DIR}/fixtures/negative-consistency"

check_file() {
  local file="$1"
  local q="$2"
  local ans ev risk
  ans="$(awk -v q="$q" '$0 ~ q {f=1} f&&/^Answer:/ {sub(/^Answer:[[:space:]]*/,""); print; exit}' "$file")"
  ev="$(awk -v q="$q" '$0 ~ q {f=1} f&&/^Evidence:/ {sub(/^Evidence:[[:space:]]*/,""); print; exit}' "$file")"
  risk="$(awk -v q="$q" '$0 ~ q {f=1} f&&/^Remaining risk:/ {sub(/^Remaining risk:[[:space:]]*/,""); print; exit}' "$file")"

  [[ "$ans" == "問題なし" || "$ans" == "問題あり" ]] || { echo "FAIL: invalid Answer in $q" >&2; return 1; }
  [[ -n "$ev" ]] || { echo "FAIL: missing Evidence in $q" >&2; return 1; }
  if [[ "$ans" == "問題なし" && -z "$ev" ]]; then
    echo "FAIL: 問題なし requires evidence in $q" >&2; return 1
  fi
  if ! echo "$ev" | grep -Eqi 'diff|test|ci|contract|checklist|todo'; then
    echo "FAIL: Evidence must reference diff/test/CI/contract/checklist/TODO in $q" >&2; return 1
  fi
  if [[ -n "$risk" ]] && ! echo "$risk" | grep -Eqi 'todo|out[- ]of[- ]scope'; then
    echo "FAIL: Remaining risk must link TODO or out-of-scope in $q" >&2; return 1
  fi
}

if [[ "${1:-}" == "--self-test" ]]; then
  fails=0
  for spec in pass.md:pass fail-empty-evidence.md:fail fail-q3-blocking.md:fail fail-required-not-executed-pass.md:fail; do
    f="${spec%%:*}"; e="${spec##*:}"
    if bash "$0" "${FIXTURES_DIR}/$f" >/dev/null 2>&1; then a=pass; else a=fail; fi
    [[ "$a" == "$e" ]] || { echo "FAIL: $f -> $a expected $e"; fails=$((fails+1)); }
  done
  [[ $fails -eq 0 ]] && echo "PASS" && exit 0 || exit 1
fi

file="${1:-}"; [[ -f "$file" ]] || { echo "Usage: $0 <file>|--self-test" >&2; exit 1; }

check_file "$file" 'Q1\.'
check_file "$file" 'Q2\.'
check_file "$file" 'Q3\.'

q3_ans="$(awk '/Q3\./{f=1} f&&/^Answer:/ {sub(/^Answer:[[:space:]]*/,""); print; exit}' "$file")"
elig="$(grep -m1 '^Completion-Eligibility:' "$file" | sed 's/^Completion-Eligibility:[[:space:]]*//')"

if [[ "$q3_ans" == "問題あり" && "$elig" == "PASS" ]]; then
  echo "FAIL: Q3 is 問題あり but Completion-Eligibility is PASS" >&2; exit 1
fi
if grep -Eq 'REQUIRED_NOT_EXECUTED.*PASS' "$file"; then
  echo "FAIL: REQUIRED_NOT_EXECUTED must not be PASS-equivalent" >&2; exit 1
fi

echo "Negative Consistency Gate: PASS"
