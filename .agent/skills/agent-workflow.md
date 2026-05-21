# Skill: Agent Workflow

## Purpose

This skill defines the lightweight execution order for task work.

It does not replace `AGENTS.md`, `.agent/rules/rule.md`, or `.agent/README.md`.
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
  - `.agent/rules/rule.md`
  - `.agent/README.md`
  - `.agent/skills/agent-workflow.md`
  - open matching `.agent/prompt/<work-type>.md` only when the work type trigger applies
- READ_TASK_MATERIALS
  - read issue / prompt explicit対応資料 and必読リスト
  - explicit materials and required-read lists are task-required input and must be read
  - this does not mean docs bundle is always-read
  - task-required materials are skip不可 before implementation / audit / completion
- READ_TARGET_SURFACES
  - read target files / target functions / target directories
  - use the matched `.agent/prompt/<work-type>.md` to decide required SSOT and triggered protocols when applicable
  - after a protocol target is selected by trigger/prompt/ssot-map, use `.agent/protocols/index.yaml` to locate section markers before reading protocol body sections
  - protocol index grep hits are route selection aids only (not PASS/FAIL judgment)
  - read `.agent/docs/ssot-map.yaml` only when the target surface needs SSOT mapping or the prompt/task explicitly requires it
  - read corresponding `.agent/docs/` resume/index only when ssot-map or task materials require it
  - read mapped `docs/` SSOT required_docs before implementation/audit only after mapping confirms they are relevant
  - read supporting_docs when mapping indicates they are needed
  - do not infer design intent from filenames only
- DEFINE_SCOPE
  - define in-scope and out-of-scope from task materials, applicable prompt router, `.agent/docs/`, mapped `docs/` SSOT, and target surfaces
  - do not expand implementation surface outside defined scope
- SCENARIO_CONTRACT
  - open `.agent/protocols/scenario-contract.md` only when runtime claim / canonical route / persistence / projection changes are involved
  - create scenario contract before implementation when triggered
- IMPLEMENT
  - implement according to scenario contract and defined scope
  - preserve canonical route / explicit failure / no silent fallback
- FILL_CHECKLISTS
  - after implementation, fill only triggered checklists
  - protocol index routing does not allow skipping checklist/protocol workflow steps
  - policy changes: policy-judgment
  - boundary changes: runtime-boundary-matrix / boundary-identity
  - checklist is viewpoint recording, not final pass judgment
- VERIFY_SCENARIO_DIFF
  - compare full diff with scenario contract
  - verify consistency across contract, checklist, and actual diff
- JUDGMENT
  - open `.agent/protocols/completion.md` and `.agent/protocols/reports-and-todos.md` only for completion / TODO[x] / report judgment
  - before reading triggered JUDGMENT protocol bodies, use `.agent/protocols/index.yaml` grep_keys / section_markers to route the minimal sections to read
  - `.agent/protocols/index.yaml` is not a judgment SSOT; protocol body remains the decision source
  - grep hits are read-route selection only and must not be used as PASS/FAIL judgment
  - `NOT_EXECUTED` is not `PASS`
  - if blocking exists, do not push / complete / TODO[x]
  - CI executes completion judgment checks before structure check as post-implementation verification order; this does not reorder workflow steps
- STRUCTURE_CHECK
  - run `bash .agent/tests/check-structure.sh` last
  - structure check is structural validation, not semantic substitute
- PUSH_OR_PR
  - execute only after all triggered gates pass
  - completion summary must include remaining TODO
  - for a new PR, keep the PR body thin: purpose, high-level scope, and durable references only
  - for an existing PR update, push first, then post a follow-up PR comment (changed summary, checks, remaining TODO, PR-body-thin note) per `.agent/protocols/reports-and-todos.md`
  - for an existing PR update, completion is blocked until either follow-up PR comment is actually posted after push, or posting is impossible and fallback reporting is emitted
  - if a PR comment cannot be posted, the final summary must include `PR_COMMENT_NOT_POSTED` and the exact comment body to paste
  - chat/final summary alone is not a substitute for existing-PR follow-up comment
  - check success (including structure/completion checks) is not a substitute for PR follow-up comment posting state

## Scope Discipline

- Do not read all protocols by default.
- Do not read all docs by default.
- Do not read all skills by default.
- Do not read all prompt routers by default.
- Open only the minimum surfaces required by trigger and task scope.
