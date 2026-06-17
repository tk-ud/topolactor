# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `external-port-substrate-implementation` | external_port_substrate / 7 consumer bundles + credential_requirement substrate 実装 todo | partial | 1 | `product.external_port_substrate` | `docs/design/external-port-substrate-ssot.yaml` |
| `file-storage-port-consumer` | file_storage_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-file-storage-ssot.yaml` |
| `email-port-consumer` | email_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-email-ssot.yaml` |
| `stripe-port-consumer` | stripe_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-stripe-ssot.yaml` |
| `webhook-inbox-port-consumer` | webhook_inbox_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-webhook-inbox-ssot.yaml` |
| `job-scheduler-port-consumer` | job_scheduler_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-job-scheduler-ssot.yaml` |
| `audit-approval-port-consumer` | audit_approval_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-audit-approval-ssot.yaml` |
| `export-sftp-port-consumer` | export_sftp_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-export-sftp-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (file_storage / email / audit_approval / export_sftp) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。scheduler consumer (job_scheduler) は built-in/external port seed binding が完了済み (内蔵 scheduler は port substrate 非依存)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。

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

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する

---



## Bundle `external-port-substrate-implementation`

**Status:** partial
**Roadmap/status SSOT:** `product.external_port_substrate`
**SSOT:** `docs/design/external-port-substrate-ssot.yaml`

問題点:
external_port_substrate と 7 consumer bundles / credential_requirement substrate の SSOT 境界は確定済み。残作業は設計確定ではなく、DB seed / record / projection 解決、generic access/response/hook connect/receive、各 consumer bundle 接続を実装すること。

目的:
SSOT を再定義せず、`docs/design/external-port-substrate-ssot.yaml` と各 runtime bundle SSOT に従って external_port_substrate と 7 consumer bundles / credential_requirement substrate の実装残を管理する。詳細作業は `.agent/tasks/external-port-substrate-implementation-todo.md` へ委譲する。

実装方針:
- [x] `external-port-substrate-seed-coding` bundle increment: external port physical tables / seed policy-step surface / generic resolver-executor boundary を partial 実装する
- [x] `auth-external-credential-management-topology-projection` bundle increment: auth / external credential management を fixed-form topology / manifest / screen_data_shape / Step 2.5 relation projection として seed 実装する
- [x] DB repository atomic encrypted credential update を実装する
- [x] `external-port-canonical-physical-binding-execution` bundle increment: physical table catalog / manifest binding seed / `LoadPortRecordByCanonicalBindingAsync` (admin projection validation only) を実装した。PR#458/#459 で追加された `canonical_binding_*` consumer dispatch branch は post-merge cleanup で削除済み。consumer path は `port_target_ref` lane のみ。
- [x] consumer bundle seed binding: file_storage / email / stripe / webhook_inbox / job_scheduler / audit_approval / export_sftp の port records / policies / policy_steps を seed で追加した (runtime新設なし、port_target_ref lane 既存利用)。
- [ ] consumer bundle 経路実装 (export_job → port record → generic connect 等) は各 bundle consumer todo で管理する。

対応資料:
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/auth-db-session-credential-ssot.yaml`

対象ファイル名:
- `docs/design/external-port-substrate-ssot.yaml`
- `db/topology_tables.sql`
- `db/seed_empty.sql`
- `backend/runtime/ExternalPortCredentialRefresher.cs`
- `backend/repository/NpgsqlExternalPortPolicyRepository.cs`
- `backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs`
- `.agent/tests/check-external-port-substrate-seed-coding.sh`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`
- `docs/system-roadmap.yaml`

対象関数名またはruntime境界名:
- `ExternalPortRecord`
- `ExternalPortPolicy`
- `ExternalPortPolicyStep`
- `IExternalPortResolver`
- `IExternalPortPolicyRepository`
- `IExternalPortPolicyStepExecutor`
- `ExternalPortPolicyStepExecutor.ExecutePolicyAsync`
- `ExternalPortResolver.ResolveAsync`

対象 surface 名:
- `external_port_substrate`（共通基盤 SSOT surface）
- `external-port-substrate-seed-coding`（parent: `external-port-substrate-implementation`, partial）
- `auth-external-credential-management-topology-projection`（parent: `external-port-substrate-implementation`, implemented）
- `credential_requirement`（port record 付属要件 surface）
- `admin_setting_projection`（port 設定 admin role write surface）

---

## Bundle `file-storage-port-consumer`

**Status:** partial
**SSOT:** `docs/design/runtime-bundle-file-storage-ssot.yaml`

PR#460 完了済み: access_port / response_port seed binding / credential_requirement / policy_steps / UI Builder portTargetRef 配線前提。
PR#463 で実装済み: physical table / manifest binding / checksum coupling / operation_key executor handlers / backend+frontend tests。
provider-specific client / runtime は追加しない。
既存レーン参照: `docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane`

実装済み:
- [x] export_job / file_artifact / checksum_record / manifest / signed_download_authorizations physical table 接続実装 (`db/topology_tables.sql`)
- [x] physical table manifest binding (file_storage manifest / physical_table_manifest_bindings) (`db/seed_empty.sql`)
- [x] IFileStorageRepository + NpgsqlFileStorageRepository (後方互換のため保持)
- [x] operation_key_allowed_values を external-port-substrate-ssot.yaml に追加
- [x] backend tests: FileStorageBundleDispatchTests
- [x] external_integration_completion_gate を external-port-substrate-ssot.yaml / runtime-bundle-file-storage-ssot.yaml に明記

completion gate 対応 (PR#463):
- [x] Issue 0: SSOT/todo に external_integration_completion_gate を追記 (4軸 + credential resolution)
- [x] Issue 2b: external_credential_vault に reference_key 列追加; LoadByReferenceKeyAsync 実装; ExternalPortCredentialReferenceResolver を reference_key 経由に修正
- [x] Issue 2a: file_storage access_port / response_port policy_steps に load_encrypted_credential_payload (3) / decrypt_for_runtime_use (4) / inject_authorization_header (6) を追加; domain steps を 9-13 に再番号付け
- [x] Issue 4: execute_db_function operation_key + IExternalPortDbFunctionRepository + topology.fs_* PostgreSQL functions 追加; FileStorageBundleStepHandler は compute_checksum のみ保持
- [x] Issue 3: capture_response を MarkOnly から HttpResponse.Body → context.OutputProp に変更; ExternalPortDispatchRuntime が SseEventBroadcaster 経由で SSE に broadcast
- [x] Issue 1: fileStoragePortConsumer.test.ts に draftPreviewResultToEmission 経由の DB projection test を追加

残 todo:
- [ ] UI Builder form preset / portTargetRef action wiring (export_job → access/response port connect)
- [ ] evidence / runtime_event_log: export_job_initiated / file_write_completed / checksum_verified / signed_url_generated
- [ ] projection response: signed download authorization / file artifact projection

対応資料:
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`

---

## Bundle `email-port-consumer`

**Status:** partial
**SSOT:** `docs/design/runtime-bundle-email-ssot.yaml`

PR#460 完了済み: response_port (smtp) seed binding / credential_requirement / policy_steps / UI Builder portTargetRef 配線前提。
残作業は physical table / approval evidence / delivery evidence / projection 接続。SMTP provider-specific client / runtime は追加しない。
既存レーン参照: `docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane`

残 todo:
- [ ] email_draft / approval_record / delivery_evidence physical table 接続実装
- [ ] physical table manifest binding (email manifest / screen_data_shape)
- [ ] UI Builder form preset / portTargetRef action wiring (UI approval → response_port connect)
- [ ] evidence / runtime_event_log: dispatch_initiated / send_success / send_failure / approval_recorded
- [ ] projection response: delivery status / approval evidence projection

対応資料:
- `docs/design/runtime-bundle-email-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`

---

## Bundle `stripe-port-consumer`

**Status:** partial
**SSOT:** `docs/design/runtime-bundle-stripe-ssot.yaml`

PR#460 完了済み: hook_port (stripe) seed binding / credential_requirement / policy_steps (verify_signature_by_config / enqueue_scheduler_event)。hook consumer のため UI Builder portTargetRef 配線ではなく hook_port receive wiring を使用する。
残作業は physical table / intake snapshot / verification evidence / projection 接続。Stripe provider-specific client / runtime は追加しない。
既存レーン参照: `docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane`

残 todo:
- [ ] webhook_intake_snapshot / verification_evidence / payment_state physical table 接続実装
- [ ] physical table manifest binding (stripe manifest / screen_data_shape)
- [ ] hook_port receive wiring: hook_path / route_key resolution → port record resolution → scheduler enqueue boundary
- [ ] evidence / runtime_event_log: webhook_received / verification_success / verification_failure / payment_state_projected
- [ ] projection response: payment state / verification evidence projection

対応資料:
- `docs/design/runtime-bundle-stripe-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`

---

## Bundle `webhook-inbox-port-consumer`

**Status:** partial
**SSOT:** `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`

PR#460 完了済み: hook_port (generic_webhook) seed binding / credential_requirement / policy_steps (verify_signature_by_config / enqueue_scheduler_event)。hook consumer のため UI Builder portTargetRef 配線ではなく hook_port receive wiring を使用する。
残作業は physical table / intake snapshot / verification evidence / scheduler wiring 接続。webhook provider-specific client / runtime は追加しない。
既存レーン参照: `docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane`

残 todo:
- [ ] webhook_intake_snapshot / signature_verification_evidence physical table 接続実装
- [ ] physical table manifest binding (webhook_inbox manifest / screen_data_shape)
- [ ] hook_port receive wiring: hook_path / route_key resolution → scheduler enqueue boundary (hook_port_receive → scheduler_enqueue_event → external_port_runtime)
- [ ] evidence / runtime_event_log: webhook_received / signature_verification_success / signature_verification_failure / intake_snapshot_created / scheduler_enqueued
- [ ] projection response: intake status / verification evidence projection

対応資料:
- `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

---

## Bundle `job-scheduler-port-consumer`

**Status:** partial
**SSOT:** `docs/design/runtime-bundle-job-scheduler-ssot.yaml`

PR#460 完了済み: access_port (external_scheduler, credential_kind=none) / hook_port (built_in_scheduler, credential_kind=none) seed binding / policy_steps。
topolactor 内蔵 scheduler (runtime_timeline_scheduler) は port substrate に依存しない。
残作業は scheduler evidence / job status projection surface / cron trigger wiring 接続。runtime_timeline_scheduler の in-memory queue は変更しない。外部スケジューラー provider-specific client は追加しない。
既存レーン参照: `docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane`

残 todo:
- [ ] scheduler evidence / job status projection surface 接続実装 (DB queue 新設ではない。runtime queue authority は既存 RuntimeTimelineScheduler)
- [ ] cron trigger driver loop 実装 (built-in scheduler は port substrate に依存しないこと)
- [ ] hook trigger intake wiring (外部スケジューラー hook のみ port substrate 使用)
- [ ] evidence / runtime_event_log: trigger_received / scheduler_enqueued / execution_started / execution_completed / execution_failed
- [ ] projection response: job status projection
- [ ] built-in scheduler path が port substrate に依存しないことの test / guard 追加

対応資料:
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

---

## Bundle `audit-approval-port-consumer`

**Status:** partial
**SSOT:** `docs/design/runtime-bundle-audit-approval-ssot.yaml`

PR#460 完了済み: response_port (notification) seed binding / credential_requirement / policy_steps / UI Builder portTargetRef 配線前提。
残作業は physical table / approval evidence / notification evidence / projection 接続。notification provider-specific client / runtime は追加しない。
既存レーン参照: `docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane`

残 todo:
- [ ] approval_request / approval_evidence / notification_evidence physical table 接続実装
- [ ] physical table manifest binding (audit_approval manifest / screen_data_shape)
- [ ] UI Builder form preset / portTargetRef action wiring (approval → response_port connect)
- [ ] evidence / runtime_event_log: approval_requested / approval_reviewed / approval_granted / approval_rejected
- [ ] projection response: approval status / audit evidence projection

対応資料:
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`

---

## Bundle `export-sftp-port-consumer`

**Status:** partial
**SSOT:** `docs/design/runtime-bundle-export-sftp-ssot.yaml`

PR#460 完了済み: response_port (sftp) seed binding / credential_requirement / policy_steps / UI Builder portTargetRef 配線前提。
残作業は physical table / manifest / checksum / SFTP transfer wiring 接続 (file-storage-port-consumer の完了後)。SFTP provider-specific client / runtime は追加しない。
既存レーン参照: `docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane`

残 todo:
- [ ] sftp_transfer_log physical table 接続実装 (file_storage_bundle 依存)
- [ ] physical table manifest binding (export_sftp manifest / screen_data_shape)
- [ ] checksum 転送前後の検証境界実装 (port substrate と独立)
- [ ] manifest 確認境界実装
- [ ] portTargetRef action wiring: export_job → response_port 解決 → SFTP transfer
- [ ] evidence / runtime_event_log: transfer_initiated / transfer_completed / transfer_failed / checksum_mismatch
- [ ] projection response: transfer status projection

対応資料:
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`