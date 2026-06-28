# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `instance-port-substrate` | credential-backed instance connection / instance function call substrate | acceptance_pending | 1 | `product.instance_port_substrate` | `docs/design/instance-port-substrate-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 3 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

---

## Bundle `instance-port-substrate`

**Status:** acceptance_pending
**Roadmap/status SSOT:** `product.instance_port_substrate`
**Primary SSOT:** `docs/design/instance-port-substrate-ssot.yaml`

目的:
credential-backed instance connection / instance function call を external_port_substrate と混同せず、sibling substrate として設計・実装する。SSOT 上の設計判断は閉じており、残作業は runtime / primitive / execution authority 実装と guard 整合。

PR522で実装済みとして扱う範囲:
- `credential-management-instance-settings-topology`
- 既存 `auth.external.credential_management.projection` の `instance_settings` category 拡張
- public-safe JSON template download/import 境界
- `db_instance_port` / `runtime_instance_port` / `instance_connection_policy` / `instance_operation_authority_binding` の seed/data-defined projection
- Admin UI Builder Design Inspector の `dispatchInstanceOperation` / `instanceTargetRef` approved candidate authoring 境界
- `jsonTemplateShape` authority化禁止 / free-text targetRef 禁止 / runtime seed file scanning 禁止 guard

残問題:
- runtime execution substrate（DDL / repository / instance_port_runtime / primitive adapters / fail-close tests）は実装済み。残scopeは、runtime-only secret material を外部投入した live generic instance integration acceptance の確認のみ。

改善方針:
implementation_change で、SSOT通りに `instance_port_runtime` を `external_port_runtime` の sibling lane として追加し、manifest-authorized operation binding / function/schema allowlist / timeout / result sanitize / fail-close を実装する。`call_postgres_function` の Topolactor DB `topology.*` 専用境界は維持する。provider_kind / required_by_bundle / provider label で C# selector を作らない。

対応資料:
- `docs/design/instance-port-substrate-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/admin-console-workflow-ssot.yaml`
- `.agent/tasks/instance-port-substrate-implementation-todo.md`
- `.agent/tests/check-instance-port-substrate.sh`

対象ファイル名候補:
- `db/topology_tables.sql` (runtime-executable instance records / authority binding DDL)
- `backend/runtime/AbstractFunctionRuntime.cs` (instance primitive adapter wiring)
- `backend/runtime/*InstancePort*` (new sibling runtime lane)
- `backend/repository/*InstancePort*` (policy / authority / credential reference read surface)
- `backend/tests/Topolactor.Runtime.Tests/*InstancePort*` or related runtime tests
- `.agent/tests/check-instance-port-substrate.sh` (runtime/primitive guard extension)

対象関数名候補:
- future `InstancePortDispatchRuntime.DispatchAsync`
- future `CallInstancePostgresFunctionPrimitiveAdapter.ExecuteAsync`
- future `CallBoundInstanceFunctionPrimitiveAdapter.ExecuteAsync`
- future `ResolveInstancePortRecordAsync`
- future `ResolveInstanceCredentialReferenceAsync`
- future `VerifyInstanceConnectionPolicyAsync`
- future `VerifyInstanceOperationAuthorityBindingAsync`
- future `SanitizeInstanceFunctionResultAsync`

NG軸:
- external_port_substrate の access_port / response_port / hook_port へ DB/runtime instance connection を混入する
- `instance` を external_port `credential_kind` に追加する
- `call_postgres_function` の `^topology\.` / Topolactor DB connectionString 制限を汎用化する
- frontend payload / seed payload / projection / log / JSON download に DB connection string, endpoint 実値, raw SQL, secret, runtime-only material, table authority, function authority を入れる
- provider_kind / required_by_bundle / provider label 文字列で C# if/switch 分岐する
- provider-specific runtime handler を第一候補にする
- provider-specific schema / external instance semantic authority を Topolactor DB に作る
- external instance を Topolactor runtime SSOT として扱う
- dedicated credential admin UI / standalone credential plane / dedicated credential route/panel を作る
- 手書き admin UI / hardcoded targetRef / hardcoded action button で Gate 0 の seed/data-defined surface を迂回する

残受入条件:
- [x] runtime-executable instance port DDL / seed rows / repository read surface が SSOT に従って追加されている。
- [x] `instance_port_runtime` が `external_port_runtime` の sibling lane として追加されている。
- [x] `call_instance_postgres_function` は manifest-authorized function のみ実行し、function/schema allowlist / instance authority binding / timeout / result sanitize / fail-close を持つ。
- [x] `call_bound_instance_function` は instance operation authority binding / output shape / secret-deny projection を必須にする。
- [x] `call_postgres_function` は Topolactor DB 内 `topology.*` 専用のまま保持されている。
- [x] credential は `reference_key` / DB guarded vault / runtime secret resolver 経由で runtime-only 解決され、plaintext connection string は SSOT / seed / UI / projection / log に出ない。
- [x] provider_kind / required_by_bundle は data only で C# selector ではない。
- [x] 特定 consumer 専用 handler / schema / semantic authority を追加していない。
- [x] missing credential / missing instance policy / missing authority binding / unauthorized function / timeout / secret result denial / provider selector attempt の fail-close tests がある。
- [ ] live generic instance integration acceptance を runtime-only secret material の外部投入で確認する。

---

## Bundle `helper-manual`

**Status:** not_started
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

SSOT 上、helper/manual category candidates は実装ではなく方針整理。site page / UI component / help screen component 実装は explicitly out of scope。

- [ ] helper/manual category candidates を user promise / safety boundary / onboarding policy として整理する（ページ・コンポーネント実装はしない）
- [ ] Desktop AI / CLI / MCP Reader 向けに、plain business language と approval boundary のライティング方針を整理する
- [ ] 現行 external_port_substrate 実装で user-facing に案内可能な範囲を監査し、`external_bundle_guide` へ JSON 受け渡し型の外部 surface 例として整理する。Notion / Google Sheets / Slack / GitHub Issues / generic webhook / external REST API は入力・通知・承認・作業依頼 surface の例として案内してよいが、専用 connector 実装済み・iframe/frame 埋め込み・外部 service の runtime SSOT 化・外部 tool direct DB write・provider-specific runtime/client/handler は案内しない。対応資料: `docs/design/user-facing-helper-manual-ssot.yaml`, `docs/design/external-port-substrate-ssot.yaml`, `docs/design/extended-runtime-bundle-registry-ssot.yaml`。

---

## Bundle `product-nocode-loop-acceptance`

**Status:** acceptance_pending
**Roadmap/status SSOT:** `docs/system-roadmap.yaml`

実装 bundle ではなく、統合 UX の手動受入 / hand-debug evidence gap。runtime dispatch loop、ProjectionShell SSE refresh、recommend child island、SQL Attention feedback projection、admin CSV/JSON import、admin authoring routes は実装済みとして扱い、未実装扱いに戻さない。

- [ ] `product.dynamic_support_nocode_loop` の combined UX を、authoring guidance → SQL Attention feedback → M6 admin loop の通し手動受入 / hand-debug で確認する

---
