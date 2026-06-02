# Agent Task List — PR336 workflow boundary hardening

## Blocking (resolved in branch — verify on merge)

- [x] Admin route drift corrected against `docs/design/runtime-orchestration-ssot.yaml`: canonical workflow is `/admin`, `/admin/contents`, `/admin/ui-builder`, `/admin/manifests`; retained import/direct hub-navigation/runtime wrappers are isolated under `/dev/admin/*`.
- [x] `/admin/contents` is presented as new manifest creation; `/admin/manifests` renders existing manifest relation / hub operations in-page.
- [x] Contents promote guard fails closed until validation has executed without blocking issues.

- [x] `TryProjectWiringAsync` uses `topology.physical_tables.table_ref` (SSOT); legacy `dbTableName` accepted at API boundary.
- [x] Hub grouping primary UI on `/admin/manifests`; contents shows readonly summary + promote gate.
- [x] `ManifestScreenOperationDeriver` uses manifest-scoped target/layer (list vs detail no longer share `admin/default/entity/Read`).

## Implementation gap (explicit — not blocking promote path)

- [ ] Contents UI: structured inputs for relation/join, aggregation viewing key, aggregation display columns.
      → SSOT: `admin-console-workflow-ssot.yaml` db_design; current: tableRef, columns, searchTargets, aggregationSpec string only.
- [ ] Backend: persist structured relation/join + aggregation display fields on `screen_data_shape` topology extension.
      → Depends on schema design in `docs/design/db-schema.yaml` + validator updates.
- [ ] Promote: explicit validation when `table_ref` not found in `topology.physical_tables` (currently skips wiring insert silently).
      → Prefer explicit skipped status in projection result vs silent no-op.

## Optional follow-up

- [ ] Decide whether retained `/dev/admin/import`, `/dev/admin/hub-navigation`, and `/dev/admin/runtime` legacy/debug wrappers can be deleted after migration/debug consumers are reviewed. [legacy-debug-isolation]

- [ ] `product.dynamic_support_nocode_loop` manual acceptance (unchanged from roadmap).
- [ ] Auto-refresh dispatcher axes on contents save when manifest_key already set on manifests page (partial: refresh on assign_screen_data_shape + assign_hub_grouping).

## Admin Console UX 改善（次フェーズ対象）

- [ ] ユーザー向け語彙の補完（ContentsScreenDesignPanel ほか）
  → `ContentsScreenDesignPanel.tsx`: "physical table ref（topology.physical_tables.table_ref）" を「参照テーブル名」等に置換
  → "import schema 名" を「取り込みデータ定義名」相当に
  → カラム定義の "nullable" を「空欄許可」に
  → `dispatcher: role/target/layer/action` raw 表示を advanced 開示ブロックに退避
  → ハブ割当エリアの `hub_id=…` / `manifest_key=…` raw 値をラベル変換表示に
  → `adminUxTerms.ts` に不足語彙を追加: `UX_TABLE_REF`（参照テーブル名）/ `UX_IMPORT_SCHEMA`（取り込みデータ定義名）/ `UX_NULLABLE`（空欄許可）
  → 回帰防止: `adminUxGuard.test.ts` の banned terms に追加
  [ux-vocabulary]
  SSOT: docs/design/admin-console-workflow-ssot.yaml (admin_contents.domain_ownership)

- [ ] コンテンツ制作フロー単純化（ContentsScreenDesignPanel / ContentsPromotionPanel）
  → `ContentsScreenDesignPanel.tsx` の「有効化（canonical 投影）」ボタンと `handlePromote` を除去し `ContentsPromotionPanel` に集約（重複排除）
  → ハブ未割当の案内を inline 通知のみにし、別ページへの強制リンク遷移案内を排除
  → 下書き作成・設計保存・有効化の 3 操作をステップ表示（作成 → 設計保存 → 有効化）で段階整理
  [ux-simplification]
  SSOT: docs/design/admin-console-workflow-ssot.yaml (admin_contents.content_bundle_lifecycle)

- [ ] DB カラム型指定を選択式に変更（ContentsScreenDesignPanel カラム定義 dataType）
  → `ContentsScreenDesignPanel.tsx` line 365–374: `<input type="text" placeholder="type">` を `<select>` に変更
  → 選択肢（候補）: text / integer / bigint / boolean / numeric / timestamp with time zone / date / jsonb / uuid / varchar
  → 定義先: `screenAuthoringIntent.ts` か `adminUxTerms.ts` に `DB_COLUMN_TYPE_OPTIONS` を追加
  → フリーテキスト入力は「その他（詳細設定）」開示ブロック内に残す
  [ux-select]
  SSOT: docs/design/admin-console-workflow-ssot.yaml (admin_contents.db_design)
