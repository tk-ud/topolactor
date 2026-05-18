# Agent Contract

1. Preserve the canonical runtime route.
2. No silent fallback; broken refs and boundary failures are explicit results.
3. Runtime / persistence / projection changes require a temporary scenario contract (`.agent/tmp/tmp.txt`).
4. Before completion, verify the full branch diff against the scenario contract and Runtime Boundary Failure Matrix.
5. Run required local checks; run `bash .agent/tests/check-structure.sh` last.

Detailed rules and judgment criteria live under `.agent/rules/`.
Executable checks and expanded verification live under `.agent/scripts/`, `.agent/checklists/`, and `.agent/tests/`.
Persistent inspection reports live under `.agent/reports/`.

## Runtime Boundary Failure Matrix

For changes that add or wire endpoint / frontend API proxy / repository write / admin operation / persistence mutation / DB-backed registry operation, verify at least:

1. success path
2. authentication / authorization failure
3. request validation failure
4. malformed id / malformed payload
5. not found
6. persistence constraint failure
7. repository / backend unavailable
8. frontend proxy status propagation
9. UI-visible error state
10. post-write read consistency

If any matrix item is intentionally out of scope, state why in the completion report or PR summary.


Policy Judgment Gate details and execution are defined in `.agent/rules/rule.md` and `.agent/checklists/check-policy-judgment.sh`.
Temporary Scenario Contract details are defined under `.agent/rules/rule.md`.
