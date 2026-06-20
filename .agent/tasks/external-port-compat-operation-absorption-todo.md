# External port compatibility operation absorption todo

Status: `partial`
Primary SSOT: `docs/design/abstract-function-primitive-registry-ssot.yaml`

Read `AGENTS.md` before implementation. Treat SSOT docs as canonical.

## Problem

The external port policy executor still owns behavior for legacy compatibility operation keys. The SSOT now defines the external/event behavior as AbstractFunction primitives, so the old keys must not remain the canonical execution authority.

## Purpose

Keep legacy keys as migration shims only. Runtime behavior should pass through `execute_abstract_function` and `AbstractFunctionExecutor`.

## Implementation plan

- [ ] Move remaining seed/policy rows from legacy compatibility keys to `execute_abstract_function`.
- [ ] Add abstract function manifests for the external/event primitive paths where missing.
- [ ] Change `ExternalPortPolicyStepExecutor` so legacy compatibility keys either delegate through an explicit manifest-backed key or fail closed.
- [ ] Add tests for both missing-delegate fail-close and successful delegate execution.
- [ ] Add a structure check so canonical seed paths do not reintroduce the legacy keys as authority.

## Target files

- `backend/runtime/ExternalPortCredentialRefresher.cs`
- `backend/runtime/AbstractFunctionRuntime.cs`
- `db/seed_empty.sql`
- `db/topology_tables.sql`
- `backend/tests/Topolactor.Runtime.Tests/*`
- `.agent/tests/check-abstract-function-completion-alignment.sh`

## NG axis

- legacy compatibility keys owning runtime behavior directly
- hidden fallback when the abstract-function delegate key is absent
- provider-specific or bundle-specific branching
- marking the migration complete without fail-close and delegate-path tests
