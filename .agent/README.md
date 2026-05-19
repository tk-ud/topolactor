# .agent Directory Purpose

`.agent/` is the governance-and-operations support area for agent work in this repository.
It separates always-read guidance from condition-triggered references, and separates operational outputs from temporary working notes.

## Read Order

1. `AGENTS.md` (repository entry contract)
2. `.agent/README.md` (this role-and-route guide)
3. `.agent/rules/rule.md` (always-read operating rules and trigger map)
4. `.agent/skills/agent-workflow.md` (execution order: materials → target surfaces → scope → scenario gate → implementation → verification)
5. Only when needed, open relevant `.agent/docs/` resume/index, `.agent/skills/structure-check.md` (and other task skills), and `.agent/protocols/*.md`

## Directory Roles

- `AGENTS.md`:
  Defines the agent role, provides the entry handoff to `.agent/README.md`, and routes readers to `.agent/rules/rule.md`; it is not a direct entrypoint for reading all `docs/`.
- `.agent/docs/`:
  Resume/index surface for SSOT materials under `docs/`. `ssot-map.yaml` defines change-surface → docs/ SSOT mappings; not a signal to read all `docs/` by default.
- `.agent/rules/`:
  Always-read rule surface. Defines required behavior boundaries, prohibitions, and directory-role framing under `.agent/`.
- `.agent/skills/`:
  `agent-workflow.md` is the always-read lightweight execution workflow. Other skill files are task procedures read only when executing the corresponding task/check. Skills are task procedures.
- `.agent/protocols/`:
  Condition-triggered governance reference points. Protocols are not an always-read bundle and not a single always-on workflow.
- `.agent/reports/`:
  Storage for inspection results and audit/maintenance report outputs.
- `.agent/tasks/`:
  TODO surface where unresolved work is preserved from reports or remaining task items.
- `.agent/scripts/`:
  Helper tools invoked during work.
- `.agent/tests/`:
  CI/check execution surface.
- `.agent/tmp/`:
  Temporary working memo surface for in-progress operations; not a permanent deliverable surface.

## What Is Always Read

- `AGENTS.md`
- `.agent/README.md`
- `.agent/rules/rule.md`
- `.agent/skills/agent-workflow.md`

These define baseline obligations and operating posture for every task.

## What Is Read Only When Needed

- `.agent/protocols/*.md`: read only when that protocol's trigger condition matches the change.
- `.agent/skills/agent-workflow.md`: always read as the lightweight execution workflow.
- other `.agent/skills/*.md`: read only when executing the corresponding task/check procedure.

This avoids the misread that all protocols must be read and applied on every task.

Workflow Order Invariant is defined in `.agent/rules/rule.md` and must be preserved across all entry routes.

## Reports / Tasks / Tmp Usage

- Put inspection/audit result artifacts in `.agent/reports/`.
- Preserve unresolved follow-up as TODO items in `.agent/tasks/`.
- Use `.agent/tmp/` only for temporary in-task notes/contracts and remove/clear temporary artifacts by process rules.
- Do not treat `.agent/tmp/` as a long-term report or completion-summary location.

## Non-Goals

- This file does not redefine runtime/application behavior.
- This file does not replace protocol-specific procedures.
- This file does not make all protocols always-on.
- This file does not convert skills into completion-governance rules.
