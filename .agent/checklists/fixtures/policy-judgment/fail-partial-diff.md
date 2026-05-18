# Policy Judgment Checklist — fixture: FAIL (checklist not based on full branch diff)

Scenario: agent ran git diff and completed required scenario contract verification (Q12=yes), but
the checklist answers are not based on the full branch diff + scenario contract verification (Q13=no) — the agent
answered based only on the latest commit or edited files.
Q13=no triggers V12.
Expected result: FAIL — 1 violation (V12).

---

## Q1 — Policy or runtime-affecting value introduced or modified?

Answer: no

---

## Q2 — Production fallback constant present?

Answer: n/a

---

## Q3 — Explicit missing-policy status returned?

Answer: n/a

---

## Q4 — Value resolved from a policy surface?

Answer: n/a

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

Answer: n/a

---

## Q11 — Demo / mock / static values isolated?

Answer: n/a

---

## Q12 — Full branch diff inspected, and tmp scenario contract verified when required?

Answer: yes

---

## Q13 — Checklist based on full branch diff and scenario contract verification when required?

Answer: no

---

## Q14 — Required local checks passed?

Answer: yes

---

## Q15 — Remaining TODOs listed?

Answer: yes
