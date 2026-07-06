# Agent Contract

## Role

Agent executes requested repository work while preserving canonical runtime route and explicit-failure behavior.

## Why this contract exists

**Default agent behavior is unsafe for this repository.** Without an explicit entry route, agents typically:

- Jump from the user message straight to code search and edits, **without reading applicable SSOT**
- Treat **existing implementation as design authority** ("the code already does X, so X is correct")
- Skip worktype routing, negative-case contracts, and proof-surface identification
- Reintroduce patterns SSOT explicitly prohibits (e.g. per-node dispatch wiring, silent fallback, duplicate draft routes)
- Claim completion from partial test runs or conversation inference instead of the full `agent-ui-local-test` chain through `summary` (or fallback routed `required_checks`)

**Following Entry Route below does not guarantee correctness by itself**, but it materially reduces these failure modes by forcing SSOT reachability, scoped contracts, and explicit verification before and after mutation.

### When Entry Route is followed (effective)

Three phases per [`docs/governance/agent-ui-protocol-ssot.yaml`](docs/governance/agent-ui-protocol-ssot.yaml) `flow_order` (see [`.agent/tools/README.md`](.agent/tools/README.md)):

1. **initial_contract** — `agent-ui-initial-contract`: `worktypes` → `start` → `resolve-ssot` → `sections` → `end` (follow each subcommand's `next_step`).
   - `start`: worktype route from [`.agent/routes/worktype-required-protocols.yaml`](.agent/routes/worktype-required-protocols.yaml); tool-generated `uuid`/`datetime`; routed prompt and required/triggered protocol **full text** (`prompt_content`, `protocol_trigger_hints[].content`); `workflow_procedure` from `.agent/skills/agent-workflow.md`. Whether a `triggered_protocols` condition applies to the current task is still an agent judgment call.
   - `resolve-ssot` / `sections`: applicable SSOT section subtrees only — not resolved in `start`.
   - `end`: writes `senario-tmp.md` (scope + negative cases; must not be committed) and appends compact metadata to [`.agent/tools/logs/tool.log`](.agent/tools/logs/tool.log). Reuse the same `uuid`/`datetime` through `summary`.

2. **implementation** — mutate product/runtime/source only within the scenario contract and applicable SSOT. Tool output is not SSOT authority.

3. **local_test** — `agent-ui-local-test`: `run-worktype-tests` → `read-senario-tmp` → `checklist` → `checks` → `summary` → `PUSH_OR_PR` when `pass_or_fail` is `pass`.
   - `run-worktype-tests` / `summary` run the worktype's `required_checks` from `.agent/routes/worktype-required-protocols.yaml`.
   - `checks` runs `check-structure.sh` alone before `summary`; `summary` re-runs routed checks and emits `pass_or_fail` plus `completion_contract` fields. `summary` does **not** append to `tool.log`. `pass_or_fail` is not an implemented/partial/not_started judgment.

**Repository Design Order** ([`.agent/rules/rule.md`](.agent/rules/rule.md)): `SSOT -> wiring -> test/proof surface -> implementation`. Stop and repair an earlier layer before coding if it is missing.

**Supplemental verification**: task-specific gates (e.g. `check-runtime-semantics.sh`, domain proof bundles) may run before `local_test`, but they do not replace the `agent-ui-local-test` chain or raw `check-structure.sh` as the completion substitute.

### When Entry Route is skipped (dangerous)

| Skipped step | Typical harm |
|---|---|
| No applicable SSOT read (`resolve-ssot` / `sections` or fallback equivalent) | SSOT deviation shipped as "enhancement"; prohibited UX/API paths reappear |
| No tool `start` / `end` (scenario contract + `tool.log`) | Scope creep; negative boundaries not fixed before coding; missing usage id |
| Code-as-authority reasoning | "Align with existing implementation" overrides canonical design |
| No full `agent-ui-local-test` chain through `summary` | False completion; `pass_or_fail`, checklist interview, and routed-check evidence missing |
| Fallback: prompt/protocol router skipped | Wrong worktype; required/triggered protocols never opened |

If the user request is urgent, **still read the applicable SSOT section first** — speed without SSOT is the highest-risk path in this repo.

## Entry Route

Canonical order matches [`.agent/README.md`](.agent/README.md) Route Position:

1. Read [`.agent/rules/rule.md`](.agent/rules/rule.md) — always-on prohibitions, Repository Design Order, worktype branch.
2. **Claude web/remote only:** read [`.agent/protocols/claude.md`](.agent/protocols/claude.md) once at `READ_ENTRY`; then continue below.
3. Read [`.agent/README.md`](.agent/README.md) — directory map (not protocol bodies).
4. **Tool-first** (when `.agent/tools/agent-ui-initial-contract` is usable): follow `next_step` through `initial_contract` → implement → `agent-ui-local-test` through `summary` ([`.agent/tools/README.md`](.agent/tools/README.md), [`docs/governance/agent-ui-protocol-ssot.yaml`](docs/governance/agent-ui-protocol-ssot.yaml)). Do not manually re-open every `.agent/prompt/*`, `.agent/protocols/*`, or `.agent/skills/agent-workflow.md` for sequencing — `start` already inlined the routed subset.
5. **Fallback** (tool not usable): [`.agent/skills/agent-workflow.md`](.agent/skills/agent-workflow.md) → choose worktype id → [`.agent/routes/worktype-required-protocols.yaml`](.agent/routes/worktype-required-protocols.yaml) → matching [`.agent/prompt/<worktype>.md`](.agent/prompt/) → required/triggered [`.agent/protocols/`](.agent/protocols/) → checklist/tests per routed `required_checks`.

Minimal workflow invariant (fallback detail): see `READ_ENTRY -> … -> STRUCTURE_CHECK -> PUSH_OR_PR` in [`.agent/rules/rule.md`](.agent/rules/rule.md).

## Triggered Governance References

Condition-triggered surfaces from worktype routing (`.agent/routes/worktype-required-protocols.yaml`) and [`.agent/rules/rule.md`](.agent/rules/rule.md) trigger map — not always-on:

- Runtime Boundary Failure Matrix — `.agent/protocols/` (condition-triggered).
- Policy Judgment Gate — [`.agent/protocols/policy-judgment.md`](.agent/protocols/policy-judgment.md) and `.agent/checklists/check-policy-judgment.sh` when triggered.
- Temporary Scenario Contract — [`.agent/protocols/scenario-contract.md`](.agent/protocols/scenario-contract.md) when triggered (e.g. `implementation_change` + persistence/projection changes).
- Recursive Verification Gate / completion governance — [`.agent/protocols/completion.md`](.agent/protocols/completion.md).

Under tool-first, required protocol text is inlined at `start`; triggered protocol text is inlined with its `trigger_condition` — the agent still decides whether that condition applies before treating it as binding.

## Work Posture

- Do not treat all protocols, docs, or skills as always-read scope.
- Open protocol/checklist/test surfaces only when the worktype route or a trigger condition matches the current change.
- Do not treat `.agent/tools` output, `docs/system-roadmap.yaml`, or `.agent/tasks/todo.md` as SSOT authority, proof completion, or implementation-state evidence.
- **Verification:** when the Agent UI tool is usable, complete verification through the `agent-ui-local-test` chain (`summary` with `pass_or_fail`). On fallback only, run routed `required_checks` from `.agent/routes/worktype-required-protocols.yaml` and run `bash .agent/tests/check-structure.sh` last among them.

## Top-Level Directory Agenda

- `.agent/`
  - Agent-facing documentation, rules, prompts, protocols, checklists, tests, tasks, reports, tools, and helper scripts.
  - Start here (`AGENTS.md`), then [`.agent/rules/rule.md`](.agent/rules/rule.md), optional Claude `READ_ENTRY`, [`.agent/README.md`](.agent/README.md), then tool-first (`initial_contract` → implement → `local_test`) or fallback skills/worktype route. Do not treat all `.agent/` files as always-read scope.

- `docs/`
  - Canonical design / governance SSOT surface.
  - Architecture, runtime boundaries, data model, and implementation judgment must be checked against the matching docs.

- `db/`
  - Semantic topology space.
  - Owns database schema, topology tables, promotion tables, SQL attention logs, seeds, and persistence-side topology authority.

- `backend/`
  - Abstract runtime.
  - Owns thin HTTP endpoint boundary, runtime executor, operation vector / attractor resolution, semantic mapper, repository boundary, guards, and runtime contract schemas.

- `frontend/`
  - Physical projection space.
  - Fresh / Deno / Preact frontend.
  - Owns routes, islands, UI components, package projection, frontend schema, registry, runtime executor, and backend API clients.

- `infra/`
  - Local/demo/bootstrap hosting boundary.
  - Owns compose wiring, nginx reverse proxy route boundary, and local compose environment template.
  - Does not own runtime policy, topology semantics, database schema authority, or frontend projection judgment.

- `articles/`
  - Public article / explanatory surface.
  - Not authoritative implementation SSOT.
