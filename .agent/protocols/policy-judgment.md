# Policy Judgment Gate

This protocol is condition-triggered. It is not an always-on read.

## Workflow Guard

- Use this protocol only in FILL_CHECKLISTS or JUDGMENT stages.
- Do not use this protocol to bypass earlier workflow steps.

## Trigger scope

Run this gate when changes include any of:

- runtime/policy-affecting value introduction or modification
- recommendation/scoring/threshold/retention/routing/validation/promotion/disclosure/emission/projection behavior changes
- Registry/Manifest/function_parameters/structure_map/package/schema changes
- docs or summaries that assert runtime or policy behavior

If none apply, declare NOT_REQUIRED/OUT_OF_SCOPE with rationale in completion-facing reporting.

Checklist is fixed at 15 questions. Q additions are prohibited.

Any Policy Judgment violation is blocking under Recursive Verification Gate.

Violation table:

- V1: Q5=yes
- V2: Q6=yes
- V3: Q2=yes and Q3=no
- V4: Q1=yes and Q4=no
- V5: Q7=yes
- V6: Q8=yes
- V7: Q9=yes
- V8: Q10=no
- V9: Q11=no
- V10: Q12=no
- V11: Q14=no
- V12: Q13=no
- V13: Q15=no
- V14: answer not in {yes, no, n/a}
- V15: missing answer
- V16: answer count is not 15


CI queued/in_progress is not PASS.
scope-irrelevant workflow-level skip is not blocking.
