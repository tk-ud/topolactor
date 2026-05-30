# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。

完了済み作業・PR修正履歴・旧方針の残骸は残さない。
未完了の implementation / design / SSOT / test-authoring task がある場合のみ、次の形式で追加する。

CI検証待ち、remote CI pass確認、local tool不足、未実行チェックの記録はこのファイルに追加しない。
それらはPRサマリ/完了レポートの verification / Required Check Scope に記載する。


作業中に既存TODOへ一時的な in-progress 印を付ける場合は、チェックボックス（`[x]`）ではなく HTML comment marker を使う。
- marker: `<!-- agent:in-progress -->`
- 使い方: 対象TODOの**直下に単独行**で一時的に付与する（inline付与はしない）
- 完了条件: 作業完了前に必ず marker 単独行を削除する（残存は構造チェック失敗）

```md
## <Area>

- [ ] <具体的な未完了作業>
      → <理由・対象ファイル・次の判断点>
```

## Dynamic Support Nocode Loop — manual acceptance

- [ ] `product.dynamic_support_nocode_loop` の manual acceptance / hand-debug verification を実施し、authoring guidance・SQL Attention feedback・M6 self-hosted admin authoring loop が同一UX導線として受入可能か確認する。
      → 残理由は implementation gap ではない。M6 self-hosted admin authoring loop、SQL Attention SQLA-1..5、SQL Attention live DB E2E、roadmap/test-bundles 正規化は完了済みで、M6/M7 core runtime production-ready 判定は維持する。future optional external connector surfaces は M6/M7 blocker ではない。

## DB namespace migration bundle

roadmap ref: `design.db_manifest_ui_topology_meaning_split`

- [ ] `topologys` → `topology` schema rename migration を DB / SQL / backend / frontend 全体で設計・実行する。
      → 理由: `topologys` は naming drift。canonical schema は `topology`。migration は db/*.sql + C# repository 参照 + seed を同時に更新する必要あり。
- [ ] `topologys.registrar_entries` を `topology.physical_tables` に移行し、`physical_table_id bigint` 正本と import / create / select flow を整合する。
      → 理由: physical table catalog は `topology.physical_tables` が正本。
- [ ] `hubs.hubs` を `hubs.hub` に移行し、`relation jsonb` join config と `id` / `relationKey` / `joinType` required validation を設計・実装する。
      → 理由: `hubs.hub` は topology meaning space / pseudo-RDB physical table group / join definition owner。

## Manifest Hub / topology manifest split bundle

roadmap ref: `design.db_manifest_ui_topology_meaning_split`

- [ ] `manifest` (public unqualified) の責務を `topology.wiring_physical_to_package` と `hubs.topology_manifests` に整理・移行する。
      → 理由: 単一画面manifest wiring は topology 側、manifest群 grouping は hubs 側。
- [ ] `hubs.hub_relations` を fixed hub sequence / UI transition order / topology meaning space sequence table として再定義・移行する。
      → 理由: 現 `relation_registry_id` + `weight` は正本ではなく、weight は fixed sequence authority ではない。

## Phase Attention runtime migration bundle

roadmap ref: `design.db_manifest_ui_topology_meaning_split`

- [ ] Phase Attention runtime の x/y/z を `population_count` / `population_recordcount` / `axis_population_recordcount` から canonical hubs space axes（`x=hubs.hub_relations`, `y=hubs.hub`, `z=hubs.topology_manifests`）へ移行する。
      → 理由: SSOT canonical 定義は hubs space axes。現 DB SQL function `logs.generate_attention_phase_vector` および C# `BuildPhaseVectorJson` は population count を使用しており、canonical axis migration が必要。
- [ ] `w` / `l2_norm` exploration budget gate を実装設計へ反映し、weak=near+narrow topK、mid=normal topK、high=expanded/farther distance band or permutation expansion に分岐する。
      → 理由: Phase Attention は full-space repeated search ではなく、topN physical heat / topK hub candidates / policy-defined expansion limits で bounded にする。

## Recommend target migration bundle

roadmap ref: `design.db_manifest_ui_topology_meaning_split`

- [ ] Recommend target を `topology.*` / `topology.wiring_physical_to_package` / `topology.components_*` / `context_*` learning surfaces に分離し、SQL Attention の hubs target と混同しないよう境界を設計する。
      → 理由: SQL Attention は hubs target、Recommend は topology/context target。境界を backend runtime に反映する。
- [ ] `context_*` public tables の `topology` schema 配置可否と recommendation 境界を判断し、配置先を確定する。
      → 理由: public `context_*` は正本配置ではない。

## Seed / demo data migration bundle

roadmap ref: `design.db_manifest_ui_topology_meaning_split`

- [ ] `db/demo_seed.sql` を `hubs` / `topology` / `logs` の 3 schema 構成に合わせて修正する。
      → 理由: 現 seed は `hubs.hubs`、`topologys.*`、public/unqualified tables、legacy UI builder semantics を含む。DB namespace migration bundle 完了後に実施する。
- [ ] `ui_component_*` public tables と旧 `components` / `design` / `packages` を `topology.components_bucket` / `components_style_design` / `components_layout_design` / `components_package_design` 系へ移行する。
      → 理由: 旧 UI builder 系と public UI topology tables は canonical topology schema へ寄せる。
