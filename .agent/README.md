# .agent Directory Purpose

`.agent/` is the governance-and-operations support area for agent work in this repository.
It separates always-read guidance from condition-triggered references, and separates operational outputs from temporary working notes.

## Read Order

1. `AGENTS.md` (repository entry contract)
2. `.agent/rules/rule.md` (always-read operating rules and trigger map)
3. `.agent/README.md` (this role-and-route guide)
4. `.agent/skills/agent-workflow.md` (execution order: materials → target surfaces → scope → scenario gate → implementation → verification)
5. When applicable, open matching `.agent/prompt/<work-type>.md` as work-type router
6. Only when needed, open relevant `.agent/docs/` resume/index, mapped `docs/` SSOT, corresponding task skills, and condition-triggered `.agent/protocols/*.md`

## Directory Roles

- `AGENTS.md`:
  Defines the agent role and provides the entry handoff to `.agent/rules/rule.md`, then `.agent/README.md`; it is not a direct entrypoint for reading all `docs/`.
- `.agent/docs/`:
  Resume/index surface for SSOT materials under `docs/`. `ssot-map.yaml` defines change-surface → docs/ SSOT mappings; not a signal to read all `docs/` by default.
- `.agent/rules/`:
  Always-read rule surface. Defines required behavior boundaries, prohibitions, and directory-role framing under `.agent/`.
- `.agent/skills/`:
  `agent-workflow.md` is the always-read lightweight execution workflow. Other skill files are task procedures read only when executing the corresponding task/check. Skills are task procedures.
- `.agent/prompt/`:
  Work-type router surface for selecting required SSOT and triggered governance gates. Prompt files are not protocols and not an always-read bundle.
- `.agent/protocols/`:
  Condition-triggered governance reference points. Protocols are not an always-read bundle and not a single always-on workflow. `index.yaml` is a lightweight grep route for finding protocol sections and is not protocol body.
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
- `.agent/README.md`
- `.agent/skills/agent-workflow.md`

These define baseline obligations and operating posture for every task.

## What Is Read Only When Needed

- `.agent/prompt/*.md`: read only when the current work type matches the router trigger.
- `.agent/protocols/*.md`: read only when that protocol's trigger condition matches the change.
- `.agent/docs/` resume/index and mapped `docs/` SSOT: read only when target surfaces require SSOT mapping.
- other `.agent/skills/*.md`: read only when executing the corresponding task/check procedure.

This avoids the misread that all protocols must be read and applied on every task.

Workflow Order Invariant is defined in `.agent/rules/rule.md` and must be preserved across all entry routes.

## Reports / Tasks / Tmp Usage

- Put inspection/audit result artifacts in `.agent/reports/`.
- Preserve unresolved follow-up as TODO items in `.agent/tasks/`.
- Use `.agent/tmp/` only for temporary in-task notes/contracts and remove/clear temporary artifacts by process rules.
- Do not treat `.agent/tmp/` as a long-term report or completion-summary location.

## Estimated Token Consumption

Agent execution consumes:

```text
total_input_tokens ~= normal_prompt_tokens + repository_system_read_tokens
```

Where:

- `normal_prompt_tokens`: the user/task prompt, issue text, PR summary, explicit task materials.
- `repository_system_read_tokens`: `.agent` governance files read before or during the task.
- Hidden model/provider system prompts are not measurable from this repository, so this estimate covers repository-side agent governance overhead only.

Current `.agent` read overhead estimate:

| Route | Estimated tokens |
|---|---:|
| Always-read baseline: `AGENTS.md` + `.agent/rules/rule.md` + `.agent/README.md` + `.agent/skills/agent-workflow.md` | ~3,200-4,000 |
| Baseline + `.agent/protocols/index.yaml` | ~5,200-6,300 |
| Baseline + index + 1 targeted protocol section | ~5,500-7,000 |
| Baseline + index + completion/report judgment sections | ~6,200-8,500 |
| Heavy route with prompt router + ssot-map + mapped docs/SSOT + multiple protocol sections | ~10,000-20,000+ |

Practical formula:

```text
light_task_tokens ~= normal_prompt_tokens + 3.5k
protocol_task_tokens ~= normal_prompt_tokens + 6k-8k
ssot_heavy_task_tokens ~= normal_prompt_tokens + 10k-20k+
```

These are provisional estimates, not exact tokenizer measurements. Update them when token accounting or file size changes materially.

## Non-Goals

- This file does not redefine runtime/application behavior.
- This file does not replace protocol-specific procedures.
- This file does not make all protocols always-on.
- This file does not convert skills into completion-governance rules.
