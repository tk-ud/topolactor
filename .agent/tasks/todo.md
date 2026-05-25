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

## SSOT Wiring Audit CI 次段階計画

- [ ] sh CI は AI / Agent 用の軽量運用 guard として維持し、アプリ本体の設計逸脱監査を C# / dotnet test CI へ分離する
      → 対象責務: sh は governance 構造検査、C# CI は SSOT 配線設計の逸脱監査。

- [ ] C# / dotnet test による SSOT wiring audit CI を 4系統で設計する
      → 1) Topology Registration CI（topology/package/schema/relation/component ref と linking/binding の逸脱監査）
      → 2) Hub Registration CI（hub registration / relation route / hub_current / attention logs の逸脱監査）
      → 3) Scheduler / Runtime CI（RuntimeTimelineScheduler / ManifestDispatcher / RuntimeExecutor の canonical route 逸脱監査）
      → 4) Component Registration CI（component_definition / ComponentDataHub / ui_topology_tensor 接続の逸脱監査）

- [ ] 初期段階 C# CI は判定面を diagnostics / evidence / eligibility に限定し、DB書き込み・自動昇格を scope out として固定する
      → 対象責務: staging → active 昇格判定や registry promotion の根拠面を先に整備し、実データ変更は後続に分離する。

- [ ] 後続タスクとして C# test skeleton / SSOT YAML loader / fixtures / diagnostics evidence DTO を分離起票する
      → 対象ファイル候補: backend test project, docs/design SSOT readers, fixture surface, promotion decision evidence contract.

- [ ] C# direct semantic tests for `OutputLaneRouter.RouteAsync` / `AdminRuntime.ExecuteDataAsync` を追加する
      → 対象責務: live E2E ではなく、dispatcher / output lane / admin runtime の意味境界を fixture で直接検証する。
      → 対象ファイル候補: `backend/tests/Topolactor.Runtime.Tests/`, `backend/runtime/OutputLaneRouter.cs`, `backend/runtime/AdminRuntime.cs`。
      → 完了条件: `.agent/tests/check-unified-test-gate.sh` の NOT_COVERED から該当2関数を削除できること。

- [ ] [bundle][CI][SSOT vocabulary contract] SSOT YAML の許可集合・構造・命名を正本として implementation discrete values の subset 準拠を検査する CI contract を追加する（次段階 hardening）
      → 目的: SSOT YAML を allowed vocabulary / allowed shape / canonical relation の唯一正本にし、実装側が正本にない離散値を使った場合は CI fail とする。新語彙は implementation 先行ではなく SSOT 先行追加を通過条件にする。
      → CI Attention 追加観点: CI result を pass/fail 二値ではなく structured status vector（pass/gap/blocking/drift/not_covered）として扱い、agent action（carry-over/repair/stop）分岐に接続する。
      → silent fallback は CI 上で pass 扱いせず、境界失敗を structured status として明示露出する。
      → 対象責務（bundle 単位で一体管理）:
        - component catalog classification
        - DB seed / status / type / kind
        - event type / runtime destination
        - property naming
        - validation class
        - pipeline required_identity / prohibited vocabulary
      → 対象ファイル候補:
        - SSOT: `docs/framework-core.yaml`, `docs/framework-policy.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/ci-contract-ssot.yaml`, `docs/design/component-catalog-classification-ssot.yaml`, `docs/governance/agent-governance-routing-ssot.yaml`
        - governance map: `.agent/docs/ssot-map.yaml`
        - CI test surface: `.agent/tests/check-pipeline-continuity.sh`, `.agent/tests/check-unified-test-gate.sh`, （必要なら）`.agent/tests/check-ssot-vocabulary-contract.sh`
      → 完了条件:
        - test 側で expected list / expectedKeys / union vocabulary を重複再定義しない。
        - test は常に SSOT YAML を読み、implementation values ⊆ SSOT allowed values を検査する。
        - SSOT 未定義値が実装に出た場合は CI fail とし、SSOT 側へ先行追加した後に pass する。
        - check-structure.sh を巨大語彙 grep 集に肥大化させず、構造ガード責務を維持する。
        - 本 bundle は Issue #86 / #241 の分類 PR とは混ぜず、CI hardening の次段階として独立管理する。


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
