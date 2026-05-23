#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FAILURES=0
fail(){ echo "FAIL: $1" >&2; FAILURES=$((FAILURES+1)); }
pass(){ echo "OK  : $1"; }
check_term(){ local f="$REPO_ROOT/$1" t="$2"; grep -qF -- "$t" "$f" && pass "$1 -> $t" || fail "$1 missing: $t"; }

echo "=== Completion Judgment Invariant/Vocabulary Guard Check ==="

check_term ".agent/rules/rule.md" "Workflow Order Invariant"
check_term ".agent/rules/rule.md" "JUDGMENT"
check_term ".agent/rules/rule.md" "STRUCTURE_CHECK"
check_term ".agent/rules/rule.md" "Do not treat structure check as semantic judgment."

check_term ".agent/protocols/completion.md" "required check scope declaration"
check_term ".agent/protocols/completion.md" "REQUIRED_EXECUTED"
check_term ".agent/protocols/completion.md" "REQUIRED_NOT_EXECUTED"
check_term ".agent/protocols/completion.md" "NOT EXECUTED ≠ PASS"
check_term ".agent/protocols/completion.md" "WorkEvent output sink gap is blocking for completion eligibility"
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

if [ "$FAILURES" -eq 0 ]; then
  echo "=== Completion judgment checks passed ==="
  exit 0
else
  echo "=== $FAILURES completion judgment check(s) failed ===" >&2
  exit 1
fi
