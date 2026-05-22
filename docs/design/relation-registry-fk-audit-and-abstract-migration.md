# Relation Registry FK Audit / Abstract Migration SSOT

## Purpose

This document defines the SSOT policy for relation_registry-driven FK audit and abstract migration.
The design scope is policy and wiring contracts.

## Authority and Policy Surfaces

- `relation_registry` is the authority for relation reference integrity and migration candidate generation.
- Manifest is an operational resolution surface on DB values (conditions, ordering, thresholds, scope), not a JSON file artifact.
- Runtime-variable parameters are resolved from `function_parameters.parameter_value` (JSONB).
- JSONB is a mutable parameter container and does not conflict with DB-driven runtime policy.

## Physical FK and Semantic FK Boundary

### Physical FK

Physical FK is a DB constraint chosen and applied only when approved through policy-driven migration flow.
Physical FK candidates are generated from audit/migration logic and are not auto-applied by reference existence alone.

### Semantic FK

Semantic FK means relation consistency defined by `relation_registry` authority even when physical FK is not present.
Semantic FK violations are explicit runtime validation results and must not be silently ignored.

## Required Audit Targets

The following references are the minimum required audit scope:

- `hubs.relation_registry_id`
- `entities.relation_ids`
- `hub_relations.relation_registry_id`
- `structure_maps.relation_registry_id`
- `relation_registry.master_ids`

## Periodic Audit Policy

Periodic audit flow:

1. C# Runtime selects `relation_registry` and referencing datasets.
2. Runtime detects:
   - dangling reference
   - inactive reference
   - missing relation
   - candidate mismatch
3. Runtime returns explicit status outputs.

Required explicit statuses include:

- `OK`
- `MissingPolicy`
- `MalformedPolicy`
- `ValidationFailed`
- `DanglingReference`
- `InactiveReference`
- `MissingRelation`
- `CandidateMismatch`

Silent fallback is prohibited.

## Abstract Migration Policy on Registry Addition

Flow when relation registry is added or changed:

1. Add/update/deprecate `relation_registry` record.
2. Resolve applicability conditions, target scope, thresholds, and ordering from Manifest + `function_parameters.parameter_value`.
3. Generate migration candidates:
   - FK candidate
   - index candidate
   - relation binding candidate
   - structure_map candidate
4. Run CI gate with C# package runtime validation runner.
5. Move passing candidates to admin approval.
6. Apply only approved candidates.

This order must stay consistent with existing promotion policy: CI gate before admin approval, admin approval before apply.

## Execution and Adapter Policy

- DB command strings must not be stored as executable runtime policy payload.
- C# package runtime validation runner is the primary execution subject.
- Shell script execution is optional and limited to allowlisted adapter usage.

## Error Contract Policy

The runtime/audit/migration contract must keep explicit error statuses and must not degrade into fallback defaults:

- missing policy => explicit status
- malformed policy => explicit status
- validation failed => explicit status

No silent fallback is allowed for relation reference integrity or migration decisioning.

## Runtime Boundary Contract

- relation_registry FK audit runtime must validate relation reference integrity.
- abstract migration runtime must generate candidate changes only through validated manifest/function-parameter policy.
- executable DB command strings must not be stored as runtime policy payload.
- all missing/malformed/failed validation states must return explicit statuses.
