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

## Optional follow-up

- [x] Delete legacy/debug/helper wrappers `/dev/admin/import`, `/dev/admin/hub-navigation`, `/dev/admin/runtime`, `/dev/admin/seed`, `/dev/admin/context-token-registry`, and `/dev/admin/registry-vector-validate`; future useful implementation converges on canonical surfaces. [legacy-debug-isolation]
- [ ] `product.dynamic_support_nocode_loop` manual acceptance (unchanged from roadmap).
