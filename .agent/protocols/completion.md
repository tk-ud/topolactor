# Completion Governance Protocol (Agenda: completion-governance)

Completion Sequence

## Workflow Guard

- Use this protocol only during JUDGMENT for completion eligibility / TODO `[x]` / completion summary decisions.
- Do not skip SCENARIO_CONTRACT / IMPLEMENT / FILL_CHECKLISTS / VERIFY_SCENARIO_DIFF before this judgment stage.
- NOT_REQUIRED / OUT_OF_SCOPE declarations must be explicit.
- Trigger-non-applicable gates must be declared as NOT_REQUIRED / OUT_OF_SCOPE explicitly; silent skip is prohibited.

## Trigger condition

Run this protocol only when any of the following is attempted:

- writing completion summary / completion report
- completion eligibility decision
- updating `.agent/tasks/todo.md` item to `[x]`

This is the SSOT for completion-governance decisions.

## Scope

This protocol owns:

- Recursive Verification Gate
- Required Check Scope Declaration Gate
- Remote CI Equivalence Gate
- Failure Triage Self-Recursion Gate
- Audit Gap Response Gate
- completion order and blocking criteria

## Execution procedure

1. Scenario Contract
   - When scenario-contract trigger applies, create `.agent/tmp/tmp.txt` before implementation.
   - Fix intent before coding: user-visible scenario/runtime claim, expected route, expected read/write/append/cache/return order, expected status, and failure paths.
2. Implementation
   - Implement against the scenario contract when present.
   - If implementation intentionally diverges from contract, update contract with explicit reason.
3. Checklist Fill
   - After implementation, fill applicable checklist/declaration surfaces.
   - Fill policy-judgment / boundary-identity / required check scope only when each trigger applies.
   - Checklist fill at this stage is viewpoint capture, not final completion judgment.
4. Scenario Diff Verification
   - Inspect full branch diff (`git status --short`, `git diff -- . ":(exclude).git"`, `git diff --cached -- . ":(exclude).git"`).
   - Verify full diff against scenario contract when triggered.
   - Verify contract/checklist/actual-diff consistency.
   - If mismatch exists, fix implementation or update contract with reason, then re-verify.
5. Judgment
   - Apply Failure Triage Self-Recursion Gate.
   - Execute policy judgment / boundary judgment / completion eligibility / failure triage / remote CI equivalence in triggered scope.
   - Apply Required Check Scope declaration labels:
     - REQUIRED_EXECUTED
     - REQUIRED_NOT_EXECUTED
     - NOT_REQUIRED
     - OUT_OF_SCOPE
   - Apply Recursive Verification Gate and blocking criteria.
   - Record unexecuted / queued / remote-CI-dependent checks in the completion report verification section, not as `.agent/tasks/todo.md` items.
   - Only after gate pass may TODO items be marked `[x]`.
6. Structure Verification
   - Run `bash .agent/tests/check-structure.sh` last.
   - Structure Check is the always-on required gate.
   - Structure verification is structural consistency check; it is not a semantic substitute.
7. Push
   - Push only after all triggered gates pass and no blocking condition remains.

## Completion / failure decision

Blocking (completion prohibited):

- any required gate/check status is FAIL
- missing or ambiguous Required Check Scope declaration
- REQUIRED_NOT_EXECUTED without equivalent remote CI success (scope-relevant)
- remote CI equivalent queued/in_progress/failure/cancelled/skipped-unjustified for required scope
- scenario-contract mismatch when triggered
- boundary matrix or boundary identity gap when triggered
- policy-judgment violation when triggered
- unclassified failure in failure triage
- missing required sections for audit gap response
- report/diff contradiction

Pass eligibility requires all blocking items resolved or, when the blocker exposes unfinished implementation/design/SSOT/test-authoring work, explicitly preserved as Remaining TODO under gate rules.

Verification-only blockers are not `.agent/tasks/todo.md` items. CI waiting, remote CI pass confirmation, local tool absence, and unexecuted-check bookkeeping belong in the completion report's Required Check Scope / verification section. If such verification reveals concrete follow-up work, preserve only that concrete work in `.agent/tasks/todo.md`.

NOT EXECUTED ≠ PASS.
scope-irrelevant workflow-level skip is not blocking.
Remote CI Equivalence Gate: REQUIRED_NOT_EXECUTED is never PASS without equivalent remote CI success.

Completion report entries include failure triage result and required check scope declaration.