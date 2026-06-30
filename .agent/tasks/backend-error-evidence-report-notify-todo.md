# backend-error-evidence-report-notify-substrate todo

対象repo: `github.com/tk-ud/topolactor`

## Bundle

- Bundle ID: `backend-error-evidence-report-notify-substrate`
- Status: `not_started`
- Worktype: `implementation_change` after Codex `.agent` wiring
- Primary SSOT: `docs/design/backend-error-evidence-report-notify-ssot.yaml`
- Roadmap/status SSOT: `pending_system_roadmap_alignment`

## 作業分担

- ChatGPT: SSOT / bundle todo 作成
- Codex: `.agent` route / ssot-map / check 配線
- Claude: todo implementation_change 処理

## 目的

backend 実行基盤の system error を `logs.error` 相当の append-only evidence として永続化し、derived error_report / notify queue / admin projection へ接続する。

DTO failure、通常 validation reject、通常 authError、業務 policy reject、ユーザー入力ミスは `logs.error` に混ぜない。

## 残問題

- backend error 処理が `ILogger.LogError` と caller response に散在しており、横断の永続 error evidence が無い。
- `topology.runtime_event_log` は external-port consumer domain event evidence であり、backend-wide system error evidence ではない。
- `AbstractFunctionExecutor.ExecuteAsync` は execute_abstract_function の集約境界だが、現行は compensation best-effort 後に rethrow するだけで error evidence append が無い。
- `RuntimeExecutor` / `ManifestDispatcher` / `AdminRuntime` / scheduler / `DbNotifyListener` / repository の system error が同じ envelope に集約されていない。
- `logs.error` insert 後の notify hook / durable queue / admin projection / report aggregation が未定義・未実装。
- CI Attention は backend error handler authority ではないため、`logs.error` を CI Attention に流す設計は不可。

## 改善方針

implementation_change で、Primary SSOT に従って `logs.error` / error_report / error_notify_queue の永続 substrate を追加し、`IBackendErrorEvidenceAppender` と classifier を実装する。

最初の集約点は `AbstractFunctionExecutor.ExecuteAsync` とし、primitive adapter 失敗・manifest/authority/runtime lane/binding/registry 失敗を分類して system error のみ append する。

DTO failure / 通常 authError / 業務 policy reject は append 対象外とする。

`logs.error` INSERT trigger は error取得ではなく post-insert hook として durable notify queue へ積み、`pg_notify` は wake-up のみに使う。

## 対応資料

- `docs/design/backend-error-evidence-report-notify-ssot.yaml`
- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `docs/design/ci-contract-ssot.yaml`
- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`

## 対象ファイル名候補

- `db/topology_tables.sql` or future `db/backend_error_evidence_tables.sql`
- `backend/runtime/AbstractFunctionRuntime.cs`
- `backend/runtime/RuntimeExecutor.cs`
- `backend/runtime/ManifestDispatcher.cs`
- `backend/runtime/AdminRuntime.cs`
- `backend/runtime/ExternalPortDispatchRuntime.cs`
- `backend/runtime/ExternalPortCredentialRefresher.cs`
- `backend/scheduler/RuntimeTimelineScheduler.cs`
- `backend/scheduler/SchedulerJobRunner.cs`
- `backend/scheduler/DbNotifyListener.cs`
- `backend/repository/*Error*` or future `backend/repository/NpgsqlBackendErrorEvidenceAppender.cs`
- `backend/schema/*Error*` or future `backend/schema/BackendErrorEvidenceContracts.cs`
- `backend/tests/Topolactor.Runtime.Tests/*Error*`
- `backend/tests/Topolactor.Integration.Tests/*Error*`

## 対象関数名候補

- `AbstractFunctionExecutor.ExecuteAsync`
- future `IBackendErrorEvidenceAppender.AppendAsync`
- future `NpgsqlBackendErrorEvidenceAppender.AppendAsync`
- future `BackendErrorClassifier.Classify`
- future `AppendErrorEvidencePrimitiveAdapter.ExecuteAsync`
- `RuntimeExecutor.ExecuteAsync`
- `RuntimeExecutor.ErrorResponse`
- `ManifestDispatcher.DispatchAsync`
- `ManifestDispatcher.DispatchToHandlerAsync`
- `AdminRuntime.ExecuteDataAsync`
- `RuntimeTimelineScheduler.ExecuteAsync`
- `RuntimeTimelineScheduler.EnqueueCronTrigger`
- `RuntimeTimelineScheduler.EnqueueHookTrigger`
- `SchedulerJobRunner.RunAsync`
- `DbNotifyListener.HandleNotificationPayload`
- `DbNotifyListener.HandleSqlAttentionDraftCandidatePayload`
- `ExternalPortDispatchRuntime.ExecuteAsync`
- `ExternalTokenRefresher.RefreshIfNeededAsync`

## 残受入条件

- [ ] `.agent` route / ssot-map / check 配線が Codex により追加され、Claude implementation_change 前に参照可能になっている。
- [ ] `logs.error` append-only table と index が追加されている。
- [ ] `logs.error_report` または同等の derived aggregation が追加されている。
- [ ] `logs.error_notify_queue` と `logs.error` AFTER INSERT trigger / `pg_notify` wake-up が追加されている。
- [ ] `IBackendErrorEvidenceAppender` と `NpgsqlBackendErrorEvidenceAppender` が追加されている。
- [ ] classifier が DTO failure / 通常 authError / 業務 policy reject / user input miss を `logs.error` 対象外にしている。
- [ ] `AbstractFunctionExecutor.ExecuteAsync` が system error のみ append し、既存 compensation / rethrow / fail-close semantics を保っている。
- [ ] `RuntimeExecutor` / `ManifestDispatcher` / `AdminRuntime` / scheduler / `DbNotifyListener` / repository 境界の system error が同じ evidence envelope に接続されている。
- [ ] `append_error_evidence` primitive adapter を追加する場合、それは明示 step 用であり global catch 代替ではないことが test で確認されている。
- [ ] `topology.runtime_event_log` を backend-wide error log と誤用していない。
- [ ] CI Attention へ `logs.error` を流していない。
- [ ] admin projection は derived read-side であり、`logs.error` evidence row を frontend が直接編集しない。
