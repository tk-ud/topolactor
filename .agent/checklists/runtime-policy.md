# Runtime Policy Checklist

Complete this checklist before reporting completion on any task that touches
runtime behavior, data-defined topology, canonical route steps, or frontend projection.

Answer each question with exactly one of: `yes` / `no` / `n/a`

---

## Q1 — Runtime-affecting value introduced or modified?

Does this change introduce or modify a value that controls runtime behavior —
scoring, thresholds, limits, retention windows, sort order, routing, projection
shape, validation, or emission?

Answer:

---

## Q2 — Production fallback constant present?

If Q1=yes: is the value currently substituted with a hardcoded default when
topology policy is absent (e.g. `?? someValue`, `LIMIT 100`, a magic number
constant)?

If Q1=no or n/a, answer n/a.

Answer:

---

## Q3 — Explicit missing-policy status returned?

If Q2=yes: does the code return an explicit missing-policy or missing-parameter
status instead of substituting the fallback constant?

If Q2=no or n/a, answer n/a.

Answer:

---

## Q4 — Value resolved from a policy surface?

If Q1=yes: is the value resolved at runtime from a policy surface
(function_parameters, Registry, Manifest, structure_map, or package-schema
parameter)?

If Q1=no or n/a, answer n/a.

Answer:

---

## Q5 — Silent fallback introduced?

Does this change introduce a silent fallback — a value substituted automatically
when a runtime policy is absent, without surfacing an error to the caller?

Answer:

---

## Q6 — Unexplained production runtime constant introduced?

Does this change introduce an unexplained constant that controls runtime output,
candidate ranking, filtering, retention, routing, or projection behavior?

(Test fixtures, loop counters, protocol constants, and display-only values are
exempt. Inline values that affect the canonical flow result are not.)

Answer:

---

## Q7 — Canonical runtime route step bypassed?

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

(Resolving from defaultStructureMap / defaultComponentRegistry in the frontend
canonical flow is not business logic — it is projection. Deriving or computing
business state in the frontend is.)

Answer:

---

## Q9 — Broken reference swallowed silently?

Does this change suppress a broken-reference error or resolve a missing ref
silently instead of returning an explicit validation error?

Answer:

---

## Q10 — Structure check passed?

Has `.agent/tests/check-structure.sh` been run and returned zero failures for
this change?

Answer:

---

## Violation Summary

The check script (`check-runtime-policy-local.sh`) fails on:

| Rule | Condition |
|---|---|
| V1 | Q5 = yes |
| V2 | Q6 = yes |
| V3 | Q2 = yes AND Q3 = no |
| V4 | Q1 = yes AND Q4 = no |
| V5 | Q7 = yes |
| V6 | Q8 = yes |
| V7 | Q9 = yes |
| V8 | Q10 = no |
| V9 | Any answer not in {yes, no, n/a} |
