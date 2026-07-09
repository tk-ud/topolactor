# Agent Rules (Always-On)

## Entry route: effectiveness vs bypass risk

Agents that skip `AGENTS.md` Entry Route and Repository Design Order routinely:

- Implement from user message + existing code without reading `docs/design/*-ssot.*` / `docs/governance/*-ssot.*`
- Justify SSOT violations as "matching current implementation" or "implementation ahead of SSOT"
- Omit tool `start` / `end` (scenario contract + `.agent/tools/logs/tool.log` usage record)
- Claim completion without the full `agent-ui-local-test` chain through `summary` (or fallback routed `required_checks`)

Following Entry Route (see repository-top `AGENTS.md`) is **required**, not optional polish:

| Layer | If followed | If skipped |
|---|---|---|
| `AGENTS.md` + tool-first (`initial_contract` → implement → `local_test`) | Worktype, SSOT sections, scenario contract, routed checks before/after mutation | Ad-hoc scope; highest SSOT drift risk |
| SSOT read before implement (`resolve-ssot` / `sections` or fallback) | Design authority from canonical docs | Code becomes false SSOT |
| `SSOT -> wiring -> test/proof -> implement` | Missing layers repaired before coding | Shipped behavior contradicts design |
| Explicit verification (`local_test` or fallback `required_checks`) | Completion backed by `pass_or_fail` / runner evidence | False "done" from partial checks |

Tool output and existing code are **never** SSOT authority. Only applicable SSOT sections are.

## Prohibitions

- Do not bypass Workflow Order Invariant.
- Do not bypass Repository Design Order Invariant.
- Do not bypass existing `.agent/tools` when an existing tool fits the work.
- Do not treat structure check as semantic judgment.
- Do not mark completion before required judgment gates.
- Do not use silent fallback for runtime boundary failures.
- Do not read all prompt/protocol/docs bundles by default.
- Do not implement before the relevant SSOT, wiring, and test/proof surface are defined.
- Do not treat `docs/system-roadmap.yaml` or `.agent/tasks/todo.md` as the authoritative source for implementation state. Read actual code and related tests to determine current state; roadmap and todo are dynamic reference points subject to constant change.
- Do not treat `.agent/tools` output as SSOT authority, proof completion, completion judgment, semantic audit judgment, or implemented / partial / not_started status evidence by itself.
- Do not use ordinary `.agent/tools` observation flow to mutate Topolactor product/runtime/source implementation surfaces. Tool/governance-side writes are allowed only when the applicable SSOT and task scope explicitly require them.

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

## Existing Tool Usage

Agents should use existing `.agent/tools` when they match the task.

Rules:
- Prefer existing tools over ad-hoc manual inspection or reimplementation.
- Treat existing tools as useful repository navigation, observation, query, and proof-surface helpers.
- Reuse existing tools before adding a new tool.
- Add a new tool only when existing tools cannot express the required SSOT contract or workflow boundary.
- Tool output is still not SSOT authority, proof completion, semantic completion, or implemented / partial / not_started judgment by itself.
- Ordinary observation tool use must not mutate Topolactor product/runtime/source implementation surfaces.
- Tool-side and governance-side writes, such as Agent UI evidence files or tool-owned state, require explicit SSOT/task scope and must stay inside that boundary.

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

Use `.agent/routes/worktype-required-protocols.yaml` as executable reference for prompt/protocol/check mapping. When `.agent/tools/agent-ui-initial-contract` is usable, its `worktypes` subcommand lists these canonical ids with their routed prompt/checks directly from that file.

## Branch to Prompt

Tool-first: when `.agent/tools/agent-ui-initial-contract` is usable, follow `next_step` through `initial_contract` (`worktypes` → `start` → `resolve-ssot` → `sections` → `end`) then, after implementation, `agent-ui-local-test` through `summary` (see `.agent/tools/README.md` and `docs/governance/agent-ui-protocol-ssot.yaml`). `start` inlines the routed prompt's full text (`prompt_content`) and the routed required/triggered protocols as normalized structured fields (`protocol_obligations[]`, not full text); SSOT sections are read in `resolve-ssot` / `sections`, not in `start` alone.

Fallback: after worktype decision, open only matching prompt router:

- `.agent/prompt/audit.md`
- `.agent/prompt/specific.md`
- `.agent/prompt/implementation-change.md`
- `.agent/prompt/design-change.md`
- `.agent/prompt/todo-maintenance.md`
- `.agent/prompt/existing-pr-update.md`
