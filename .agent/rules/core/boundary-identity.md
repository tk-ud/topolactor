# Core Rule: Boundary Identity

Read this rule when a change extends or modifies an endpoint, frontend API proxy, repository write, admin operation, persistence mutation, DB-backed registry operation, append log, current table, frontend projection, or UI action.

## End-to-End Boundary Identity

When a change extends an existing boundary with a new event, write path, append log, current table, repository mutation, API payload, frontend projection, or UI action, agents must verify End-to-End Boundary Identity.

The verification must compare:

- DB identity
- backend contract identity
- API payload identity
- repository mutation identity
- frontend projection identity
- UI action identity

Any omitted identity field must be explicitly justified. Multi-instance leakage scenarios must be checked before completion.

Use `.agent/protocols/runtime-boundary-matrix.md` and `.agent/checklists/boundary-identity.md` when this rule is triggered.
