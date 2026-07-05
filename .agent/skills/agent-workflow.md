# Skill: Agent Workflow

## Purpose

This skill defines the lightweight execution order for task work.

It does not replace `AGENTS.md`, `.agent/rules/rule.md`, or `.agent/README.md`.
Follow those always-read sources first.

This skill defines workflow procedure order only.
It does not own worktype routing, prompt selection, protocol selection, checklist selection, or completion-summary wording.
Those authorities remain in:

- `AGENTS.md`
- `.agent/rules/rule.md`
- `.agent/routes/worktype-required-protocols.yaml`
- matching `.agent/prompt/<worktype>.md`
- triggered `.agent/protocols/*`
- `.agent/protocols/completion-summary.md`

## Execution Order

```text
READ_ENTRY
→ READ_TASK_MATERIALS
→ READ_TARGET_SURFACES
→ DEFINE_SCOPE
→ SCENARIO_CONTRACT
→ IMPLEMENT
→ FILL_CHECKLISTS
→ VERIFY_SCENARIO_DIFF
→ JUDGMENT
→ STRUCTURE_CHECK
→ PUSH_OR_PR
```

## Step Boundaries

- READ_ENTRY
  - Read the entry surfaces named by `AGENTS.md`.
  - Use Tool-first only through the boundary described by `AGENTS.md` and `.agent/tools/README.md`.
  - When Tool-first is unavailable, record the fallback reason and use the routed prompt fallback from `AGENTS.md`.
  - Do not duplicate worktype routing here.
- READ_TASK_MATERIALS
  - Read the user prompt, issue, PR, and explicit required materials.
  - Explicit required-read lists are task input and must be handled before implementation, audit, or completion.
- READ_TARGET_SURFACES
  - Read only target files, target functions, target directories, and SSOT surfaces selected by the active route.
  - Do not infer design intent from filenames only.
- DEFINE_SCOPE
  - Define in-scope and out-of-scope from task materials, routed prompt/protocol surfaces, mapped SSOT, and target files.
  - Do not expand implementation surface outside defined scope.
- SCENARIO_CONTRACT
  - Apply only when the routed prompt/protocol trigger requires it.
- IMPLEMENT
  - Implement only inside the defined scope and preserve explicit-failure behavior.
- FILL_CHECKLISTS
  - Fill only triggered checklists.
  - Checklist output is viewpoint recording, not final pass judgment.
- VERIFY_SCENARIO_DIFF
  - Compare the diff with the task contract and triggered checklist/protocol requirements.
- JUDGMENT
  - Apply only triggered judgment protocols.
  - Protocol index hits are route-selection aids only, not pass/fail judgment.
  - `NOT_EXECUTED` is not `PASS`.
- STRUCTURE_CHECK
  - When Agent UI tool is usable: complete `agent-ui-local-test` (`checks` then `summary`; both include `check-structure.sh` via routed checks). On fallback only: run worktype `required_checks` from `.agent/routes/worktype-required-protocols.yaml` with `bash .agent/tests/check-structure.sh` last among them.
  - Structure check is structural validation, not semantic substitute.
- PUSH_OR_PR
  - Push or update PR only after triggered gates/checks are handled.
  - Push/PR completion summary must include remaining TODO, per `.agent/protocols/completion-summary.md`.
  - Existing-PR updates require follow-up PR comment handling; detail remains owned by `.agent/protocols/completion-summary.md`.

## Scope Discipline

- Do not read all protocols by default.
- Do not read all docs by default.
- Do not read all skills by default.
- Do not read all prompt routers by default.
- Open only the minimum surfaces required by trigger and task scope.
