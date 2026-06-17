# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `external-port-substrate-implementation` | external_port_substrate / external 8 bundle 実装 todo | partial | 1 | `product.external_port_substrate` | `docs/design/external-port-substrate-ssot.yaml` |
| `file-storage-port-consumer` | file_storage_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-file-storage-ssot.yaml` |
| `email-port-consumer` | email_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-email-ssot.yaml` |
| `stripe-port-consumer` | stripe_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-stripe-ssot.yaml` |
| `webhook-inbox-port-consumer` | webhook_inbox_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-webhook-inbox-ssot.yaml` |
| `job-scheduler-port-consumer` | job_scheduler_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-job-scheduler-ssot.yaml` |
| `audit-approval-port-consumer` | audit_approval_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-audit-approval-ssot.yaml` |
| `export-sftp-port-consumer` | export_sftp_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-export-sftp-ssot.yaml` |

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
external_port_substrate と external 8 bundle の SSOT 境界は確定済み。残作業は設計確定ではなく、DB seed / record / projection 解決、generic access/response/hook connect/receive、各 consumer bundle 接続を実装すること。

目的:
SSOT を再定義せず、`docs/design/external-port-substrate-ssot.yaml` と各 runtime bundle SSOT に従って external_port_substrate と external 8 bundle の実装残を管理する。詳細作業は `.agent/tasks/external-port-substrate-implementation-todo.md` へ委譲する。

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

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-file-storage-ssot.yaml`

問題点:
file_storage_bundle の credential（object storage access key / secret key）が standalone credential 管理 plane の対象として設計されていた。port substrate との接続実装が未着手。

目的:
file_storage_bundle を external_port_substrate の access_port / response_port consumer として確立する。object storage credential は port record 付属の credential_requirement として管理し、standalone credential 管理 plane は作らない。

実装方針:
- [ ] file_storage_bundle の access_port / response_port consumer として seed / DB record / projection 接続を実装する
- [ ] object storage credential_kind を external として port record に付属させる実装を追加する（standalone 管理 plane 不使用）
- [ ] export_job → port record 解決 → generic access/response port connect の経路実装を追加する

対応資料:
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-file-storage-ssot.yaml`

対象 surface 名:
- `access_port`（object storage アクセス）
- `response_port`（object storage 返送）
- `credential_requirement`（object storage credential 付属要件）

---

## Bundle `email-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-email-ssot.yaml`

問題点:
email_bundle の SMTP credential が standalone credential 管理 plane の対象として設計されていた。response_port consumer としての接続実装が未着手。

目的:
email_bundle を external_port_substrate の response_port（provider_kind: smtp）consumer として確立する。SMTP credential は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。

実装方針:
- [ ] email_bundle の response_port（smtp）consumer として seed / DB record / projection 接続を実装する
- [ ] SMTP credential_kind を external として port record に付属させる実装を追加する
- [ ] UI approval → response_port 解決 → SMTP dispatch の経路実装を追加する

対応資料:
- `docs/design/runtime-bundle-email-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-email-ssot.yaml`

対象 surface 名:
- `response_port`（SMTP 送信 port）
- `credential_requirement`（SMTP credential 付属要件）

---

## Bundle `stripe-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-stripe-ssot.yaml`

問題点:
stripe_bundle の webhook secret が standalone credential 管理 plane の対象として設計されていた。hook_port consumer としての接続実装が未着手。

目的:
stripe_bundle を external_port_substrate の hook_port（provider_kind: stripe）consumer として確立する。Stripe webhook secret は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。

実装方針:
- [ ] stripe_bundle の hook_port（stripe）consumer として seed / DB record / projection 接続を実装する
- [ ] Stripe webhook secret の credential_kind を external として hook_port に付属させる実装を追加する
- [ ] hook_port → signature verification → payment state projection の経路実装を追加する

対応資料:
- `docs/design/runtime-bundle-stripe-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-stripe-ssot.yaml`

対象 surface 名:
- `hook_port`（Stripe webhook 受信 port）
- `credential_requirement`（Stripe webhook secret 付属要件）

---

## Bundle `webhook-inbox-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`

問題点:
webhook_inbox_bundle の webhook signing key が standalone credential 管理 plane の対象として設計されていた。hook_port consumer としての接続実装が未着手。

目的:
webhook_inbox_bundle を external_port_substrate の hook_port consumer として確立する。webhook signing key は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。

実装方針:
- [ ] webhook_inbox_bundle の hook_port consumer として seed / DB record / projection 接続を実装する
- [ ] webhook signing key の credential_kind を external として hook_port に付属させる実装を追加する
- [ ] hook_port → signature verification → scheduler 境界の実装を追加する

対応資料:
- `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`

対象 surface 名:
- `hook_port`（webhook 受信 port）
- `credential_requirement`（webhook signing key 付属要件）

---

## Bundle `job-scheduler-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-job-scheduler-ssot.yaml`

問題点:
job_scheduler_bundle の外部スケジューラー provider credential が standalone credential 管理 plane の対象として設計されていた。access_port / hook_port consumer としての接続実装が未着手。

目的:
job_scheduler_bundle を external_port_substrate の access_port / hook_port consumer として確立する。外部スケジューラー credential は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。topolactor 内蔵 scheduler 利用時は credential_kind: none。

実装方針:
- [ ] job_scheduler_bundle の access_port / hook_port consumer として seed / DB record / projection 接続を実装する
- [ ] 外部スケジューラー credential_kind（external または none）の port record 付属実装を追加する
- [ ] scheduler → manifest_dispatcher 境界が port substrate に依存しないことを確認する

対応資料:
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`

対象 surface 名:
- `access_port`（外部スケジューラーアクセス port）
- `hook_port`（スケジューラー hook 受信 port）
- `credential_requirement`（外部スケジューラー credential 付属要件）

---

## Bundle `audit-approval-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-audit-approval-ssot.yaml`

問題点:
audit_approval_bundle の承認通知 credential が standalone credential 管理 plane の対象として設計されていた。response_port consumer としての接続実装が未着手。

目的:
audit_approval_bundle を external_port_substrate の response_port consumer として確立する。承認通知 credential は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。

実装方針:
- [ ] audit_approval_bundle の response_port consumer として seed / DB record / projection 接続を実装する
- [ ] 承認通知 credential_kind の port record 付属実装を追加する
- [ ] approval → response_port 解決 → 通知送信の経路実装を追加する

対応資料:
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`

対象 surface 名:
- `response_port`（承認通知送信 port）
- `credential_requirement`（承認通知 credential 付属要件）

---

## Bundle `export-sftp-port-consumer`

**Status:** not_started
**SSOT:** `docs/design/runtime-bundle-export-sftp-ssot.yaml`

問題点:
export_sftp_bundle の SFTP credential（host / user / key）が standalone credential 管理 plane の対象として設計されていた。response_port consumer としての接続実装が未着手。file_storage_bundle との責務分担境界は SSOT に従い、実装時に崩さない。

目的:
export_sftp_bundle を external_port_substrate の response_port（provider_kind: sftp）consumer として確立する。SFTP credential は port record 付属の credential_requirement として管理し、standalone 管理 plane は作らない。

実装方針:
- [ ] export_sftp_bundle の response_port（sftp）consumer として seed / DB record / projection 接続を実装する
- [ ] SFTP credential_kind を external として response_port に付属させる実装を追加する
- [ ] export_job → port record 解決 → SFTP transfer の経路実装を追加する（file-storage-port-consumer の完了を前提）
- [ ] 転送前後の checksum 検証境界を port substrate と独立して実装 / テストする

対応資料:
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/cli-model-context-protocols-port-ssot.yaml`

対象ファイル名:
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`

対象 surface 名:
- `response_port`（SFTP 転送 port）
- `credential_requirement`（SFTP credential 付属要件）
