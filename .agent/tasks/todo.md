# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `instance-port-substrate` | credential-backed instance connection / instance function call substrate | not_started | 3 | `product.instance_port_substrate` | `docs/design/instance-port-substrate-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `cli-mcp-dispatch-secured-port` | CLI/MCP read/export/import-candidate port 親境界 | partial | 5 subBundles | `product.external_port_substrate` / `product.core_runtime_route` | `docs/design/cli-model-context-protocols-port-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

---

## Bundle `instance-port-substrate`

**Status:** not_started
**Roadmap/status SSOT:** `product.instance_port_substrate`
**Primary SSOT:** `docs/design/instance-port-substrate-ssot.yaml`

目的:
credential-backed instance connection / instance function call を external_port_substrate と混同せず、sibling substrate として設計・実装する。SSOT 上の設計判断は閉じており、残作業は Bundle 単位の実装・guard 整合。

監査追記: credential management instance settings topology 未実装:
- 問題点: `auth.external.credential_management.projection` は manifest `00000000-0000-0000-0000-000000000092` として `db/seed_empty.sql` に seed 済みで、user/auth boundary manifest `00000000-0000-0000-0000-000000000091` と external credential context / policy template selection は fixed-form projection として存在する。しかし `instance_settings` は同 credential management projection / topology に未接続。
- 目的: 既存 credential management projection を拡張し、`user_auth` / `external` / `instance_settings` を select / mode / category で切り替える。instance settings では `db_instance_port` / `runtime_instance_port` / `instance_connection_policy` / `instance_operation_authority_binding`、JSON template download/import、registered Runtime/DB list-edit、operation list-edit、validate-preview-apply を扱う。
- 改善方針: implementation_change で hub relation / projection topology / `screen_data_shape` / relationIntents / admin action wiring / guard を追加する。新規 standalone credential plane / dedicated credential route/panel / provider-specific UI は作らない。JSON template は owner-reviewed decision に従い public-safe shape を出してよいが、secret / endpoint実値 / connection string / raw SQL / runtime-only material / approval bypass authority を出さない。
- 対応資料: `docs/design/instance-port-substrate-ssot.yaml`, `docs/design/external-port-substrate-ssot.yaml`, `docs/design/runtime-bundle-secret-credential-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `.agent/tasks/instance-port-substrate-implementation-todo.md`, `.agent/tests/check-instance-port-substrate.sh`, `.agent/tests/check-auth-external-credential-projection.sh`
- 対象ファイル名: `db/seed_empty.sql`, `db/topology_tables.sql`, `.agent/tests/check-instance-port-substrate.sh`, `backend/runtime/AdminRuntime.cs`, `backend/repository/UiTopologyRepository.cs`, `backend/repository/NpgsqlUiTopologyRepository.cs`, `frontend/api/adminApi.ts`, `frontend/lib/packageWiringOptions.ts`, `frontend/lib/packageWiringPicker.ts`, `frontend/islands/UiBuilderAdmin.tsx`
- 対象関数名: `AdminRuntime.ExecuteDataAsync`, `DataUpdatePackageWiringAsync`, `DataListExternalPortAuthoringCandidatesAsync`, `UpdatePackageWiringAsync`, `ListExternalPortAuthoringCandidatesAsync`, future `ListInstanceSettingsCandidatesAsync`, future `ValidateInstanceSettingsImportAsync`, future `ApplyInstanceSettingsImportAsync`

対応資料:
- `docs/design/instance-port-substrate-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `.agent/tasks/instance-port-substrate-implementation-todo.md`
- `.agent/tests/check-instance-port-substrate.sh`

対象ファイル名候補:
- `db/topology_tables.sql` (future DDL only; current PR does not add instance tables)
- `db/seed_empty.sql` (credential management projection extension / instance_settings category seed)
- `backend/runtime/AbstractFunctionRuntime.cs` (future primitive adapter only)
- `backend/runtime/*InstancePort*` (future runtime lane only)
- `backend/repository/*InstancePort*` (future policy repository only)
- `frontend/lib/packageWiringOptions.ts` (future admin/UI Builder target surface vocabulary only after SSOT is updated)
- `frontend/lib/packageWiringPicker.ts` (future admin/UI Builder targetRef helper only after SSOT is updated)
- `frontend/islands/UiBuilderAdmin.tsx` (future PackageWiringEditor candidate/wiring surface only after SSOT is updated)
- `backend/runtime/AdminRuntime.cs` (future admin action candidate/wiring read surface only after SSOT is updated)
- `backend/repository/UiTopologyRepository.cs` / `backend/repository/NpgsqlUiTopologyRepository.cs` (future candidate read/update repository surface only after SSOT is updated)

NG軸:
- external_port_substrate の access_port / response_port / hook_port へ DB/runtime instance connection を混入する
- `instance` を external_port `credential_kind` に追加する
- `call_postgres_function` の `^topology\.` / Topolactor DB connectionString 制限を汎用化する
- frontend payload / seed payload / projection / log / JSON download に DB connection string, endpoint 実値, raw SQL, secret, runtime-only material, table authority, function authority を入れる
- provider_kind / required_by_bundle / provider label 文字列で C# if/switch 分岐する
- provider-specific runtime handler を第一候補にする
- provider-specific schema / external instance semantic authority を Topolactor DB に作る
- external instance を Topolactor runtime SSOT として扱う
- admin contents / UI Builder / PackageWiringEditor / action wiring を未設計のまま runtime lane / primitive だけ実装して implemented 扱いにする
- dedicated credential admin UI / standalone credential plane / dedicated credential route/panel を作る
- 手書き admin UI / hardcoded targetRef / hardcoded action button で Gate 0 の seed/data-defined surface を迂回する

受入条件:
- [ ] instance port DDL / seed / repository / runtime lane が SSOT に従って追加されている。
- [ ] 既存 credential management projection 系で `user_auth` / `external` / `instance_settings` を select / mode / category 切替できる。
- [ ] `instance` は external_port `credential_kind` ではなく instance settings category として扱われる。
- [ ] JSON template download/import は public-safe shape のみを扱い、secret・endpoint実値・connection string・raw SQL・runtime-only material・approval bypass authority を出さない。
- [ ] `call_instance_postgres_function` は manifest-authorized function のみ実行し、function/schema allowlist / instance authority binding / timeout / result sanitize / fail-close を持つ。
- [ ] `call_postgres_function` は Topolactor DB 内 `topology.*` 専用のまま保持されている。
- [ ] credential は `reference_key` / DB guarded vault / runtime secret resolver 経由で runtime-only 解決され、plaintext connection string は SSOT / seed / UI / projection / log に出ない。
- [ ] provider_kind / required_by_bundle は data only で C# selector ではない。
- [ ] 特定 consumer 専用 handler / schema / semantic authority を追加していない。
- [ ] admin contents / UI Builder / PackageWiringEditor / admin action candidate / targetRef / projection registration の接続方針が SSOT 上で定義され、Gate 0 の admin_authoring_completion_gate を満たす。
- [ ] admin projection は credential reference / policy / authority binding を data-defined に扱い、secret / endpoint / connection string / raw SQL / function authority を projection しない。
- [ ] UI Builder / Design Inspector は approved instance operation の `trigger` / `payloadFrom` / `outputProp` 割当だけを扱い、instance function definition / address edit / schema edit / raw SQL edit / credential edit を持たない。
- [ ] `.agent/tests/check-instance-port-substrate.sh` と必要な runtime/backend/frontend/admin wiring tests が追加・通過している。

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

## Bundle `cli-mcp-dispatch-secured-port`

**Status:** partial
**Roadmap/status SSOT:** `product.external_port_substrate` / `product.core_runtime_route`
**Primary SSOT:** `docs/design/cli-model-context-protocols-port-ssot.yaml`
**Referenced SSOT:** `docs/design/external-port-substrate-ssot.yaml` / `docs/design/runtime-orchestration-ssot.yaml` / `docs/system-roadmap.yaml`

役割:
MCP/CLI read/export/import-candidate port 全体の親境界。実装対象ではなく、SSOT セクション単位の subBundle 索引と共通 NG 軸を持つ。旧 `cli-mcp-dispatch-secured-read-export-port` は単一巨大実装 Bundle としては扱わない。

共通NG軸:
- direct DB connection / direct SQL execution
- Core API 直叩き / dedicated backend handler / dispatch bypass
- 未認証 CLI/MCP access / unauthorized scope expansion
- credential read/export / plaintext credential response
- AI/CLI/MCP による DB commit / approval / delete / payment / email send 実行
- 外部AI構造化出力の正本扱い
- read/export と import-candidate を同一実装 Bundle に混ぜる

推奨実装順序:
1. `cli-mcp-read-scope-port`
2. `cli-mcp-export-job-port`
3. `cli-mcp-file-stream-port`
4. `cli-mcp-import-candidate-port`
5. `cli-mcp-surface-metadata-port`

順序理由:
read/export は比較的安全な read-side boundary。import-candidate は external AI structured output を扱うため、正本化禁止・approval 前 commit 禁止・preview diff/evidence 境界を先に閉じる必要がある。そのため read/export と import-candidate を同一実装 Bundle に混ぜない。

### SubBundle `cli-mcp-read-scope-port`

**Status:** implemented
**対応SSOTファイル:** `docs/design/cli-model-context-protocols-port-ssot.yaml` / `docs/design/runtime-orchestration-ssot.yaml`
**対応SSOTセクション名:** `core_invariant.canonical_read_export_lane` / `api_responsibility` / `data_reader_responsibility` / `admin_ui_configuration`

目的:
- user auth/authz fail-close
- `cli_reader_port` scope resolution
- allowed tables / columns / filters / periods / roles / users の解決
- credential/capability requirement resolution
- ManifestDispatcher/runtime dispatch 解決済み request のみ Data Reader へ受け入れる
- read/search/aggregate/analyze/validate の authorized read model 化

対象ファイル名候補:
- `backend/runtime/ManifestDispatcher.cs`
- `backend/runtime/RuntimeExecutor.cs`
- `backend/runtime/TopologyFunctionBinder.cs`
- `backend/repository/*Cli*` / `backend/repository/*Reader*`
- `backend/Program.cs` (MCP/API port entry only; dedicated dispatch bypass route は不可)
- `db/topology_tables.sql` (port config/audit surface only when SSOT-backed)
- `db/seed_empty.sql` (admin config projection seed only when SSOT-backed)

NG軸:
- direct DB connection
- direct SQL execution
- dispatch bypass
- unauthorized read scope expansion
- Context API / Data Reader を dispatch 解決なしで実行
- credential/capability requirement を plaintext credential 渡しにする

受入条件:
- [x] read/search/aggregate/analyze/validate は user auth/authz → scope resolution → credential/capability requirement resolution → ManifestDispatcher/runtime dispatch → Data Reader/authorized read model を必ず通る。
- [x] Data Reader は dispatch resolved request のみ受け付け、未解決 request を fail-close する。
- [x] allowed tables / columns / filters / periods / roles / users / row scope が admin config 由来で解決される。
- [x] direct DB connection / direct SQL / Core API direct call / dedicated handler bypass の guard または tests がある。
- [x] audit_log / runtime_event_log 境界を skip しない。

Implemented evidence update (2026-06-27): authorized read scope runtime substrate exists for read/search/aggregate/analyze/validate with fail-close auth/scope/capability checks, dispatch-resolved request guard, sanitized runtime event append, DDL/seed admin/runtime surfaces, Program.cs ManifestDispatcher handler registration, and guard/unit tests. PR515後main監査で検出された SSOT 内整合漏れは、runtime-orchestration SSOT の `backend_runtime_destinations` と `backend_dispatchable_kinds` の両方に `cli_reader_port_runtime` を揃え、再発検出 check を追加して解消済み。Parent bundle remains partial by subBundle index because export/import/file-stream subBundles remain out of scope.

out_of_scope:
- export_job 生成 / file generation / manifest/checksum 生成
- file stream / direct file download
- import_structured_output / draft_operation / commit_candidate
- DB commit / approval / delete / payment / email send
- browser UI automation

### SubBundle `cli-mcp-export-job-port`

**Status:** implemented
**対応SSOTファイル:** `docs/design/cli-model-context-protocols-port-ssot.yaml`
**対応SSOTセクション名:** `export_job` / `manifest` / `data_reader_responsibility`

目的:
- `create_export_job`
- export ledger 記録
- manifest generation
- csv/json/pdf/zip generation
- checksum generation
- `source_record_ids` capture

対象ファイル名候補:
- `backend/runtime/ManifestDispatcher.cs`
- `backend/runtime/RuntimeExecutor.cs`
- `backend/repository/*Export*`
- `backend/repository/*Reader*`
- `db/topology_tables.sql` (export_job / manifest ledger only when SSOT-backed)
- `db/seed_empty.sql` (export format/admin projection only when SSOT-backed)

NG軸:
- export without export_job
- export without manifest/checksum
- unauthorized bulk export
- source_record_ids なしの搬出
- export_job / audit_log / runtime_event_log skip

受入条件:
- [x] export は必ず authorized read scope に基づく `export_job` として記録される。

Implemented evidence update (2026-06-27): `create_export_job` is handled inside `AuthorizedCliReaderPortRuntime` only after ManifestDispatcher supplies a manifest id and the existing cli_reader_port auth/scope/capability checks pass. The runtime reads through `AuthorizedCliReaderQuery`, requires config-resolved port_id, idempotency key, and csv/json/pdf/zip format, rejects missing source_record_ids, generates csv/json/pdf/zip format-distinct packages with file-byte checksums, and persists export job ledger/manifest/checksum/generated_files plus one-time `runtime_event_log` evidence through `CliReaderPortRepository`. Npgsql/DDL guard coverage verifies config-derived `port_id`, `create_export_job` runtime-event CHECK support, export_job_id-based manifest URI, package-byte checksum semantics, idempotent retry via existing job/manifest/checksum/evidence, and existing-DB ALTER safeguards for generated_files/approval/status fields. Guard/unit tests cover manifest/checksum/source ids and detect import-candidate/approval scope creep. Parent bundle remains partial because import-candidate and surface-metadata subBundles remain out of scope.
