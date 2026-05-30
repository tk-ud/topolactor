# SQL Attention Logs SSOT

## 1. Role

This markdown is the semantic SSOT for SQL Attention logs, including l2 norm observation meaning, norm-level route meaning, and physical table pressure meaning.

- Focus: formulas, meanings, routes, boundaries.
- Structural contracts (policy/schema/function/trigger/implementation order) are managed in `docs/design/sql-attention-logs-ssot.yaml` (including keys such as neighbor_score_min and phase_expansion_limit).

## 2. Core Definition

SQL Attention is a DB-native hub-attractor observation model.

- SQL Attention is **not** SQL-side Transformer QK dot-product reproduction.
- SQL Attention target is `hubs.*` Tensor / attractor (for example attractor_key-aligned hub-side semantics).
- `topology.*` and registry surfaces are projection/support layers, not the SQL Attention target itself.
- SQL Attention is not topology search.
- SQL Attention is not registry search.

## 3. Observation Planes

SQL Attention observation uses three distinct planes:

- `logs.current` = physical pressure current.
- `logs.hub_current` = hub / attractor current.
- `logs.attention` = evidence plane.

These planes must remain semantically separate.

## 4. Main Attention Route

Main Attention is the primary SQL Attention exploration route and represents hub-attractor exploration.

```text
logs.current × logs.hub_current
→ hub-attractor neighbor search
→ neighbor_score / hit_rank / vector_json
→ logs.attention
```

This is the core search body of SQL Attention.

## 5. Phase Attention Route

Phase Attention is a post-main auxiliary route.

```text
logs.attention.vector_json
→ q = w + xi + yj + zk
→ phase_vector_json
```

- Main Attention is the primary exploration.
- Phase Attention is downstream auxiliary transformation.
- Phase Attention must not be treated as SQL Attention primary exploration.

## 6. Parent / Child Boundary

SQL Attention and topology recommendation are parent/child related, not identical.

- SQL Attention = parent observation model over hub-attractor field movement/expansion pressure.
- Topology recommendation currents = child projection surface for discrete candidate ranking.

The child projection must not be treated as SQL Attention itself.

## 7. Evidence Meaning Separation

Do not collapse the three evidence meanings into a single score.

```text
statistics      = convergence confidence / stability / continuity
Attention       = current excitation / neighbor hit strength
Phase Attention = exploratory variance / shifted candidate direction
```

`statistics`, `Attention`, and `Phase Attention` each preserve different evidence meaning.

## 8. Hubs Space Hierarchy

```text
hubs.hub
  └─ hubs.topology_manifests
       └─ hubs.hub_relations
```

- `hubs.hub` owns topology meaning space and join definition (`relation` JSONB).
- `hubs.topology_manifests` belongs to one `hubs.hub` and groups hub-side manifest sets.
- `hubs.hub_relations` belongs to one topology manifest; source hub is derived through
  `topology_manifest_id -> hubs.topology_manifests.hub_id`, not through `hub_relations.hub_id`.

## 9. Quaternion / Attractor Semantics

Phase semantics follow:

- `q = w + xi + yj + zk`
- `q = attractor`
- `w` = l2 norm / physical table heat / physical_table_id excitation strength from logs.current.
- `x` = hubs.hub_relations — manifest-scoped hub sequence / UI transition axis; not a global hub-to-hub relation graph.
- `y` = hubs.hub — topology meaning space axis.
- `z` = hubs.topology_manifests — manifest grouping axis.
- `i / j / k` = phase movement amount / movement vector over hubs space.

`phase_vector` is evidence/candidate data and is not automatic mutation.

`population_count` and `recordcount` are not canonical Phase Attention axes. The canonical axes are the hubs space references defined above.

## 9.1 Exploration budget gate (w / l2_norm)

Phase Attention exploration is bounded by `w` (`l2_norm` / physical table heat from `logs.current`), not full-space repeated search.

| Tier | Norm level / w | Search mode |
|------|----------------|-------------|
| weak | `low` or below `norm_level_medium` | near-neighbor + narrow topK |
| mid  | `medium` or between thresholds | normal topK |
| high | `high` or at/above `norm_level_high` | expanded distance band or permutation expansion |

Policy-defined limits live in `topology.function_parameters` for `sql_attention_hub_attractor_exploration` / `default_policy` (`exploration_budget_tiers`, `norm_level_high`, `norm_level_medium`, `max_hub_kinds_per_current`, `max_attention_rows_saved`). Phase Attention does not auto-mutate topology or registry.

## 10. Glossary

- `logs.diff`:
  - physical mutation pressure source on time axis.
- `logs.current`:
  - current basis / recalculable pressure projection.
  - not the SQL Attention conclusion itself.
- `logs.hub_current`:
  - hub-side attractor current / exploration target cache.
  - not topology mutation surface.
- `logs.attention`:
  - append-only evidence row surface.
  - statistics / attention / phase-attention are not collapsed.
- `write boundary`:
  - boundary that appends evidence rows to `logs.attention`.
- `production evidence filling`:
  - writes measured values such as `l2_norm`, `vector_json`, `evidence_json`, `neighbor_score`.
- `phase_vector`:
  - auxiliary evidence transform derived from main vector evidence.
- `topology projection recommendation`:
  - consumer projection surface derived from evidence; not SQL Attention body.

## 11. Write/Mutation Boundary

- `logs.attention` is append-only evidence storage.
- Refresh and watch boundaries are current-basis and level-detection semantics.
- Topology/registry mutation is outside SQL Attention evidence writing route.

## 12. Target Boundary

- Primary target: `hubs.*` Tensor / attractor semantics.
- Not primary target: direct `topology.*` / registry search as SQL Attention body.
- `topology.*` and registry are projection/support layers consuming evidence.

## 13. Non-goals

- Reproducing Transformer QK Attention in SQL.
- Treating Phase Attention as primary exploration.
- Collapsing statistics + Attention + Phase Attention into one score.
- Auto-mutating registry/topology state from `phase_vector` evidence.


## SQL Attention Recommendation Boundary (clarified)

SQL Attention は単なる推薦UXラベルではなく、`hub / hub construction / hub relation / attractor current / logs evidence` を根拠として、次の hub 候補・接続候補・導線候補・projection 候補を育てる推薦挙動である。

境界条件:
- `topology_recommendation_current` は SQL Attention 本体ではなく child projection / consumer。
- SQL Attention は fixed route を自動上書きしない。
- SQL Attention は `registry` / `topology` を phase_vector や attention current から自動 mutation しない。
- runtime/backend の canonical dispatch / explicit failure / boundary guard は維持される。
