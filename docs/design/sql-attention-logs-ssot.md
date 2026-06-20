# SQL Attention Logs SSOT

## 1. Role

This markdown is the semantic SSOT for SQL Attention and Phase Attention evidence generation.

- Focus: observed signals, recommendation targets, exploration fields, evidence lineage, and mutation boundaries.
- Structural contracts, policy-source rules, schema responsibilities, runtime boundaries, and staged wiring order are defined in `docs/design/sql-attention-logs-ssot.yaml`.

## 2. Recommendation pressure lane boundary

SQL Attention owns the **concept projection recommendation** lane (`sql_attention_projection`): which hub / projection / topology to examine next across hubs and manifest-scoped relation space.

Hub-local next-candidate recommendation (`ui_pressure`, `state_pressure`) is defined only in `context-route-recommendation.yaml`. SQL Attention must not inject projection hits into hub-local `next_operation` / `next_enum_item` style results.

Canonical cross-reference: `recommendation_pressure_lane_boundary` in `sql-attention-logs-ssot.yaml` and `hub_local_recommendation_pressure_lanes` in `context-route-recommendation.yaml`.

## 3. SQL Attention Definition

SQL Attention observes physical-table pressure and recommends hub-relation evidence. It is not SQL-side Transformer QK dot-product reproduction and it is not an automatic topology authoring route.

```text
logs.diff
→ logs.current
→ topN rank / norm level change detection
→ physical_table_id / physical_table_name
→ related topology_manifest_id[] resolution
→ hubs.hub_relations exploration
→ hub_relation_id / topology_manifest_id / hub_id hit evidence
→ logs.attention append-only evidence
```

The canonical SQL Attention trigger is a policy-qualified change in the `logs.current` topN surface: topN membership, rank order, norm delta, or norm level. `logs.diff` is the physical mutation-pressure source; `logs.current` is the rebuildable calculation basis.

The canonical exploration field is `hubs.hub_relations`. SQL Attention resolves the `topology_manifest_id[]` related to each triggered physical table through explicit active `topology.physical_table_manifest_bindings`, explores `hubs.hub_relations` from those manifest IDs, derives each hit's `hub_id` through `hubs.topology_manifests`, and appends hit evidence to `logs.attention`. Resolution must return an explicit no-hit / failure boundary rather than using an implicit join, nullable fallback, or oldest-row fallback.

## 4. Hubs Space Hierarchy

```text
hubs.hub
  └─ hubs.topology_manifests
       └─ hubs.hub_relations
```

- `hubs.hub` owns topology meaning space and join definition (`relation` JSONB).
- `hubs.topology_manifests` belongs to one `hubs.hub` and groups hub-side manifest sets.
- `hubs.hub_relations` belongs to one topology manifest, forms a manifest-scoped relation sequence, and is the SQL Attention exploration field.
- A relation hit resolves its source hub through `hub_relation_id -> topology_manifest_id -> hubs.topology_manifests.hub_id`; `hubs.hub_relations` is not a global hub-to-hub graph.

## 5. Observation and Support Planes

The planes remain semantically separate.

- `logs.diff` = append-only physical mutation-pressure source.
- `logs.current` = rebuildable physical-pressure basis and topN / rank / norm-level trigger surface.
- `hubs.hub_relations` = canonical SQL Attention exploration field.
- `logs.hub_current` = optional support cache / derived hub current. It may accelerate reads, but it is not the SQL Attention exploration body or meaning authority.
- `logs.attention` = append-only generation log for SQL Attention and Phase Attention evidence.

## 6. SQL Attention Hit Evidence

A SQL Attention hit records the generation lineage from the triggered physical-table pressure to the explored hub-relation field.

```text
physical_table_id / physical_table_name
→ topology_manifest_id[]
→ hubs.hub_relations
→ hub_relation_id / topology_manifest_id / hub_id
→ logs.attention SQLAT evidence row
```

A SQL Attention evidence row preserves enough identity to trace the trigger current, resolved manifest, explored relation, derived hub, policy source, rank / score observations, and generation lineage. Evidence rows are append-only observations. They are not canonical topology objects.

## 7. Phase Attention Definition

Phase Attention begins from SQL Attention hit evidence and explores farther through ID spaces. Its canonical axes are IDs, not count scalars.

```text
w = l2_norm

x = hub_relation_id hit by SQL Attention near-neighbor exploration
y = topology_manifest_id present on x
z = hub_id registered by y

i = hub_relation_id[] found by farther exploration from the x ID vector, bounded by w
j = i plus topology_manifest_id[] found by farther exploration from the y ID vector
k = j plus hub_id[] found by farther exploration from the z ID vector

q = logs.attention phaseAT evidence row storing the generated ID-space exploration evidence
```

The expression `q = w + xi + yj + zk` names the Phase Attention generation relation. It does **not** mean that `q` is a Draft, an adopted canonical object, or a count-scalar tuple. `q` is the append-only `phaseAT` evidence row stored in `logs.attention`.

`w` controls the bounded exploration strength. All threshold values, topN values, rank / norm policies, topK values, and expansion limits are resolved from data-defined policy sources rather than hidden literals.

## 8. Deprecated Count-Scalar Interpretation

The following interpretation is legacy / deprecated and is not canonical Phase Attention semantics:

```text
x = hub_relations_count
y = hub_count
z = topology_manifests_count
```

Counts, record counts, aggregates, and cache populations may remain statistics or support-cache observations. They must not be treated as the canonical `x / y / z` Phase Attention ID spaces.

## 9. Evidence Generation Line and Promotion Boundary

`logs.attention` is an append-only generation log.

```text
logs.current trigger
→ SQLAT hit evidence row
→ phaseAT evidence row
→ explicit Draft promotion command or explicit user operation
→ explicit adopted operation
→ canonical topology / manifest / hub_relation reflection
```

Boundary rules:

- A `phaseAT` row is evidence only. It is not a Draft canonical object.
- Draft creation or promotion requires an explicit user operation or explicit command.
- Adopted reflection requires an explicit adopted operation.
- SQL Attention and Phase Attention never auto-mutate topology, registry, manifests, or hub relations.
- Evidence generation must preserve its lineage across SQLAT hit rows, phaseAT rows, and any later explicit Draft / adopted operation.

## 10. Evidence Meaning Separation

Do not collapse the evidence layers into one score.

```text
statistics      = convergence confidence / stability / continuity
SQL Attention   = current excitation / hub-relation hit strength
Phase Attention = farther ID-space exploration evidence
```

Statistics, SQL Attention hits, and Phase Attention generations preserve different meanings even when they share one append-only log surface.

## 11. Parent / Child Boundary

SQL Attention and topology recommendation are related but not identical.

- SQL Attention = parent observation and recommendation-evidence model over physical-table pressure and the `hubs.hub_relations` field.
- Topology recommendation currents = child projection surfaces that consume evidence for discrete ranking.
- `topology.*` and registry surfaces remain canonical configuration / meaning surfaces; SQL Attention does not author them automatically.

## 12. Write / Mutation Boundary

Allowed SQL Attention and Phase Attention writes:

- Refresh rebuildable `logs.current` projection state.
- Optionally refresh rebuildable `logs.hub_current` support-cache state.
- Append SQLAT and phaseAT evidence generations to `logs.attention`.

Prohibited automatic writes:

- topology mutation
- registry mutation
- manifest mutation
- hub-relation mutation
- migration execution
- Draft creation or promotion
- adopted reflection

## 13. Policy Boundary

Policy values are data-defined. Runtime and SQL layers resolve policy values from explicit sources such as `topology.function_parameters`, a manifest policy binding, or a policy table.

This rule applies to:

- watched topN size
- rank-change and norm-level trigger thresholds
- norm delta thresholds
- neighbor score thresholds
- near / far exploration mode
- topK selection
- expansion limits
- evidence save limits
- archive policy

## 14. Staged Wiring Order

The contract wiring order is:

1. Refresh the SQL Attention / Phase Attention SSOT semantics.
2. Wire SSOT vocabulary and checks to the ID-space and append-only generation-line definitions.
3. Align schema and persistence contracts for SQLAT / phaseAT lineage and resolved `topology_manifest_id` evidence.
4. Align SQL functions so `logs.current` changes resolve manifest IDs and explore `hubs.hub_relations`; retain `logs.hub_current` only as a support cache when useful.
5. Align backend exploration and repository writes with ID-space Phase Attention evidence.
6. Align explicit Draft promotion and adopted reflection commands without introducing automatic mutation.

## 15. Non-goals

- Reproducing Transformer QK Attention in SQL.
- Treating `logs.hub_current` as the canonical exploration body.
- Treating count scalars as canonical Phase Attention axes.
- Treating `q` as a Draft or adopted topology object.
- Auto-mutating topology, registry, manifests, or hub relations from evidence.

## 16. Compatibility Vocabulary

The canonical route above retains the following structural vocabulary for cross-document continuity:

- `l2 norm` is the human-readable spelling of `l2_norm`.
- `physical table` means the `physical_table_id / physical_table_name` subject observed through `logs.current`.
- `norm-level` detection is part of the policy-qualified topN trigger.
- `neighbor_score_min` and `phase_expansion_limit` are data-defined policy keys; they are not hidden literals.
- `phase_vector_json` may carry serialized phaseAT evidence payload, but the canonical meaning is the append-only phaseAT evidence row and its generation lineage.
- SQL Attention is not topology search.
- SQL Attention is not registry search.
- The legacy phrase `hub-attractor exploration` must be interpreted as manifest-scoped `hubs.hub_relations` exploration, not as `logs.hub_current` exploration authority.
- Hubs surfaces remain vectorizable Tensor coordinates for observation and recommendation evidence.
- `attractor_key` may remain support identity in evidence or cache projections; it does not replace `hub_relation_id / topology_manifest_id / hub_id` hit identity.

## SQL Attention recommendation child projection lane

SQL Attention projection recommendation is rendered as the `sql_attention_projection` section of the `recommend_navigation_child_island` under `main_projection_island`. It is a sibling lane to hub-local recommendation pressure, not an input to hub-local `next_operation`, `next_enum_item`, or `state_pressure` arrays.

The backend resolves the source context (`sourceSetId`) and exposes either a fetchable/renderable SQL Attention projection spec or an explicit unavailable/error status. The frontend must not supply a fixed sourceSetId fallback, infer topology promotion, or convert SQL Attention candidates into UI operation or enum candidates. Executable actions, if later attached to a recommendation candidate, must use backend-resolved runtime dispatch wiring rather than frontend-derived target/layer/action.

## SQL Attention manifest topology key expansion draft lane

This lane consumes SQL Attention evidence and remains a SQL-only evidence consumer. It does not change the SQL Attention exploration field: `hubs.hub_relations` is used only as the near-neighbor key discovery space.

```text
logs.attention SQLAT evidence
→ SQL-Attention-explored hub relation neighborhood
→ high-pressure discrete Key extraction
→ full registered manifest topology search by Key
→ hit-set common-axis / column-pressure aggregation
→ draft candidate JSONB insert
→ Markdown projection insert
→ DB NOTIFY / SSE projection signal
```

The hub-relation neighborhood is the **Key extraction space**, not the candidate completion space. High-pressure discrete Keys discovered there are expanded across the full registered manifest topology space: topology manifest JSONB, screen data shape JSONB, logical tables, logical columns, enum group or discrete-value metadata, and physical-table manifest bindings when available.

Discrete Keys may be enum values, statuses, categories, types, kinds, states, booleans, or other low-cardinality values that can slice a candidate search set. Candidate compilation searches the Key hit-set for same-name axes, same-type axes, same-name-and-same-type common axes, enum group matches, value overlap when available, `logs.diff` pressure, `logs.attention` pressure, table-ref reuse, and manifest reuse.

The canonical output authority is a `source=sql_attention` draft candidate JSONB record. Markdown is only a human-readable projection for discovery and review, preferably through the Markdown viewer / team dashboard saved-view surface. Markdown must not become runtime authority, topology promotion authority, event wiring authority, or the only saved state.

Draft output is insert-only at this lane. A draft payload must preserve source evidence refs, candidate lane, high-pressure Key, hit manifest refs, hit table refs, common-axis candidates, candidate columns, score, and draft status. Raw count alone is insufficient scoring; routine high-frequency values need lift or pressure delta, ID columns are axis or dimension candidates rather than primary display text, and generic columns should be dampened.

The Markdown projection body may be generated in SQL, but it must not contain raw HTML, island markup, CSS class authority, executable scripts, or promotion instructions as authority. The UI decides display surface and placement. DB notification on insert should use structured payload fields such as `event_type=sql_attention_draft_candidate_created`, `source=sql_attention`, `candidate_id`, `candidate_lane`, and `markdown_projection_id`.

Prohibited shortcuts:

- completing the candidate inside the hub-relation neighborhood only
- full-space schema mining without SQL Attention evidence
- requiring C# candidate inference
- mutating active manifests, topology registry, hub relations, or runtime routes
- auto-apply or auto-promote
- treating Markdown as runtime SSOT
- generating HTML, island markup, or UI placement from SQL
