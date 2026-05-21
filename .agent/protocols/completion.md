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
  - includes initial final summary and existing PR follow-up comment
  - excludes initial PR body (follow thin PR body policy in `.agent/protocols/reports-and-todos.md`)
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
   - Batch execution is allowed, but batch completion is not automatic.
   - For batch work, judge every TODO item independently against its own completion condition, roadmap status, required evidence, and required checks.
   - Partial / skeleton implementation, remaining `known_gap_ref`, unmet `completion_condition`, unconnected runtime lane, or missing required test evidence blocks TODO `[x]` for that item.
   - Only after gate pass may TODO items be marked `[x]`.
6. Responsibility boundary judgment (implementation agent vs auditor)
   - Implementation agent must provide decision material, not self-author completion truth by default.
   - Implementation agent responsibilities:
     - produce implementation diff
     - provide completion summary / PR follow-up material
     - classify checks as PASS / FAIL / NOT_EXECUTED / REMOTE_REQUIRED
     - keep local NOT_EXECUTED and remote CI success as separate facts; never collapse them
     - provide remaining risks, close candidates, and roadmap-update candidates as Auditor TODO inputs
   - Auditor responsibilities:
     - confirm required checks / PR CI all-green (or equivalent required-success evidence)
     - judge whether implementation meaningfully satisfies target gap / TODO semantics
     - resolve Auditor TODO classification
     - finalize canonical updates to `.agent/tasks/todo.md` and `docs/system-roadmap.yaml`
   - Implementation agent is not the default final arbiter for canonical TODO/roadmap closure on its own diff unless explicit direct-update conditions are all satisfied.
7. Implementation-agent direct canonical update conditions
   - Implementation agent may directly update `.agent/tasks/todo.md` and/or `docs/system-roadmap.yaml` only when all conditions hold:
     1) task scope explicitly includes TODO/roadmap canonical maintenance,
     2) required checks (or remote CI equivalent required success) are confirmed,
     3) target TODO/gap completion condition is semantically satisfied,
     4) no remaining concrete implementation/design/SSOT/test-authoring residue exists,
     5) update is consistent with completion and reports-and-todos protocols.
   - If any condition is unknown, disputed, or pending auditor judgment, defer canonical closure and emit Auditor TODO instead.
8. Structure Verification
   - Run `bash .agent/tests/check-structure.sh` last.
   - Structure Check is the always-on required gate.
   - Structure verification is structural consistency check; it is not a semantic substitute.
9. Push
   - Push only after all triggered gates pass and no blocking condition remains.



## Local/Remote required check mapping (dotnet/deno)

When a required local check cannot run because a required tool is absent, classify it as `REQUIRED_NOT_EXECUTED` (not PASS), then reconcile it only via equivalent remote CI success evidence.

Apply this mapping only after Required Check Scope declaration:
- `REQUIRED_EXECUTED` / `REQUIRED_NOT_EXECUTED`: remote evidence requirement applies for that scope.
- `NOT_REQUIRED` / `OUT_OF_SCOPE`: local missing-tool result is recorded, but remote evidence is not required for the current PR scope.

| Local scope | Local command | Missing tool behavior | Local status | Remote workflow | Remote job/check | Remote evidence rule |
|---|---|---|---|---|---|---|
| backend test scope | `bash .agent/tests/check-backend-tests.sh` | `dotnet` missing returns non-zero and states not executed / not pass | `REQUIRED_NOT_EXECUTED` | `backend-tests` | `backend-tests` | `REMOTE_REQUIRED` is satisfied only when PR check is `success` for the matching scope. |
| frontend type-check scope | `bash .agent/tests/check-frontend-types.sh` | `deno` missing returns non-zero and states not executed / not pass | `REQUIRED_NOT_EXECUTED` | `frontend-types` | `check-frontend-types` | `REMOTE_REQUIRED` is satisfied only when PR check is `success` for the matching scope. |

Remote states `queued`, `in_progress`, `failure`, `cancelled`, and unjustified `skipped` are not PASS and do not satisfy Remote CI Equivalence Gate.

Completion summary must keep these as separate facts:
- local execution fact (`PASS` vs `REQUIRED_NOT_EXECUTED`)
- remote evidence fact (`REMOTE_REQUIRED` pending vs remote `success`)

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

- Roadmap Status Gate
  - When completion summary / TODO `[x]` / implemented claim / production_ready claim appears, verify `docs/system-roadmap.yaml` implementation_registry for target components.
  - If component remains `skeleton` / `partial` / `not_started`, do not report completed/production_ready.
  - Raising to `status: implemented` requires satisfied `completion_condition` and evidence.
  - Raising to `production_ready: true` requires no remaining `known_gap_ref`.
  - If production code still contains skeleton/stub/dummy/pass-through markers, `implemented` / `production_ready` is blocking.
  - Any contradiction between roadmap status and PR summary / completion report / TODO update is blocking.

- Batch Completion Gate
  - A batch PR may touch multiple TODO items, but each item must be classified separately as completed, partial, out-of-scope, or still remaining.
  - Do not mark a parent TODO `[x]` when the PR only adds a partial surface, skeleton boundary, unconnected helper, or implementation note.
  - If an item still has concrete implementation / design / SSOT / test-authoring residue, keep it open or rewrite it into a smaller remaining TODO.
  - Do not leave residual work under an `[x]` TODO item.

- CI Failure Index Gate
  - Required check failures are semantic indexes, not just terminal blockers.
  - For each failed required check, map the failure to:
    1. failed surface,
    2. responsible SSOT / protocol / target file,
    3. concrete implementation fix or remaining TODO.
  - Compilation and typecheck failures index implementation surface and contract-boundary mismatches.
  - Runtime-semantics failures index canonical route / runtime policy mismatches.
  - Pipeline-continuity failures index lane identity / route continuity mismatches.
  - Structure Check success must not override compile, type, runtime, pipeline, or domain test failure.

Pass eligibility requires all blocking items resolved or, when the blocker exposes unfinished implementation/design/SSOT/test-authoring work, explicitly preserved as Remaining TODO under gate rules.

Verification-only blockers are not `.agent/tasks/todo.md` items. CI waiting, remote CI pass confirmation, local tool absence, and unexecuted-check bookkeeping belong in the completion report's Required Check Scope / verification section. If such verification reveals concrete follow-up work, preserve only that concrete work in `.agent/tasks/todo.md`.

Defer-to-auditor conditions (do not directly close canonical TODO/roadmap in implementation pass):
- required PR CI interpretation is pending auditor confirmation
- roadmap status raise requires semantic judgment beyond executed diff evidence
- TODO closure would rely on implementer self-judgment without independent confirmation
- local NOT_EXECUTED ↔ remote CI evidence mapping needs explicit reconciliation
- remaining-work classification (implementation residue vs verification bookkeeping) is still ambiguous

NOT EXECUTED ≠ PASS.
scope-irrelevant workflow-level skip is not blocking.
Remote CI Equivalence Gate: REQUIRED_NOT_EXECUTED is never PASS without equivalent remote CI success.

Completion report entries include failure triage result and required check scope declaration.
Completion Summary Template Gate scope reminder:
- writing completion summary trigger includes initial final summary and existing PR follow-up comment
- initial PR body is out of scope and remains thin PR body policy
- final summary omission from template scope is a completion reporting gap
