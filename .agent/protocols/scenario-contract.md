# Temporary Scenario Contract

This protocol is condition-triggered. It is not an always-on read.

## Trigger scope

Create and verify `.agent/tmp/tmp.txt` when changes include runtime claim, canonical route behavior, persistence behavior, or projection behavior.

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
10. Recursive Verification Gate notes

Before completion, verify full branch diff against this contract.

Failure or mismatch is blocking under Recursive Verification Gate.


## Boundary Extension Scenario

Boundary Extension Scenario must include Multi-instance leakage review and explicit identity checks, including Frontend projection identity and UI action identity.
