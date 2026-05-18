# Temporary Scenario Contract

`.agent/tmp/tmp.txt` is a temporary scenario contract, not a free-form memo.

Create with:

- `bash .agent/scripts/create-tmp.sh`

Delete with:

- `bash .agent/scripts/delete-tmp.sh`

Required fields:

1. user-visible scenario or runtime claim
2. entry operation / request shape
3. expected canonical runtime route
4. expected read / write / append / cache / return order
5. seed / fixture / policy data
6. expected emission / projection / status
7. required side effects including failure paths
8. Runtime Boundary Failure Matrix coverage and intentional out-of-scope reasons
9. known non-goals
10. Recursive Verification Gate notes (blocking failures found / fix recursion performed / remaining out-of-scope TODO)

Before completion, verify full branch diff against this contract.

Recursive verification requirements:

- Scenario-contract verification failure is blocking; completion is not allowed.
- If tmp contract and actual diff conflict, either:
  - fix implementation and re-verify, or
  - when the contract was wrong, update contract with explicit reason and re-verify.
- Complete recursive verification before deleting tmp.txt.

Rules:

- tmp is commit-prohibited
- tmp remaining must fail structure check

## Boundary Extension Scenario

When creating the Scenario Contract, agents must explicitly answer before implementation:

- Which existing boundary identity / state / policy / projection / UI action this change extends
- Whether boundary multi-instance operation could cause cross-boundary leakage in DB / Backend / API / Frontend projection / UI action
- What the minimum leakage-detection scenario is

Required verification items:

- existing boundary being extended
- DB primary key / unique key / FK / CHECK identity
- contract / event / DTO identity
- API request / response identity
- repository INSERT / UPSERT conflict identity
- repository UPDATE / DELETE WHERE identity
- Frontend projection identity
  - list key
  - cache key
  - selected item key
  - form state key
- UI action identity
  - button click
  - feedback
  - update
  - delete
- Multi-instance leakage scenario where omitted identity columns would cause cross-boundary leakage
- explanation for any intentionally omitted identity field

