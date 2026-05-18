#!/usr/bin/env bash
# check-runtime-policy-local.sh — local-only Runtime Policy Checklist Gate
#
# Usage: bash .agent/tests/check-runtime-policy-local.sh <checklist-file>
#
# Validates a completed runtime-policy.md checklist against topolactor
# architectural rules. Local only — not wired to any GitHub Actions workflow.
# Requires only bash. No credentials, no build tools.

set -euo pipefail

CHECKLIST="${1:-}"
if [ -z "$CHECKLIST" ]; then
  echo "Usage: check-runtime-policy-local.sh <checklist-file>" >&2
  echo "Example: bash .agent/tests/check-runtime-policy-local.sh .agent/checklists/runtime-policy.md" >&2
  exit 1
fi

if [ ! -f "$CHECKLIST" ]; then
  echo "FAIL: Checklist file not found: $CHECKLIST" >&2
  exit 1
fi

FAILURES=0

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

# ─── Parse answers ────────────────────────────────────────────────────────────
# Each completed answer is on a line matching "^Answer: "
# Answers are collected in order: Q1 first, Q10 last.

declare -a ANSWERS=()
while IFS= read -r line; do
  raw="${line#Answer: }"
  normalized="${raw,,}"              # lowercase
  normalized="${normalized%"${normalized##*[![:space:]]}"}"  # trim trailing space
  normalized="${normalized#"${normalized%%[![:space:]]*}"}"  # trim leading space
  ANSWERS+=("$normalized")
done < <(grep "^Answer: " "$CHECKLIST")

TOTAL="${#ANSWERS[@]}"

echo ""
echo "=== Runtime Policy Checklist Gate ==="
echo "File: $CHECKLIST"
echo "Answers found: $TOTAL"
echo ""

if [ "$TOTAL" -ne 10 ]; then
  fail "Expected exactly 10 Answer: lines, found $TOTAL. Fill in all Q1–Q10."
  echo ""
  echo "=== Runtime Policy Checklist: FAIL ===" >&2
  exit 1
fi

# ─── Validate answer values ───────────────────────────────────────────────────

VALID_SET=("yes" "no" "n/a")
FORMAT_OK=true

for i in "${!ANSWERS[@]}"; do
  a="${ANSWERS[$i]}"
  qnum=$((i + 1))
  valid=false
  for v in "${VALID_SET[@]}"; do
    if [ "$a" = "$v" ]; then
      valid=true
      break
    fi
  done
  if [ "$valid" = true ]; then
    echo "OK  Q${qnum}: ${a}"
  else
    fail "Q${qnum}: invalid or empty answer \"${a}\" — must be yes / no / n/a"
    FORMAT_OK=false
  fi
done

# ─── Apply violation rules ────────────────────────────────────────────────────
# Only applied when all answers have valid format.

if [ "$FORMAT_OK" = true ]; then
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

  echo ""
  echo "=== Violation checks ==="

  # V1: Silent fallback introduced
  if [ "$Q5" = "yes" ]; then
    fail "V1 (Q5=yes): silent fallback detected — runtime policy value must not be substituted with a default when policy is absent"
  else
    echo "OK  V1: Q5=${Q5}"
  fi

  # V2: Unexplained production runtime constant
  if [ "$Q6" = "yes" ]; then
    fail "V2 (Q6=yes): hardcoded production runtime constant detected — must be resolved from a policy surface (function_parameters / Registry / Manifest / structure_map)"
  else
    echo "OK  V2: Q6=${Q6}"
  fi

  # V3: Fallback present with no explicit error replacement
  if [ "$Q2" = "yes" ] && [ "$Q3" = "no" ]; then
    fail "V3 (Q2=yes, Q3=no): production fallback constant exists but no explicit missing-policy status has been returned in its place"
  else
    echo "OK  V3: Q2=${Q2}, Q3=${Q3}"
  fi

  # V4: Runtime value not from a policy surface
  if [ "$Q1" = "yes" ] && [ "$Q4" = "no" ]; then
    fail "V4 (Q1=yes, Q4=no): runtime-affecting value introduced but not resolved from a policy surface"
  else
    echo "OK  V4: Q1=${Q1}, Q4=${Q4}"
  fi

  # V5: Canonical route bypassed
  if [ "$Q7" = "yes" ]; then
    fail "V5 (Q7=yes): canonical runtime route step bypassed — all steps are required"
  else
    echo "OK  V5: Q7=${Q7}"
  fi

  # V6: Business logic in frontend
  if [ "$Q8" = "yes" ]; then
    fail "V6 (Q8=yes): business logic or data computation added to frontend projection layer — frontend must be projection only"
  else
    echo "OK  V6: Q8=${Q8}"
  fi

  # V7: Broken reference swallowed
  if [ "$Q9" = "yes" ]; then
    fail "V7 (Q9=yes): broken reference swallowed silently — must return explicit validation error"
  else
    echo "OK  V7: Q9=${Q9}"
  fi

  # V8: Structure check not passed
  if [ "$Q10" = "no" ]; then
    fail "V8 (Q10=no): check-structure.sh not run or not passing — run it before reporting completion"
  else
    echo "OK  V8: Q10=${Q10}"
  fi
fi

# ─── Result ───────────────────────────────────────────────────────────────────

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "=== Runtime Policy Checklist: PASS ==="
  exit 0
else
  echo "=== Runtime Policy Checklist: ${FAILURES} violation(s) — FAIL ===" >&2
  exit 1
fi
