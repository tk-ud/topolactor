# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `preset_team_markdown_saved_view_seed` | Preset / Markdown saved-view unresolved work queue | partial | 1 | `product.preset_db_seed_registration`, `product.component_markdown_authoring_projection`, `product.md_viewer_projection_component`, `product.completed_preset_seed_projection_gate` | `docs/design/team-markdown-dashboard-saved-view-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

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

## Bundle `preset_team_markdown_saved_view_seed`

**Status:** partial
**Owner / target SSOT:** `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`
**Parent SSOT:** `docs/design/admin-console-workflow-ssot.yaml` (`preset_ecosystem`)
**Supporting SSOT:** `docs/design/mock-preset-intake-compiler-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/runtime-orchestration-ssot.yaml`

実装済み:
- DB migration `db/migrations/team_markdown_registry_tables.sql` (3 tables: template_registry, saved_view, saved_view_event)
- `db/init.sql` にマイグレーション追加
- `backend/schema/TeamMarkdownContracts.cs` (全 request/response 型 + `CompletedPresetSeedValidator`)
- `backend/repository/TeamMarkdownRepository.cs` + `NpgsqlTeamMarkdownRepository.cs`
- `backend/runtime/AdminRuntime.TeamMarkdown.cs` (template create/list/get/update/archive, saved_view create/search/get/refresh/update/archive)
- `AdminRuntime.cs` に `_teamMarkdownRepository` フィールド・コンストラクタ追加、dispatch に team_markdown レイヤー追加
- `Program.cs` に `NpgsqlTeamMarkdownRepository` DI 登録追加
- `frontend/api/teamMarkdownApi.ts` (全 API 関数)
- `frontend/components/MdViewer.tsx` (hardcoded projection component)
- `frontend/islands/TeamMarkdownDashboard.tsx` (search input, result cards, click expand drawer)
- `backend/tests/Topolactor.Runtime.Tests/TeamMarkdownSavedViewTests.cs`
- `frontend/tests/teamMarkdownSavedView.test.ts`
- `check-bootstrap-validation.sh` に team_markdown テーブル検証追加
- `docs/design/db-schema.yaml` に migration_ddl_available エントリ更新
- `docs/system-roadmap.yaml` の 4 capability バンドルを partial に更新

残タスク (carry-over):
- [ ] template_registration_modal_or_drawer UI (frontend) — MarkdownTemplateRegistryForm
- [ ] record_markdown_bind_form UI (frontend) — RecordMarkdownBindForm (placeholder to field binding UI)
- [ ] seed_builder helper for client-side construction
- [ ] clone saved view backend action implementation
- [ ] UIBuilder preset_ecosystem md_viewer child surface wiring
- [ ] full_drawer_placement_via_UIBuilder_canvas

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
