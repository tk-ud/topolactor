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
- tests: classifier / executor boundary / explicit-step primitive / dispatcher boundary / bridge（Runtime.Tests）、appender+trigger+queue+report+projection+redaction+append-only / queue claim-ack-fail roundtrip / **bridge end-to-end accepted→acknowledged**（Integration.Tests live-DB）。
- **error-notify `hook_port` seed**（`db/backend_error_notify_hook_port_seed.sql`）: active hook_port（`/hooks/error_notify` / `error_notify` / `credential_kind=none`）+ hook_port policy `error_notify_hook_port_logger_sink`（`append_runtime_event_log` logger sink step、SSOT logger_sink_boundary 準拠の first implementation）。init.sql に配線済み。
- **bridge dispatch evidence（2系統）**: (1) direct seam test（`BackendErrorNotifyBridgeEndToEndLiveDbTests`）で hook_port + policy resolve → accepted → `acknowledged`。(2) **production route test**（`BackendErrorNotifyBridgeProductionRouteLiveDbTests`、full init.sql）で bridge → `SchedulerBackendErrorNotifyHookDispatcher` → `RuntimeTimelineScheduler.AlignAndDispatchAsync` → `ManifestDispatcher` → `external_port_runtime` → seeded hook_port + policy resolve → accepted → `acknowledged` + `backend_error_notify_delivered` consumer event を検証済み（Gate0 production route 要件充足）。

## PR536 scope 判定（reviewer 監査 2026-06-30）

PR536 内の deliverable scope（backend error evidence / report / notify substrate、post-notify bridge、`error_notify` hook_port seed、production route accepted→acknowledged evidence）は **実装済み / OK**。PR536 では **新規 admin UI を実装しない**。下記 admin/errorlogs read-side は **別PR follow-up** として切り出す。PR536 は admin read-side 未完のため bundle 全体としては `partial` 継続だが、これは「未実装に戻す」意味ではなく「残scopeを別PRへ分離するための partial」。

## 別PR follow-up scope（PR536 では実装しない）

- [ ] admin read-side surface: `current.error_report_projection` の unresolved system error report viewer。既存 admin data-projection lane（dispatch → admin_runtime → `ExecuteDataAsync` + seed manifest の写像、`sql_attention:list_projection` 等の pattern 再利用）。read-only、`logs.error` evidence row を frontend から直接編集しない。
- [ ] admin/errorlogs UI test。
- [ ] hook 設定の扱い（**新規専用画面を作らない**）: error-notify hook 設定は backend error notification 専用画面ではなく、**既存の投影側クレデンシャル管理画面** `auth.external.credential_management.projection`（manifest `00000000-0000-0000-0000-000000000092`）に載せる。この projection は既に `external_hook_ports` / `hook_port` / `external_port_context` / `policy_template_key` を扱う surface（`canonical_port_bindings` で `hook_port → topology.external_hook_ports`、`screen_data_shape` に list/update operation）。`error_notify` は seed 済みの **hook_port record の1つ**であり uuid で一意に識別される。`stripe` / `webhook_inbox` / `job_scheduler` / `error_notify` が同じ `hook_port` kind の複数 record として並ぶ形を維持する。
- [ ] 各 manifest / projection / hook_port は必ず uuid で識別される前提を維持。

## follow-up scope の NG（厳守）

- backend error notification 専用 admin 画面を作る。
- `hook_port` kind を1件固定（singleton）にする。dispatch 対象は `hook_path` / `route_key` / `required_by_bundle` / `provider_kind` / active policy / uuid で解決し、kind 選択だけで呼び先 hook を決めない。`error_notify` だけを kind から暗黙選択する仕様は禁止。
- provider-specific / bundle-specific C# 分岐を追加する。
- `topology.runtime_event_log` を backend-wide error log として流用する。
- CI Attention に `logs.error` を流す。

## PR536 完了条件（充足済み）

- [x] error-notify hook_port seed/config が存在し、bridge dispatch が accepted → `acknowledged` まで通る live evidence（direct seam + production route の2系統）。

## 別PR follow-up 完了条件

- [ ] admin が unresolved error report を read-side projection 経由で閲覧でき、evidence row を直接編集しない guard。
- [ ] error-notify hook record が既存 `auth.external.credential_management.projection`（manifest `...0092`）に hook_port record として（multi-record の1行・uuid 識別で）載っていることを確認。
- [ ] admin/errorlogs UI test が緑。
- [ ] follow-up 完了確認後にのみ: 本 todo 削除、`.agent/tasks/todo.md` 索引行削除、Roadmap status を implemented へ更新、evidence_ref 反映。

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
