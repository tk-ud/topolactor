# Skill: Agent Workflow

## Purpose

This skill defines the lightweight execution order for task work.

It does not replace `AGENTS.md`, `.agent/README.md`, or `.agent/rules/rule.md`.
Follow those always-read sources first.

## Execution Order

```text
READ_ENTRY
→ SCENARIO_CONTRACT
→ IMPLEMENT
→ FILL_CHECKLISTS
→ VERIFY_SCENARIO_DIFF
→ JUDGMENT
→ STRUCTURE_CHECK
→ PUSH_OR_PR
```

## Step Routing (reference only)

- READ_ENTRY
  - `AGENTS.md`
  - `.agent/README.md`
  - `.agent/rules/rule.md`
- SCENARIO_CONTRACT
  - `.agent/protocols/scenario-contract.md` (triggered scope only)
- IMPLEMENT
  - implement using scoped files and fixed scenario intent when contract exists
- FILL_CHECKLISTS
  - fill policy-judgment / boundary-identity / required-check declarations only when corresponding triggers apply
- VERIFY_SCENARIO_DIFF
  - verify scenario contract and full branch diff consistency when scenario trigger applies
- JUDGMENT
  - `.agent/protocols/completion.md` and `.agent/protocols/reports-and-todos.md` only when completion / TODO / report judgment is needed
- STRUCTURE_CHECK
  - run `bash .agent/tests/check-structure.sh` last
  - structure check is structural validation, not semantic substitute
- PUSH_OR_PR
  - execute only after triggered gates pass and blocking conditions are cleared

## Scope Discipline

- Do not read all protocols by default.
- Do not read all docs by default.
- Do not read all skills by default.
- Open only the minimum surfaces required by trigger and task scope.
