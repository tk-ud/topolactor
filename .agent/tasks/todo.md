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

## SSOT Wiring Audit CI carry-over（roadmap completion bundle）

- [ ] [bundle][CI][Topology Registration closure]
      → roadmap entry: `system_ci.topology_registration`（`docs/system-roadmap.yaml`）。
      → completion_condition: `system_ci.dotnet_ssot_wiring_audit_tests.completion_condition.topology_registration_ci_lane_defined`。
      → known_gap_ref: `system_ci.dotnet_ssot_wiring_audit_tests.known_gap_ref` の「Topology / Hub / Scheduler / Component registration CI lanes remain broader follow-up.」。
      → 対象SSOT: `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/ci-contract-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`。
      → 対象ファイル候補: `backend/tests/Topolactor.Runtime.Tests/`, `.agent/tests/check-pipeline-continuity.sh`, `docs/system-roadmap.yaml`。
      → 検出したい drift / gap: topology/package/schema/relation/component reference continuity と topology linking/binding の SSOT 逸脱が diagnostics/evidence で検出不能な状態。
      → out_of_scope: CI待ち・remote CI pass確認・実DB昇格処理・実装修正の atom 分割。

- [ ] [bundle][CI][Hub Registration closure]
      → roadmap entry: `system_ci.hub_registration`（`docs/system-roadmap.yaml`）。
      → completion_condition: `system_ci.dotnet_ssot_wiring_audit_tests.completion_condition.hub_registration_ci_lane_defined`。
      → known_gap_ref: `system_ci.dotnet_ssot_wiring_audit_tests.known_gap_ref` の「Topology / Hub / Scheduler / Component registration CI lanes remain broader follow-up.」。
      → 対象SSOT: `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/ci-contract-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`。
      → 対象ファイル候補: `backend/tests/Topolactor.Runtime.Tests/`, `backend/runtime/RuntimeExecutor.cs`, `docs/system-roadmap.yaml`。
      → 検出したい drift / gap: hub registration / relation route / hub_current / SQL Attention 境界（auto-mutation禁止）を lane 監査で証跡化できない状態。
      → out_of_scope: runtime route 自体の実装変更、attention 推薦ロジック拡張、CI結果の待機メモ。

- [ ] [bundle][CI][Scheduler Runtime route closure]
      → roadmap entry: `system_ci.scheduler_runtime`（`docs/system-roadmap.yaml`）。
      → completion_condition: `system_ci.dotnet_ssot_wiring_audit_tests.completion_condition.scheduler_runtime_ci_lane_defined`。
      → known_gap_ref: `system_ci.dotnet_ssot_wiring_audit_tests.known_gap_ref` の「Topology / Hub / Scheduler / Component registration CI lanes remain broader follow-up.」。
      → 対象SSOT: `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/system-ci-admin-runtime-callable-surface.yaml`。
      → 対象ファイル候補: `backend/scheduler/RuntimeTimelineScheduler.cs`, `backend/runtime/ManifestDispatcher.cs`, `backend/runtime/RuntimeExecutor.cs`, `backend/tests/Topolactor.Runtime.Tests/`。
      → 検出したい drift / gap: RuntimeTimelineScheduler → ManifestDispatcher → RuntimeExecutor canonical route continuity を diagnostics/evidence/eligibility 面で閉じられない状態。
      → out_of_scope: runtime destination追加、dispatcher仕様変更、remote環境依存の live 結果追記。

- [ ] [bundle][CI][Component Registration closure]
      → roadmap entry: `system_ci.component_registration`（`docs/system-roadmap.yaml`）。
      → completion_condition: `system_ci.dotnet_ssot_wiring_audit_tests.completion_condition.component_registration_ci_lane_defined`。
      → known_gap_ref: `system_ci.dotnet_ssot_wiring_audit_tests.known_gap_ref` の「Topology / Hub / Scheduler / Component registration CI lanes remain broader follow-up.」。
      → 対象SSOT: `docs/design/component-catalog-classification-ssot.yaml`, `docs/design/ci-contract-ssot.yaml`, `docs/design/runtime-orchestration-ssot.yaml`。
      → 対象ファイル候補: `db/seed_empty.sql`, `db/demo_seed.sql`, `backend/repository/NpgsqlUiTopologyRepository.cs`, `backend/tests/Topolactor.Runtime.Tests/`。
      → 検出したい drift / gap: YAML static vocabulary → catalog implementation → ui_component_bucket seed/bootstrap instance → runtime adapter/renderer support → registration/promotion surface の identity continuity を lane 監査で保証できない状態。
      → out_of_scope: visual layout builder 完了主張、frontend diagnostic panel 実装、DB seed を vocabulary authority とみなす記述。

- [ ] [bundle][CI][Diagnostics evidence eligibility closure]
      → roadmap entry: `system_ci.dotnet_ssot_wiring_audit_tests`（`docs/system-roadmap.yaml`）。
      → completion_condition: `system_ci.dotnet_ssot_wiring_audit_tests.completion_condition.initial_phase_returns_diagnostics_evidence_eligibility_only`。
      → known_gap_ref: `system_ci.dotnet_ssot_wiring_audit_tests.known_gap_ref` の「diagnostics persistence / audit integration remains follow-up.」。
      → 対象SSOT: `docs/design/ci-contract-ssot.yaml`, `docs/design/system-ci-admin-runtime-callable-surface.yaml`, `docs/design/pipeline-continuity-ssot.yaml`。
      → 対象ファイル候補: `backend/tests/Topolactor.Runtime.Tests/SystemCi*`, `backend/runtime/SystemOperationCiRuntime.cs`, `.agent/tests/check-pipeline-continuity.sh`。
      → 検出したい drift / gap: diagnostics/evidence/eligibility 判定面と persistence/audit integration 後続面の境界が曖昧で、未完了条件が non-blocker 化される状態。
      → out_of_scope: persistence 実装追加、audit DB書き込み、remote運用監視の記録。
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

- [ ] [bundle][manifest-driven-ui][topology-db-operation] UI/UX primitive catalog と abstract function primitive registry を task surface として整理する
      → scope: 実装指示ではなく TODO bundle。frontend component 単体ではなく、manifest-driven UI / topology DB operation の primitive catalog と function registry 境界を定義する。
      → boundary(ssot): search node は hub、display subject は entity を維持し、frontend は topology judgment を持たず candidate / preview / confirm surface に限定する。
      → boundary(update): inline update は `preview_update_patch` / `validate_candidate` / `apply_confirmed_update` の順で explicit apply し、本体更新と `append_diff_log` の境界を明記する。
      → boundary(computation): 計算系 primitive/function は即 mutation せず preview → validate → explicit apply を必須にし、結果は candidate として確認後に適用する。
      → boundary(lookup): 外部 lookup は canonical SSOT にしない。郵便番号/住所/電話番号 lookup は adapter または candidate surface として扱い、電話番号↔住所 lookup は内部 master / 登録済みデータ / 許可済み provider に限定する。
      → boundary(style): font/background/color/spacing/layout は hardcode せず manifest/style token 経由で扱う。
      → boundary(yaml-static-vocabulary): 既存 YAML enum / classification vocabulary は DB移行対象にしない。YAML enum は静的制約・CI補助・分類定義として残す。
      → boundary(db-registry-scope): SQL Attention / runtime recommendation の対象にしたい primitive のみ DB registry + seed bootstrap に登録する。DB登録対象は user / AI / usecase により増減・推薦・rank・promotion される runtime-growable primitive に限定する。
      → boundary(seed-role): seed は DB registry の初期 bootstrap row であり authority ではない。標準 primitive catalog 初期値のみ `db/seed_empty.sql` / `db/demo_seed.sql` に置く。
      → boundary(runtime-growth): user / AI / usecase で増える primitive は seed 追記ではなく runtime DB row として追加する。
      → boundary(registry-candidates): `component_primitive_registry`, `function_primitive_registry`, `calculation_primitive_registry`, `lookup_primitive_registry`, `table_operation_primitive_registry`, `design_token_registry`, `layout_primitive_registry` を候補 registry として扱う。
      → boundary(sql-attention-apply): DB登録された primitive は SQL Attention recommendation / ranking / promotion candidate 対象になり得るが、SQL Attention は registry を直接 mutate しない。apply は preview → validate → explicit confirm → registry write → diff_log append を通す。
      → boundary(ci-shift): CI は YAML subset のみで閉じず、seed/bootstrap rows と DB registry contract の整合検査を補助線として扱う。
      → UI/UX primitive categories:
        - Text / DB search: `AutoCompleteInput`, `SuggestInput`, `SearchCombobox`, `SelectImportDialog`, `RelationCandidatePicker`, `RecentInputSuggest`, `DuplicateMergeCandidatePanel`。
        - Inline edit / update / audit: `InlineEditableField`, `InlineEditableJsonbField`, `PatchPreviewPanel`, `DiffStrikeText`, `AuditDiffDrawer`, `OptimisticUpdateBoundary`, `ConfirmedUpdateButton`。
        - Design / visual token: `FontTokenEditor`, `BackgroundColorEditor`, `TextColorEditor`, `SpacingTokenEditor`, `BorderRadiusEditor`, `ThemePreviewPanel`。
        - Table / list operation: `FacetedFilterBar`, `ColumnFilter`, `ColumnVisibilityEditor`, `SortControl`, `GroupByControl`, `SavedViewSelector`, `BulkActionPanel`, `VirtualizedDataTable`。
        - Kanban / drag-and-drop: `KanbanBoard`, `DragDropStateTransition`, `DragSortList`, `RelationDropZone`, `TreeReorderDropZone`。
        - Calculation / topology computation: `CalculationPreviewPanel`, `FormulaBuilder`, `ComputedFieldPreview`, `RelationScorePreview`, `HubStatisticsPanel`, `AggregationPreviewTable`, `CrossEntityCalculationPanel`, `TopologyDistancePreview`, `RouteCostPreview`, `AttentionWeightPreview`。
        - External / helper lookup: `KanaAssistInput`, `PostalAddressLookup`, `AddressPostalLookup`, `TelAddressCandidateLookup`, `NormalizeAddressCandidate`, `LookupCandidateConfirmPanel`。
        - Recommended inspector / safety: `CommandPalette`, `FieldResolverInspector`, `CandidateConfidenceBadge`, `ConflictResolutionPanel`, `RelationPathPreview`, `SchemaPromotionCandidatePanel`, `UndoTimeline`, `EmptyStateActionPanel`。
      → abstract function registry candidates (non-implementation):
        - Search / suggest: `search_hub_candidates(query, context)`, `search_entity_candidates(query, context)`, `suggest_relation_candidates(hub_id, context)`, `autocomplete_field_value(target, field, query, context)`。
        - Normalize / lookup / import: `normalize_text(value, rule)`, `extract_kana(value)`, `resolve_postal_address(postal_code)`, `resolve_address_postal(address)`, `resolve_tel_candidate(tel_or_address)`, `import_rows_to_candidates(source, mapping)`。
        - Candidate update / audit: `validate_candidate(candidate, manifest)`, `preview_update_patch(target, payload)`, `apply_confirmed_update(target, payload)`, `append_diff_log(target, before, after, editor)`。
        - Calculation registry: `calculate_entity_field_value(entity_id, formula, context)`, `calculate_relation_score(from_id, to_id, context)`, `calculate_relation_cooccurrence(source, target, window)`, `calculate_hub_statistics(hub_id, filters)`, `calculate_group_statistics(target, group_by, filters)`, `calculate_topology_distance(from_hub, to_hub, context)`, `calculate_route_cost(route, context)`, `calculate_attention_weight(target, evidence)`, `calculate_rank_score(candidates, policy)`, `preview_computed_field(target, formula, context)`, `validate_formula_contract(formula, manifest)`, `promote_computation_candidate(path, evidence)`。
      → out_of_scope: frontend/backend/DB 実装、drag-and-drop 実装、計算 engine 実装、external provider 接続、schema 追加、seed 変更。

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

## Admin Visual Layout Builder (Issue #89)

- [ ] visual layout builder の mouse 操作 UI と layout tensor DB 管理を実装する
      → 依存関係: Issue #86（frontend_ui_component_system）は完了済み。着手可能。
      → 対象責務: layout tensor schema + drag/drop UI island 実装。
      → 対象ファイル: `db/ui_topology_tables.sql`, `frontend/islands/`, `docs/registrar-admin-ui-specification.md`。
      → 詳細:
        - LayoutBuilderSection は ui-builder.tsx に文書化済みだが UI 実装（drag/drop）は未着手。
        - `layoutId` / `styleTokenId` / `responsiveRuleId` の DB schema 未追加。
      → 完了条件: `layout_tensor_and_variable_css_boundaries_are_defined` / `layoutId_styleTokenId_responsiveRuleId_are_saved_to_DB` / `frontend_adapter_is_a_stable_projection_surface`（詳細は `docs/system-roadmap.yaml` の `admin_visual_layout_builder.completion_condition` 参照）。
