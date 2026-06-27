# Instance Port Substrate Implementation TODO

Status: not_started
Roadmap bundle: `product.instance_port_substrate`
Primary SSOT: `docs/design/instance-port-substrate-ssot.yaml`

This file preserves future implementation work. It is not evidence of implemented runtime behavior.

## Bundle-level future work

- [ ] Add DB schema / seed surfaces for `db_instance_port`, `runtime_instance_port`, `instance_connection_policy`, and `instance_function_authority_binding` without plaintext connection strings or endpoint real values.
- [ ] Add `credential-management-instance-settings-topology` by extending the existing `auth.external.credential_management.projection` seed with instance settings. Do not create a standalone credential plane or dedicated credential route/panel.
- [ ] Add `instance_port_runtime` as a sibling runtime lane to `external_port_runtime`.
- [ ] Add abstract function primitive support for `call_instance_postgres_function` and `call_bound_instance_function` with manifest-authorized function/schema/instance/output bindings.
- [ ] Keep existing `call_postgres_function` limited to Topolactor DB `topology.*` functions and fixed Topolactor connectionString.
- [ ] Reuse the DB guarded vault / runtime secret reference model by `reference_key`; do not create a standalone credential runtime or admin UI.
- [ ] Add fail-close tests for missing credential, missing instance policy, missing function/schema binding, timeout, secret projection denial, provider selector attempts, and unauthorized function names.
- [ ] Add guards proving `provider_kind`, `required_by_bundle`, and provider labels are data only and do not select C# handlers.
- [ ] Treat multiple external DB/runtime instances as consumer-agnostic `instance_authority_key` rows only; do not add provider-specific runtime handlers, provider-specific schemas, or external instance semantic authority to the Topolactor DB.

## Bundle increment `credential-management-instance-settings-topology`

Status: not_started
Parent bundle: `instance-port-substrate`

問題点:
- `auth.external.credential_management.projection` は manifest `00000000-0000-0000-0000-000000000092` として `db/seed_empty.sql` に seed 済みで、user/auth boundary manifest `00000000-0000-0000-0000-000000000091` と external credential context / policy template selection は fixed-form projection として存在する。
- しかし instance settings は同 credential management projection / topology に未接続。
- 現行 instance-port SSOT は runtime lane / DDL / primitive / authority binding に寄っており、既存 credential management projection を user/auth・external・instance の同型切替へ拡張する seed / guard / authoring boundary が未実装。

目的:
- 既存 credential management projection を拡張し、`user_auth` / `external` / `instance_settings` を select / mode / category で切り替える。
- `instance_settings` で `db_instance_port` / `runtime_instance_port` / `instance_connection_policy` / `instance_operation_authority_binding` を扱う。
- 新規 standalone credential plane / dedicated credential route / dedicated panel / provider-specific UI は作らない。

改善方針:
- `db/seed_empty.sql` の manifest `092` `auth.external.credential_management.projection` または後継 compatible manifest に、instance settings category を seed する。
- 既存 fixed-form projection / `screen_data_shape` / relationIntents / validate-preview-apply 境界を再利用し、UI は可能な限り user/auth・external と同型にする。
- JSON template download / JSON import / registered Runtime-DB list-edit / operation list-edit / validate-preview-apply を credential management projection の instance settings category として定義する。
- seed-aligned JSON template は構造情報漏洩を防ぐため、secret / endpoint実値 / connection string / raw SQL / private key / runtime-only decrypted payload / executable authority を出さない。
- Admin UI Builder / Design Inspector は新画面やinstance設定editorではなく、既存 Event 設定語彙に `dispatchInstanceOperation` / `instanceTargetRef` を追加し、approved instance operation の `trigger` / `payloadFrom` / `outputProp` 割当だけに限定する。

対応資料:
- `docs/design/instance-port-substrate-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-bundle-secret-credential-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `db/seed_empty.sql`
- `.agent/tests/check-auth-external-credential-projection.sh`
- `.agent/tests/check-instance-port-substrate.sh`

対象ファイル名:
- `db/seed_empty.sql`
- `db/topology_tables.sql`
- `docs/design/instance-port-substrate-ssot.yaml`
- `.agent/tests/check-instance-port-substrate.sh`
- `backend/runtime/AdminRuntime.cs`
- `backend/repository/UiTopologyRepository.cs`
- `backend/repository/NpgsqlUiTopologyRepository.cs`
- `frontend/api/adminApi.ts`
- `frontend/lib/packageWiringOptions.ts`
- `frontend/lib/packageWiringPicker.ts`
- `frontend/islands/UiBuilderAdmin.tsx`

対象関数名:
- `AdminRuntime.ExecuteDataAsync`
- `DataUpdatePackageWiringAsync`
- `DataListExternalPortAuthoringCandidatesAsync`
- `UpdatePackageWiringAsync`
- `ListExternalPortAuthoringCandidatesAsync`
- future: `ListInstanceSettingsCandidatesAsync`
- future: `ValidateInstanceSettingsImportAsync`
- future: `ApplyInstanceSettingsImportAsync`

NG軸:
- `instance` を external_port `credential_kind` に追加する
- standalone credential management plane / dedicated credential route / dedicated panel を追加する
- provider_kind / required_by_bundle で C# if/switch selector を作る
- secret / endpoint実値 / connection string / raw SQL / private key / runtime-only decrypted payload を seed / projection / log / JSON download に出す
- Admin UI Builder / Design Inspector の Event 設定に instance function definition / address edit / schema edit / raw SQL edit / credential edit を持たせる

受入条件:
- [ ] existing credential management projection 系で `user_auth` / `external` / `instance_settings` を select / mode / category 切替できる。
- [ ] `instance` は external_port `credential_kind` ではなく instance settings category として扱う。
- [ ] `db_instance_port` / `runtime_instance_port` / operation binding / connection policy が seed/data-defined に扱われる。
- [ ] JSON template download/import が secret・endpoint実値・connection string・raw SQL・executable authority を出さない。
- [ ] approved operation 以外は Admin UI Builder Design Inspector の Event action candidate に出ない。
- [ ] dedicated credential route / standalone credential plane / provider-specific handler を追加していない。
- [ ] `.agent/tests/check-instance-port-substrate.sh` が existing credential management projection extension / instance_settings category / JSON template leak guard / Design Inspector event-authoring scope boundary を検出する。

## Explicitly out of scope for the SSOT wiring PR

- `AbstractInstanceRuntimeHandler`
- `InstancePortDispatchRuntime`
- `CallInstancePostgresFunctionPrimitiveAdapter`
- instance port DDL / seed rows
- external instance SQL function implementation
- provider-specific integration runtime
