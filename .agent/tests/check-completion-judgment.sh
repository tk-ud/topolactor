#!/usr/bin/env bash
set -euo pipefail

# This script is a governance vocabulary/invariant guard.
# It does not replace human/agent semantic JUDGMENT over PR diff,
# scenario contract, checklist contents, and required check scope.
# Structure check is not a judgment substitute.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0

fail(){ echo "FAIL: $1" >&2; FAILURES=$((FAILURES+1)); }
pass(){ echo "OK  : $1"; }

check_term(){
  local file="$REPO_ROOT/$1"; local term="$2"
  if grep -qF -- "$term" "$file"; then pass "$1 -> $term"; else fail "$1 missing: $term"; fi
}

echo "=== Completion Judgment Invariant/Vocabulary Guard Check ==="

check_term ".agent/rules/rule.md" "Workflow Order Invariant"
check_term ".agent/rules/rule.md" "READ_TASK_MATERIALS"
check_term ".agent/rules/rule.md" "READ_TARGET_SURFACES"
check_term ".agent/rules/rule.md" "DEFINE_SCOPE"
check_term ".agent/rules/rule.md" "Issue / prompt explicit materials and required-read lists are task-required input"
check_term ".agent/rules/rule.md" ".agent/docs/ssot-map.yaml"
check_term ".agent/rules/rule.md" "structure check is not a judgment substitute"
check_term ".agent/rules/rule.md" "push / PR update / TODO \`[x]\` / completion summary are allowed only after JUDGMENT and STRUCTURE_CHECK"

check_term ".agent/skills/agent-workflow.md" "READ_TASK_MATERIALS"
check_term ".agent/skills/agent-workflow.md" "READ_TARGET_SURFACES"
check_term ".agent/skills/agent-workflow.md" "DEFINE_SCOPE"
check_term ".agent/skills/agent-workflow.md" "NOT_EXECUTED"
check_term ".agent/skills/agent-workflow.md" "completion summary must include remaining TODO"
check_term ".agent/skills/agent-workflow.md" "follow-up PR comment"
check_term ".agent/skills/agent-workflow.md" "PR_COMMENT_NOT_POSTED"
check_term ".agent/skills/agent-workflow.md" "chat/final summary alone is not a substitute"
check_term ".agent/protocols/reports-and-todos.md" "Existing PR updates require a follow-up PR comment after push"
check_term ".agent/protocols/reports-and-todos.md" "PR_COMMENT_NOT_POSTED"

check_term ".agent/prompt/README.md" "work-type router surface"
check_term ".agent/prompt/README.md" "not an always-read bundle"
check_term ".agent/prompt/todo-maintenance.md" "Todo Maintenance Prompt Router"
check_term ".agent/prompt/todo-maintenance.md" ".agent/protocols/reports-and-todos.md"
check_term ".agent/prompt/todo-maintenance.md" "Open issues are not automatically TODOs"

check_term ".agent/protocols/scenario-contract.md" "Scenario Contract is created before implementation"
check_term ".agent/protocols/scenario-contract.md" "Precondition: Workflow Order Invariant Gate"
check_term ".agent/protocols/scenario-contract.md" "docs/ SSOT reload"

check_term ".agent/protocols/completion.md" "Use this protocol only during JUDGMENT"
check_term ".agent/protocols/completion.md" "Do not skip SCENARIO_CONTRACT / IMPLEMENT / FILL_CHECKLISTS / VERIFY_SCENARIO_DIFF"

check_term ".agent/protocols/policy-judgment.md" "NOT_REQUIRED/OUT_OF_SCOPE"
check_term ".agent/protocols/runtime-boundary-matrix.md" "Use this protocol only in FILL_CHECKLISTS or JUDGMENT stages"
check_term ".agent/checklists/policy-judgment.md" "NOT_REQUIRED"
check_term ".agent/checklists/policy-judgment.md" "OUT_OF_SCOPE"
check_term ".agent/protocols/runtime-boundary-matrix.md" "out-of-scope"

check_term ".agent/docs/ssot-map.yaml" "required_docs"
check_term ".agent/docs/required-paths.yaml" "required_files"

# clarity guard: CI order note should not imply global workflow reordering
check_term ".agent/protocols/completion.md" "completion order and blocking criteria"

if [ "$FAILURES" -eq 0 ]; then
  echo "=== Completion judgment checks passed ==="
  exit 0
else
  echo "=== $FAILURES completion judgment check(s) failed ===" >&2
  exit 1
fi