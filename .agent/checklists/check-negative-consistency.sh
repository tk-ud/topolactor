#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FIXTURES_DIR="${SCRIPT_DIR}/fixtures/negative-consistency"

fail(){ echo "FAIL: $1" >&2; exit 1; }

extract_field() {
  local file="$1" q="$2" key="$3"
  awk -v q="$q" -v key="$key" '$0 ~ q {f=1} f&&index($0,key)==1 {sub("^" key "[[:space:]]*",""); print; exit}' "$file"
}

check_question() {
  local file="$1" q="$2"
  local ans ev risk
  ans="$(extract_field "$file" "$q" 'Answer:')"
  ev="$(extract_field "$file" "$q" 'Evidence:')"
  risk="$(extract_field "$file" "$q" 'Remaining risk:')"

  [[ -n "$ans" ]] || fail "$q Answer is missing or empty"
  [[ "$ans" == "問題なし" || "$ans" == "問題あり" ]] || fail "$q Answer must be 問題なし/問題あり"
  [[ -n "$ev" ]] || fail "$q Evidence is missing or empty"
  if ! echo "$ev" | grep -Eqi 'diff|test|ci|contract|checklist|todo'; then
    fail "$q Evidence must reference diff/test/CI/contract/checklist/TODO"
  fi
  if [[ -n "$risk" ]] && ! echo "$risk" | grep -Eqi 'todo|out[- ]of[- ]scope|none|なし'; then
    fail "$q Remaining risk must link TODO/out-of-scope (or explicit none)"
  fi
  echo "$ans"
}

if [[ "${1:-}" == "--self-test" ]]; then
  fails=0
  for spec in     pass.md:pass     fail-empty-evidence.md:fail     fail-q3-blocking.md:fail     fail-q1-blocking.md:fail     fail-q2-blocking.md:fail     fail-eligibility-missing.md:fail     fail-eligibility-invalid.md:fail     fail-required-not-executed-pass.md:fail
  do
    f="${spec%%:*}"; e="${spec##*:}"
    if bash "$0" "${FIXTURES_DIR}/$f" >/dev/null 2>&1; then a=pass; else a=fail; fi
    [[ "$a" == "$e" ]] || { echo "FAIL: $f -> $a expected $e"; fails=$((fails+1)); }
  done
  [[ $fails -eq 0 ]] && echo "PASS" && exit 0 || exit 1
fi

file="${1:-}"; [[ -f "$file" ]] || { echo "Usage: $0 <file>|--self-test" >&2; exit 1; }

q1="$(check_question "$file" 'Q1\.')"
q2="$(check_question "$file" 'Q2\.')"
q3="$(check_question "$file" 'Q3\.')"

elig_line="$(grep -m1 '^Completion-Eligibility:' "$file" || true)"
[[ -n "$elig_line" ]] || fail 'Completion-Eligibility is missing'
elig="${elig_line#Completion-Eligibility:}"
elig="$(echo "$elig" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//')"
[[ -n "$elig" ]] || fail 'Completion-Eligibility is empty'
[[ "$elig" == "PASS" || "$elig" == "BLOCKING" ]] || fail 'Completion-Eligibility must be PASS or BLOCKING'

if [[ "$q1" == "問題あり" || "$q2" == "問題あり" || "$q3" == "問題あり" ]]; then
  [[ "$elig" == "BLOCKING" ]] || fail 'Any 問題あり requires Completion-Eligibility BLOCKING'
fi

if [[ "$elig" == "PASS" ]]; then
  [[ "$q1" == "問題なし" && "$q2" == "問題なし" && "$q3" == "問題なし" ]] || fail 'Completion-Eligibility PASS requires all Q1/Q2/Q3 = 問題なし'
fi

if grep -Eq 'REQUIRED_NOT_EXECUTED.*PASS' "$file"; then
  fail 'REQUIRED_NOT_EXECUTED must not be PASS-equivalent'
fi

echo "Negative Consistency Gate: PASS"
