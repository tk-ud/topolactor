# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `mock-preset-intake-compiler` | Mock Preset Intake Compiler / UIBuilder Preset Registry | partial | 1 | `docs/design/mock-preset-intake-compiler-ssot.yaml` |
| `preset_team_markdown_saved_view_seed` | Preset ecosystem child saved Markdown view / md_viewer | not_started | 1 | `docs/design/team-markdown-dashboard-saved-view-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `ui-builder-default-route-navigation` | UI Builder ルート遷移デフォルト配線 | implemented | 1 | `docs/design/admin-console-workflow-ssot.yaml` |

---

## Bundle `future-external-bundle-gate`

**Status:** not_started
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion / Google Sheets / Slack / GitHub Issues / generic webhooks / external REST API connectors は、個別 SSOT と connector adapter contract が揃うまで optional external surface として実装しない（CSV/JSON admin import と M6 self-hosted no-code loop とは別 bundle）

---

## Bundle `helper-manual`

**Status:** not_started
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

SSOT 上、helper/manual category candidates は実装ではなく方針整理。site page / UI component / help screen component 実装は explicitly out of scope。

- [ ] helper/manual category candidates を user promise / safety boundary / onboarding policy として整理する（ページ・コンポーネント実装はしない）
- [ ] Desktop AI / CLI / MCP Reader 向けに、plain business language と approval boundary のライティング方針を整理する

---


## Bundle `mock-preset-intake-compiler`

**Status:** partial  
**SSOT:** `docs/design/mock-preset-intake-compiler-ssot.yaml`

External SVG/XML/Figma-like visual mock は runtime SSOT ではなく、AI inference なしで取り込む non-authoritative visual source snapshot。保存済み preset は reusable draft template であり、load 時は selected route package の tmp canvas draft に bind し、preview / validate / apply を経るまで active topology へ直接保存しない。

**実装済み（初期 + session 3 + chpt6 session）:**

- [x] `topology.mock_preset_*` tables migration DDL を作成する（registry / object_mapping / wiring_candidate / compile_snapshot）  
  → `db/migrations/mock_preset_registry_tables.sql` (CREATE TABLE IF NOT EXISTS / idempotent)  
  → bootstrap path: `db/init.sql`、CI: `backend-tests.yml` schema setup。DI 配線済み（Program.cs）。
- [x] backend admin runtime actions (create/list/get/compile/bind/save_mappings) を実装する  
  → `backend/runtime/AdminRuntime.MockPreset.cs` (partial class), `backend/schema/MockPresetContracts.cs`, `backend/repository/MockPresetRepository.cs`, `backend/repository/NpgsqlMockPresetRepository.cs`  
  → NpgsqlMockPresetRepository DI 配線済み。UpsertObjectMappingAsync / UpsertWiringCandidateAsync 有効。
- [x] UIBuilder preset uploader modal_or_drawer を実装する（SVG/XML/Figma-like visual mock intake、AI inference なし）  
  → `frontend/islands/PresetUploaderDrawer.tsx`, `frontend/runtime/visualMockParser.ts`  
  → mappingArray + wiringCandidates が save_mappings 経由で DB に保存済み。
- [x] UIBuilder saved preset load select を実装する  
  → `frontend/islands/UiBuilderAdmin.tsx` (loadPresetList / selectedPresetId select)
- [x] loaded preset bind to selected route package tmp canvas draft を実装し、active topology への直接保存を禁止する  
  → `frontend/islands/UiBuilderAdmin.tsx` (handleLoadPreset → bindMockPreset → applyCanvasFromTensorPatch)
- [x] preview / validate / apply boundary preservation を検証する  
  → bind は draftNodes (local state) のみ更新; canonical topology write は既存 `layout_patch:apply` ルート経由; `frontend/tests/mockPresetIntake.test.ts` bind result shape test
- [x] object mapping persistence を完結させる（mapping persistence closed）  
  → save_mappings action / UpsertObjectMappingAsync / UpsertWiringCandidateAsync / UNIQUE 制約追加
- [x] UIBuilder save current canvas as preset を実装する  
  → handleSaveCanvasAsPreset / CanvasPresetSeed / source_kind=ui_builder_canvas
- [x] capabilityTags gate の interactive wiring/binding UI panel を実装する  
  → WiringSelection state / emits_event / controlled_value / field_binding / confirmed checkbox
- [x] NpgsqlMockPresetRepository を本番 DI/startup に配線する  
  → backend/Program.cs に DI registration 追加
- [x] migration apply path を閉じる  
  → db/init.sql (bootstrap) + CI backend-tests.yml schema setup
- [x] E2E DB integration test (MockPresetLiveDbEndToEndTests 5 tests) — live DB 全パス  
  → create / save_mappings / wiring_candidates / compile-bind round-trip / list; bind が topology.ui_topology_tensor へ書かないことを検証済み
- [x] docs/design/db-schema.yaml: mock_preset テーブル migration_status を ddl_included_in_bootstrap_and_ci に更新

**実装済み（session 3 で完了）:**

- [x] object mapping persistence を完結させる（mapping persistence closed）
  - `PresetUploaderDrawer.tsx` の mappingArray + wiring candidates を `saveMockPresetMappings` で送信
  - `frontend/api/mockPresetApi.ts` に `saveMockPresetMappings` / `MockPresetObjectMapping` / `MockPresetWiringCandidate` 追加
  - `backend/runtime/AdminRuntime.MockPreset.cs` に `mock_preset:save_mappings` action を実装（mappings + wiringCandidates 両対応）
  - `NpgsqlMockPresetRepository.UpsertObjectMappingAsync` を override 実装
  - `NpgsqlMockPresetRepository.UpsertWiringCandidateAsync` を追加実装
  - `db/migrations/mock_preset_registry_tables.sql` に UNIQUE (preset_id, source_object_id) と UNIQUE (preset_id, source_object_id, capability_tag) 制約追加
- [x] UIBuilder save current canvas as preset を実装する
  - `handleSaveCanvasAsPreset` が canvas data を `CanvasPresetSeed` として export し、drawer に渡す
  - `source_kind=ui_builder_canvas`, layout_patch_json, componentMappings, visualTreeJson, sourceHash を構築
  - Drawer の canvas mode: pre-populated read-only mapping view で save
- [x] capabilityTags gate の interactive wiring/binding UI panel を実装する
  - `PresetUploaderDrawer.tsx` に `WiringSelection` state と `wiringSelections` Map
  - `emits_event` → route_navigation / runtime_dispatch 選択 UI
  - `controlled_value` → value_binding 選択 UI
  - `field_binding` → db_jsonb_field_binding 選択 UI
  - `requires_event_binding` → 配線未設定時に orange 警告 + 数カウント表示
  - 配線確定チェックボックス (confirmed / pending)
  - 保存時 wiringCandidates として save_mappings に送信
- [x] NpgsqlMockPresetRepository を本番 DI/startup に配線する
  - `backend/Program.cs` に `MockPresetRepository` DI registration 追加
  - `AdminRuntime` constructor 呼び出しに `sp.GetRequiredService<MockPresetRepository>()` 渡し

**完了（chpt6 session で完了）:**

- [x] migration apply path を閉じる: db/init.sql (bootstrap) に `\i /db/migrations/mock_preset_registry_tables.sql` を追加。CI backend-tests.yml の schema setup ステップにも追加。既存 DB は手動適用。
- [x] E2E DB integration test: `MockPresetLiveDbEndToEndTests` (5 tests: create / save_mappings / wiring_candidates / compile-bind round-trip / list) — live DB で全パス。
- [x] E2E object mapping round-trip test: SaveMappings_UpsertsObjectMappingRows, SaveWiringCandidates_UpsertsWiringCandidateRows, CompileAndBind_RoundTrip 各テストで確認済み。bind が topology.ui_topology_tensor へ書かないことも検証済み。
- [x] docs/design/db-schema.yaml: mock_preset テーブル status を `migration_ddl_available_not_production_applied` → `migration_ddl_available_bootstrap_included` に更新。migration_status も `ddl_available_not_applied` → `ddl_included_in_bootstrap_and_ci` に更新。

**未達（外部 production 環境検証待ち）:**

- [ ] docs/design/db-schema.yaml: 別途 external production deployment 環境での migration 適用確認後、canonical tables list に昇格する。現状は `migration_ddl_available_bootstrap_included` のまま。

## Bundle `preset_team_markdown_saved_view_seed`

**Status:** not_started
**Owner / target SSOT:** `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`
**Parent SSOT:** `docs/design/admin-console-workflow-ssot.yaml` (`preset_ecosystem`)
**Supporting SSOT:** `docs/design/mock-preset-intake-compiler-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/runtime-orchestration-ssot.yaml`

Preset ecosystem / preset seed / saved view 配下の child surface として、team shared saved Markdown view / `md_viewer` を実装する completion bundle。独立巨大 feature / Notion clone ではない。physical table / jsonb record が canonical data authority、rendered markdown saved view は projection、Markdown body は runtime SSOT ではない。`completed_preset_seed_json` は UX hard gate であり、optional ではない。今回は TODO / roadmap 整理のみで、実装コード変更・DB migration 作成・backend runtime action 実装・frontend UI 実装は未着手。

- [ ] DB migration / schema を完成させる（`topology.team_markdown_template_registry`, `topology.team_markdown_saved_view`, `topology.team_markdown_saved_view_event`, `completed_preset_seed_json jsonb not null`, `card_metadata_json jsonb not null`, migration / bootstrap / CI schema setup 経路、`docs/design/db-schema.yaml` と migration 実態の整合）
- [ ] backend runtime actions を完成させる（markdown template create / list / get / update / archive、saved view create / search / get / refresh / update / archive、clone saved view to another record、create follow-up todo candidate は planned item として保持可能、explicit error / no silent fallback、frontend direct DB write 禁止）
- [ ] markdown renderer / binding resolver を完成させる（Markdown template placeholder を explicit binding で解決、physical table column / jsonb path / saved query result field / static text 対応、AI inference 禁止、markdown body parsing による refresh / rebind 禁止、unresolved required placeholder は save blocking、optional placeholder は empty state 表示）
- [ ] completed preset seed builder / validator を完成させる（saved view 作成時に `completed_preset_seed_json` を必ず保存し、`seed_version`, `template_ref`, `source_ref`, `binding_ref`, `render_ref`, `adjustment_ref`, `dashboard_ref`, `lineage_ref`, `rendered_markdown_hash`, `card_metadata_json`, `search_index_basis_json` を含める。seed 欠損時は refresh / rebind / clone を invalid 扱いし、seed validation failure は明示エラーにする）
- [ ] frontend team dashboard / md viewer surface を完成させる（saved markdown view search input、result cards、click expand drawer_or_panel、rendered markdown viewer、binding summary、preset seed summary、source record ref、adjustment status、open source record / edit saved view adjustment / refresh from source record / clone saved view to another record / archive saved view / copy markdown / create follow-up todo candidate actions）
- [ ] UIBuilder / Preset ecosystem integration を完成させる（UIBuilder `preset_ecosystem` から `md_viewer` child surface を参照可能にし、preset load は selected route package tmp canvas draft へ bind、active topology へ直接保存しない、preview / validate / apply boundary を維持、`completed_preset_seed_json` を `md_viewer` の hard gate として UI に表示）
- [ ] search behavior を完成させる（saved view title、rendered markdown、search_index_text、source_table_ref、tags、status を検索対象にし、default status active filter、result card click で drawer_or_panel 展開、search は saved view を mutate しない）
- [ ] tests を完成させる（DB migration / schema shape、backend template create/list/get/update/archive、backend saved view create/search/get/refresh/update/archive、`completed_preset_seed_json` required、incomplete seed blocks refresh/rebind/clone、markdown renderer explicit binding without AI inference、refresh uses seed binding_json not markdown body parsing、user_adjustment_patch preserved during refresh、frontend search card / click expand、md viewer seed summary、preset load does not write active topology、structure / db-schema checks）

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する

---

## Bundle `ui-builder-default-route-navigation`

**Status:** implemented  
**SSOT:** `docs/design/admin-console-workflow-ssot.yaml`

/admin/ui-builder の component-level wiring に、通常導線で「指定されたルートへ飛ぶ」デフォルト配線を追加する。raw dispatcher fields は normal-view に出さず、既存の package wiring / target_ref / route_key / manifest wiring と衝突しない保存形式にする。

- [x] UI Builder で、クリック可能コンポーネントに route navigation のデフォルト配線を設定・保存・再読込・投影できるようにする（SSOT / roadmap / tests も同一 bundle で更新）
  - RouteNavigationWiringPreset を PackageDesignPanel 通常導線に追加
  - encodeRouteNavigationTargetRef / parseRouteNavigationTargetRef / isRouteNavigationTargetRef を packageWiringPicker.ts に追加
  - target_ref: "route:<routeKey>" 形式で保存（manifest:... と衝突しない）
  - raw dispatcher fields は <details> PackageWiringEditor のみ（通常導線に出さない）
  - SSOT (admin-console-workflow-ssot.yaml) / roadmap / tests 同一 bundle で更新済み
- [x] 投影 runtime での route navigation 実行を frontend-local lane として実装（runtime 閉鎖）
  - isNavigationWiringKind / buildRouteNavigationEventBinding を renderEmission.ts に追加
  - buildRuntimeDispatchSpec: navigation wiringKind → null（backend dispatch しない）
  - runtimeComponentFactory.ts emitBoundEvent: routeNavigation binding → globalThis.location.href
  - ManifestDispatcherTargetRefTests: route: prefix → TARGET_REF_INVALID 防衛テスト追加
  - pipeline-continuity-ssot.yaml: navigation_wiring_execution_contract 追加
  - admin-console-workflow-ssot.yaml: route_navigation.runtime_execution 追加
  - roadmap known_gap_ref から runtime navigation gap を削除
