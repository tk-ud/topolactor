# Policy Judgment Gate

Policy Judgment Gate trigger:

- runtime/policy-affecting value introduction or modification
- recommendation/scoring/threshold/retention/routing/validation/promotion/disclosure/emission/projection behavior changes
- Registry/Manifest/function_parameters/structure_map/package/schema changes
- docs or summaries that assert runtime or policy behavior

Checklist is fixed at 15 questions. Q additions are prohibited.

Recursive Verification Gate requirement:

- Any Policy Judgment violation is a blocking failure; completion is not allowed.
- If violation is fixable in the same task scope, return to fix phase and rerun policy judgment.
- If not fixable in scope, keep task incomplete and record explicit remaining TODO.

Q12/Q13 meaning:

- answers must be based on full branch diff inspection
- when required, answers must include scenario contract verification
- when required, answers must include Runtime Boundary Failure Matrix verification
- missing required verification forces Q12=no and Q13=no
- any detected mismatch or missing verification in these surfaces triggers recursion to fix phase before completion

Delegated or split work does not inherit verification. Each agent making implementation/policy/completion decisions must verify independently.

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
