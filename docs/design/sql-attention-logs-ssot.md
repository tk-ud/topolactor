# SQL Attention Logs SSOT

## Status

This document is the design SSOT for SQL Attention logs, pressure-current calculation, norm-triggered exploration, and attention evidence persistence.

Physical schema implementation status:

- Abstract contract names: `logs.current` and `logs.attention` (contract-level names).
- Implemented physical tables (hub-attractor boundary): `logs.current`, `logs.hub_current`, and `logs.attention` (schema/index/constraint surface).
- Not implemented: refresh function bodies, norm watch function bodies, DB triggers, scheduler/runtime hub-attractor exploration, phase_vector generation runtime logic.



Registry-aware naming boundary:

- `logs.current` / `logs.hub_current` / `logs.attention` are physical implementation tables in this phase.
- `logs.current` is the physical log-pressure current.
- `logs.hub_current` is the hub Tensor/attractor current.
- `logs.attention` is the registry exploration evidence plane linked to physical current.
- table-specific names `logs.table_current` / `logs.table_attention` are not adopted.




Neighbor exploration policy range (initial policy contract):

- neighbor_score_min: 0.85
- neighbor_score_max: 1.00
- strong_hit_threshold: 0.95
- normal_hit_threshold: 0.90
- exploratory_hit_threshold: 0.85

Score band contract:

- 1.00〜0.95 = strong / near-isomorphic hit
- 0.95〜0.90 = normal neighbor hit
- 0.90〜0.85 = exploratory / phase candidate hit
- below 0.85 = initial reject or evidence-only

Exploration budget policy keys (policy-resolved):

- max_hub_kinds_per_current
- max_hub_tables_per_kind
- topK_per_hub_kind
- phase_expansion_limit
- max_attention_rows_saved

Policy values above are contracts and must be resolved from manifest/function_parameters/policy table, not hardcoded literals in SQL/runtime implementation.

PostgreSQL namespace alignment:

- SQL Attention logs are implemented under PostgreSQL `logs` schema (`logs.current`, `logs.hub_current`, `logs.attention`).
- This namespace boundary separates statistics/observation evidence from `hubs` and `topologys` meaning layers.

This document does not define public marketing copy. Public articles may describe SQL Attention at a higher level, but implementation and audit decisions must follow this SSOT.

## Core definition

SQL Attention is not SQL-side Transformer QK dot-product reproduction.

SQL Attention is a DB-native runtime observation mechanism:

- SQL Attention is not topology search.
- SQL Attention is not registry search.
- SQL Attention target is hubs.* Tensor / attractor.

```text
logs.* signal sources
→ logs.current calculation basis
→ l2 norm level watch
→ scheduler/runtime hub-attractor exploration
→ logs.attention evidence
```

The essential idea is to convert physical-table operation pressure into a bounded attention query and then search hubs.* Tensor/attractor neighbors for projection-ready hub hits.

## Philosophy and structure

SQL Attention keeps three meanings separate.

```text
statistics      = convergence confidence / stability / continuity
Attention       = current excitation / neighbor hit strength
Phase Attention = exploratory variance / shifted candidate vector
```

These must not be collapsed into one score at the evidence layer.

```text
statistics
- count
- recordcount
- EMA
- trend
- feedback

Attention
- l2_norm
- neighbor_score
- vector_json

Phase Attention
- phase_vector_json
```

Meaning:

```text
statistics      = whether the candidate is stable or trustworthy over time
Attention       = what is strongly excited now
Phase Attention = which shifted direction may produce useful unexplored candidates
```

EMA and other statistical values are retained as the stable-confidence layer. They can be used later by policy/adoption logic, but they should not overwrite `vector_json` or `phase_vector_json`.

The evidence shape should allow later visualization such as:

```text
statistics trend
vs
attention norm trend
vs
phase vector trend
```

This keeps the duality visible:

```text
vector_json       = convergent neighbor hit
phase_vector_json = divergent/exploratory candidate direction
```

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

### logs.hub_current — registry-side population/phase basis current

`logs.hub_current` stores registry-side population current used to calculate phase basis, z-score normalization, and movement distance guardrails.

It is a projection/cache current and is not an adopted topology state, and it does not mutate registries.

### logs.attention — neighbor hit / phase evidence log

`logs.attention` stores the result of hub-attractor exploration and its phase-shifted candidate vector.

`logs.attention` must carry both `current_id` (physical current reference) and `hub_current_id` (hub-attractor plane reference).

If `logs.current` exists, `logs.attention` must be linked to it so that every attention hit has evidence back to the current basis that produced it.

```text
logs.current
→ hub-attractor exploration
→ logs.attention(vector, phase_vector)
```

`logs.attention` is append-only / archive-required evidence, not an in-memory-only result.

The minimal mental model is:

```text
id
l2_norm
vector
phase_vector
```

Meaning:

```text
vector       = hub-attractor neighbor hit vector
phase_vector = vector distorted by Phase Attention across i/table, j/column, k/ui axes
```

Keeping `phase_vector` on `logs.attention` makes later phase-candidate aggregation and norm-trend visualization straightforward.

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
→ scheduler/runtime hub-attractor exploration
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

## Phase Attention draft

Phase Attention is part of the `logs.attention` abstract evidence contract shape, not a separate candidate table by default.

When hub-attractor exploration produces a hit, Phase Attention may distort the hit vector into a phase vector.

```text
neighbor hit vector
→ l2_norm strength
→ i/table, j/column, k/ui phase distortion
→ phase_vector stored on logs.attention
```

Draft basis:

```text
i = table direction
j = column / jsonb_path / axis direction
k = UI / component operation direction
```

For table registry phase basis (initial physical implementation):

```text
i = table
j = column / jsonb_path / axis
k = UI / component operation
```

For other registry kinds (relation/component/state), i/j/k basis must be defined according to each registry grammar before physical implementation.

### relation registry N×N calculation rule (contract)

In relation registry exploration, source_axis and target_axis array lengths may differ.

For N×N calculation, the shorter side is adjusted by **calculation-local zero padding** only.

Contract rules:

- zero padding is calculation-time only (calculation-local), not persistent schema
- padded elements are not real registry candidates
- padded elements must be excluded from:
  - population_count
  - z-score denominator
  - phase candidate output
- `phase_vector_json` stores real candidate components only
- optional metadata such as `padding_applied` / `padded_size` may be stored only as calculation metadata in `evidence_json`


Draft behavior:

```text
stronger l2_norm allows farther movement from the attention vector.
movement distance is corrected by normalized table-registry i/j/k population, such as z-score.
```

This is an experimental over-optimization guard: a strong attention hit does not only choose the nearest known candidate. It can also create a deliberately shifted candidate vector for later review and aggregation.

Draft calculation shape:

```text
base_vector = logs.attention.vector_json
axis_z_score = z_score(table_registry_i_j_k_population)
move_distance = f(l2_norm, axis_z_score, policy_caps)
phase_vector = distort(base_vector, i_table, j_column, k_ui, move_distance)
```

Guardrails:

```text
- phase_vector is evidence/candidate data, not adopted topology state
- no direct registry mutation
- no automatic migration execution
- no automatic column promotion
- no unbounded phase movement
```

## Completion boundary

Dangerous misunderstanding:

```text
logs aggregation completed = SQL Attention completed
```

Correct boundary:

```text
logs aggregation completed = attention-query basis is ready
hub-attractor hit and evidence saved = attention observation completed
phase_vector generated on logs.attention = phase candidate visible for later review/aggregation
adoption/migration = separate implementation path
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


## Schema contract (design-only)

This section defines DB schema contracts only.
It does not implement functions, triggers, scheduler jobs, or runtime exploration execution.

### logs source contract

`logs.*` is a signal-source family, not a single table.
Each source table must publish explicit pressure semantics and source kind.

Abstract source schema contract:

```text
source_id
source_name
source_kind
physical_table_id
physical_table_name
source_axis_kind
source_axis_ref
count_value
recordcount_value
weight_value
recency_window
observed_at
archive_policy
retention_policy
evidence_json
```

Initial minimal signal basis:

```text
table pressure                 = logs.diff
column / axis pressure         = logs.candidate
ui / component operation pressure = logs.ui_operation
```

Extensibility condition:

```text
new logs.* source is allowed only when:
- source_kind is explicitly defined
- pressure semantics are explicitly defined
- physical_table_id identity semantics are preserved
```

### logs.current contract

Role contract:

```text
- calculation basis memo
- pressure matrix basis
- l2 norm calculation basis
- topN norm-level snapshot comparison basis
```

Suggested schema fields:

```text
current_id
source_set_id
basis_window
physical_table_id
physical_table_name
basis_vector_json
pressure_matrix_json
count_total
recordcount_total
l2_norm
norm_rank
norm_level
previous_norm_level
level_changed
evaluated_at
updated_at
```

Persistence semantics:

```text
logs.current is a regenerable projection/cache.
it is not final attention evidence and not archive-required append log.
```

### norm-level watch contract

Watch policy contract (initial):

```text
watch target = top3 norm-level records
watch timing = every logs.current refresh
if no level/membership/order/delta change: return
if changed: mark as exploration candidate
```

Trigger conditions:

```text
- top3 membership changed
- top3 order changed
- norm delta exceeds policy threshold
- norm level changed
```

Policy-source contract:

```text
threshold/watch values are policy-resolved
(from Manifest / function_parameters / policy table in future)
fixed literals are not embedded in contract implementation.
```

### logs.attention contract

Role contract:

```text
hub-attractor exploration hit and phase-vector evidence log
```

Suggested schema fields:

```text
attention_id
current_id
source_set_id
statistics_json
ema_score
l2_norm
vector_json
phase_vector_json
permutation_key
hub_id
attractor_key
hub_relation_id
relation_registry_id
neighbor_score
hit_rank
evidence_json
created_at
archive_policy
```

Persistence semantics:

```text
logs.attention is append-only and archive-required evidence.
each attention row must reference logs.current.current_id.
statistics_json / ema_score store the stable-confidence layer.
vector_json stores the convergent neighbor hit vector.
phase_vector_json stores the exploratory phase-shifted candidate vector.
```

### existing table alignment contract

Existing tables remain non-canonical unless this contract is satisfied:

```text
topology_edit_log
- classification: mismatch by default
- reason: target_table semantics are not guaranteed physical_table_id
- alignment requirement: explicit physical table identity contract

promotion_candidates
- classification: conditional logs.candidate
- requirement: candidate pressure axis semantics + lifecycle retention

context_event
- classification: conditional logs.ui_operation
- requirement: operation/component usage semantics + policy-driven retention

context_hub_recommendation_current
- classification: mismatch
- reason: recommendation projection current != logs.current calculation basis
```

### non-goals

- no function implementation
- no DB trigger implementation
- no scheduler/runtime exploration implementation
- no destructive changes to existing tables
- no large-scale SQL replacement
- no automatic registry mutation from phase_vector
- no automatic migration or column promotion from phase_vector
- no evidence-layer collapse into a single score


## Function / trigger contract (design-only)

This section defines contract boundaries only.
No DB migration, DB function body, trigger body, scheduler code, or runtime implementation is included in this phase.

### Source append / source aggregation contract

`logs.diff` / `logs.candidate` / `logs.ui_operation` append events are source facts.
Aggregation contract is:

```text
source append
→ source aggregation window resolve
→ refresh_logs_current(source_set_id, basis_window)
```

Contract rule:

```text
trigger can observe every append, but no-change paths must return early.
```

### DB trigger boundary contract

DB trigger responsibility is intentionally lightweight.

Allowed in DB trigger scope:

```text
- call current refresh contract surface
- update logs.current lightweight basis rows/projection
- evaluate l2_norm / norm-level snapshot contract hooks
- mark dirty flag / level_changed
- mark exploration candidate metadata
- return without side effects when no change
```

Prohibited in DB trigger scope:

```text
- hub-attractor exploration execution
- phase_vector distortion/generation execution
- direct registry mutation
- automatic migration execution
- automatic column promotion execution
```

### Function contract surfaces

Concrete names may change at implementation time, but responsibility boundaries are fixed by this SSOT.

#### 1) `refresh_logs_current(source_set_id, basis_window)`

Role:

```text
- aggregate logs.* into logs.current calculation basis
- refresh basis_vector_json / pressure_matrix_json
- compute/update count_total / recordcount_total
- preserve regenerable projection semantics
```

#### 2) `calculate_l2_norm(current_id or basis_vector)`

Role:

```text
- compute l2_norm from current basis vector
- persist/return l2_norm for watch comparison
- no registry exploration side effect
```

#### 3) `compare_norm_level_snapshot(top_n)`

Role:

```text
- compare topN membership/order/level and norm delta
- no-change => return
- changed => level_changed/dirty signal
```

Initial policy:

```text
top_n default = 3 (policy-resolved, not hardcoded literal in implementation)
```

#### 4) `mark_attention_exploration_candidate(current_id)`

Role:

```text
- mark candidate for scheduler/runtime exploration queue surface
- candidate marking only (no exploration execution)
```

#### 5) `write_logs_attention(...)`

Role:

```text
- append evidence row to logs.attention
- require current_id linkage to logs.current
- persist statistics_json / ema_score / l2_norm / vector_json / phase_vector_json / neighbor_score / evidence_json
- keep evidence meanings separated (no single-score collapse)
```

#### 6) `generate_phase_vector(...)`

Boundary:

```text
- scheduler/runtime-side function contract
- not executed inside every-write DB trigger
- output is candidate evidence only
```

### No-change return contract

No-change short-circuit is mandatory:

```text
if topN membership/order/level/delta has no effective change:
  return
```

Monitoring may run every refresh; heavy exploration work must not run without change.

### Exploration candidate marking contract

When norm-level change is detected:

```text
mark exploration candidate
→ scheduler/runtime consumes candidate
→ hub-attractor exploration executed outside DB trigger
```

### Attention evidence write contract

`logs.attention` is evidence log, not adoption state.

Required evidence payload contract:

```text
statistics_json
ema_score
l2_norm
vector_json
phase_vector_json
neighbor_score
evidence_json
```

### statistics / Attention / Phase Attention separation contract

Separation is mandatory at function contract level:

```text
statistics_json / ema_score
= convergence confidence / stability / continuity

l2_norm / vector_json / neighbor_score
= current excitation / neighbor hit

phase_vector_json
= exploratory variance / phase candidate direction
```

These are not merged into one scalar score in this layer.

### Phase Attention function contract

Input contract:

```text
- logs.attention.vector_json
- logs.attention.l2_norm
- table registry i/j/k population
- z-score normalized values
- policy caps (manifest/function_parameters/policy table resolved)
```

Output contract:

```text
- logs.attention.phase_vector_json
```

Guardrails:

```text
- phase_vector is candidate/evidence, not adopted state
- phase_vector does not auto-trigger registry mutation
- phase_vector does not auto-trigger migration
- phase_vector does not auto-trigger column promotion
```

## Implementation order

1. Create this SSOT and matching YAML.
2. Wire SSOT into repository maps / required paths / structure checks.
3. Align README/internal design docs with this SSOT without changing public article intent.
4. Define schema/function/trigger contracts.
5. Implement logs.current basis update and norm-level monitoring.
6. Implement logs.attention evidence persistence with statistics, vector, and phase_vector.
7. Implement scheduler/runtime hub-attractor exploration.
8. Implement Phase Attention vector distortion only after policy caps and evidence linkage are fixed.

## One-sentence definition

SQL Attention converts physical-side `logs.*` signals into a `logs.current` calculation basis, watches l2 norm-level changes, and only when the level changes explores registry composition neighbors and records statistics, `l2_norm`, `vector`, `phase_vector`, and hit evidence into `logs.attention` without collapsing statistics, Attention, and Phase Attention into one score.

## Target boundary clarification

- SQL Attention explores hubs.* Tensor/attractor from logs pressure.
- topologys.* is not SQL Attention direct search target; it is projected meaning space attached to hit hub/attractor.
- topology-side recommendation is based on statistics/EMA/history/usage trend only.


## Support-surface note

Registry/topology references are projection/support surfaces, not SQL Attention target surfaces.
