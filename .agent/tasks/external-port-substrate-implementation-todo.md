# External Port Substrate Implementation Todo

対象 repo: `github.com/tk-ud/topolactor`

このファイルは `.agent/tasks/todo.md` の `external-port-substrate-implementation` / port consumer 群を、設計 todo ではなく実装 todo として扱うための詳細作業面。

## Status

partial / minimal primitive skeleton; auth/external credential management topology projection implemented

## SSOT

- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/runtime-bundle-email-ssot.yaml`
- `docs/design/runtime-bundle-stripe-ssot.yaml`
- `docs/design/runtime-bundle-webhook-inbox-ssot.yaml`
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`

## 問題点

`external_port_substrate` と 8 bundle の SSOT 設計は確定済み。todo 側では設計確定作業として残っていたが、実際の未処理は実装作業。DB guarded credential vault DDL / generic refresher primitive skeleton / fixed-form auth-external credential management projection manifest seed / production DB-backed external port policy read repository は追加済みだが、DB repository atomic encrypted credential payload update / consumer bundle wiring / canonical physical binding execution は未完了。

## 目的

外部連携 8 bundle を、standalone credential 管理 plane ではなく `external_port_substrate` 上の port record consumer として実装する。

## 実装方針

- `topology.external_access_ports`, `topology.external_response_ports`, `topology.external_hook_ports` を実装する。
- `topology.external_credential_vault` / `topology.external_credential_refresh_attempt` は minimal DDL 済み。DB repository atomic encrypted_payload + token_hash + expires_at/version update 実装は残作業。
- `IExternalPortPolicyRepository` の production Npgsql read substrate は実装済み。active port/policy を fail-close で読み、provider_kind は DB data として扱う。
- `topology.physical_tables` catalog と external port tables の登録 / bootstrap / seed 整合を実装する。現状の `physical_binding` topology entry は seed/projection marker であり、canonical `screen_data_shape.tableRef` / `dbTableName` -> `topology.wiring_physical_to_package` binding execution は残作業として扱う。
- `credential_kind` (`auth` / `external` / `none`), `port_kind` (`access_port` / `response_port` / `hook_port`), `provider_kind`, `port_setting_projection`, `consumer_bundle_binding`, `credential_requirement` を DB seed / projection で解決できるようにする。
- [x] admin 権限の projection 側管理画面で、port record context 内の credential_kind / provider_kind / reference_key / required_by_bundle / consumer_bundle_binding を fixed-form topology / manifest / screen_data_shape / Step 2.5 relation projection として seed 実装する。
- backend は provider 別 hardcode ではなく、汎用 access_port connect / response_port connect / hook_port receive / port record resolution のみを持つ。
- file_storage / email / stripe / webhook_inbox / job_scheduler / audit_approval / export_sftp / credential requirement substrate を consumer として接続する。

## 禁止

- provider 別・bundle 別 projection hardcode
- backend 側 standalone credential 管理 plane
- dedicated credential route / panel
- provider 別 runtime execution
- raw credential plaintext persistence in DB / UI / SSOT / logs（provider再提示が必要な場合の DB guarded encrypted_payload は例外）

## 対象ファイル候補

- `db/schema.sql`
- `db/topology_tables.sql`
- `db/init.sql`
- `db/seed_empty.sql`
- `docs/design/db-schema.yaml`
- `backend/runtime/**`
- `backend/repository/**`
- `frontend/runtime/**`
- `frontend/islands/**`
- `frontend/components/**`
- `.agent/tests/**`

## 対象 surface / function

- `admin_setting_projection`
- `seed_projection_resolution`
- `generic_access_port_connect_function`
- `generic_response_port_connect_function`
- `generic_hook_port_receive_function`
- `db_seed_resolved_port_record_resolution_for_consumer_runtime`
- `physical_table_row_validate_preview_apply_boundary`

## Consumer bundle implementation

### file_storage_bundle

- access_port / response_port binding を seed / DB record / projection に追加する。
- object storage credential_kind を external として port record に付属させる。
- export_job -> port record resolution -> generic access/response port connect の経路を実装する。

### email_bundle

- response_port (`provider_kind: smtp`) binding を seed / DB record / projection に追加する。
- SMTP credential_kind を external として port record に付属させる。
- UI approval -> port record resolution -> generic response_port connect の経路を実装する。

### stripe_bundle

- hook_port (`provider_kind: stripe`) binding を seed / DB record / projection に追加する。
- webhook credential_kind を external として hook_port に付属させる。
- hook_path / provider / header / route key -> port record resolution -> scheduler aligned runtime event の経路を実装する。

### webhook_inbox_bundle

- hook_port binding を seed / DB record / projection に追加する。
- webhook credential_kind を external として hook_port に付属させる。
- hook_port -> scheduler 境界の受信経路を実装する。

### job_scheduler_bundle

- access_port / hook_port binding を seed / DB record / projection に追加する。
- external / none credential_kind を port record に付属させる。
- built-in scheduler path が port substrate に依存しないことをテストする。

### audit_approval_bundle

- response_port binding を seed / DB record / projection に追加する。
- approval notification credential_kind を port record に付属させる。
- approval -> port record resolution -> generic response_port connect の経路を実装する。

### export_sftp_bundle

- response_port (`provider_kind: sftp`) binding を seed / DB record / projection に追加する。
- SFTP credential_kind を external として response_port に付属させる。
- export_job -> port record resolution -> generic response_port connect の経路を実装する。
- checksum 検証境界を port substrate と独立して実装 / テストする。

### credential requirement substrate

- standalone bundle として実装しない。
- `credential_requirement` seed / projection / port record attachment として扱う。

## Required checks

- `bash .agent/tests/check-worktype-routing.sh`
- `bash .agent/tests/check-completion-judgment.sh`
- `bash .agent/tests/check-runtime-bundle-ssots.sh`
- `bash .agent/tests/check-structure.sh`

## Bundle increment `external-port-substrate-seed-coding`

Status: partial
Parent bundle: `external-port-substrate-implementation`

問題点:
- credential vault / generic refresher skeleton exists, but access_port / response_port / hook_port records and DB policy-step seed execution surface were not yet represented as runnable substrate.
- provider-specific runtime handlers remain prohibited; provider_kind must stay seed/record data rather than C# control flow.

目的:
- Add DB seed-driven external port physical tables and generic ordered policy-step runtime substrate so consumer bundles can later bind through records instead of provider-specific services.

改善方針:
- Add minimal physical tables for `topology.external_access_ports`, `topology.external_response_ports`, `topology.external_hook_ports`, `topology.external_port_policies`, and `topology.external_port_policy_steps`.
- Add seed policy rows whose `operation_key` values are constrained to the external-port SSOT allowed set.
- Add generic resolver/executor C# records and interfaces; execution dispatch is by operation_key registry only.
- Add production Npgsql `IExternalPortPolicyRepository` read substrate for active port records and policy steps without plaintext credential projection.
- Keep hook policies at scheduler enqueue boundary; do not directly execute webhook runtime.

対応資料:
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`
- `docs/design/auth-db-session-credential-ssot.yaml`
- `docs/design/extended-runtime-bundle-registry-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`

対象ファイル名:
- `docs/design/external-port-substrate-ssot.yaml`
- `db/topology_tables.sql`
- `db/seed_empty.sql`
- `backend/runtime/ExternalPortCredentialRefresher.cs`
- `backend/tests/Topolactor.Runtime.Tests/ExternalPortCredentialRefresherTests.cs`
- `.agent/tests/check-external-port-substrate-seed-coding.sh`
- `.agent/tasks/todo.md`
- `.agent/tasks/external-port-substrate-implementation-todo.md`

対象関数名またはruntime境界名:
- `ExternalPortRecord`
- `ExternalPortPolicy`
- `ExternalPortPolicyStep`
- `IExternalPortResolver`
- `IExternalPortPolicyRepository`
- `IExternalPortCredentialReferenceResolver`
- `IExternalPortPolicyStepExecutor`
- `ExternalPortResolver.ResolveAsync`
- `ExternalPortResolver.FailCloseOnInvalidPortRecord`
- `ExternalPortPolicyStepExecutor.ExecutePolicyAsync`
- `ExternalPortPolicyStepExecutor.ExecuteAsync`

remaining_todo:
- DB-backed `IExternalPortPolicyRepository` production read implementation is implemented for active port records, active policies, and ordered active policy steps.
- DB repository atomic encrypted credential payload update remains in the parent credential-vault bundle.
- Admin setting projection, validate/preview/apply integration, and consumer bundle wiring remain out of scope for this partial increment.


## Bundle increment `auth-external-credential-management-topology-projection`

Status: implemented
Parent bundle: `external-port-substrate-implementation`

問題点:
- external port physical tables and generic policy-step seed existed, but auth / external credential management had no fixed-form topology projection tying auth boundary, external port context, policy template selection, and validate-preview-apply boundaries together.

目的:
- Establish credential management as an existing manifest projection surface, not as a UI Builder component/preset, dedicated credential route/panel, or physical-table generic row editor.

実装内容:
- Seed active manifest `auth.external.credential_management.projection` with screen_data_shape logical tables for `external_port_context` and `policy_template_selection`.
- Reuse Step 2.5 relationIntents to join `external_port_context.auth_user_id` to active `auth.user.boundary` remote target manifest `00000000-0000-0000-0000-000000000091`.
- Expose credential metadata only: `credential_kind`, `provider_kind`, `reference_key`, `required_by_bundle`, `port_kind`, `consumer_bundle_binding`, and policy template key.
- Mark draft-edit / validate-preview-apply / no-UIBuilder-authority / no-physical-row-editor / policy-template-selection-only boundaries in manifest topology.
- Classify the `physical_binding` topology entry as a seed/projection marker; canonical physical binding execution through `screen_data_shape.tableRef` / `dbTableName` and `topology.wiring_physical_to_package` remains TODO.
- Add `.agent/tests/check-auth-external-credential-projection.sh` guard for projection presence, Step 2.5 relation, secret marker exclusion, and forbidden UI Builder / route / panel escapes.

remaining_todo:
- DB-backed `IExternalPortPolicyRepository` production read implementation is implemented for active port records, active policies, and ordered active policy steps.
- DB repository atomic encrypted credential payload update remains in the parent credential-vault bundle.
- Canonical physical binding execution remains out of scope for this increment.
- Consumer bundle wiring remains out of scope for this increment.
