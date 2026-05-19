#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FIXTURES_DIR="${SCRIPT_DIR}/fixtures/boundary-identity"

trim() {
  local s="$1"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

run_check() {
  local checklist="$1"
  local vfails=0

  vfail() {
    echo "FAIL: $1" >&2
    vfails=$((vfails + 1))
  }

  if [ ! -f "$checklist" ]; then
    echo "FAIL: Checklist file not found: $checklist" >&2
    return 1
  fi

  declare -a answers=()
  while IFS= read -r line; do
    raw="${line#Answer: }"
    norm="$(trim "${raw,,}")"
    answers+=("$norm")
  done < <(grep '^Answer: ' "$checklist" || true)

  total="${#answers[@]}"
  echo "=== Boundary Identity Gate ==="
  echo "File: $checklist"
  echo "Answers found: $total / 15"

  if [ "$total" -ne 15 ]; then
    vfail "Expected exactly 15 Answer: lines, found ${total}"
    return 1
  fi

  local i a ok
  for i in "${!answers[@]}"; do
    a="${answers[$i]}"
    ok=0
    case "$a" in
      yes|no|n/a) ok=1 ;;
    esac
    if [ "$ok" -eq 1 ]; then
      echo "OK  Q$((i+1)): $a"
    else
      vfail "Q$((i+1)) invalid answer: '$a' (must be yes/no/n/a)"
    fi
  done

  if [ "$vfails" -gt 0 ]; then
    return 1
  fi

  Q1="${answers[0]}"; Q2="${answers[1]}"; Q3="${answers[2]}"; Q4="${answers[3]}"; Q5="${answers[4]}"
  Q6="${answers[5]}"; Q7="${answers[6]}"; Q8="${answers[7]}"; Q9="${answers[8]}"; Q10="${answers[9]}"
  Q11="${answers[10]}"; Q12="${answers[11]}"; Q13="${answers[12]}"; Q14="${answers[13]}"; Q15="${answers[14]}"

  echo "=== Violation checks ==="

  if [ "$Q1" = "yes" ] && [ "$Q2" = "no" ]; then vfail "Q1=yes AND Q2=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q3" = "no" ]; then vfail "Q1=yes AND Q3=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q4" = "no" ]; then vfail "Q1=yes AND Q4=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q5" = "no" ]; then vfail "Q1=yes AND Q5=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q6" = "no" ]; then vfail "Q1=yes AND Q6=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q7" = "no" ]; then vfail "Q1=yes AND Q7=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q8" = "no" ]; then vfail "Q1=yes AND Q8=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q9" = "no" ]; then vfail "Q1=yes AND Q9=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q10" = "no" ]; then vfail "Q1=yes AND Q10=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q11" = "no" ]; then vfail "Q1=yes AND Q11=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q12" = "no" ] && [ "$Q13" = "no" ]; then vfail "Q1=yes AND Q12=no AND Q13=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q14" = "no" ]; then vfail "Q1=yes AND Q14=no"; fi
  if [ "$Q1" = "yes" ] && [ "$Q15" = "no" ]; then vfail "Q1=yes AND Q15=no"; fi

  if [ "$vfails" -eq 0 ]; then
    echo "=== Boundary Identity Gate: PASS ==="
    return 0
  fi

  echo "=== Boundary Identity Gate: ${vfails} violation(s) — FAIL ===" >&2
  return 1
}

if [ "${1:-}" = "--self-test" ]; then
  echo "=== Boundary Identity Gate — Self-test ==="
  fails=0
  for spec in \
    "pass.md:pass" \
    "fail-unanswered.md:fail" \
    "fail-missing-identity.md:fail" \
    "fail-missing-leakage-test.md:fail" \
    "fail-missing-todo-update.md:fail"
  do
    f="${spec%%:*}"; expect="${spec##*:}"; p="${FIXTURES_DIR}/${f}"
    if run_check "$p" >/dev/null 2>&1; then actual="pass"; else actual="fail"; fi
    if [ "$actual" = "$expect" ]; then
      echo "OK  ${f} -> ${actual}"
    else
      echo "FAIL: ${f} -> ${actual} (expected ${expect})"
      fails=$((fails + 1))
    fi
  done
  [ "$fails" -eq 0 ] && { echo "=== Self-test: PASS ==="; exit 0; }
  echo "=== Self-test: ${fails} failure(s) — FAIL ===" >&2
  exit 1
fi

if [ -z "${1:-}" ]; then
  echo "Usage: bash .agent/checklists/check-boundary-identity.sh <checklist-file>" >&2
  echo "   or: bash .agent/checklists/check-boundary-identity.sh --self-test" >&2
  exit 1
fi

run_check "$1"
