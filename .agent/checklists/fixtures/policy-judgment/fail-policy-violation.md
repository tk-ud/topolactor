# Policy Judgment Checklist — fixture: FAIL (policy violations)

Scenario: agent introduced a hardcoded LIMIT 100 as a production runtime constant
without resolving it from function_parameters, and substituted ?? 3650 as a
silent fallback for a missing RecentDays policy value.

Expected violations: V1 (Q5=yes), V2 (Q6=yes), V3 (Q2=yes,Q3=no), V4 (Q1=yes,Q4=no).
Expected result: FAIL — 4 violations.

---

## Q1 — Policy or runtime-affecting value introduced or modified?

Answer: yes

---

## Q2 — Production fallback constant present?

Answer: yes

---

## Q3 — Explicit missing-policy status returned?

Answer: no

---

## Q4 — Value resolved from a policy surface?

Answer: no

---

## Q5 — Silent fallback introduced?

Answer: yes

---

## Q6 — Unexplained production policy constant introduced?

Answer: yes

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

## Q12 — Full branch diff inspected?

Answer: yes

---

## Q13 — Checklist based on full branch diff?

Answer: yes

---

## Q14 — Required local checks passed?

Answer: yes

---

## Q15 — Remaining TODOs listed?

Answer: yes
