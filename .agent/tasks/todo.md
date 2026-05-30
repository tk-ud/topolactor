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

## Phase Attention exploration budget gate

roadmap ref: `design.db_manifest_ui_topology_meaning_split`

- [ ] `w` / `l2_norm` exploration budget gate を実装設計へ反映し、weak=near+narrow topK、mid=normal topK、high=expanded/farther distance band or permutation expansion に分岐する。
      → 理由: Phase Attention は full-space repeated search ではなく、topN physical heat / topK hub candidates / policy-defined expansion limits で bounded にする。canonical x/y/z axis migration は完了済み。この item は exploration budget branching の設計・実装タスク。

## Recommend target migration bundle

roadmap ref: `design.db_manifest_ui_topology_meaning_split`

- [ ] Recommend target を `topology.*` / `topology.wiring_physical_to_package` / `topology.components_*` / `context_*` learning surfaces に分離し、SQL Attention の hubs target と混同しないよう境界を設計する。
      → 理由: SQL Attention は hubs target、Recommend は topology/context target。境界を backend runtime に反映する。
- [ ] `context_*` public tables の `topology` schema 配置可否と recommendation 境界を判断し、配置先を確定する。
      → 理由: public `context_*` は正本配置ではない。

## Manifest main-path retirement bundle

roadmap ref: `design.db_manifest_ui_topology_meaning_split`

- [ ] `public.manifest` を compatibility-only に降格し、admin import flow を `topology.wiring_physical_to_package` / `hubs.topology_manifests` canonical tables へ移行する。
      → 理由: manifest はcompatibility surface として保持中（admin_import_snapshot FK 依存）。新規 wiring は topology.wiring_physical_to_package が正本。manifest の admin import 依存を canonical tables へ移行し、manifest を deprecate または view として残す。

## UI topology schema migration bundle

roadmap ref: `design.db_manifest_ui_topology_meaning_split`

- [ ] `ui_component_*` public tables と旧 `components` / `design` / `packages` を `topology.components_bucket` / `components_style_design` / `components_layout_design` / `components_package_design` 系へ移行する。
      → 理由: 旧 UI builder 系と public UI topology tables は canonical topology schema へ寄せる。DB namespace migration (topologys→topology, hubs.hubs→hubs.hub, seed 更新) は完了済み。この item は public UI builder tables の canonical topology schema 配置タスク。

## Hub relations legacy schema migration bundle

roadmap ref: `design.db_manifest_ui_topology_meaning_split`

- [ ] 既存 DB の legacy `hubs.hub_relations`（`hub_id` / `target_hub_id` / `relation_registry_id` グローバルグラフ形）から manifest-scoped 正本形（`topology_manifest_id` + `related_hub_id` + `sequence_position` + `relation_config` + `status`）への data-preserving migration DDL を設計・実装する。
      → 理由: PR #315 follow-up で bootstrap DDL は `CREATE TABLE IF NOT EXISTS` のみに修正済み（破壊的 DROP CASCADE 除去）。既存 DB 向け移行方針: 各 source `hub_id` に対応する `hubs.topology_manifests` を決定または作成し、`target_hub_id` → `related_hub_id`、`sequence_position` を導出する。自動移行ポリシーは SSOT 未確定のため本 PR では実装しない。SSOT: `docs/design/db-schema.yaml` compatibility_history.hub_relations_legacy_global_graph。
