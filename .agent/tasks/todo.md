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

## DB / Manifest Hub / UI Topology meaning split

- [ ] public/unqualified table namespace migration を設計し、`manifest` / `context_*` / `ui_component_*` などを `hubs` / `topology` / `logs` のいずれかへ責務移行する。
      → 理由: public table はSSOT正本にしない。
- [ ] `topologys` → `topology` schema rename migration を設計する。
      → 理由: `topologys` は naming drift であり、canonical schema は `topology`。
- [ ] `topologys.registrar_entries` を `topology.physical_tables` に移行し、`physical_table_id bigint` 正本と import / create / select flow を整合する。
      → 理由: physical table catalog は `topology.physical_tables` が正本。
- [ ] `hubs.hubs` を `hubs.hub` に移行し、`relation jsonb` join config と `id` / `relationKey` / `joinType` required validation を設計・実装する。
      → 理由: `hubs.hub` は topology meaning space / pseudo-RDB physical table group / join definition owner。
- [ ] `manifest` の責務を `topology.wiring_physical_to_package` と `hubs.topology_manifests` に整理・移行する。
      → 理由: 単一画面manifest wiring は topology 側、manifest群 grouping は hubs 側。
- [ ] `hubs.hub_relations` を fixed hub sequence / UI transition order / topology meaning space sequence table として再定義・移行する。
      → 理由: 現 `relation_registry_id` + `weight` は正本ではなく、weight は fixed sequence authority ではない。
- [ ] SQL Attention target を hubs 空間（`hubs.hub` / `hubs.hub_relations` / `hubs.topology_manifests`）のみに整理する。
      → 理由: `logs.current` / `logs.hub_current` / `logs.attention` は current/evidence persistence surfaces として維持し、新規 hub current table は作らない。
- [ ] `logs.diff` を physical table lifecycle mutation pressure source として維持し、`logs.current` および physical_table_id heat / l2_norm basis への接続を設計する。
      → 理由: physical heat の upstream signal source を落とさない。
- [ ] Recommend target を `topology.*` / `topology.wiring_physical_to_package` / `topology.components_*` / `context_*` learning surfaces に分離する。
      → 理由: SQL Attention の hubs target と Recommend の topology/context target を混同しない。
- [ ] Phase Attention の quaternion axis を `w=logs.current l2_norm/physical table heat`、`x=hubs.hub_relations`、`y=hubs.hub`、`z=hubs.topology_manifests`、`i/j/k=phase movement amount` としてSSOT・実装に反映する。
      → 理由: population_count / recordcount は観測値であり、canonical x/y/z axis ではない。
- [ ] `w` / `l2_norm` exploration budget gate を実装設計へ反映し、weak=near+narrow topK、mid=normal topK、high=expanded/farther distance band or permutation expansion に分岐する。
      → 理由: Phase Attention は full-space repeated search ではなく、topN physical heat / topK hub candidates / policy-defined expansion limits で bounded にする。
- [ ] `context_*` public tables の `topology` schema 配置可否と recommendation 境界を判断する。
      → 理由: public `context_*` は正本配置ではない。
- [ ] `ui_component_*` public tables と旧 `components` / `design` / `packages` を `topology.components_bucket` / `components_style_design` / `components_layout_design` / `components_package_design` 系へ移行する。
      → 理由: 旧UI builder系と public UI topology tables は canonical topology schema へ寄せる。
- [ ] `db/demo_seed.sql` の旧意味追従を修正し、`hubs` / `topology` / `logs` の3 schema構成に合わせる。
      → 理由: 現seedは `hubs.hubs`、`topologys`、public/unqualified table、legacy UI builder semantics を含む。
