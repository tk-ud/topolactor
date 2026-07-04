#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0
PASS_COUNT=0
VERBOSE="${CHECK_VERBOSE:-0}"
fail(){ echo "FAIL: $1" >&2; FAILURES=$((FAILURES+1)); }
pass(){ PASS_COUNT=$((PASS_COUNT+1)); if [ "$VERBOSE" = "1" ]; then echo "OK  : $1"; fi; }
check_term(){ local f="$REPO_ROOT/$1" t="$2"; grep -qF -- "$t" "$f" && pass "$1 -> $t" || fail "$1 missing: $t"; }
run_quiet_subcheck(){
  local script="$1" tmp code
  tmp="$(mktemp)"
  set +e
  CHECK_VERBOSE="$VERBOSE" bash "$REPO_ROOT/$script" >"$tmp" 2>&1
  code=$?
  set -e
  if [ "$code" -eq 0 ]; then
    PASS_COUNT=$((PASS_COUNT+1))
    if [ "$VERBOSE" = "1" ]; then cat "$tmp"; fi
  else
    echo "FAIL subcheck script=$script exit=$code" >&2
    cat "$tmp" >&2
    FAILURES=$((FAILURES+1))
  fi
  rm -f "$tmp"
}

if [ "$VERBOSE" = "1" ]; then echo "=== Completion Judgment Invariant/Vocabulary Guard Check ==="; fi

check_term ".agent/rules/rule.md" "Workflow Order Invariant"
check_term ".agent/rules/rule.md" "JUDGMENT"
check_term ".agent/rules/rule.md" "STRUCTURE_CHECK"
check_term ".agent/rules/rule.md" "Do not treat structure check as semantic judgment."

check_term ".agent/protocols/completion.md" "required check scope declaration"
check_term ".agent/protocols/completion.md" "REQUIRED_EXECUTED"
check_term ".agent/protocols/completion.md" "REQUIRED_NOT_EXECUTED"
check_term ".agent/protocols/completion.md" "NOT EXECUTED ≠ PASS"
check_term ".agent/protocols/completion.md" "WorkEvent output sink gap is blocking for completion eligibility"
check_term ".agent/protocols/completion.md" "Abstract Function Bundle Completion Alignment Gate"
check_term ".agent/protocols/completion.md" "primitive execution order, result-context binding, fail-close status, and no provider/bundle-specific branching"
check_term ".agent/protocols/completion.md" "Global abstract-function migration order is mandatory"
check_term ".agent/protocols/completion.md" "SSOT fix and abstract function primitive/manifest generation"
check_term ".agent/protocols/completion.md" "concrete function/handler deletion or explicit compatibility-fallback classification"
check_term ".agent/protocols/completion.md" "No TODO/roadmap status may be advanced by wording alone"
check_term ".agent/protocols/completion.md" "scenario-contract"
check_term ".agent/protocols/scenario-contract.md" "docs/ SSOT reload"

check_term ".agent/protocols/completion-summary.md" "WorkEvent Output Sink Contract"
check_term ".agent/protocols/completion-summary.md" "existing_pr_update"
check_term ".agent/protocols/completion-summary.md" "PR follow-up comment"
check_term ".agent/protocols/completion-summary.md" "PR_COMMENT_NOT_POSTED"
check_term ".agent/protocols/completion-summary.md" "POSTED + VERIFIED"
check_term ".agent/protocols/completion-summary.md" "completion summary"
check_term ".agent/protocols/completion-summary.md" "### 残タスク引き継ぎ指示"

check_term ".agent/skills/agent-workflow.md" "NOT_EXECUTED"
check_term ".agent/skills/agent-workflow.md" "completion summary must include remaining TODO"
check_term ".agent/skills/agent-workflow.md" "follow-up PR comment"

check_term ".agent/routes/worktype-required-protocols.yaml" "existing_pr_update"
check_term ".agent/routes/worktype-required-protocols.yaml" "check-completion-judgment.sh"

check_term ".agent/protocols/audit.md" "PR merge unit is completion Bundle"
check_term ".agent/protocols/audit.md" "checkpoint clear is not main merge approval"
check_term ".agent/protocols/audit.md" "partial Bundle state is not mergeable while same-Bundle residue remains"
check_term ".agent/protocols/audit.md" "carry-over is not an approval substitute for unresolved work inside the same Bundle"
check_term ".agent/protocols/audit.md" "small implementation progress may continue within the same PR after audit clear"
check_term ".agent/protocols/audit.md" "commit granularity is not a governance requirement"
check_term ".agent/prompt/audit.md" "main merge-readiness は completion Bundle として main に入れてよい整合境界を満たすことを要求する"
check_term ".agent/prompt/audit.md" "checkpoint clear は同一PR内で次checkpointへ進む許可"
check_term ".agent/protocols/implementation-change.md" "implementation atom を別PR化して、同一Bundle未達を main へ分割投入してはならない。"
check_term ".agent/prompt/implementation-change.md" "Bundle未達PRを merge する根拠ではない"
check_term ".agent/prompt/existing-pr-update.md" "follow-up PR へ逃がして partial merge することは禁止"
check_term ".agent/prompt/specific.md" "specific route must not carve unresolved scope out of an active completion Bundle"
check_term ".agent/protocols/audit.md" "implemented / close / completion / TODO"
check_term ".agent/protocols/audit.md" "Request Changes とする。"
check_term ".agent/protocols/audit.md" "implemented-target PR の Approve 根拠にはならない。"
check_term ".agent/protocols/audit.md" "implemented 未達 + TODO細分化なし + carry-over 指示なし + Approve は禁止（Request Changes）。"
check_term ".agent/prompt/audit.md" ".agent/protocols/audit/ section shards の approve_judgment_axis に従う。"
check_term ".agent/prompt/audit.md" "prompt 側では completion 判定本文を重複定義しない。"
check_term ".agent/protocols/audit.md" "roadmap completion bundle"
check_term ".agent/protocols/audit.md" "Issue は入口・作業チケット"
check_term ".agent/prompt/audit.md" "todo_granularity_judgment"
check_term ".agent/prompt/implementation-change.md" "todo_granularity_judgment"
check_term ".agent/protocols/implementation-change.md" "todo_granularity_guard"
check_term ".agent/protocols/implementation-change.md" "foundation_ssot_read_gate"
check_term ".agent/protocols/implementation-change.md" "docs/framework-core.yaml"
check_term ".agent/protocols/implementation-change.md" "docs/design/runtime-orchestration-ssot.yaml"
check_term ".agent/protocols/implementation-change.md" "docs/design/pipeline-continuity-ssot.yaml"
check_term ".agent/protocols/audit.md" "foundation_ssot_read_gate"
check_term ".agent/protocols/audit.md" "docs/framework-policy.yaml"
check_term ".agent/protocols/audit.md" "top_level_ssot_checked"
check_term ".agent/protocols/todo-carry-over.md" "canonical TODO carry-over は roadmap completion bundle 単位"
check_term ".agent/protocols/todo-carry-over.md" "foundation_ssot_read_gate"
check_term ".agent/protocols/audit.md" "## required_output_contract"
check_term ".agent/protocols/audit.md" "- todo_granularity_judgment"
check_term ".agent/prompt/audit.md" "## output_shape"
check_term ".agent/prompt/audit.md" "- todo_granularity_judgment"
check_term ".agent/prompt/implementation-change.md" "scope, implementation delta, protocol decisions, todo_granularity_judgment, check results"

if [ "$FAILURES" -eq 0 ]; then
  run_quiet_subcheck ".agent/tests/check-abstract-function-completion-alignment.sh"
  if [ "$FAILURES" -eq 0 ]; then
    echo "PASS check-completion-judgment.sh assertions=${PASS_COUNT}"
  else
    echo "=== $FAILURES completion judgment subcheck(s) failed ===" >&2
    exit 1
  fi
  exit 0
else
  echo "=== $FAILURES completion judgment check(s) failed ===" >&2
  exit 1
fi
