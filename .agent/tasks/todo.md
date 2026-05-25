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
      → Approve可能な非ブロッカー残件。frontend runtime event emit 面の適用対象拡張。

- [ ] [integration-test][pr-220] `component_operation_event_log` の PostgreSQL 実体 integration test 追加
      → Approve可能な非ブロッカー残件。append-only永続化境界の実DB検証を追加する。

## TODO dependency map（execution order）

1. Frontend Component Event Runtime（Issue #86 前提）
2. UI primitive catalog bucket投入/promote（Issue #86）
3. Visual layout builder（Issue #89, depends on #86）

---

## Runtime Recommendation Pipeline

- [x] recommendation_blend を operation 候補にも適用するか判断し、必要なら `candidate_kind="operation"` current row 設計を起票/実装する
      → 判定: **現時点では非適用**。operation候補は route mutation authority と混同しやすいため、blend は token候補の recommendation surface に限定する。resolver 側は candidate_kind="token" のみ読取。
      → 対象ファイル候補: `backend/runtime/ContextRouteRecommendationResolver.cs`, `backend/schema/ContextRoutePolicyContracts.cs`, recommendation current row 設計資料。

- [x] seed/demo seed に追加済みの `topology_vector_runtime.recommendation_blend` について、本番運用値を確定し seed以外の反映面（manifest/policy row）を確認する
      → 確認結果: seed/demo seed は `function_parameters(default_policy)` 行として runtime読取面に接続済み。resolverに追加magic numberは導入しない。運用値は `attention_score_weight=1.0, trend/statistics=0.0, scope_limit=1000` を継続。
      → 対象ファイル候補: `db/seed_empty.sql`, `db/demo_seed.sql`, policy manifest surfaces, `backend/runtime/ContextRouteRecommendationResolver.cs`。

## Frontend Component Event Runtime (Issue #86 前提)

- [ ] Frontend Component Event Runtime の残scopeを完了する（Issue #86 前提）
      → 実装済み面: frontend queue/flush/localStorage fallback、`/api/component-events/append` route、backend append endpoint、idempotency境界、frontend/backend tests は存在する。
      → 残作業: OperationPanel 以外を含む全component emit配線、component registration 依存の接続、実DB/live verification、運用hardening（retry/監視/失敗運用）を完了境界まで詰める。
      → 対象ファイル候補: `frontend/runtime/frontendScheduler.ts`, `frontend/routes/api/component-events/append.ts`, `backend/endpoint/ComponentEventAppendEndpoint.cs`, `frontend/tests/frontendComponentEventRuntime.test.ts`, `backend/tests/Topolactor.Runtime.Tests/FrontendComponentEventLogLaneTests.cs`, `frontend/components/`。
      → SSOT参照: `docs/design/runtime-orchestration-ssot.yaml`, `docs/framework-core.yaml`, `docs/framework-policy.yaml`。

## Admin Visual Layout Builder (Issue #89)

- [ ] visual layout builder の mouse 操作 UI と layout tensor DB 管理を実装する
      → 依存関係: **Issue #86 完了後に着手**（component DB registration が前提）。
      → 対象責務: layout tensor schema + drag/drop UI island 実装。
      → 対象ファイル: `db/ui_topology_tables.sql`, `frontend/islands/`, `docs/registrar-admin-ui-specification.md`。
      → 詳細:
        - LayoutBuilderSection は ui-builder.tsx に文書化済みだが UI 実装（drag/drop）は未着手。
        - `layoutId` / `styleTokenId` / `responsiveRuleId` の DB schema 未追加。
      → 完了条件: `docs/system-roadmap.yaml` の `admin_visual_layout_builder status=implemented`。


- [ ] Audit no-escape + truth-surface drift guard (completion bundle)
  - scope: keep audit protocol/rules, README public claims, roadmap status text, and canonical TODO synchronized to SSOT boundary + implementation+tests reality.
  - required drift rule: roadmap/TODO/README/implementation_registry/file existence must never substitute for implementation file/test reads.
  - completion_condition: invalid-audit blocking gate is explicit, `Repo implementation checked: yes` requires listed implementation/test reads, and `roadmap_todo_drift` handling is explicit.
  - remaining follow-up: SQL Attention live DB verification/hardening + evidence-quality expansion, CI broader status vocabulary expansion (`drift`/`not-covered`) only when implemented in runtime status model, and diagnostics persistence/audit integration.
