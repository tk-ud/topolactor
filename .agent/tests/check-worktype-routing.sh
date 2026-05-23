#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ROUTE="$ROOT/.agent/routes/worktype-required-protocols.yaml"

test -f "$ROUTE"
for key in audit specific implementation_change design_change todo_maintenance existing_pr_update; do
  rg -q "^  ${key}:" "$ROUTE"
done
for ref in \
  .agent/prompt/audit.md \
  .agent/prompt/specific.md \
  .agent/prompt/implementation-change.md \
  .agent/prompt/design-change.md \
  .agent/prompt/todo-maintenance.md \
  .agent/prompt/existing-pr-update.md \
  .agent/protocols/audit.md \
  .agent/protocols/specific.md \
  .agent/protocols/implementation-change.md \
  .agent/protocols/design-change.md; do
  test -f "$ROOT/$ref"
done

echo "PASS: worktype routing check"
