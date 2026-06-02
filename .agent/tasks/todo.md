# Agent Task List — admin canonical no-code workflow convergence

## Blocking (resolved in branch — verify on merge)

- [x] Admin route drift corrected against `docs/design/runtime-orchestration-ssot.yaml`: Fresh `/admin/*` registry contains only `/admin`, `/admin/contents`, `/admin/ui-builder`, `/admin/manifests`; legacy/debug/helper `/dev/admin/*` wrappers are deleted.
- [x] `/admin/contents` is limited to single-page manifest creation; `/admin/manifests` owns created manifest hub membership, inter-manifest relations, navigation ordering, and page-group continuity.
- [x] Contents promote guard fails closed until validation has executed without blocking issues.
- [x] `TryProjectWiringAsync` uses `topology.physical_tables.table_ref` (SSOT); legacy `dbTableName` accepted at API boundary.
- [x] Hub membership, manifest relation, and navigation ordering UI is owned by `/admin/manifests`; contents has no draft hub-assignment gate.
- [x] `ManifestScreenOperationDeriver` uses manifest-scoped target/layer (list vs detail no longer share `admin/default/entity/Read`).

## Implementation gap — `frontend.admin_routes` completion bundle

Roadmap entry: `frontend.admin_routes`. Detailed workflow authority: `docs/design/admin-console-workflow-ssot.yaml`.
These are implementation gaps after SSOT clarification; this documentation-only change does not implement them.

### `/admin/contents` authoring wizard

- [x] Reflect the explicit contents wizard steps in UI (front half): empty draft creation → DB reference → columns → steps ④⑤ explicitly marked as not-yet-implemented/next-step. Full 8-step display (initial data → optional relation/join intent → search key → aggregation/display group → validate/preview/register → /admin/ui-builder handoff) remains in later bundles. [authoring-wizard-front]
- [x] Replace normal-view free-text DB column type input with select UI. Candidates: text / integer / bigint / boolean / numeric / timestamp with time zone / date / jsonb / uuid / varchar. Free text isolated under その他（詳細入力）. Existing `dataType` persistence format preserved. [authoring-wizard-front]
- [x] Add initial-data topology-intent authoring flow with validate → sample preview → explicit manifest promote/register; do not add silent/direct DB writes. [admin-routes-completion] Initial-data candidates are stored in the screen_data_shape extension; actual business-row insertion is explicitly the separate content_bundle validated draft → preview → explicit promote route and is not a frontend.admin_routes completion condition.
- [x] Add structured relation/join input for a draft's data-shape intent without moving created-manifest hub membership, inter-manifest relations, or navigation ordering out of `/admin/manifests`. [admin-routes-completion]
- [x] Add user-facing search-key selection for `searchTargets`. [admin-routes-completion] searchKeyColumns multi-select from defined columns; raw searchTargets in advanced disclosure.
- [x] Add aggregation-key and display-group selection with mandatory sample viewing / preview. Do not expose `group by` as primary UX vocabulary. [admin-routes-completion] aggregationKey select + displayColumns multi-select + SamplePreviewPanel.

### Backend persistence and explicit validation

- [x] Persist structured relation/join and aggregation display fields on the `screen_data_shape` topology extension. [admin-routes-completion] searchKeyColumns/aggregationKey/displayColumns/relationIntents/initialDataRows added to topology extension JSON and backend contracts.
- [x] Fail explicitly without partial canonical writes when `table_ref` is not found in `topology.physical_tables`; current wiring projection skip must not remain a silent no-op. [admin-routes-completion] ProjectOnPromoteAsync preflights before canonical upsert and returns WIRING_TABLE_REF_NOT_FOUND; live-DB regression asserts no hubs.topology_manifests or topology.wiring_physical_to_package residue.

### `/admin/ui-builder` projection authoring

- [x] Add catalog-based component placement on layout canvas / preview with keyboard or button alternatives to drag and drop. [closed-prior — UiBuilderAdmin already implemented]
- [x] Add selectable CSS / Tailwind / design-token layout settings with visual or before/after preview; isolate advanced raw input. [closed-prior — UiBuilderAdmin AdvancedManualOverride pattern]
- [x] Add component-level wiring selection from DB / manifest / topology registry references; move raw dispatcher `role / target / layer / action` fields to advanced disclosure. [closed-prior — AdvancedManualOverride in UiBuilderAdmin]
- [x] Preserve validate → preview → explicit apply and prohibit direct DB writes / silent fallback. [closed-prior — callLayoutPatch route]
- [x] Add post-apply handoff to CI / local guard / agent-governance checks for generated-artifact drift, registry drift, and SSOT consistency auditing. [closed-prior — CI tab in UiBuilderAdmin]

### User-facing vocabulary and flow cleanup

- [x] Replace internal normal-view terms in `ContentsScreenDesignPanel.tsx`: `physical table ref` → 「参照テーブル名」, `import schema 名` → 「取り込みデータ定義名」, `nullable` → 「空欄許可」.
      → `adminUxTerms.ts` に UX_FIELD_TABLE_REF / UX_FIELD_IMPORT_SCHEMA / UX_FIELD_NULLABLE 追加済み。`adminUxGuard.test.ts` に banned-term regression 追加済み。[ux-vocabulary]
- [x] Consolidate promote action in `ContentsPromotionPanel` and present draft creation → design save → promote as explicit steps.
      → promote 導線を ContentsPromotionPanel に集約し、① 下書き作成 → ② 設計保存 → ③ 内容確認 → 有効化 のステップ表示を追加。validation gating: manifest data shape + promotion metadata の両面で確認し、どちらか blocking なら有効化不可。[ux-simplification]

## Implementation gap — `admin_visual_layout_builder` completion bundle

Roadmap entry: `admin_visual_layout_builder`. Status promoted from `partial` → `implemented`.

### layoutId round-trip from DB

- [x] Read confirmed `layoutId`/`routeKey` from backend response after successful apply via `summary.layoutId` (already extracted by `projectLayoutPatchSummary`). [layoutId-round-trip]
- [x] Call `setLayoutId(confirmedLayoutId)` on successful apply to confirm DB-authoritative identity. [layoutId-round-trip]
- [x] Raise explicit `LAYOUT_ID_MISMATCH` error (no silent fallback) if backend returns different `layoutId` than was sent; set lifecycle phase to `applied_fail`. [layoutId-round-trip]
- [x] Tests: 5 layoutId round-trip tests in `frontend/tests/visualLayoutBuilder.test.ts`. [layoutId-round-trip]

### Full responsive token rule UI

- [x] Replace hardcoded `{ md: selectedTokenRefs }` placeholder with `responsiveTokenRules: ResponsiveTokenRules` state. [responsive-token-ui]
- [x] Add `ResponsiveTokenRuleEditor` component with breakpoint tabs (sm/md/lg/xl) in normal view; raw JSON textarea isolated under `AdvancedManualOverride` disclosure. [responsive-token-ui]
- [x] Export `RESPONSIVE_BREAKPOINTS`, `BreakpointKey`, `ResponsiveTokenRules`, `filterEmptyResponsiveRules` from `frontend/runtime/visualLayoutUtils.ts`. [responsive-token-ui]
- [x] `filterEmptyResponsiveRules` strips empty breakpoint entries before backend submission. [responsive-token-ui]
- [x] Tests: 5 filterEmptyResponsiveRules + 4 responsive rule per-breakpoint tests in `frontend/tests/visualLayoutBuilder.test.ts`. [responsive-token-ui]

## CLI/MCP Port SSOT — future implementation TODOs

Added by: CLI Model Context Protocols Port SSOT design_change
SSOT ref: docs/design/cli-model-context-protocols-port-ssot.yaml, docs/design/extended-runtime-bundle-registry-ssot.yaml

- [ ] CLI/MCP Port の実装SSOT（Data Reader / Context API / export job DB schema）を別SSOTとして作成する [cli-mcp-port-implementation-ssot]
- [ ] Email Bundle の別SSOT作成（UI catalog / backend dispatch / runtime 設計）[email-bundle-ssot]
- [ ] Stripe Bundle の別SSOT作成（webhook verification / paid state 設計）[stripe-bundle-ssot]
- [ ] File/Storage Bundle の別SSOT作成 [file-storage-bundle-ssot]
- [ ] Export/SFTP Bundle の別SSOT作成 [export-sftp-bundle-ssot]
- [ ] future_optional_external_surface_bundles（Notion/Sheets/Slack/GitHub/Webhook）は個別SSOTが揃うまで実装しない [future-bundle-ssot-gate]

## User-facing Helper / Manual — future implementation TODOs

Added by: user-facing-helper-manual-ssot design_change
SSOT ref: docs/design/user-facing-helper-manual-ssot.yaml

- [ ] helper/manual category 候補（はじめての業務アプリ作成〜外部Bundle連携の考え方）の実装設計を行う [helper-manual-category-design]
- [ ] Desktop AI / CLI / MCP Reader 向けユーザー説明文言ライティング方針を確定する [helper-manual-copywriting-policy]
- [ ] ヘルプコンポーネント実装は user-facing-helper-manual-ssot のカテゴリ構造に基づいて行う [helper-component-impl-gate]

## Optional follow-up

- [x] Delete legacy/debug/helper wrappers `/dev/admin/import`, `/dev/admin/hub-navigation`, `/dev/admin/runtime`, `/dev/admin/seed`, `/dev/admin/context-token-registry`, and `/dev/admin/registry-vector-validate`; future useful implementation converges on canonical surfaces. [legacy-debug-isolation]
- [ ] `product.dynamic_support_nocode_loop` manual acceptance (unchanged from roadmap).
