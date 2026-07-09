# Temporary Scenario Contract

This protocol is condition-triggered. It is not an always-on read.

## Route scope

`.agent/tmp/tmp.txt` (this protocol) is the **fallback-route** scenario contract, used only when `.agent/tools/agent-ui-initial-contract` is unavailable. When the tool is usable, `senario-tmp.md` (created exclusively by `agent-ui-initial-contract end`, per `docs/governance/agent-ui-protocol-ssot.yaml` `senario_tmp_contract.generation_authority`) is the tool-first scenario contract instead -- do not create `.agent/tmp/tmp.txt` as a second/duplicate contract when the tool-first route already produced `senario-tmp.md`.

## Trigger scope

Create and verify `.agent/tmp/tmp.txt` when changes include runtime claim, canonical route behavior, persistence behavior, or projection behavior, and the tool-first route is unavailable.

## Precondition: Workflow Order Invariant Gate

Before creating scenario contract:

- READ_TASK_MATERIALS completed.
- READ_TARGET_SURFACES completed.
- DEFINE_SCOPE completed.
- Reconfirm issue/prompt explicit materials.
- Perform docs/ SSOT reload from `.agent/docs/ssot-map.yaml` for changed surfaces.
- Record reloaded material names in scenario contract.

A scenario contract without docs/ SSOT reload is invalid.


## Substrate Contract

For changes that add or alter UI/action/runtime/admin surfaces, the scenario contract must declare before implementation:

- which surfaces are hardcoded runtime substrate
- which surfaces are seed-defined or data-defined
- which existing abstractions are reused
- which new reusable abstractions are introduced
- why any dedicated route/island/API/helper is necessary
- which SSOT grants the exception, if any

A scenario contract that omits this declaration for touched UI/action/runtime/admin surfaces is invalid. Dedicated route / island / frontend API / helper additions require explicit proof that existing seed/entity/projection/action, dispatch -> entity -> runtime, repository, audit, validation, or lifecycle substrate cannot express the behavior.

## Position in completion sequence

- Scenario Contract is created before implementation (intent fixation stage).
- Scenario Diff Verification is executed after implementation and checklist fill, and before final judgment.

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
