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

- [ ] Reflect the explicit contents wizard steps in UI: empty draft creation → DB reference → columns → initial data → optional table relation intent → search key → aggregation/display group with sample viewing → validate/preview/register → `/admin/ui-builder` handoff.
- [ ] Replace normal-view free-text DB column type input with select UI. Candidates: text / integer / bigint / boolean / numeric / timestamp with time zone / date / jsonb / uuid / varchar. Keep free text isolated under advanced / other.
- [ ] Add initial-data registration flow with validate → preview → explicit apply or promote; do not add silent/direct DB writes.
- [ ] Add structured relation/join input for a draft's data-shape intent without moving created-manifest hub membership, inter-manifest relations, or navigation ordering out of `/admin/manifests`.
- [ ] Add user-facing search-key selection for `searchTargets`.
- [ ] Add aggregation-key and display-group selection with mandatory sample viewing / preview. Do not expose `group by` as primary UX vocabulary.

### Backend persistence and explicit validation

- [ ] Persist structured relation/join and aggregation display fields on the `screen_data_shape` topology extension.
      → Depends on schema design in `docs/design/db-schema.yaml` and validator updates.
- [ ] Fail explicitly when `table_ref` is not found in `topology.physical_tables`; current wiring projection skip must not remain a silent no-op.
      → Prefer an explicit skipped/error status in projection result.

### `/admin/ui-builder` projection authoring

- [ ] Add catalog-based component placement on layout canvas / preview with keyboard or button alternatives to drag and drop.
- [ ] Add selectable CSS / Tailwind / design-token layout settings with visual or before/after preview; isolate advanced raw input.
- [ ] Add component-level wiring selection from DB / manifest / topology registry references; move raw dispatcher `role / target / layer / action` fields to advanced disclosure.
- [ ] Preserve validate → preview → explicit apply and prohibit direct DB writes / silent fallback.
- [ ] Add post-apply handoff to CI / local guard / agent-governance checks for generated-artifact drift, registry drift, and SSOT consistency auditing.

### User-facing vocabulary and flow cleanup

- [ ] Replace internal normal-view terms in `ContentsScreenDesignPanel.tsx`: `physical table ref` → 「参照テーブル名」, `import schema 名` → 「取り込みデータ定義名」, `nullable` → 「空欄許可」.
      → Add missing `adminUxTerms.ts` vocabulary and banned-term regression coverage in `adminUxGuard.test.ts`. [ux-vocabulary]
- [ ] Consolidate promote action in `ContentsPromotionPanel` and present draft creation → design save → promote as explicit steps. [ux-simplification]

## Optional follow-up

- [x] Delete legacy/debug/helper wrappers `/dev/admin/import`, `/dev/admin/hub-navigation`, `/dev/admin/runtime`, `/dev/admin/seed`, `/dev/admin/context-token-registry`, and `/dev/admin/registry-vector-validate`; future useful implementation converges on canonical surfaces. [legacy-debug-isolation]
- [ ] `product.dynamic_support_nocode_loop` manual acceptance (unchanged from roadmap).
