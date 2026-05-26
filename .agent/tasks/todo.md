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

## Non-blocking cleanup / hardening carry-over

- [ ] [cleanup][pr-220] `ContextRouteRepository.cs` の XML comment / indentation cleanup
      → Approve可能な非ブロッカー残件。実装意味やSSOT completion conditionを変えない範囲で整備する。

- [ ] [surface-expansion][pr-220] `OperationPanel` 以外の主要 component / projection surface への emit-only 配線拡張
      → Approve可能な非ブロッカー残件。frontend runtime event emit 面の適用対象拡張。canonical route は閉鎖済み。component 種別・CI 対象範囲の拡大が目的。
      → 対象ファイル候補: `frontend/components/`, `frontend/runtime/frontendScheduler.ts`, `frontend/tests/frontendComponentEventRuntime.test.ts`。

- [ ] [integration-test][pr-220] `component_operation_event_log` の PostgreSQL 実体 integration test 追加
      → Approve可能な非ブロッカー残件。append-only永続化境界の実DB検証を追加する。
      → 対象ファイル候補: `backend/tests/Topolactor.Integration.Tests/`, `backend/endpoint/ComponentEventAppendEndpoint.cs`。

- [ ] [hardening] Frontend Component Event Runtime の retry / 監視 / 失敗運用 hardening
      → canonical route は閉鎖済み。production_ready:false のまま。retry 境界・監視フック・失敗運用パスの hardening が残る。
      → 対象ファイル候補: `frontend/runtime/frontendScheduler.ts`, `frontend/routes/api/component-events/append.ts`。


## UI/UX Primitive Catalog and Abstract Function Registry

- [ ] [Codex-follow-up][bundle][manifest-driven-ui][topology-db-operation] UI/UX primitive catalog と abstract function registry の Codex 向け閉じ作業
      → 上位SSOT作成完了: `docs/design/ui-ux-primitive-catalog-ssot.yaml`（8 categories / 82 primitives）
      → 上位SSOT作成完了: `docs/design/abstract-function-primitive-registry-ssot.yaml`（6 categories / 49 functions）
      → frontend/components/catalog.ts 拡張完了: 82 primitive lineup entries（8カテゴリ）追加、`UI_UX_PRIMITIVE_CATALOG_IDENTITIES` export 追加
      → docs/system-roadmap.yaml 更新完了: `ui_ux_primitive_catalog_and_abstract_function_registry` エントリ追加
      → meaning boundary 全件 SSOT に明文化完了（frontend/mutation/calculation/lookup/sql-attention/seed/style-token/yaml-vocab）
      → Codex follow-up として残る作業（implementation atom ではなく completion bundle 単位）:
        1. [codex] SSOT reader tests: `docs/design/ui-ux-primitive-catalog-ssot.yaml` / `docs/design/abstract-function-primitive-registry-ssot.yaml` の構造整合テスト追加
           → 対象: `.agent/tests/` または `backend/tests/` のSSOT読取テスト
        2. [codex] catalog.ts 全件 subset 検査: `UI_UX_PRIMITIVE_CATALOG_IDENTITIES` と `COMPONENT_CATALOG_ENTRIES` の componentKey 整合性静的テスト
           → 対象: `frontend/tests/`
        3. [codex] seed/bootstrap rows: primitive registry 候補の `db/seed_empty.sql` / `db/demo_seed.sql` への初期 bootstrap row 追加（authority ではなく bootstrap として）
           → 対象: `db/seed_empty.sql`, `db/demo_seed.sql`
        4. [codex] runtime adapter / renderer 到達性テスト: 新 catalog entry の runtimeConnected 昇格候補について `runtimeComponentAdapter.ts` + `runtimePrimitiveRenderer.ts` の機械的到達性テスト追加
           → 対象: `frontend/tests/runtimeComponentAdapter.test.ts`, `frontend/tests/runtimePrimitiveRenderer.test.ts`
        5. [codex] roadmap status 整合: `ui_ux_primitive_catalog_and_abstract_function_registry` の status を `implemented` に上げる条件（React実装 / adapter+renderer到達性 / DB seed bootstrap rows / SSOT reader tests / catalog identity subset static check）
           → `docs/system-roadmap.yaml` の known_gap_ref を消化した時点で更新
      → boundary(ssot): search node は hub、display subject は entity を維持し、frontend は topology judgment を持たず candidate / preview / confirm surface に限定する。
      → boundary(update): inline update は `preview_update_patch` / `validate_candidate` / `apply_confirmed_update` の順で explicit apply し、本体更新と `append_diff_log` の境界を明記する。
      → boundary(computation): 計算系 primitive/function は即 mutation せず preview → validate → explicit apply を必須にする。
      → boundary(lookup): 外部 lookup は canonical SSOT にしない。adapter / candidate surface として扱う。
      → boundary(sql-attention): SQL Attention は registry / primitive を直接 mutate しない。recommendation / ranking は candidate surface に限定。
      → boundary(seed-role): seed は bootstrap row であり authority ではない。
      → out_of_scope (引き続き): React 実装、drag-and-drop 実装、計算 engine 実装、external provider 接続、schema 追加。

## Frontend Projection Constructor / Manifest Mapping

- [ ] [projection-constructor][mapping-source] ProjectionDefinition / constructor mapping の canonical supply source を定義する
      → 対象: backend manifest response / dispatch response / manifest fetch route のどれを supply source とするか。
      → frontend が ProjectionDefinition をテスト内・呼び出し側で手渡しするだけの状態を completion としない。
      → frontend は topology judgment / SQL Attention judgment を持たない。
      → `projectionFromEmission` helper は実装済みとして扱う。

- [ ] [projection-constructor][response-contract] backend/frontend response contract に ProjectionDefinition / constructor mapping の受け渡し境界を反映する
      → 対象候補: backend emission / manifest response DTO / frontend `Emission` / `DispatchResponse` / manifest fetch response。
      → `Emission.data` だけでは constructor mapping supply proof にならない。
      → 必要なら field 追加、または manifest fetch route を明示する。

- [ ] [projection-constructor][runtime-definition-load] frontend projection runtime が canonical route から ProjectionDefinition を受け取り `setProjectionDefinition` できる経路を実装/証明する
      → 対象候補: `frontend/runtime/projectionRuntime.ts`, `frontend/runtime/renderEmission.ts`, dispatch/manifest response handling surface。
      → SSE projection event が来る前に definition が設定される、または未設定時に explicit failure / explicit policy になること。
      → 現状の `projectionFromEmission(emission, definition)` は caller-supplied definition 前提なので、このTODOを完了扱いにしない。

- [ ] [projection-constructor][end-to-end-proof] manifest response / dispatch response から ProjectionDefinition / constructor mapping を取得し、`projectionFromEmission` / `constructProjection` に到達する end-to-end test を追加する
      → 完了条件: `manifest_response_constructor_mapping_end_to_end_route_is_proven`
      → 現在のように test-local `ProjectionDefinition` を手渡しするだけでは不可。
      → RuntimeTopologyComponentProps envelope / runtime adapter / runtime factory interface boundary は完了済みとして扱う。

- [ ] [projection-constructor][explicit-missing-definition] ProjectionDefinition / constructor mapping が欠落した場合の explicit error / policy を定義する
      → silent fallback 禁止。
      → projection runtime が no definition で skip するだけなら roadmap 上は partial のまま。
      → error / diagnostic / explicit no-op policy のどれかを SSOT整合で明示する。

## TODO dependency map（execution order）

1. Frontend Component Event Runtime — canonical route 閉鎖済み（frontend queue / flush / localStorage fallback / `/api/component-events/append` / backend append endpoint / idempotency / frontend・backend tests）。残作業は surface expansion / hardening（non-blocking、上記参照）。
2. Visual layout builder（Issue #89）— Issue #86 依存クリア済み、着手可能。

---

## Runtime Recommendation Pipeline

- [x] recommendation_blend を operation 候補にも適用するか判断し、必要なら `candidate_kind="operation"` current row 設計を起票/実装する
      → 判定: **現時点では非適用**。operation候補は route mutation authority と混同しやすいため、blend は token候補の recommendation surface に限定する。resolver 側は candidate_kind="token" のみ読取。
      → 対象ファイル候補: `backend/runtime/ContextRouteRecommendationResolver.cs`, `backend/schema/ContextRoutePolicyContracts.cs`, recommendation current row 設計資料。

- [x] seed/demo seed に追加済みの `topology_vector_runtime.recommendation_blend` について、本番運用値を確定し seed以外の反映面（manifest/policy row）を確認する
      → 確認結果: seed/demo seed は `function_parameters(default_policy)` 行として runtime読取面に接続済み。resolverに追加magic numberは導入しない。運用値は `attention_score_weight=1.0, trend/statistics=0.0, scope_limit=1000` を継続。
      → 対象ファイル候補: `db/seed_empty.sql`, `db/demo_seed.sql`, policy manifest surfaces, `backend/runtime/ContextRouteRecommendationResolver.cs`。


## UI/UX Primitive Executable Component Slice

- [ ] #264 完了反映: runtime/catalog/factory 境界は implemented 扱いとして維持する
      → pipeline 意味は `runtime adapter(normalize + factory reachability check) -> runtime registry(kind lookup) -> runtime factory(kind -> constructor/component interface) -> concrete component`。
      → `runtimeConnected:true` は factory/constructor reachable を意味する。#264 all green（runtime semantics audit 含む）を根拠に partial へ戻さない。

- [ ] representative UI/UX executable component slice を completion bundle 単位で昇格する
      → 単位は `componentKind selector -> RuntimeComponentFactory interface registration -> catalog sourcePath promotion -> runtimeConnected:true promotion -> static/type/deno による interface reachability check` を 1 bundle とする。

- [ ] UI/UX primitive catalog の実行可能昇格を段階実装する
      → 初回 representative slice は interface/thin-wrapper 中心で実施し、個別 UI behavior test 必須化ではなく catalog 昇格 invariant を主軸に進める。
      → representative names（昇格対象一覧）: AutoCompleteInput / SearchCombobox / CandidateConfidenceBadge / InlineEditableField / PatchPreviewPanel / ApplyConfirmDialog / FacetedFilterBar / VirtualizedDataTable / LayoutDropZone / ComponentPlacementHandle / SnapGridOverlay / StyleTokenPicker / ThemePreviewPanel / DryRunResultPanel / ValidationErrorPanel。

- [ ] 実装済み primitive の catalog.ts sourcePath を CATALOG_SSOT から実ファイルへ昇格する
      → 未実装 primitive は sourcePath: CATALOG_SSOT のまま維持し、実装完了時のみ昇格する。

- [ ] 実装済み primitive の runtimeConnected を false から true へ昇格する
      → factory registration + runtimePrimitiveRenderer/runtimeComponentAdapter 到達性証明後のみ true。

- [ ] component slice reachability check bundle を追加する
      → catalog -> componentKind -> factory/interface reachability を static check / TypeScript check / Deno test で検証する（個別 UI behavior test 必須化はしない）。
      → primitive に付随する abstract function / calculation / validation / external lookup / mutation boundary / patch generation / layout collision は UI component ではなく runtime function boundary として unit test 対象にする。

- [ ] 未実装 primitive は catalog_definition_only / runtimeConnected:false / registrationRequired:true のまま維持する
      → 誤昇格防止。未実装 primitive を implemented 扱いしない。

- [ ] roadmap status を実装実態に合わせて更新する
      → frontend.ui_ux_executable_component_slice を実装進捗に合わせて not_started/partial/implemented へ更新する。

## Abstract Function / Heavy Primitive Function Boundary

- [ ] [abstract-function][unit-tests] function-backed primitive が使う heavy function boundary に unit tests を追加する
      → 対象: calculation / validation / patch generation / external lookup / layout collision / mutation boundary / ranking explanation。
      → component rendering の単体テストではなく、付加処理関数の unit test として扱う。
      → promoted primitive がこれらの function を invoke する場合、UI component behavior test ではなく function boundary test を追加する。

## Admin Visual Layout Builder (Issue #89)

- [ ] [issue-89][db-runtime] styleTokenId / responsiveRuleId の本体DB schema・runtime保存導線を実装する
      → 今回は css dictionary wiring/CI/UI selector まで。styleTokenId/responsiveRuleId の本体保存は未実装。

- [ ] [issue-89][ui] mouse-driven layout editor 本体を実装する
      → 今回は selector 候補表示と token ref draft shape まで。drag/drop editor は未着手。

- [ ] visual layout builder の mouse 操作 UI と layout tensor DB 管理を実装する
      → 依存関係: Issue #86（frontend_ui_component_system）は完了済み。着手可能。
      → 対象責務: layout tensor schema + drag/drop UI island 実装。
      → 対象ファイル: `db/ui_topology_tables.sql`, `frontend/islands/`, `docs/registrar-admin-ui-specification.md`。
      → 詳細:
        - LayoutBuilderSection は ui-builder.tsx に文書化済みだが UI 実装（drag/drop）は未着手。
        - `layoutId` / `styleTokenId` / `responsiveRuleId` の DB schema 未追加。
      → 完了条件: `layout_tensor_and_variable_css_boundaries_are_defined` / `layoutId_styleTokenId_responsiveRuleId_are_saved_to_DB` / `frontend_adapter_is_a_stable_projection_surface`（詳細は `docs/system-roadmap.yaml` の `admin_visual_layout_builder.completion_condition` 参照）。
