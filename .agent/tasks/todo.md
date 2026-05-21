# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。

完了済み作業・PR修正履歴・旧方針の残骸は残さない。
未完了の implementation / design / SSOT / test-authoring task がある場合のみ、次の形式で追加する。

CI検証待ち、remote CI pass確認、local tool不足、未実行チェックの記録はこのファイルに追加しない。
それらはPRサマリ/完了レポートの verification / Required Check Scope に記載する。

```md
## <Area>

- [ ] <具体的な未完了作業>
      → <理由・対象ファイル・次の判断点>
```


## SQL Attention Logs SSOT 配線後の次フェーズ

- [ ] function / trigger contract を実装可能な粒度で固定する
      → l2 norm trigger と attention evidence の入力・算出・保持境界を runtime-orchestration と整合させる。

- [ ] norm-level watch policy implementation を実装する
      → 監視条件・threshold 解決（Manifest / function_parameters / policy table）と return/trigger 分岐を実装する。

- [ ] logs.current / logs.attention physical schema implementation を行う
      → schema contract は docs/design/sql-attention-logs-ssot.md / .yaml に定義済み。DB migration と runtime read/write contract を実装する。

- [ ] scheduler/runtime registry-neighbor exploration を実装する
      → exploration 実行責務 (scheduler vs runtime) と evidence persistence 連携を実装する。

- [ ] physical tableid 対応の実装判断を行う
      → topology_edit_log を logs.diff として流用する場合の physical table identity 追加/写像方式を決定する（今回、schema本実装は未着手）。

## Runtime Orchestration SSOT 準拠 (SSOT: docs/design/runtime-orchestration-ssot.yaml)

SSOT参照必読:
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`

### Backend

- [ ] [Claude] Gap-1 残: `target_layer_action_destination_selection` の ManifestDispatcher への完全移管
      → RuntimeExecutor 内の demo/admin target dispatch 分岐を TargetDispatchOverride クラスに隔離済み (RuntimeExecutor_target_layer_action_dispatch_branches_are_removed_or_explicitly_isolated 充足)。
      → 残: TargetDispatchOverride → ManifestDispatcher への完全移管 (manifest-driven routing)。これには ManifestDispatcher への依存注入変更が必要。設計判断が先決。
      → 完了条件: target_layer_action_destination_selection_is_moved_to_manifest_dispatcher
      → 対象: `backend/runtime/TargetDispatchOverride.cs`, `backend/runtime/ManifestDispatcher.cs`
      → docs/system-roadmap.yaml: backend.manifest_dispatcher, backend.runtime_executor = partial

- [ ] [Claude] Gap-2 partial: `runtime_timeline_scheduler` の client trigger を統一キューに整列させる
      → 現在 client trigger は ManifestDispatcher に直接同期呼び出し (HTTP response contract 保持のための意図的例外)。
      → cron/hook/client の 3 トリガ全てを同一 Channel で整列する完全実装は未達。
      → 判断点: HTTP response contract を壊さずに統一整列を実現できるか設計が必要。
      → 対象: `backend/scheduler/RuntimeTimelineScheduler.cs`
      → docs/system-roadmap.yaml: backend.runtime_timeline_scheduler = partial

- [ ] [Claude] Gap-7 残: SSE E2E test の live DB 経路と scheduler routing を実装する (Issue #123)
      → DbNotifyListener.HandleNotificationPayload の unit test 追加済み (live DB 不要, DbNotifyListenerPayloadTests)。
      → 残: DbNotifyListener → pg_notify → broadcaster の live DB 経路テスト (live DB 必要)。
      → 残: hook_or_db_notify_event_enters_scheduler_before_sse_emission 完了条件。現在 DbNotifyListener が broadcaster に直接 Broadcast しており scheduler を経由していない。scheduler routing 変更は設計判断が先決。
      → 対象: `backend/scheduler/DbNotifyListener.cs`, `backend/tests/`
      → docs/system-roadmap.yaml: backend.sse_emitter = partial (known_gap_ref: Gap-7)

## Seed Import/Export Runtime (Issue #84)

- [ ] [Claude] Seed import の Gap-1 依存部分を完全実装する (manifest-driven routing 確立後)
      → save / load / validate / preview は実装済み。import は skeleton (Gap-1 依存)。
      → Gap-1 (target_layer_action_destination_selection → ManifestDispatcher 移管) 解消後に ImportAsync を完全実装する。
      → 完了条件: seed_import_export_runtime status=implemented (docs/system-roadmap.yaml)
      → 対象: `backend/runtime/SeedRuntime.cs` (ImportAsync の skeleton 部分)、`backend/runtime/ManifestDispatcher.cs`

## Frontend UI Component System (Issue #86)

- [ ] [Claude] primitive component を UI topology tensor に DB 登録し drift を解消する
      → Button / Input / Table / Card は frontend/components/ にコードのみ存在 (drift / GAP 状態)。
      → 各 component を PackageGeneratorRuntime 経由で componentId / packageId 発行 → ui_topology_tensor に DB 保存する。
      → CRUD wiring / CanDI wiring の責務境界を registrar-admin-ui-specification.md に明記する。
      → 完了条件: code-only component が 0 件になる (全て DB topology tensor に接続)
      → 対象: `db/ui_topology_tables.sql` (component 登録 surface 追加)、`docs/registrar-admin-ui-specification.md`

## Admin Visual Layout Builder (Issue #89)

- [ ] [Claude] visual layout builder の mouse 操作 UI と layout tensor DB 管理を実装する
      → LayoutBuilderSection は ui-builder.tsx に文書化済みだが UI 実装 (drag/drop) は未着手。
      → layoutId / styleTokenId / responsiveRuleId の DB schema 未追加。
      → 前提: Issue #86 component DB 登録完了後。
      → 完了条件: admin_visual_layout_builder status=implemented (docs/system-roadmap.yaml)
      → 対象: `db/ui_topology_tables.sql` (layout token schema)、`frontend/islands/` (drag/drop UI island)、`docs/registrar-admin-ui-specification.md`

## Runtime Environment Test Gate

- [ ] [Claude] GitHub Actionsで実行される詳細Runtime/環境テストを追加する
      → 現在の `unified-test-gate` は function / runtime / integration / frontend contract の骨格CIとして成立しているが、実環境寄りの詳細テストは不足している。
      → 残: docker-compose起動、DB接続、migration適用、db_notify / output lane、env / volume / seed storage、実行中アプリへのAPI経由E2E。
      → `OutputLaneRouter.RouteAsync` と `AdminRuntime.ExecuteDataAsync` は `check-unified-test-gate.sh` 上で NOT_COVERED として明記済み。
      → 単なる `echo` や存在確認だけで pass するテストは禁止。起動・接続・実行・検証・後始末まで行う。
      → 対象: `.github/workflows/unified-test-gate.yml`, `.github/workflows/*environment*.yml` または新規 workflow, `.agent/tests/check-unified-test-gate.sh`, `.agent/tests/check-runtime-environment.sh` 新規候補, `docker-compose.yml`, `backend/tests/Topolactor.Runtime.Tests/*.cs`, `backend/tests/Topolactor.Integration.Tests/*.cs`, `db/**/*.sql`, `backend/runtime/OutputLaneRouter.cs`, `backend/runtime/AdminRuntime.cs`, `backend/repository/*Notify*.cs`, `backend/repository/*Npgsql*.cs`
      → 対象関数: `OutputLaneRouter.RouteAsync`, `AdminRuntime.ExecuteDataAsync`, `ManifestDispatcher.DispatchAsync`, `RuntimeExecutor.ExecuteAsync`, `TargetDispatchOverride.TryHandleAsync`, `DbNotifyRepository` の notify 実行関数, migration / seed / bootstrap 関連関数
      → todo: 詳細テスト用 script 追加、GitHub Actions workflow から script 実行、docker-compose起動、DB healthcheck/readiness wait、migration適用、seed/env/volume準備、backend実環境起動、API経由で `ManifestDispatcher → RuntimeExecutor` を検証、`AdminRuntime.ExecuteDataAsync` の直接またはAPI経由テスト追加、`OutputLaneRouter.RouteAsync` / db_notify 実行確認、DB通知・ログ・副作用検証、seed storage / volume 書き込み読み込み検証、失敗時の docker logs / DB logs 出力、終了時の docker compose down、解消済み NOT_COVERED の削除、未実装環境依存項目の REMAINING_TODO 明記、最後に `bash .agent/tests/check-structure.sh` 実行
      → 完了条件: GitHub Actions上で詳細Runtime/環境テストが実行される / docker-compose・DB・migration・env・volume の少なくとも一部が実検証される / `OutputLaneRouter.RouteAsync` または `AdminRuntime.ExecuteDataAsync` の未カバー状態が改善される / missing tool・skipped test・起動失敗が pass にならない / 失敗時に原因調査できるログが残る / `unified-test-gate` と `check-structure.sh` が green になる
