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
- `frontend/lib/packageWiringOptions.ts` (future Design Inspector event target surface candidate vocabulary only after SSOT is updated)
- `frontend/lib/packageWiringPicker.ts` (future Design Inspector event targetRef helper only after SSOT is updated)
- `frontend/islands/UiBuilderAdmin.tsx` (future Design Inspector event candidate/wiring surface only after SSOT is updated)
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
- admin contents / Design Inspector event wiring / PackageWiringEditor action wiring を未設計のまま runtime lane / primitive だけ実装して implemented 扱いにする
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
- [ ] admin contents / Admin UI Builder Design Inspector event candidate / PackageWiringEditor targetRef / projection registration の接続方針が SSOT 上で定義され、Gate 0 の admin_authoring_completion_gate を満たす。
- [ ] admin projection は credential reference / policy / authority binding を data-defined に扱い、secret / endpoint / connection string / raw SQL / function authority を projection しない。
- [ ] Admin UI Builder Design Inspector event authoring は approved instance operation の `trigger` / `payloadFrom` / `outputProp` 割当だけを扱い、instance function definition / address edit / schema edit / raw SQL edit / credential edit を持たない。
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
