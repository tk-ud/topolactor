# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `preset_team_markdown_saved_view_seed` | Preset / Markdown saved-view unresolved work queue | not_started | 1 | `product.preset_db_seed_registration`, `product.component_markdown_authoring_projection`, `product.md_viewer_projection_component`, `product.completed_preset_seed_projection_gate` | `docs/design/team-markdown-dashboard-saved-view-ssot.yaml` |
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

**Status:** not_started
**Owner / target SSOT:** `docs/design/team-markdown-dashboard-saved-view-ssot.yaml`
**Parent SSOT:** `docs/design/admin-console-workflow-ssot.yaml` (`preset_ecosystem`)
**Supporting SSOT:** `docs/design/mock-preset-intake-compiler-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/runtime-orchestration-ssot.yaml`

Preset / Markdown / saved view 周辺の未処理作業 queue。TODO は実装 bundle queue なので DB migration、backend action、frontend authoring、projection component、test をまとめて保持するが、roadmap 上の product capability 境界とは同型にしない。roadmap では Preset DB seed / data-driven registration、component DB bind + Markdown save/edit UI、`md_viewer` hardcoded projection component、`completed_preset_seed_json` projection gate を別 capability として扱う。physical table / jsonb record が canonical data authority、rendered markdown saved view は projection、Markdown body は runtime SSOT ではない。`completed_preset_seed_json` は Preset DB seed registration そのものではなく、DB seed/template/binding/render 情報を保存済み view の refresh / rebind / clone / projection 時に検証する required gate。今回は TODO / roadmap 整理のみで、実装コード変更・DB migration 作成・backend runtime action 実装・frontend UI 実装は未着手。

- [ ] `preset_team_markdown_saved_view_seed` unresolved work queue を実装する
  - Preset DB seed / registration lane を完成させる（Preset catalog seed data、preset metadata、bootstrap / data-driven registration、`docs/design/mock-preset-intake-compiler-ssot.yaml` と `docs/design/db-schema.yaml` の registry / seed 実態整合。`md_viewer` hardcoded component 実装はこの lane に含めない）
  - Component DB bind + Markdown save/edit lane を完成させる（`topology.team_markdown_template_registry`, `topology.team_markdown_saved_view`, `topology.team_markdown_saved_view_event`, `card_metadata_json jsonb not null`, migration / CI schema setup、markdown template create/list/get/update/archive、saved view create/search/get/refresh/update/archive、frontend direct DB write 禁止）
  - markdown renderer / binding resolver を完成させる（Markdown template placeholder を explicit binding で解決、physical table column / jsonb path / saved query result field / static text 対応、AI inference 禁止、markdown body parsing による refresh / rebind 禁止、unresolved required placeholder は save blocking、optional placeholder は empty state 表示）
  - `completed_preset_seed_json` projection gate を完成させる（saved view 作成時に `completed_preset_seed_json jsonb not null` を必ず保存し、`seed_version`, `template_ref`, `source_ref`, `binding_ref`, `render_ref`, `adjustment_ref`, `dashboard_ref`, `lineage_ref`, `rendered_markdown_hash`, `card_metadata_json`, `search_index_basis_json` を含める。seed 欠損時は refresh / rebind / clone / projection を invalid 扱いし、seed validation failure は明示エラーにする。これは Preset DB seed registration ではなく saved-view projection/save/view 側の validation gate）
  - `md_viewer` hardcoded projection component lane を完成させる（saved markdown view search input、result cards、click expand drawer_or_panel、rendered Markdown viewer、binding summary、preset seed summary、source record ref、adjustment status、open source record / edit saved view adjustment / refresh from source record / clone saved view to another record / archive saved view / copy markdown / create follow-up todo candidate actions）
  - UIBuilder / Preset ecosystem integration を完成させる（UIBuilder `preset_ecosystem` から saved-view / `md_viewer` child surface を参照可能にし、preset load は selected route package tmp canvas draft へ bind、active topology へ直接保存しない、preview / validate / apply boundary を維持、`completed_preset_seed_json` gate 結果を UI に表示）
  - search behavior を完成させる（saved view title、rendered markdown、search_index_text、source_table_ref、tags、status を検索対象にし、default status active filter、result card click で drawer_or_panel 展開、search は saved view を mutate しない）
  - tests を完成させる（Preset seed / registration、DB migration / schema shape、backend template create/list/get/update/archive、backend saved view create/search/get/refresh/update/archive、`completed_preset_seed_json` required、incomplete seed blocks refresh/rebind/clone/projection、markdown renderer explicit binding without AI inference、refresh uses seed binding_json not markdown body parsing、user_adjustment_patch preserved during refresh、frontend search card / click expand、md viewer seed summary、preset load does not write active topology、structure / db-schema checks）

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending  
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する
