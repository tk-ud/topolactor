#!/usr/bin/env bash
set -euo pipefail

# Lightweight attention reminder hook.
# This hook intentionally does not enforce legacy workflow-state gating.
# Execution order governance is judged via .agent protocol/checklist/final CI.

FILE_PATH="$(cat | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('tool_input',{}).get('file_path',''))" 2>/dev/null || true)"

if [ -n "${FILE_PATH}" ]; then
  echo "[agent-reminder] Edit/Write target: ${FILE_PATH}" >&2
fi

echo "[agent-reminder] Follow workflow order in .agent/rules/rule.md and .agent/skills/agent-workflow.md." >&2
echo "[agent-reminder] Keep checklist templates immutable; use .agent/tmp working copies and clean tmp before completion." >&2

exit 0
