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
  - if the executing agent is Claude (Claude Code on the web / remote execution environment), read `.agent/protocols/claude.md` for environment setup before any local tool execution
  - open matching `.agent/prompt/<work-type>.md` only when the work type trigger applies
  - for existing PR follow-up updates, route via `.agent/prompt/existing-pr-update.md`
- READ_TASK_MATERIALS
  - read issue / prompt explicit対応資料 and必読リスト
  - explicit materials and required-read lists are task-required input and must be read
  - this does not mean docs bundle is always-read
  - task-required materials are skip不可 before implementation / audit / completion
- READ_TARGET_SURFACES
  - read target files / target functions / target directories
  - use the matched `.agent/prompt/<work-type>.md` to decide required SSOT and triggered protocols when applicable
  - after a protocol target is selected by trigger/prompt/ssot-map:
    - for protocols named as direct-read in `.agent/rules/rule.md`
      (policy-judgment.md, runtime-boundary-matrix.md, scenario-contract.md),
      read the protocol file directly without opening index.yaml
    - for all other protocols, use `.agent/protocols/index.yaml` to locate
      section markers before reading protocol body sections
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
  - open `.agent/protocols/completion.md` for completion eligibility judgment; open `.agent/protocols/completion-summary.md` for terminal summary/output sink; open `.agent/protocols/todo-carry-over.md` only when remaining TODO classification is needed; open `.agent/protocols/report-surfaces.md` only when report placement routing is needed
  - before reading triggered JUDGMENT protocol bodies (completion.md, completion-summary.md, todo-carry-over.md, report-surfaces.md, registry-tensor-policy.md), use `.agent/protocols/index.yaml` section-level `sections[].grep_keys` / `sections[].marker` routes to read only the minimal protocol sections
  - for small direct-read protocols (policy-judgment.md, runtime-boundary-matrix.md, scenario-contract.md) triggered at JUDGMENT, read them directly without index.yaml
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
  - when needed, create/update PR body first as a thin entry summary only
  - completion / follow-up summary composition must follow `## Completion Summary Template` in `.agent/protocols/completion-summary.md`
  - initial Codex / Agent final completion summary is in template scope even on first PR creation
  - for a new PR, keep the PR body thin: purpose, high-level scope, and durable references only
  - for an existing PR update, execute this fixed external-state order (policy judgment remains in `.agent/protocols/completion-summary.md`):
    - work-type route source: `.agent/prompt/existing-pr-update.md`
    1. determine whether this work is an existing-PR update
    2. confirm push or remote PR update actually occurred
    3. identify target PR number/URL and head commit
    4. apply follow-up comment requirement from `.agent/protocols/completion-summary.md`
    5. confirm whether PR comment posting capability is available
    6. when posting is available, post follow-up PR comment after push
    7. verify posted comment exists in PR conversation
    8. if posting is unavailable or post-verification is unavailable, emit `PR_COMMENT_NOT_POSTED` with exact paste-ready comment body in final summary
    9. only then emit final summary
  - for existing-PR updates, follow-up PR comment is required and uses the same template structure
  - follow-up PR comment requirement applies only to existing-PR updates
  - check/report sequence in PUSH_OR_PR order: (1) run required local checks in triggered scope with NOT_EXECUTED kept separate from PASS, (2) run `bash .agent/tests/check-structure.sh` last, (3) push / remote PR update, (4) for existing PR update post follow-up PR comment and verify posted state (or emit `PR_COMMENT_NOT_POSTED` evidence), (5) emit final completion summary in template scope
  - follow Prompt Type / Work Type Output Switch in `.agent/protocols/completion-summary.md` for required output surfaces and required external actions
  - Completion Summary Template defines body shape only and must not be used as an action substitute
  - for existing-PR updates, chat/final summary alone is not a substitute for PR follow-up comment handling
  - check success (including structure/completion checks) is not a substitute for PR follow-up comment posting/verification state

## Scope Discipline

- Do not read all protocols by default.
- Do not read all docs by default.
- Do not read all skills by default.
- Do not read all prompt routers by default.
- Open only the minimum surfaces required by trigger and task scope.
