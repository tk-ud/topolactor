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
