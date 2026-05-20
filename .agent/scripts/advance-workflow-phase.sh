#!/usr/bin/env bash
set -euo pipefail
# deprecated helper: optional local memo only.
# Do not treat this as mandatory workflow authority.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STATE_FILE="$REPO_ROOT/.agent/tmp/workflow-state.env"

PHASES=(
  "READ_ENTRY"
  "READ_TASK_MATERIALS"
  "READ_TARGET_SURFACES"
  "DEFINE_SCOPE"
  "SCENARIO_CONTRACT"
  "IMPLEMENT"
  "FILL_CHECKLISTS"
  "VERIFY_SCENARIO_DIFF"
  "JUDGMENT"
  "STRUCTURE_CHECK"
  "PUSH_OR_PR"
)

fail() {
  echo "WORKFLOW_PHASE_FAIL: $1" >&2
  exit 1
}

phase_index() {
  local target="$1"
  local i
  for i in "${!PHASES[@]}"; do
    if [ "${PHASES[$i]}" = "$target" ]; then
      echo "$i"
      return 0
    fi
  done
  return 1
}

read_state_value() {
  local key="$1"
  if [ ! -f "$STATE_FILE" ]; then
    return 1
  fi
  grep -E "^${key}=" "$STATE_FILE" | head -n 1 | cut -d= -f2- || true
}

write_state() {
  local next="$1"
  local completed="$2"

  mkdir -p "$(dirname "$STATE_FILE")"

  cat > "$STATE_FILE" <<EOF
NEXT_REQUIRED=$next
COMPLETED=$completed
EOF
}

STEP_DONE="${1:-}"

[ -n "$STEP_DONE" ] || fail "usage: bash .agent/scripts/advance-workflow-phase.sh <STEP_DONE>"

DONE_INDEX="$(phase_index "$STEP_DONE" || true)"
[ -n "$DONE_INDEX" ] || fail "unknown step: $STEP_DONE"

if [ ! -f "$STATE_FILE" ]; then
  cat > "$STATE_FILE" <<'EOF'
NEXT_REQUIRED=READ_ENTRY
COMPLETED=
EOF
fi

NEXT_REQUIRED="$(read_state_value NEXT_REQUIRED)"
COMPLETED="$(read_state_value COMPLETED || true)"

[ "$STEP_DONE" = "$NEXT_REQUIRED" ] || {
  echo "Expected next step: $NEXT_REQUIRED" >&2
  echo "Attempted step    : $STEP_DONE" >&2
  fail "workflow phase skip or out-of-order execution"
}

if [ -z "$COMPLETED" ]; then
  NEW_COMPLETED="$STEP_DONE"
else
  NEW_COMPLETED="$COMPLETED,$STEP_DONE"
fi

NEXT_INDEX=$((DONE_INDEX + 1))

if [ "$NEXT_INDEX" -ge "${#PHASES[@]}" ]; then
  NEW_NEXT="DONE"
else
  NEW_NEXT="${PHASES[$NEXT_INDEX]}"
fi

write_state "$NEW_NEXT" "$NEW_COMPLETED"

echo "WORKFLOW_PHASE_ADVANCED (optional local memo):"
echo "  completed=$NEW_COMPLETED"
echo "  next_required=$NEW_NEXT"
