#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FIXTURES_DIR="${SCRIPT_DIR}/fixtures/policy-judgment"

ALLOWED_NEEDS=(
  "REQUIRED_RUNTIME_CHANGE"
  "REQUIRED_POLICY_SURFACE_CHANGE"
  "REQUIRED_DOC_RUNTIME_POLICY_CLAIM"
  "NOT_REQUIRED_DOC_NO_RUNTIME_POLICY_CLAIM"
  "NOT_REQUIRED_MECHANICAL_ONLY"
  "OUT_OF_SCOPE"
)

is_allowed_need() {
  local x="$1"
  for n in "${ALLOWED_NEEDS[@]}"; do
    [ "$x" = "$n" ] && return 0
  done
  return 1
}

if [ "${1:-}" = "--self-test" ]; then
  echo ""
  echo "=== Policy Judgment Gate — Self-test ==="
  echo ""

  ST_FAILS=0

  for spec in \
    "pass.md:pass" \
    "pass-not-required-doc-no-claim.md:pass" \
    "pass-required-doc-runtime-policy-claim.md:pass" \
    "fail-unanswered.md:fail" \
    "fail-policy-violation.md:fail" \
    "fail-partial-diff.md:fail" \
    "fail-local-checks.md:fail" \
    "fail-remaining-todos.md:fail" \
    "fail-missing-need.md:fail" \
    "fail-invalid-need.md:fail" \
    "fail-missing-rationale.md:fail" \
    "fail-required-without-answers.md:fail" \
    "fail-not-required-with-answers.md:fail"
  do
    fname="${spec%%:*}"
    expect="${spec##*:}"
    fpath="${FIXTURES_DIR}/${fname}"

    if [ ! -f "$fpath" ]; then
      echo "FAIL: fixture not found: ${fpath}"
      ST_FAILS=$((ST_FAILS + 1))
      continue
    fi

    if bash "$0" "$fpath" > /dev/null 2>&1; then
      actual="pass"
    else
      actual="fail"
    fi

    if [ "$actual" = "$expect" ]; then
      echo "OK  ${fname} → ${actual}"
    else
      echo "FAIL: ${fname} → ${actual} (expected ${expect})"
      ST_FAILS=$((ST_FAILS + 1))
    fi
  done

  echo ""
  if [ "$ST_FAILS" -eq 0 ]; then
    echo "=== Self-test: PASS ==="
    exit 0
  else
    echo "=== Self-test: ${ST_FAILS} failure(s) — FAIL ===" >&2
    exit 1
  fi
fi

CHECKLIST="${1:-}"
if [ -z "$CHECKLIST" ]; then
  echo "Usage:" >&2
  echo "  check-policy-judgment.sh <checklist-file>" >&2
  echo "  check-policy-judgment.sh --self-test" >&2
  echo "Recommended flow for normal work:" >&2
  echo "  1) copy template to .agent/tmp/" >&2
  echo "  2) fill answers in .agent/tmp/ copy (never template)" >&2
  echo "  3) run this check against the .agent/tmp/ copy" >&2
  echo "  4) delete .agent/tmp/ copy before completion" >&2
  exit 1
fi

if [ ! -f "$CHECKLIST" ]; then
  echo "FAIL: Checklist file not found: $CHECKLIST" >&2
  exit 1
fi

VFAILS=0

vfail() {
  echo "FAIL: $1" >&2
  VFAILS=$((VFAILS + 1))
}

NEED_LINE="$(grep -m1 '^Policy-Judgment-Need:' "$CHECKLIST" || true)"
RATIONALE_LINE="$(grep -m1 '^Policy-Judgment-Rationale:' "$CHECKLIST" || true)"
NEED="${NEED_LINE#Policy-Judgment-Need:}"
RATIONALE="${RATIONALE_LINE#Policy-Judgment-Rationale:}"
NEED="$(echo "$NEED" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//')"
RATIONALE="$(echo "$RATIONALE" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//')"

if [ -z "$NEED_LINE" ] || [ -z "$NEED" ]; then
  vfail "Policy-Judgment-Need is missing or empty"
elif ! is_allowed_need "$NEED"; then
  vfail "Policy-Judgment-Need must be one of allowed values; got '${NEED}'"
fi

if [ -z "$RATIONALE_LINE" ] || [ -z "$RATIONALE" ]; then
  vfail "Policy-Judgment-Rationale is missing or empty"
fi

# Parse answers

declare -a ANSWERS=()
while IFS= read -r line; do
  raw="${line#Answer: }"
  norm="${raw,,}"
  norm="${norm%"${norm##*[![:space:]]}"}"
  norm="${norm#"${norm%%[![:space:]]*}"}"
  ANSWERS+=("$norm")
done < <(grep '^Answer: ' "$CHECKLIST" || true)

TOTAL="${#ANSWERS[@]}"

echo ""
echo "=== Policy Judgment Gate (Recursive Verification Gate enforced; scenario contract aware) ==="
echo "File: $CHECKLIST"
echo "Policy-Judgment-Need: ${NEED:-<missing>}"
echo "Answers found: $TOTAL"
echo ""

if [ "$VFAILS" -gt 0 ]; then
  echo "=== Policy Judgment Gate: ${VFAILS} violation(s) — FAIL ===" >&2
  exit 1
fi

if [[ "$NEED" == REQUIRED_* ]]; then
  if [ "$TOTAL" -ne 15 ]; then
    vfail "V16: REQUIRED_* must include exactly 15 Answer: lines, found ${TOTAL}"
    echo "=== Policy Judgment Gate: ${VFAILS} violation(s) — FAIL ===" >&2
    exit 1
  fi

  VALID=("yes" "no" "n/a")
  FORMAT_OK=true
  for i in "${!ANSWERS[@]}"; do
    a="${ANSWERS[$i]}"
    qnum=$((i + 1))
    ok=false
    for v in "${VALID[@]}"; do
      [ "$a" = "$v" ] && ok=true && break || true
    done
    if [ "$ok" = true ]; then
      echo "OK  Q${qnum}: ${a}"
    else
      vfail "V14/V15: Q${qnum}: invalid or empty answer '${a}' — must be yes / no / n/a"
      FORMAT_OK=false
    fi
  done

  if [ "$FORMAT_OK" = false ]; then
    echo "=== Policy Judgment Gate: ${VFAILS} format error(s) — FAIL ===" >&2
    exit 1
  fi

  Q1="${ANSWERS[0]}"; Q2="${ANSWERS[1]}"; Q3="${ANSWERS[2]}"; Q4="${ANSWERS[3]}"; Q5="${ANSWERS[4]}";
  Q6="${ANSWERS[5]}"; Q7="${ANSWERS[6]}"; Q8="${ANSWERS[7]}"; Q9="${ANSWERS[8]}"; Q10="${ANSWERS[9]}";
  Q11="${ANSWERS[10]}"; Q12="${ANSWERS[11]}"; Q13="${ANSWERS[12]}"; Q14="${ANSWERS[13]}"; Q15="${ANSWERS[14]}"

  ok_v() { echo "OK  $1"; }
  echo ""; echo "=== Violation checks ==="
  [ "$Q5" = "yes" ] && vfail "V1 (Q5=yes): silent fallback" || ok_v "V1: Q5=${Q5}"
  [ "$Q6" = "yes" ] && vfail "V2 (Q6=yes): unexplained production policy constant" || ok_v "V2: Q6=${Q6}"
  [ "$Q2" = "yes" ] && [ "$Q3" = "no" ] && vfail "V3 (Q2=yes, Q3=no): fallback present, no explicit status" || ok_v "V3: Q2=${Q2}, Q3=${Q3}"
  [ "$Q1" = "yes" ] && [ "$Q4" = "no" ] && vfail "V4 (Q1=yes, Q4=no): not resolved from policy surface" || ok_v "V4: Q1=${Q1}, Q4=${Q4}"
  [ "$Q7" = "yes" ] && vfail "V5 (Q7=yes): canonical route bypassed" || ok_v "V5: Q7=${Q7}"
  [ "$Q8" = "yes" ] && vfail "V6 (Q8=yes): business logic in frontend projection" || ok_v "V6: Q8=${Q8}"
  [ "$Q9" = "yes" ] && vfail "V7 (Q9=yes): broken reference swallowed" || ok_v "V7: Q9=${Q9}"
  [ "$Q10" = "no" ] && vfail "V8 (Q10=no): policy fields not consumed" || ok_v "V8: Q10=${Q10}"
  [ "$Q11" = "no" ] && vfail "V9 (Q11=no): demo/mock/static not isolated" || ok_v "V9: Q11=${Q11}"
  [ "$Q12" = "no" ] && vfail "V10 (Q12=no): full diff/scenario verification missing" || ok_v "V10: Q12=${Q12}"
  [ "$Q14" = "no" ] && vfail "V11 (Q14=no): required local checks not passed" || ok_v "V11: Q14=${Q14}"
  [ "$Q13" = "no" ] && vfail "V12 (Q13=no): checklist not based on full diff" || ok_v "V12: Q13=${Q13}"
  [ "$Q15" = "no" ] && vfail "V13 (Q15=no): remaining TODOs not listed" || ok_v "V13: Q15=${Q15}"

  if [ "$VFAILS" -eq 0 ]; then
    echo ""; echo "=== Policy Judgment Gate: PASS ==="; exit 0
  else
    echo ""; echo "=== Policy Judgment Gate: ${VFAILS} violation(s) — FAIL ===" >&2; exit 1
  fi
fi

# NOT_REQUIRED_* / OUT_OF_SCOPE
if [ "$TOTAL" -ne 0 ]; then
  vfail "${NEED} must include 0 Answer: lines, found ${TOTAL}"
fi

if [ "$VFAILS" -eq 0 ]; then
  echo "=== Policy Judgment Gate: NOT REQUIRED / OUT OF SCOPE declaration accepted ==="
  exit 0
else
  echo "=== Policy Judgment Gate: ${VFAILS} violation(s) — FAIL ===" >&2
  exit 1
fi
