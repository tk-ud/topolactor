#!/usr/bin/env bash
set -euo pipefail

# responsibility: routing existence / reference integrity / required vocabulary checks only
# no semantic design judgment

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ROUTE_FILE="$REPO_ROOT/.agent/routes/worktype-required-protocols.yaml"
FAILURES=0

fail(){ echo "FAIL: $1" >&2; FAILURES=$((FAILURES+1)); }
pass(){ echo "OK  : $1"; }

check_file(){
  local file="$1"
  if [ -f "$file" ]; then pass "file exists: ${file#$REPO_ROOT/}"; else fail "missing file: ${file#$REPO_ROOT/}"; fi
}

check_term(){
  local file="$1" term="$2"
  if grep -qF -- "$term" "$file"; then pass "${file#$REPO_ROOT/} -> $term"; else fail "${file#$REPO_ROOT/} missing: $term"; fi
}

check_ref_exists(){
  local ref="$1"
  local target="$REPO_ROOT/$ref"
  if [ -f "$target" ]; then pass "reference exists: $ref"; else fail "missing referenced file: $ref"; fi
}

echo "=== Work-type routing yes/no check ==="
check_file "$ROUTE_FILE"

if [ -f "$ROUTE_FILE" ]; then
  check_term "$ROUTE_FILE" "todo_maintenance:"
  check_term "$ROUTE_FILE" "design_change:"
  check_term "$ROUTE_FILE" "implementation_change:"

  check_term "$ROUTE_FILE" "prompt: .agent/prompt/todo-maintenance.md"
  check_term "$ROUTE_FILE" "prompt: .agent/prompt/design-change.md"
  check_term "$ROUTE_FILE" "prompt: .agent/prompt/implementation-change.md"

  check_term "$ROUTE_FILE" ".agent/protocols/todo-carry-over.md"
  check_term "$ROUTE_FILE" ".agent/protocols/ssot-change-impact.md"
  check_term "$ROUTE_FILE" ".agent/protocols/completion.md"
  check_term "$ROUTE_FILE" ".agent/protocols/completion-summary.md"
  check_term "$ROUTE_FILE" "runtime_persistence_projection_changes:"
  check_term "$ROUTE_FILE" "policy_scoring_threshold_changes:"
  check_term "$ROUTE_FILE" ".agent/protocols/scenario-contract.md"
  check_term "$ROUTE_FILE" ".agent/protocols/policy-judgment.md"

  check_term "$ROUTE_FILE" "Do not duplicate SSOT body into protocol."

  if grep -qiE 'full-read|read all protocols|always-read protocol bundle' "$ROUTE_FILE"; then
    fail "forbidden protocol bundle full-read vocabulary found in routing yaml"
  else
    pass "no forbidden protocol-bundle full-read vocabulary"
  fi

  refs=$(grep -oE '\.agent/[A-Za-z0-9_./-]+\.(md|sh|yaml)' "$ROUTE_FILE" | sort -u)
  while IFS= read -r ref; do
    [ -z "$ref" ] && continue
    check_ref_exists "$ref"
  done <<< "$refs"
fi

if [ "$FAILURES" -eq 0 ]; then
  echo "=== Work-type routing checks passed ==="
  exit 0
else
  echo "=== $FAILURES work-type routing check(s) failed ===" >&2
  exit 1
fi
