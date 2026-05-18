#!/usr/bin/env bash
# check-policy-judgment.sh — local-only Policy Judgment Gate
#
# Usage:
#   bash .agent/checklists/check-policy-judgment.sh <checklist-file>
#   bash .agent/checklists/check-policy-judgment.sh --self-test
#
# Local-only Agent judgment gate. NOT connected to any GitHub Actions workflow.
# Requires only bash. No credentials, no build tools.
#
# In self-test mode, validates all fixtures in fixtures/policy-judgment/:
#   pass.md               → expected: PASS
#   fail-unanswered.md    → expected: FAIL
#   fail-policy-violation.md → expected: FAIL
#   fail-partial-diff.md  → expected: FAIL
#   fail-local-checks.md  → expected: FAIL

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FIXTURES_DIR="${SCRIPT_DIR}/fixtures/policy-judgment"

# ─── Self-test mode ───────────────────────────────────────────────────────────

if [ "${1:-}" = "--self-test" ]; then
  echo ""
  echo "=== Policy Judgment Gate — Self-test ==="
  echo ""

  ST_FAILS=0

  for spec in \
    "pass.md:pass" \
    "fail-unanswered.md:fail" \
    "fail-policy-violation.md:fail" \
    "fail-partial-diff.md:fail" \
    "fail-local-checks.md:fail" \
    "fail-remaining-todos.md:fail"
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

# ─── Normal validation mode ───────────────────────────────────────────────────

CHECKLIST="${1:-}"
if [ -z "$CHECKLIST" ]; then
  echo "Usage:" >&2
  echo "  check-policy-judgment.sh <checklist-file>" >&2
  echo "  check-policy-judgment.sh --self-test" >&2
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

# ─── Parse answers ────────────────────────────────────────────────────────────
# Matches lines starting with "Answer: " (note the space — template lines
# "Answer:" with no space are intentionally excluded from the count).

declare -a ANSWERS=()
while IFS= read -r line; do
  raw="${line#Answer: }"
  norm="${raw,,}"
  norm="${norm%"${norm##*[![:space:]]}"}"
  norm="${norm#"${norm%%[![:space:]]*}"}"
  ANSWERS+=("$norm")
done < <(grep "^Answer: " "$CHECKLIST" || true)

TOTAL="${#ANSWERS[@]}"

echo ""
echo "=== Policy Judgment Gate ==="
echo "File: $CHECKLIST"
echo "Answers found: $TOTAL / 15"
echo ""

# V14: count check
if [ "$TOTAL" -ne 15 ]; then
  vfail "V16: Expected exactly 15 Answer: lines, found ${TOTAL} — fill in all Q1–Q15"
  echo ""
  echo "=== Policy Judgment Gate: ${VFAILS} violation(s) — FAIL ===" >&2
  exit 1
fi

# ─── Format validation (V12, V13) ─────────────────────────────────────────────

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
    vfail "V14/V15: Q${qnum}: invalid or empty answer \"${a}\" — must be yes / no / n/a"
    FORMAT_OK=false
  fi
done

if [ "$FORMAT_OK" = false ]; then
  echo ""
  echo "=== Policy Judgment Gate: ${VFAILS} format error(s) — FAIL ===" >&2
  exit 1
fi

# ─── Violation rules ──────────────────────────────────────────────────────────

Q1="${ANSWERS[0]}"
Q2="${ANSWERS[1]}"
Q3="${ANSWERS[2]}"
Q4="${ANSWERS[3]}"
Q5="${ANSWERS[4]}"
Q6="${ANSWERS[5]}"
Q7="${ANSWERS[6]}"
Q8="${ANSWERS[7]}"
Q9="${ANSWERS[8]}"
Q10="${ANSWERS[9]}"
Q11="${ANSWERS[10]}"
Q12="${ANSWERS[11]}"
Q13="${ANSWERS[12]}"
Q14="${ANSWERS[13]}"
Q15="${ANSWERS[14]}"

echo ""
echo "=== Violation checks ==="

ok_v() { echo "OK  $1"; }

# V1: Silent fallback
if [ "$Q5" = "yes" ]; then
  vfail "V1 (Q5=yes): silent fallback — missing policy/runtime data must not be substituted automatically without surfacing an explicit error"
else
  ok_v "V1: Q5=${Q5}"
fi

# V2: Unexplained production policy constant
if [ "$Q6" = "yes" ]; then
  vfail "V2 (Q6=yes): unexplained production policy constant — must be resolved from a policy surface (function_parameters / Registry / Manifest / structure_map)"
else
  ok_v "V2: Q6=${Q6}"
fi

# V3: Fallback present, no explicit error replacement
if [ "$Q2" = "yes" ] && [ "$Q3" = "no" ]; then
  vfail "V3 (Q2=yes, Q3=no): production fallback constant present but no explicit missing-policy / invalid-policy status returned"
else
  ok_v "V3: Q2=${Q2}, Q3=${Q3}"
fi

# V4: Policy/runtime value not from a policy surface
if [ "$Q1" = "yes" ] && [ "$Q4" = "no" ]; then
  vfail "V4 (Q1=yes, Q4=no): policy/runtime-affecting value introduced but not resolved from a policy surface"
else
  ok_v "V4: Q1=${Q1}, Q4=${Q4}"
fi

# V5: Canonical route bypassed
if [ "$Q7" = "yes" ]; then
  vfail "V5 (Q7=yes): canonical runtime route step bypassed — all steps are required"
else
  ok_v "V5: Q7=${Q7}"
fi

# V6: Business logic in frontend
if [ "$Q8" = "yes" ]; then
  vfail "V6 (Q8=yes): business logic or data computation added to frontend projection layer — frontend must be projection only"
else
  ok_v "V6: Q8=${Q8}"
fi

# V7: Broken reference swallowed
if [ "$Q9" = "yes" ]; then
  vfail "V7 (Q9=yes): broken reference swallowed silently — must return explicit validation error/status"
else
  ok_v "V7: Q9=${Q9}"
fi

# V8: Policy fields not consumed
if [ "$Q10" = "no" ]; then
  vfail "V8 (Q10=no): policy fields added/modified but not consumed by runtime or policy executor"
else
  ok_v "V8: Q10=${Q10}"
fi

# V9: Demo/mock values not isolated
if [ "$Q11" = "no" ]; then
  vfail "V9 (Q11=no): demo/mock/static values not isolated from runtime/policy behavior claims"
else
  ok_v "V9: Q11=${Q11}"
fi

# V10: Full branch diff not inspected / scenario contract not verified
if [ "$Q12" = "no" ]; then
  vfail "V10 (Q12=no): full branch diff not inspected and/or required scenario contract / boundary matrix verification missing — run 'git diff main...HEAD' and verify against .agent/tmp/tmp.txt when required before completing checklist"
else
  ok_v "V10: Q12=${Q12}"
fi

# V11: Local checks not passed
if [ "$Q14" = "no" ]; then
  vfail "V11 (Q14=no): required local checks not passed — run local CI before reporting completion"
else
  ok_v "V11: Q14=${Q14}"
fi

# V12: Checklist not based on full branch diff / scenario contract / boundary matrix verification
if [ "$Q13" = "no" ]; then
  vfail "V12 (Q13=no): checklist answers not based on full branch diff and required scenario contract / boundary matrix verification — answers must reflect the complete branch state"
else
  ok_v "V12: Q13=${Q13}"
fi

# V13: Remaining TODOs not listed
if [ "$Q15" = "no" ]; then
  vfail "V13 (Q15=no): remaining TODOs not listed in completion report or PR summary"
else
  ok_v "V13: Q15=${Q15}"
fi

# ─── Result ───────────────────────────────────────────────────────────────────

echo ""
if [ "$VFAILS" -eq 0 ]; then
  echo "=== Policy Judgment Gate: PASS ==="
  exit 0
else
  echo "=== Policy Judgment Gate: ${VFAILS} violation(s) — FAIL ===" >&2
  exit 1
fi
