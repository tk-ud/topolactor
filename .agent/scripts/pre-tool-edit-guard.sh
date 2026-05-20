#!/usr/bin/env bash
set -euo pipefail

# Read file path from stdin JSON
FILE_PATH="$(cat | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('tool_input',{}).get('file_path',''))" 2>/dev/null || true)"

# .agent/tmp/ writes are always allowed (scenario contract, workflow state)
if [[ "$FILE_PATH" == *"/.agent/tmp/"* ]] || [[ "$FILE_PATH" == *"/.agent/"* ]]; then
  exit 0
fi

REPO_ROOT="$(git -C "$(pwd)" rev-parse --show-toplevel 2>/dev/null || echo "")"
if [ -z "$REPO_ROOT" ]; then
  exit 0
fi

STATE_FILE="$REPO_ROOT/.agent/tmp/workflow-state.env"

if [ ! -f "$STATE_FILE" ]; then
  exit 0
fi

NEXT_REQUIRED="$(grep -E '^NEXT_REQUIRED=' "$STATE_FILE" | head -n 1 | cut -d= -f2- || true)"

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
