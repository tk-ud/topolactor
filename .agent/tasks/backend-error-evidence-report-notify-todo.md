# backend-error-evidence-report-notify-substrate todo

対象repo: `github.com/tk-ud/topolactor`

## Bundle

- Bundle ID: `backend-error-evidence-report-notify-substrate`
- Status: `partial`
- Worktype: `implementation_change`
- Primary SSOT: `docs/design/backend-error-evidence-report-notify-ssot.yaml`
- Roadmap/status SSOT: `product.backend_error_evidence_report_notify_substrate`
- PR: #536（同一PR内で残scopeを処理する）

## 目的

backend 実行基盤の system error を `logs.error` append-only evidence として永続化し、derived error_report / durable notify queue / post-notify bridge / admin projection へ接続する。DTO failure / 通常 validation reject / 通常 authError / 業務 policy reject / user input miss は混ぜない。

## 実装済み（PR #536 で完了・test済み）

- `db/backend_error_evidence_tables.sql`: `logs.error`(append-only + UPDATE block + index)/`logs.error_report`(derived view)/`logs.error_notify_queue`(claim lifecycle 列付き)/AFTER INSERT trigger(durable row + `pg_notify('logs_error_inserted')` wake-up only)/`current.error_report_projection`(read-side)。
- `IBackendErrorEvidenceAppender` / `NpgsqlBackendErrorEvidenceAppender`(validate / redact / bounded / 独立 connection append)。
- `BackendErrorClassifier`(DTO / 通常 authError / 業務 policy reject / user input miss を `logs.error` 対象外）。
- `AbstractFunctionExecutor.ExecuteAsync` primary boundary（system error のみ append、compensation / rethrow / fail-close 維持）。
- secondary boundary 接続: `RuntimeExecutor` / `ManifestDispatcher` / `AdminRuntime` / `RuntimeTimelineScheduler` / `SchedulerJobRunner` / `DbNotifyListener` / `ExternalPortDispatchRuntime` / `ExternalTokenRefresher`。repository 境界の system error は throw → 上位 boundary で同一 envelope に接続。
- explicit-step `append_error_evidence` primitive adapter（global catch 代替ではないことを test 済み）。
- **post-notify bridge**: `NpgsqlBackendErrorNotifyQueueRepository`（atomic claim `FOR UPDATE SKIP LOCKED` + logs.error join、ack / failed_retryable / failed_terminal、row 非削除）と `BackendErrorNotifyBridge`（LISTEN `logs_error_inserted` wake-up + 周期 poll → claim → hook trigger payload 構築 → `RuntimeTimelineScheduler` hook trigger 経由 dispatch → accepted で acknowledged / 失敗で failed_retryable|terminal）。`SchedulerBackendErrorNotifyHookDispatcher` で scheduler 経由（`ExternalPortDispatchRuntime` 直呼びしない）。
- tests: classifier / executor boundary / explicit-step primitive / dispatcher boundary / bridge（Runtime.Tests）、appender+trigger+queue+report+projection+redaction+append-only / queue claim-ack-fail roundtrip（Integration.Tests live-DB）。

## 残scope（同一PR内の次作業）

- [ ] error-notify `hook_port` の seed / config surface を追加する（`route_key` = `error_notify` 等）。bridge は data-defined の hook_path / route_key で dispatch するため、seed 未整備の間は dispatch が `failed_retryable` に留まる。provider-specific / bundle-specific C# 分岐は追加しない。
- [ ] admin workflow / admin UI: `current.error_report_projection` の unresolved system error report を admin が閲覧する read-side surface を追加する。`logs.error` evidence row を frontend が直接編集しない（read-only projection）。
- [ ] hook_port 設定面（admin UI）: error-notify hook port の設定/有効化 surface。CI Attention へ `logs.error` を流さない。`topology.runtime_event_log` を backend-wide error log に流用しない。
- [ ] 上記 admin / hook_port surface の test。

## 完了条件（残）

- [ ] error-notify hook_port seed/config が存在し、bridge dispatch が accepted → `acknowledged` まで通る live evidence。
- [ ] admin が unresolved error report を read-side projection 経由で閲覧でき、evidence row を直接編集しない guard。
- [ ] admin / hook_port surface の test が緑。
- [ ] 全 scope 完了確認後にのみ: 本 todo 削除、`.agent/tasks/todo.md` 索引行削除、Roadmap status を implemented へ更新、evidence_ref 反映。

## 禁止（継続）

- DB trigger から外部 provider を直接呼ばない。
- `pg_notify` payload だけで `acknowledged` にしない（durable queue を正本にする）。
- `topology.runtime_event_log` を backend error log に流用しない。
- CI Attention 連携を追加しない。
- DTO / validation / auth / business policy reject を system error として永続化しない。
- provider-specific / bundle-specific C# 分岐を追加しない。

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
