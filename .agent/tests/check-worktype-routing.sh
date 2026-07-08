#!/usr/bin/env bash
set -euo pipefail

# responsibility: routing existence / reference integrity / required vocabulary checks only
# no semantic design judgment

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ROUTE_FILE="$REPO_ROOT/.agent/routes/worktype-required-protocols.yaml"
FAILURES=0
PASS_COUNT=0

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
  check_term "$ROUTE_FILE" "audit:"
  check_term "$ROUTE_FILE" "specific:"
  check_term "$ROUTE_FILE" "existing_pr_update:"

  check_term "$ROUTE_FILE" "prompt: .agent/prompt/todo-maintenance.md"
  check_term "$ROUTE_FILE" "prompt: .agent/prompt/design-change.md"
  check_term "$ROUTE_FILE" "prompt: .agent/prompt/implementation-change.md"


  check_term "$ROUTE_FILE" "required_checks:"
  check_term "$ROUTE_FILE" ".agent/tests/check-structure.sh"
  check_term "$ROUTE_FILE" ".agent/tests/check-system-roadmap.sh"

  check_term "$ROUTE_FILE" "prompt: .agent/prompt/audit.md"
  check_term "$ROUTE_FILE" "prompt: .agent/prompt/specific.md"
  check_term "$ROUTE_FILE" "prompt: .agent/prompt/existing-pr-update.md"

  check_term "$ROUTE_FILE" ".agent/protocols/audit.md"
  check_term "$ROUTE_FILE" ".agent/protocols/specific.md"
  check_term "$ROUTE_FILE" ".agent/protocols/todo-carry-over.md"
  check_term "$ROUTE_FILE" ".agent/protocols/ssot-change-impact.md"
  check_term "$ROUTE_FILE" ".agent/protocols/implementation-change.md"
  check_term "$ROUTE_FILE" ".agent/protocols/completion-summary.md"
  check_term "$ROUTE_FILE" "runtime_persistence_projection_changes:"
  check_term "$ROUTE_FILE" "policy_scoring_threshold_changes:"
  check_term "$ROUTE_FILE" ".agent/protocols/scenario-contract.md"
  check_term "$ROUTE_FILE" ".agent/protocols/policy-judgment.md"

  check_term "$ROUTE_FILE" "protocol_sections_dir: .agent/protocols/audit"
  check_term "$ROUTE_FILE" "protocol_sections_dir: .agent/protocols/specific"
  check_term "$ROUTE_FILE" "protocol_sections_dir: .agent/protocols/implementation-change"
  check_term "$ROUTE_FILE" "protocol_sections_dir: .agent/protocols/design-change"
  check_term "$ROUTE_FILE" "protocol_sections_dir: .agent/protocols/todo-maintenance"
  check_term "$ROUTE_FILE" "protocol_sections_dir: .agent/protocols/existing-pr-update"
  check_term "$ROUTE_FILE" ".agent/protocols/audit/index.md"
  check_term "$ROUTE_FILE" ".agent/protocols/specific/index.md"
  check_term "$ROUTE_FILE" ".agent/protocols/implementation-change/index.md"
  check_term "$ROUTE_FILE" ".agent/protocols/design-change/index.md"


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


# prompt/protocol vocabulary guards for diff-based routing
check_term "$REPO_ROOT/.agent/prompt/audit.md" "PR diff or patch"
check_term "$REPO_ROOT/.agent/prompt/audit.md" "changed file list"
check_term "$REPO_ROOT/.agent/prompt/audit.md" ".agent/tasks/todo.md"
check_term "$REPO_ROOT/.agent/prompt/audit.md" "docs/system-roadmap.yaml"
check_term "$REPO_ROOT/.agent/prompt/audit.md" "implementation_registry"
check_term "$REPO_ROOT/.agent/prompt/audit.md" "docs/framework-policy.yaml"
check_term "$REPO_ROOT/.agent/prompt/audit.md" "top_level_ssot_checked"
check_term "$REPO_ROOT/.agent/protocols/audit.md" "Summaryだけで判断しない"
check_term "$REPO_ROOT/.agent/protocols/audit.md" "PR metadata / mergeability だけで判断しない"
check_term "$REPO_ROOT/.agent/protocols/audit/02-required-alignment-surfaces.md" "PR-side record"
check_term "$REPO_ROOT/.agent/protocols/audit/02-required-alignment-surfaces.md" "tool evidence log"
check_term "$REPO_ROOT/.agent/protocols/audit/08-output.md" "PR record checked"
check_term "$REPO_ROOT/.agent/protocols/audit/08-output.md" "Tool log checked"
check_term "$REPO_ROOT/.agent/protocols/audit/09-ng.md" "PR-side record を SSOT"
check_term "$REPO_ROOT/.agent/protocols/audit/14-blocking.md" "PR record omitted"
check_term "$REPO_ROOT/.agent/prompt/audit.md" "todo_granularity_judgment"
check_term "$REPO_ROOT/.agent/prompt/implementation-change.md" "todo_granularity_judgment"
check_term "$REPO_ROOT/.agent/prompt/implementation-change.md" "foundation_ssot_read_judgment"
check_term "$REPO_ROOT/.agent/prompt/implementation-change.md" "docs/framework-core.yaml"
check_term "$REPO_ROOT/.agent/prompt/implementation-change.md" "docs/design/runtime-orchestration-ssot.yaml"
check_term "$REPO_ROOT/.agent/prompt/implementation-change.md" "docs/design/pipeline-continuity-ssot.yaml"
check_term "$REPO_ROOT/.agent/prompt/audit.md" "## output_shape"
check_term "$REPO_ROOT/.agent/prompt/audit.md" "- todo_granularity_judgment"
check_term "$REPO_ROOT/.agent/prompt/audit.md" "foundation_ssot_read_judgment"
check_term "$REPO_ROOT/.agent/prompt/implementation-change.md" "todo_granularity_judgment"
check_term "$REPO_ROOT/.agent/prompt/specific.md" "PR差分監査"
check_term "$REPO_ROOT/.agent/prompt/specific.md" "merge判断"
check_term "$REPO_ROOT/.agent/prompt/todo-maintenance.md" "foundation_ssot_read_judgment"
check_term "$REPO_ROOT/.agent/prompt/existing-pr-update.md" "PR diff or patch"
check_term "$REPO_ROOT/.agent/prompt/existing-pr-update.md" "changed files"
check_term "$REPO_ROOT/.agent/prompt/existing-pr-update.md" ".agent/tasks/todo.md"
check_term "$REPO_ROOT/.agent/prompt/existing-pr-update.md" "docs/system-roadmap.yaml"
check_term "$REPO_ROOT/.agent/prompt/implementation-change.md" "protocol_trigger_hints[].content"
check_term "$REPO_ROOT/.agent/tools/README.md" "protocol_trigger_hints"
check_term "$REPO_ROOT/docs/governance/reference/agent-ui-tool-output-reference.yaml" "Neither is an arbitrary partial excerpt"
check_term "$REPO_ROOT/.agent/scripts/agent_tools/agent_ui_initial_contract.py" "triggered protocol full text"

if grep -qiF 'triggered protocol excerpts' "$REPO_ROOT/.agent/scripts/agent_tools/agent_ui_initial_contract.py"; then
  fail "agent-ui-initial-contract still describes routed protocol output as excerpts"
else
  pass "agent-ui-initial-contract does not describe routed protocol output as excerpts"
fi


if [ "$FAILURES" -eq 0 ]; then
  echo "PASS check-worktype-routing.sh assertions=${PASS_COUNT}"
  exit 0
else
  echo "=== $FAILURES work-type routing check(s) failed ===" >&2
  exit 1
fi
