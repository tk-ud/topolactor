# Agent Rules (Always-On)

## Prohibitions

- Do not bypass Workflow Order Invariant.
- Do not treat structure check as semantic judgment.
- Do not mark completion before required judgment gates.
- Do not use silent fallback for runtime boundary failures.
- Do not read all prompt/protocol/docs bundles by default.

## Minimal Workflow Invariant

`READ_ENTRY -> READ_TASK_MATERIALS -> READ_TARGET_SURFACES -> DEFINE_SCOPE -> SCENARIO_CONTRACT -> IMPLEMENT -> FILL_CHECKLISTS -> VERIFY_SCENARIO_DIFF -> JUDGMENT -> STRUCTURE_CHECK -> PUSH_OR_PR`

## Worktype Decision

Choose one canonical worktype id:

- `audit`
- `specific`
- `implementation_change`
- `design_change`
- `todo_maintenance`
- `existing_pr_update`

Use `.agent/routes/worktype-required-protocols.yaml` as executable reference for prompt/protocol/check mapping.

## Branch to Prompt

After worktype decision, open only matching prompt router:

- `.agent/prompt/audit.md`
- `.agent/prompt/specific.md`
- `.agent/prompt/implementation-change.md`
- `.agent/prompt/design-change.md`
- `.agent/prompt/todo-maintenance.md`
- `.agent/prompt/existing-pr-update.md`
