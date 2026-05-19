# Policy Judgment Checklist

This checklist is a lightweight **compliance-signature gate** for AGENTS.md / rule.md policy judgment requirements.
Detailed rule definitions belong in `AGENTS.md` and `.agent/protocols/policy-judgment.md`, not in incident-specific checklist expansion.

Policy-Judgment-Need:
Policy-Judgment-Rationale:

Allowed Policy-Judgment-Need values:
- REQUIRED_RUNTIME_CHANGE
- REQUIRED_POLICY_SURFACE_CHANGE
- REQUIRED_DOC_RUNTIME_POLICY_CLAIM
- NOT_REQUIRED_DOC_NO_RUNTIME_POLICY_CLAIM
- NOT_REQUIRED_MECHANICAL_ONLY
- OUT_OF_SCOPE

Declaration rules:
- REQUIRED_*: rationale is required, Answer: lines must be exactly 15, and Q1–Q15 / V1–V16 validation runs.
- NOT_REQUIRED_* / OUT_OF_SCOPE: rationale is required, Answer: lines must be exactly 0, and Q1–Q15 validation is skipped.

Complete this checklist before reporting completion on any change that involves:
runtime behavior, data-defined topology, Registry, Manifest, function_parameters,
structure_map policy, package / schema / component expansion, recommendation,
selection, scoring, threshold, retention, routing, validation, promotion,
disclosure, frontend projection claim, or docs / README / PR summary that
makes a runtime or policy behavior claim.

Not required for: documentation-only, comment-only, README-only, purely
mechanical refactor, display-only copy / style changes, or test-fixture-only
changes — unless those changes also contain a runtime or policy behavior claim.

**Before answering: run `git diff main...HEAD` (or equivalent full branch diff), verify the diff against `.agent/tmp/tmp.txt` scenario contract when required, and base answers on the complete branch state.**

Answer each question with exactly one of: `yes` / `no` / `n/a`

Any NO on blocking items requires recursion to fix phase before completion.

Audit order reminder: create tmp scenario contract when required → implement → inspect full branch diff → verify full diff against tmp scenario contract when required → checklist → scope/claim audits → relevant local CI → `bash .agent/tests/check-structure.sh` (last).

---

## Q1 — Policy or runtime-affecting value introduced or modified?

Does this change introduce or modify a value that controls runtime behavior,
policy behavior, scoring, thresholds, limits, retention windows, sort order,
routing, validation, promotion, disclosure, projection shape, or emission?

Answer:

---

## Q2 — Production fallback constant present?

If Q1=yes: is the value currently substituted with a hardcoded default when
topology / policy data is absent?

Examples:
- `?? someValue`
- `LIMIT 100`
- private constant used as production fallback
- hardcoded fallback policy

If Q1=no or n/a, answer n/a.

Answer:

---

## Q3 — Explicit missing-policy status returned?

If Q2=yes: does the code return an explicit missing-policy / missing-parameter /
invalid-policy status instead of substituting the fallback constant?

If Q2=no or n/a, answer n/a.

Answer:

---

## Q4 — Value resolved from a policy surface?

If Q1=yes: is the value resolved from a policy surface?

Examples:
- Registry
- Manifest
- function_parameters
- structure_map policy
- package-schema parameter
- explicit policy file / policy row

If Q1=no or n/a, answer n/a.

Answer:

---

## Q5 — Silent fallback introduced?

Does this change introduce a silent fallback where missing runtime / policy data
is automatically replaced without surfacing an explicit error or status?

Answer:

---

## Q6 — Unexplained production policy constant introduced?

Does this change introduce an unexplained constant that controls runtime output,
candidate ranking, filtering, retention, routing, validation, promotion,
disclosure, or projection behavior?

Test fixtures, loop counters, protocol constants, and display-only values are
exempt.

Answer:

---

## Q7 — Canonical runtime route bypassed?

Does this change skip or bypass any step of the canonical runtime route?

```
stored_topology_data → user_operation → operation_vector → attractor_resolve
→ structure_map_resolve → package_resolve → schema_resolve
→ component_expand → emission_or_projection
```

Answer:

---

## Q8 — Business logic added to frontend projection layer?

Does this change add data computation, business logic, or state derivation to
the frontend projection layer beyond rendering resolved data as props?

Answer:

---

## Q9 — Broken reference swallowed silently?

Does this change suppress a broken-reference error or resolve a missing ref
silently instead of returning an explicit validation error or status?

Answer:

---

## Q10 — Policy fields consumed by runtime / policy executor?

If this change adds or modifies policy fields in schema / JSON / seed / docs,
are all behavior-relevant fields actually consumed by the runtime or the
relevant policy executor?

If no policy field is added or modified, answer n/a.

Answer:

---

## Q11 — Demo / mock / static values isolated?

If this change adds demo / mock / static values, are they clearly isolated from
runtime / policy behavior claims?

If no demo / mock / static values are added, answer n/a.

Answer:

---

## Q12 — Full branch diff inspected, and tmp scenario contract verified when required?

Did you inspect the full branch diff before answering this checklist, and verify
that full diff against `.agent/tmp/tmp.txt` scenario contract when tmp is required?
If a mismatch or missing required verification is found, recurse to fix phase before completion.

Use `git diff main...HEAD` or an equivalent full branch diff — not only the
latest commit or edited files.

Answer:

---

## Q13 — Checklist based on full branch diff and scenario contract verification when required?

Are these checklist answers based on the full branch diff and scenario contract
verification when required, not only the latest commit or edited files?
Any boundary-matrix or scenario-contract verification gap here is a recursive blocking failure.

Answer:

---

## Q14 — Required local checks passed?

Did the required local checks pass before completion report?
If a required local check is NOT EXECUTED, equivalent remote CI success must be verified before completion; queued/in_progress is not PASS.
Structure Check is always-on, heavy CI workflows are path-scoped, and scope-irrelevant workflow-level skip is not blocking.

Required: `bash .agent/tests/check-structure.sh` (always), plus any
domain-specific check triggered by this change (db-schema, backend-tests,
frontend-types).

If a tool is missing, answer `n/a` and report NOT EXECUTED in the completion
summary. Do not answer `yes` for a check that was not actually run.
Relevant local CI includes domain checks (db-schema, backend-tests, frontend-types)
and change-triggered custom agent tests such as `check-default-entity-search.sh`.
When changed scope touches `infra/docker-compose.yml`, `.agent/scripts/bootstrap-local-postgres.sh`, or DB init path, docker-compose/bootstrap verification is required and NOT EXECUTED is not PASS.

Answer:

---

## Q15 — Remaining TODOs listed?

Are remaining TODOs explicitly listed in the completion report or PR summary?

Answer:

---

## Violation Summary

`check-policy-judgment.sh` fails on:

| Rule | Condition |
|---|---|
| V1 | Q5 = yes — silent fallback |
| V2 | Q6 = yes — unexplained production policy constant |
| V3 | Q2 = yes AND Q3 = no — fallback present, no explicit-error replacement |
| V4 | Q1 = yes AND Q4 = no — policy/runtime value not from a policy surface |
| V5 | Q7 = yes — canonical route bypassed |
| V6 | Q8 = yes — business logic in frontend projection layer |
| V7 | Q9 = yes — broken reference swallowed |
| V8 | Q10 = no — policy fields not consumed by runtime/policy executor |
| V9 | Q11 = no — demo/mock/static values not isolated |
| V10 | Q12 = no — full branch diff not inspected and/or required scenario contract verification missing |
| V11 | Q14 = no — required local checks not passed |
| V12 | Q13 = no — checklist answers not based on full branch diff and required scenario contract verification |
| V13 | Q15 = no — remaining TODOs not listed in completion report or PR summary |
| V14 | Any answer not in {yes, no, n/a} |
| V15 | Missing answer |
| V16 | Fewer or more than 15 answers |
