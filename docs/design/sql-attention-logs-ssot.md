# SQL Attention Logs SSOT

## Status

This document is the design SSOT for SQL Attention logs, pressure-current calculation, norm-triggered exploration, and attention evidence persistence.

This document does not define public marketing copy. Public articles may describe SQL Attention at a higher level, but implementation and audit decisions must follow this SSOT.

## Core definition

SQL Attention is not SQL-side Transformer QK dot-product reproduction.

SQL Attention is a DB-native runtime observation mechanism:

```text
logs.* signal sources
→ logs.current calculation basis
→ l2 norm level watch
→ scheduler/runtime registry-neighbor exploration
→ logs.attention evidence
```

The essential idea is to convert physical-table operation pressure into a bounded attention query and then search registry composition tables for nearby topology grammar.

## Three-layer logs model

### logs.* — signal sources

`logs.*` tables are aggregation sources.

They are not the final attention result and are not themselves registry grammar. They observe physical-side usage, mutation, candidates, or other runtime signals.

Initial source set:

```text
logs.diff         = physical table change pressure
logs.candidate    = column / jsonb_path / axis candidate pressure
logs.ui_operation = operation / component usage pressure
```

These names are the initial set, not the complete universe. Other signal-source combinations may use the same structure.

Required source semantics:

```text
source_name
source_kind
physical_table_id
axis_id / column_id / jsonb_path / operation_id / component_id, depending on source_kind
count_source
recordcount_source
created_at
archive_policy
retention_policy
```

### logs.current — calculation basis memo

`logs.current` is a calculation basis memo.

It stores compressed current pressure state derived from `logs.*`, so runtime does not repeatedly scan raw logs.

It is not the conclusion of SQL Attention.

```text
logs.*
→ count / recordcount / weight / recency aggregation
→ logs.current pressure matrix basis
```

`logs.current` exists to support:

```text
- pressure matrix construction
- l2 norm calculation
- top norm-level snapshot comparison
- exploration candidate creation
```

### logs.attention — neighbor hit / evidence log

`logs.attention` stores the results of registry-neighbor exploration.

If `logs.current` exists, `logs.attention` must be linked to it so that every attention hit has evidence back to the current basis that produced it.

```text
logs.current
→ registry-neighbor exploration
→ logs.attention
```

`logs.attention` is append-only / archive-required evidence, not an in-memory-only result.

## Physical table pressure is the aggregation target

The aggregation target is the physical table side.

`tableid` means physical table identity.

```text
logs.diff.tableid
logs.candidate.tableid
logs.ui_operation.tableid
```

These do not mean registry table popularity. Registry tables are composition grammar tables, not the raw usage aggregate target.

The current matrix observes user/system table construction tendency:

```text
physical table mutation pressure
physical column/candidate pressure
physical operation/component usage pressure
```

## Registry meaning

Registry tables are composition tables.

They are closer to grammar tables than vocabulary rows.

Core registry/composition examples:

```text
registrar_entries
master_registry
state_registry
relation_registry
package_registry
schema_registry
component_registry
function_parameters
structure_maps
hub_relations
```

UI topology composition examples:

```text
ui_component_bucket
ui_component_registry
ui_component_package
ui_package_component_map
ui_layout_registry
ui_wiring_registry
ui_topology_tensor
components
design
packages
```

SQL Attention applies physical-side pressure to these registry/composition tables through bounded neighbor exploration.

## Pressure matrix

`logs.current` stores a pressure matrix basis.

Rows are physical tables or physical-table combinations.
Columns are signal-source pressure axes.
Cells store count/recordcount-derived pressure values and optional weights/recency.

Example initial axes:

```text
              diff        candidate       ui_operation
physical T1   count/rows  count/rows      count/rows
physical T2   count/rows  count/rows      count/rows
physical T3   count/rows  count/rows      count/rows
```

The pressure values are observations. They do not need to be rounded into probabilities.

## Norm trigger

Registry-neighbor exploration is not executed on every log append.

Monitoring may run every time current is updated, but exploration only starts when norm level changes.

Initial policy:

```text
watch top 3 current norm-level records
if top 3 unchanged and norm delta is below threshold: return
if top 3 membership/order/level changes: mark exploration candidate
```

Flow:

```text
logs.* append
→ current basis update
→ l2 norm update
→ top3 norm-level snapshot compare
→ no level change: return
→ level change: enqueue/expose exploration candidate
→ scheduler/runtime registry-neighbor exploration
```

The trigger is norm-level change, not raw log arrival.

## Exploration stage

Exploration is a scheduler/runtime responsibility, not an every-write DB trigger responsibility.

The runtime explores registry/composition neighbors using the current pressure basis.

The runtime may alter vector order/permutation to find the strongest neighbor hit.

Bounded exploration controls must be manifest/policy-driven:

```text
current top level watch count
norm delta threshold
permutation limit
registry neighbor topK
attention evidence save limit
archive policy
```

## Completion boundary

Dangerous misunderstanding:

```text
logs aggregation completed = SQL Attention completed
```

Correct boundary:

```text
logs aggregation completed = attention-query basis is ready
registry-neighbor hit and evidence saved = attention observation completed
quaternion/topology expansion candidate created = topology-change candidate completed
```

## Existing repository mismatch notes

Existing tables are not automatically canonical logs.* unless their meaning is aligned with this SSOT.

Known caution points:

```text
topology_edit_log
- current description uses target_table as attractor_key/domain scope, not physical tableid
- therefore it is not automatically logs.diff unless physical table identity is added or clarified

promotion_candidates
- can be used as logs.candidate if adoption/rejection/pending state and column/axis pressure semantics are preserved

context_event
- close to logs.ui_operation, but retention should be manifest/policy-driven and component usage must be observed when relevant

context_hub_recommendation_current
- not the same as logs.current pressure basis
```

## Implementation order

1. Create this SSOT and matching YAML.
2. Wire SSOT into repository maps / required paths / structure checks.
3. Align README/internal design docs with this SSOT without changing public article intent.
4. Define schema/function/trigger contracts.
5. Implement logs.current basis update and norm-level monitoring.
6. Implement logs.attention evidence persistence.
7. Implement scheduler/runtime registry-neighbor exploration.

## One-sentence definition

SQL Attention converts physical-side `logs.*` signals into a `logs.current` calculation basis, watches l2 norm-level changes, and only when the level changes explores registry composition neighbors and records the hit/evidence into `logs.attention`.
