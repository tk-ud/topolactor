# Skill: Agent Workflow

## Purpose

This skill defines the lightweight execution order for task work.

It does not replace `AGENTS.md`, `.agent/README.md`, or `.agent/rules/rule.md`.
Follow those always-read sources first.

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

## Step Routing (reference only)

- READ_ENTRY
  - `AGENTS.md`
  - `.agent/README.md`
  - `.agent/rules/rule.md`
  - `.agent/skills/agent-workflow.md`
- READ_TASK_MATERIALS
  - read issue / prompt explicit対応資料 and必読リスト
  - explicit materials and required-read lists are task-required input and must be read
  - this does not mean docs bundle is always-read
  - task-required materials are skip不可 before implementation / audit / completion
- READ_TARGET_SURFACES
  - read target files / target functions / target directories
  - read `.agent/docs/ssot-map.yaml` and locate the changed surface mapping
  - read corresponding `.agent/docs/` resume/index for changed surface
  - read mapped `docs/` SSOT required_docs before implementation/audit
  - when `.agent/docs/` mapping points to docs/ SSOT, those target SSOT reads are mandatory
  - read supporting_docs when mapping indicates they are needed
  - do not infer design intent from filenames only
- DEFINE_SCOPE
  - define in-scope and out-of-scope from task materials, `.agent/docs/`, `docs/` SSOT, and target surfaces
  - do not expand implementation surface outside defined scope
- SCENARIO_CONTRACT
  - open `.agent/protocols/scenario-contract.md` only when runtime claim / canonical route / persistence / projection changes are involved
  - create scenario contract before implementation when triggered
- IMPLEMENT
  - implement according to scenario contract and defined scope
  - preserve canonical route / explicit failure / no silent fallback
- FILL_CHECKLISTS
  - after implementation, fill only triggered checklists
  - policy changes: policy-judgment
  - boundary changes: runtime-boundary-matrix / boundary-identity
  - checklist is viewpoint recording, not final pass judgment
- VERIFY_SCENARIO_DIFF
  - compare full diff with scenario contract
  - verify consistency across contract, checklist, and actual diff
- JUDGMENT
  - open `.agent/protocols/completion.md` and `.agent/protocols/reports-and-todos.md` only for completion / TODO[x] / report judgment
  - `NOT_EXECUTED` is not `PASS`
  - if blocking exists, do not push / complete / TODO[x]
  - CI executes completion judgment checks before structure check as post-implementation verification order; this does not reorder workflow steps
- STRUCTURE_CHECK
  - run `bash .agent/tests/check-structure.sh` last
  - structure check is structural validation, not semantic substitute
- PUSH_OR_PR
  - execute only after all triggered gates pass
  - completion summary must include remaining TODO

## Scope Discipline

- Do not read all protocols by default.
- Do not read all docs by default.
- Do not read all skills by default.
- Open only the minimum surfaces required by trigger and task scope.
