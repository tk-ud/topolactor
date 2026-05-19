#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(git -C "$(pwd)" rev-parse --show-toplevel 2>/dev/null || echo "")"
if [ -z "$REPO_ROOT" ]; then
  exit 0
fi

STATE_FILE="$REPO_ROOT/.agent/tmp/workflow-state.env"

# No state file = no active workflow = allow
if [ ! -f "$STATE_FILE" ]; then
  exit 0
fi

NEXT_REQUIRED="$(grep -E '^NEXT_REQUIRED=' "$STATE_FILE" | head -n 1 | cut -d= -f2- || true)"

# DONE = previous task completed cleanly = allow
if [ "$NEXT_REQUIRED" = "DONE" ] || [ -z "$NEXT_REQUIRED" ]; then
  exit 0
fi

PHASES_BEFORE_IMPLEMENT=(
  "READ_ENTRY"
  "READ_TASK_MATERIALS"
  "READ_TARGET_SURFACES"
  "DEFINE_SCOPE"
  "SCENARIO_CONTRACT"
)

for phase in "${PHASES_BEFORE_IMPLEMENT[@]}"; do
  if [ "$NEXT_REQUIRED" = "$phase" ]; then
    echo "{\"hookSpecificOutput\":{\"hookEventName\":\"PreToolUse\",\"permissionDecision\":\"deny\",\"permissionDecisionReason\":\"BLOCKED: Edit/Write requires IMPLEMENT phase or later. Current NEXT_REQUIRED=$NEXT_REQUIRED. Run advance-workflow-phase.sh to advance.\"}}"
    exit 0
  fi
done

exit 0
