#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
rg -q "Workflow Invariant|Workflow Order Invariant" "$ROOT/.agent/rules/rule.md"
rg -q "JUDGMENT" "$ROOT/.agent/protocols/completion.md"
rg -q "PR" "$ROOT/.agent/protocols/completion-summary.md"
rg -q "existing_pr_update" "$ROOT/.agent/routes/worktype-required-protocols.yaml"
echo "PASS: completion judgment vocabulary check"
