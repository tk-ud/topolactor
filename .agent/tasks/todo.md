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

- [ ] `registrar_entries` に `physical_table_id bigint` 正本を導入する migration / repository / seed 整合を設計・実装する。
      → 理由: `docs/design/db-schema.yaml` では physical table catalog の active path として固定したが、現SQLは `registrar_entry_id uuid` のまま。SQL破壊変更は次工程。
- [ ] `hubs.hubs` を Manifest Hub schema として移行し、`status` と one-screen physical table group semantics を DB / repository / seed に反映する。
      → 理由: 現SQLは `relation_registry_id` 中心で、Manifest Hub の active path と意味がずれている。
- [ ] `hubs.hubs.relation bigint[]` を追加し、unordered `physical_table_id` set として validate / authoring / seed を接続する。
      → 理由: `hubs.hubs.relation` は UI表示順・relation_registry_id・hub_relation_id・runtime weight ではないため、明示migrationが必要。
- [ ] `hubs.hub_relations` を削除するか、Manifest Hub membership とは別概念として再定義するか判断する。
      → 理由: 現 backend/frontend 接続があり、即削除不可。`weight` と logs current/attention の意味衝突も解消する。
- [ ] `topologys.entities` / `topologys.content_entity_drafts` の責務を content payload cache / draft staging として残すか削除・移行するか再定義する。
      → 理由: physical table records / Manifest Hub membership と混同しない責務境界が未確定。
- [ ] `topologys.structure_maps` を runtime resolution cache として残すか、`manifest` dispatch distribution に統合するか判断する。
      → 理由: attractor resolution と runtime manifest wiring の重複・衝突を解消する必要がある。
- [ ] `components` / `design` / `packages` の旧UI builder系を `ui_component_registry` / `ui_component_package` / `ui_package_component_map` / `ui_topology_tensor` へ移行するか削除するか判断する。
      → 理由: legacy/transition としてのみ保持し、正本扱いでは残さない。
- [ ] `manifest` の `ui_projection.packageIds` refs を `ui_component_package` に寄せるか `ui_topology_tensor` に寄せるか判断し、SQLコメント・backend validation・frontend manifest editor・tests を整合する。
      → 理由: 現コメントは legacy `packages.package_id` を指しており、active UI topology authority と衝突する。
- [ ] `logs.*.physical_table_id` を bigint に寄せるか text互換を残すか判断し、`registrar_entries.physical_table_id` migration と整合する。
      → 理由: Manifest Hub relation bigint[] と SQL Attention logs の physical table id 型を揃える必要がある。
- [ ] `db/demo_seed.sql` の旧意味追従を修正し、Manifest Hub / runtime manifest / UI topology / logs/context の分離に合わせる。
      → 理由: 現seedは `hubs.hubs.relation_registry_id`、legacy structure/entity/package semantics を含む。
