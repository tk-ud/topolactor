# .agent Directory Purpose

`.agent/` is the governance-and-operations support area for agent work in this repository.
It separates always-read guidance from condition-triggered references, and separates operational outputs from temporary working notes.

## Read Order

1. `AGENTS.md` (repository entry contract)
2. `.agent/rules/rule.md` (always-on operating rules)
3. Open only the specific `.agent/protocols/*.md` and `.agent/skills/*.md` needed for the current change scope

## Directory Roles

- `AGENTS.md`:
  Defines the agent role, the SSOT agenda orientation for `docs/`, and the handoff to `.agent/rules/rule.md`.
- `.agent/docs/`:
  Resume/index surface for SSOT materials under `docs/`.
- `.agent/rules/`:
  Always-read rule surface. Defines required behavior boundaries, prohibitions, and directory-role framing under `.agent/`.
- `.agent/skills/`:
  Lightweight workflow and structure-operation procedures. Skills are task procedures, not heavy completion-governance protocols.
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
- `.agent/rules/rule.md`

These define baseline obligations and operating posture for every task.

## What Is Read Only When Needed

- `.agent/protocols/*.md`: read only when that protocol's trigger condition matches the change.
- `.agent/skills/*.md`: read only when executing the corresponding workflow/check procedure.

This avoids the misread that all protocols must be read and applied on every task.

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
