# Policy Judgment Checklist — fixture: FAIL (local checks not passed)
Policy-Judgment-Need: REQUIRED_RUNTIME_CHANGE
Policy-Judgment-Rationale: Existing fixture exercises V11 local-check failure behavior.

Scenario: agent reports completion but local CI checks are red (check-structure.sh fails).
Q14=no triggers V11.
Expected result: FAIL — 1 violation (V11).

---

## Q1 — Policy or runtime-affecting value introduced or modified?

Answer: yes

---

## Q2 — Production fallback constant present?

Answer: no

---

## Q3 — Explicit missing-policy status returned?

Answer: n/a

---

## Q4 — Value resolved from a policy surface?

Answer: yes

---

## Q5 — Silent fallback introduced?

Answer: no

---

## Q6 — Unexplained production policy constant introduced?

Answer: no

---

## Q7 — Canonical runtime route bypassed?

Answer: no

---

## Q8 — Business logic added to frontend projection layer?

Answer: no

---

## Q9 — Broken reference swallowed silently?

Answer: no

---

## Q10 — Policy fields consumed by runtime / policy executor?

Answer: yes

---

## Q11 — Demo / mock / static values isolated?

Answer: n/a

---

## Q12 — Full branch diff inspected, and tmp scenario contract verified when required?

Answer: yes

---

## Q13 — Checklist based on full branch diff and scenario contract verification when required?

Answer: yes

---

## Q14 — Required local checks passed?

Answer: no

---

## Q15 — Remaining TODOs listed?

Answer: yes
