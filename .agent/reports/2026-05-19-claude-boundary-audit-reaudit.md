# Claude Implementation Boundary Audit — Re-audit (2026-05-19)

## Scope

Re-audit of A1 / A2 / A4 / A11 from the previous Claude Implementation Boundary Audit
(`702bae6`). Purpose: verify that conditional / caution / non-fatal findings are NOT
classified as unconditional PASS, and properly reclassify as GAP or TODO where required.

Source evidence: static code analysis of implementation files.

---

## Re-audit Findings

### A1 — context_event append: LogError is not an explicit result surface

**Audit item:** context_event append が silent failure にならないか  
**Original classification:** PASS ("Failures are explicitly logged (LogError), not silent")  
**Evidence file:** `backend/runtime/ContextRouteRecommendationResolver.cs:183-190`

```csharp
try
{
    await _contextRouteRepository.AppendContextEventAsync(contextEvent, ct);
}
catch (Exception ex)
{
    _logger.LogError(ex, "ContextRouteRepository.AppendContextEventAsync failed — continuing.");
}
```

**Re-classification: GAP**

`reports-and-todos.md` explicitly states:
> "Do not classify log-only output, PR-body-only narrative, or static-document confirmation
> as explicit behavior evidence."

`LogError` is log-only output. The caller receives no indication that the append failed.
The recommendation result is returned to the caller as-if the append succeeded.
This is not an explicit result surface.

AGENTS.md Contract Rule 2: "No silent fallback; broken refs and boundary failures are
explicit results." The append failure is not surfaced as an explicit result to the caller.

The original audit conflated "LogError is called" with "failure is not silent." These are
not equivalent. LogError ≠ explicit result surface.

**Governance deficiency:** The previous audit accepted log-only handling as "explicit
result surface," which contradicts `reports-and-todos.md` classification policy.

---

### A2 — Recoverable boundary: non-fatal side effects need explicit policy documentation

**Audit item:** transition stats / context event / TVR extension の失敗を「継続可能」にしてよい境界  
**Original classification:** PASS ("Boundaries correctly defined — side-effect operations are non-fatal")  
**Evidence files:**
- `backend/runtime/ContextRouteRecommendationResolver.cs:140-163` (transition stats)
- `backend/runtime/ContextRouteRecommendationResolver.cs:196-208` (TVR extension)

Pattern in both:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "... — continuing.");
}
```

**Re-classification: GAP (conditional)**

The "non-fatal by design" assertion in the original audit was accepted as a policy decision,
but this policy is expressed only in code comments, not in a recoverable-boundary policy
document. There is no policy surface that declares which operations are non-fatal and why.

TVR extension includes hub_attention persistence writes. If the TVR extension fails, the
hub_attention update is lost silently from the caller's perspective. This is a side-effect
write to persistent state, not merely a read operation.

The original audit concluded PASS based on architectural intent described in commit messages,
not on a documented and verifiable policy surface. Per `reports-and-todos.md`:
> "Conditional pass, caution, and non-fatal findings must not be treated as unconditional PASS."

**Governance deficiency:** Non-fatal boundary decisions are expressed in code comments
only; no authoritative policy surface declares these boundaries.

---

### A4 — DB CHECK constraints hardcode policy values

**Audit item:** DB CHECK / column name / seed policy が policy可変性と衝突していないか  
**Original classification:** PASS ("noted as infrastructure constraint requiring migration for new tiers")  
**Evidence file:** `db/context_route_tables.sql`

```sql
scope_limit   INT  NOT NULL  CHECK (scope_limit IN (1000, 3000, 10000)),
candidate_kind TEXT NOT NULL  CHECK (candidate_kind IN ('registry','hub','entity','relation','operation','token')),
feedback_kind  TEXT NOT NULL  CHECK (feedback_kind IN ('selected','ignored','missing_candidate')),
```

These hardcoded CHECK constraints exist at two locations for scope_limit and candidate_kind
(context_hub_recommendation_current and context_hub_feedback_event tables).

**Re-classification: GAP**

The original audit noted "requiring migration for new tiers" and still classified as PASS.
"Requires migration" IS a governance gap, not a conditional PASS condition.

DB CHECK constraints directly reduce policy variability:
- Adding a new scope_limit tier requires a schema migration.
- Adding a new candidate_kind requires a schema migration.
- These values should be derivable from policy/registry, but schema locks them.

`rule.md` (Architecture Rules): "DB is the semantic topology space. It stores registries,
schemas, packages, relations, structure maps, and function parameters." When the DB itself
constrains policy-configurable values, the policy layer is undermined.

**Governance deficiency:** The original PASS classification accepted a known infrastructure
limitation without recording it as a GAP requiring improvement.

---

### A11 — Persistence constraint failure not tested

**Audit item:** happy path のみのテストで boundary failure matrix を省略しないか  
**Original classification:** PASS ("Most failure paths covered; persistence constraint failure not
explicitly unit-tested for AppendContextEventAsync (non-fatal by design)")  
**Evidence file:** `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs`

**What IS tested:**
- AppendContextEventAsync ordering (lines 641-667): TESTED
- AppendContextEventAsync called on InsufficientHistory (lines 669-686): TESTED
- General policy/vector exception handling: TESTED

**What is NOT tested:**
- AppendContextEventAsync throws DbException (persistence constraint violation): NOT TESTED
- Verify that LogError is called and execution continues: NOT TESTED
- AppendContextEventAsync throws and result is still returned: NOT TESTED
- TVR extension throws and recommendation result is still returned: NOT TESTED

**Re-classification: GAP/TODO**

The original audit accepted "non-fatal by design" as justification for missing test coverage.
But per the Runtime Boundary Failure Matrix (AGENTS.md), "persistence constraint failure" is
matrix item 6 and must be verified. The "non-fatal by design" rationale is not a matrix
exemption — it is precisely the reason the test is needed (to verify non-fatal continuation
actually works).

The stub repositories in the test file (lines 756-819) do not simulate failure modes.
No test verifies that when AppendContextEventAsync throws, the recommendation result is
still returned correctly.

**Governance deficiency:** The original PASS classification accepted missing boundary failure
matrix coverage using "non-fatal by design" as an exemption without checking whether the
non-fatal path itself is exercised in tests.

---

## Governance Gaps

| ID | Finding | Original | Correct |
|----|---------|----------|---------|
| G1 | A1: LogError classified as explicit result surface | PASS | GAP |
| G2 | A2: Non-fatal boundary policy expressed in code comments only | PASS | GAP (conditional) |
| G3 | A4: "Requires migration" accepted as PASS condition | PASS | GAP |
| G4 | A11: Missing persistence constraint failure test accepted as PASS | PASS | GAP/TODO |

**Root cause for all gaps:** The previous audit treated conditional/caution/non-fatal
findings as unconditional PASS, violating the classification policy in `reports-and-todos.md`.

---

## Proposed Governance Improvements

1. **For G1 (A1):** Define an explicit policy surface distinguishing "logged failure" from
   "explicit result failure." For non-fatal log-only operations, add a documented
   `NonFatalOperationPolicy` annotation or equivalent, referenced in completion reports,
   so that future auditors can verify the intent vs. the `reports-and-todos.md` rule.

2. **For G2 (A2):** Create a recoverable-boundary policy document (e.g.,
   `.agent/protocols/recoverable-boundary.md`) that explicitly enumerates which operations
   are non-fatal, why, and what the acceptable failure behavior is. Code comments are not
   an auditable policy surface.

3. **For G3 (A4):** Record DB CHECK constraint limitations as explicit GAP items in TODO,
   not as PASS conditions. Document migration requirements and the policy variability impact
   in a design doc (e.g., `docs/design/scope-limit-policy.md`).

4. **For G4 (A11):** Add test cases for AppendContextEventAsync failure and TVR extension
   failure to verify that:
   - When AppendContextEventAsync throws, the recommendation result is still returned.
   - When TVR extension throws, the recommendation result is still returned.
   - LogError is called in both failure cases (observable via logger mock).

5. **For all gaps (meta):** Add an explicit re-audit step to the completion protocol:
   "conditional/caution/non-fatal findings must be re-classified before marking [x]."
   This prevents drift from evidence-backed PASS to caution-acceptance.

---

## Remaining TODOs

- [ ] A1 fix: Document or implement explicit result surface for AppendContextEventAsync failure.
      Current: LogError-only. Required: either return explicit result or add formal NonFatalPolicy annotation.
      → Target file: `backend/runtime/ContextRouteRecommendationResolver.cs` or new policy doc.

- [ ] A2 fix: Create recoverable-boundary policy document enumerating non-fatal operations.
      → Target file: `.agent/protocols/recoverable-boundary.md` (new) or `rule.md` extension.

- [ ] A4 fix: Record DB CHECK constraint as explicit infrastructure GAP and add migration TODO.
      → Target file: `docs/design/` (new design note), `.agent/tasks/todo.md`.

- [ ] A11 fix: Add persistence constraint failure and TVR extension failure test cases.
      → Target file: `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs`.

---

## Completion Eligibility

**Audit type:** This re-audit is a **Static Analysis / Code Evidence Audit** — findings are
based on reading source code (ContextRouteRecommendationResolver.cs, context_route_tables.sql,
ContextRouteRecommendationResolverTests.cs) and protocol documents, not observed runtime logs.

**Classification:** GAP — governance deficiency confirmed in A1/A2/A4/A11.

**Eligibility for this re-audit task (`[x]` update):**

The re-audit task is:
> "A1/A2/A4/A11 のような conditional / caution / non-fatal 判定を PASS 扱いせず、
> GAP/TODOとして扱えるか確認する。"

The purpose is to *confirm reclassification is possible and produce proper GAP/TODO records*.
This is a static audit (code reading + governance reclassification) and IS completion-eligible
from code evidence + this report.

The Remaining TODOs above (A1/A2/A4/A11 fixes) are **out-of-scope** for this re-audit task
and must be preserved as separate incomplete items in `.agent/tasks/todo.md`.

**This re-audit task itself:** completion-eligible after Recursive Verification Gate passes.
