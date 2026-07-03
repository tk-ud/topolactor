# Agent Rules (Always-On)

## Prohibitions

- Do not bypass Workflow Order Invariant.
- Do not bypass Repository Design Order Invariant.
- Do not treat structure check as semantic judgment.
- Do not mark completion before required judgment gates.
- Do not use silent fallback for runtime boundary failures.
- Do not read all prompt/protocol/docs bundles by default.
- Do not implement before the relevant SSOT, wiring, and test/proof surface are defined.
- Do not treat `docs/system-roadmap.yaml` or `.agent/tasks/todo.md` as the authoritative source for implementation state. Read actual code and related tests to determine current state; roadmap and todo are dynamic reference points subject to constant change.
- Do not treat `.agent/tools` output as SSOT authority, proof completion, completion judgment, semantic audit judgment, or implemented / partial / not_started status evidence by itself.
- Do not use `.agent/tools` for repository mutation; `.agent/tools` is an Agent-facing read-only repo observation surface, while `.agent/scripts` owns CI / gate / helper implementation bodies.

## Minimal Workflow Invariant

`READ_ENTRY -> READ_TASK_MATERIALS -> READ_TARGET_SURFACES -> DEFINE_SCOPE -> SCENARIO_CONTRACT -> IMPLEMENT -> FILL_CHECKLISTS -> VERIFY_SCENARIO_DIFF -> JUDGMENT -> STRUCTURE_CHECK -> PUSH_OR_PR`

## Repository Design Order Invariant

All repository design and implementation work must preserve this order:

`SSOT -> wiring -> test/proof surface -> implementation`

Rules:
- SSOT is the canonical design authority. Implementation is not the source of truth.
- Wiring means route/map/reference/required-path/proof-surface connectivity that makes the SSOT reachable.
- Test/proof surface must be defined before implementation is treated as executable work.
- Implementation may start only after the applicable SSOT, wiring, and test/proof surface are identified or added.
- If any earlier layer is missing, stop implementation and repair the earlier layer first.

READ_ENTRY environment trigger note:
- When the executing agent is Claude Code on the web / remote execution environment, READ_ENTRY includes reading `.agent/protocols/claude.md` as a condition-triggered environment prerequisite.
- This does not make `.agent/protocols/claude.md` always-read for non-Claude agents/environments.

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
