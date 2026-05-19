# Completion Governance Protocol (Agenda: completion-governance)

Completion Sequence

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

1. Inspect full branch diff (`git status --short`, `git diff -- . ":(exclude).git"`, `git diff --cached -- . ":(exclude).git"`).
2. Run scenario-contract verification when its trigger applies.
3. Run Runtime Boundary Failure Matrix and boundary identity verification when trigger applies.
4. Run policy-judgment gate when trigger applies; if not triggered, declare NOT_REQUIRED/OUT_OF_SCOPE with rationale.
5. Declare required check scope from changed scope with per-check label (required check scope declaration):
   - REQUIRED_EXECUTED
   - REQUIRED_NOT_EXECUTED
   - NOT_REQUIRED
   - OUT_OF_SCOPE
6. Execute required local checks for touched scope.
7. Apply Remote CI Equivalence Gate for each scope-relevant REQUIRED_NOT_EXECUTED check.
8. Apply Failure Triage Self-Recursion Gate across all executed command failures.
9. Apply Audit Gap Response Gate sections and classification.
10. Apply Recursive Verification Gate.
11. If `.agent/tmp/tmp.txt` exists and all recursive verification tasks are complete, delete via `bash .agent/scripts/delete-tmp.sh`.
12. Run `bash .agent/tests/check-structure.sh` last.
13. Only after gate pass may TODO items be marked `[x]`.

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

Pass eligibility requires all blocking items resolved or explicitly preserved as Remaining TODO under gate rules.

NOT EXECUTED ≠ PASS.
Structure Check is the always-on required gate.
scope-irrelevant workflow-level skip is not blocking.
Remote CI Equivalence Gate: REQUIRED_NOT_EXECUTED is never PASS without equivalent remote CI success.


Completion report entries include failure triage result and required check scope declaration.