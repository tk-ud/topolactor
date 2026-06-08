# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `mock-preset-intake-compiler` | Mock Preset Intake Compiler / UIBuilder Preset Registry | not_started | 9 | `docs/design/mock-preset-intake-compiler-ssot.yaml` |
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

- [x] `topology.mock_preset_*` tables migration を作成する（registry / object_mapping / wiring_candidate / compile_snapshot）  
  → `db/migrations/mock_preset_registry_tables.sql`
- [x] backend admin runtime actions for preset create/list/get/compile/bind を実装する  
  → `backend/runtime/AdminRuntime.MockPreset.cs`, `backend/schema/MockPresetContracts.cs`, `backend/repository/MockPresetRepository.cs`, `backend/repository/NpgsqlMockPresetRepository.cs`
- [x] UIBuilder preset uploader modal_or_drawer を実装する（SVG/XML/Figma-like visual mock intake、AI inference なし）  
  → `frontend/islands/PresetUploaderDrawer.tsx`, `frontend/runtime/visualMockParser.ts`
- [x] UIBuilder save current canvas as preset button を実装する  
  → `frontend/islands/UiBuilderAdmin.tsx` (handleSaveCanvasAsPreset / presetDrawerOpen)
- [x] UIBuilder saved preset load select を実装する  
  → `frontend/islands/UiBuilderAdmin.tsx` (loadPresetList / selectedPresetId select)
- [x] loaded preset bind to selected route package tmp canvas draft を実装し、active topology への直接保存を禁止する  
  → `frontend/islands/UiBuilderAdmin.tsx` (handleLoadPreset → bindMockPreset → applyCanvasFromTensorPatch)
- [x] capabilityTags gate wiring / binding UI を配線する  
  → `frontend/components/types.ts` ComponentCapabilityTag type verified; `frontend/tests/mockPresetIntake.test.ts` capabilityTags vocabulary test  
  ⚠️ **残課題**: 完全な interactive capability gate UI panel は未実装（type-level のみ）
- [x] preview / validate / apply boundary preservation を検証・実装する  
  → bind は draftNodes (local state) のみ更新; canonical topology write は既存 `layout_patch:apply` ルート経由; `frontend/tests/mockPresetIntake.test.ts` bind result shape test
- [x] unresolved object visibility を実装する  
  → `frontend/islands/PresetUploaderDrawer.tsx` unmappedCount warning; `frontend/tests/mockPresetIntake.test.ts` unresolvedJson test; `backend/runtime/AdminRuntime.MockPreset.cs` unresolvedJson in compile result

**残課題（partial 理由）:**
- [ ] NpgsqlMockPresetRepository を本番 DI/startup に配線する（DB integration 未稼働）
- [ ] capability_tags_gate の完全な interactive wiring/binding UI panel を実装する
- [ ] E2E DB integration test（live DB 上での topology.mock_preset_* テーブル動作確認）

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
