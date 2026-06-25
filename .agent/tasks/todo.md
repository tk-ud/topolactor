# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `instance-port-substrate` | credential-backed instance connection / instance function call substrate | not_started | 1 | `product.instance_port_substrate` | `docs/design/instance-port-substrate-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `cli-mcp-dispatch-secured-read-export-port` | CLI/MCP dispatch-secured read/export/import-candidate port 実装 | not_started | 1 | `product.external_port_substrate` / `product.core_runtime_route` | `docs/design/cli-model-context-protocols-port-ssot.yaml` |
| `job-scheduler-port-consumer` | external scheduler ingress receiver port substrate 接続実装 | partial | 1 | `product.job_scheduler_port_consumer` | `docs/design/runtime-bundle-job-scheduler-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。scheduler consumer (job_scheduler) は built-in/external port seed binding が完了済み (内蔵 scheduler は port substrate 非依存)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

---

## Bundle `instance-port-substrate`

**Status:** not_started
**Roadmap/status SSOT:** `product.instance_port_substrate`
**Primary SSOT:** `docs/design/instance-port-substrate-ssot.yaml`

目的:
credential-backed instance connection / instance function call を external_port_substrate と混同せず、sibling substrate として設計・実装する。今回の設計配線では実装本体は未着手のまま残す。

対応資料:
- `docs/design/instance-port-substrate-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `.agent/tasks/instance-port-substrate-implementation-todo.md`
- `.agent/tests/check-instance-port-substrate.sh`

対象ファイル名候補:
- `db/topology_tables.sql` (future DDL only; current PR does not add instance tables)
- `backend/runtime/AbstractFunctionRuntime.cs` (future primitive adapter only)
- `backend/runtime/*InstancePort*` (future runtime lane only)
- `backend/repository/*InstancePort*` (future policy repository only)

NG軸:
- external_port_substrate の access_port / response_port / hook_port へ DB/runtime instance connection を混入する
- `call_postgres_function` の `^topology\.` / Topolactor DB connectionString 制限を汎用化する
- frontend payload / seed payload / projection / log に DB connection string, endpoint 実値, raw SQL, table authority, function authority を入れる
- provider_kind / required_by_bundle / provider label 文字列で C# if/switch 分岐する
- provider-specific runtime handler を第一候補にする
- provider-specific schema / external instance semantic authority を Topolactor DB に作る
- external instance を Topolactor runtime SSOT として扱う

受入条件:
- [ ] instance port DDL / seed / repository / runtime lane が SSOT に従って追加されている。
- [ ] `call_instance_postgres_function` は manifest-authorized function のみ実行し、function/schema allowlist / instance authority binding / timeout / result sanitize / fail-close を持つ。
- [ ] `call_postgres_function` は Topolactor DB 内 `topology.*` 専用のまま保持されている。
- [ ] credential は `reference_key` / DB guarded vault / runtime secret resolver 経由で runtime-only 解決され、plaintext connection string は SSOT / seed / UI / projection / log に出ない。
- [ ] provider_kind / required_by_bundle は data only で C# selector ではない。
- [ ] 特定 consumer 専用 handler / schema / semantic authority を追加していない。
- [ ] `.agent/tests/check-instance-port-substrate.sh` と必要な runtime/backend tests が追加・通過している。

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

## Bundle `cli-mcp-dispatch-secured-read-export-port`

**Status:** not_started
**Roadmap/status SSOT:** `product.external_port_substrate` / `product.core_runtime_route`
**SSOT:** `docs/design/cli-model-context-protocols-port-ssot.yaml` / `docs/design/cli-mcp-port-implementation-ssot.yaml`

問題点:
CLI/MCP Port の read/export 境界、Context API、Data Reader、export_job、audit_log は定義済みだが、MCP/CLI access が必ず runtime dispatch 解決を通る security-critical lane として弱い。Core API 直叩き、未認証アクセス、dispatch 迂回、AI/CLI/MCP による DB 直接改変、外部AI構造化結果の正本扱いを閉じる必要がある。

目的:
MCP/CLI client → MCP API port → user auth/authz → cli_reader_port scope resolution → credential/capability requirement resolution → ManifestDispatcher/runtime dispatch → Data Reader/authorized read model → physical DB read/export job/audit log → CLI/MCP response を正本レーンとして固定する。さらに External AI structured output → MCP API port → user auth/authz → import_candidate scope resolution → credential/capability requirement resolution → ManifestDispatcher/runtime dispatch → business object assignment → draft_operation/commit_candidate creation → preview diff → user approval → canonical commit dispatch → DB commit → audit/runtime_event_log を draft/candidate lane として実装する。

実装方針:
- [ ] MCP API port 入口で user auth/authz を fail-close し、response 後 validation や downstream 任せにしない。
- [ ] cli_reader_port / import_candidate scope resolution を Context API だけで完了扱いにせず、ManifestDispatcher/runtime dispatch 解決済み request のみ Data Reader / business object assignment へ渡す。
- [ ] credential/capability requirement resolution は plaintext credential 渡しではなく、credential requirement / capability availability / policy step requirement の解決として実装する。
- [ ] create_export_job / audit_log / runtime_event_log / draft_operation / commit_candidate creation は system-controlled DB operation として限定許可し、record commit/delete/approval/payment/email send/arbitrary mutation は CLI/MCP out_of_scope として閉じる。
- [ ] 外部AI構造化出力は evidence/input として扱い、root utterance / source transcript / confidence / unresolved fields / preview diff を保持した draft_operation / commit_candidate だけを作成する。
- [ ] user approval 前の DB mutation を禁止し、approval 自体を AI/MCP/CLI から実行できないようにする。approval 後のみ canonical commit dispatch 経由で DB commit へ進める。
- [ ] external_port_substrate と混同せず、外部連携出入口と AI/CLI 安全 read/export/import-candidate 出入口を別 Bundle 境界として扱う。

対応資料:
- `docs/design/cli-model-context-protocols-port-ssot.yaml`
- `docs/design/cli-mcp-port-implementation-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`

対象ファイル名:
- `backend/runtime/ManifestDispatcher.cs`
- `backend/runtime/RuntimeExecutor.cs`
- `backend/runtime/TopologyFunctionBinder.cs`
- `backend/runtime/ExternalPortDispatchRuntime.cs`
- `backend/repository/*`
- `backend/Program.cs`
- `db/topology_tables.sql`
- `db/seed_empty.sql`
- `frontend/api/adminApi.ts`
- `frontend/islands/ContentsScreenDesignPanel.tsx`
- `frontend/islands/ProjectionShell.tsx`

対象関数名またはruntime境界名:
- `ManifestDispatcher.DispatchAsync`
- `RuntimeExecutor.ExecuteAsync`
- `TopologyFunctionBinder.Bind`
- `ScreenDataShapeQueryRuntime.TryExecuteAsync`
- `ExternalPortDispatchRuntime.ExecuteAsync`
- `ExternalPortPolicyStepExecutor.ExecutePolicyAsync`
- `Data Reader / authorized read model boundary`
- `MCP API port entry gate`
- `business object assignment candidate boundary`
- `draft_operation / commit_candidate creation boundary`
- `canonical commit dispatch boundary`

NG軸:
- Core API 直叩き / direct API wrapper / dedicated backend handler route による dispatch bypass
- 未認証 CLI/MCP access
- dispatch 解決なし Data Reader / Context API / import candidate assignment
- AI/CLI/MCP DB直接改変 / direct SQL / direct DB connection
- approval / commit / delete / payment / email send の CLI/MCP 実行
- credential read/export / plaintext credential response
- audit log skip / runtime_event_log skip
- scope外 table / column / period / row / business object assignment
- 外部AI構造化結果の正本扱い / 根拠発話・source・confidence なし自動割当
- 未確定項目の勝手な確定値化
- commit_candidate から canonical dispatch を迂回した DB 更新

受入条件:
- [ ] read/export は必ず user auth/authz → scope resolution → credential/capability requirement resolution → ManifestDispatcher/runtime dispatch → Data Reader/authorized read model を通る。
- [ ] Context API / Data Reader / import candidate assignment の dispatch bypass test / guard がある。
- [ ] AI/MCP/CLI は draft_operation / commit_candidate 作成までで、DB commit / approval execution / arbitrary mutation を実行できない。
- [ ] commit_candidate は source transcript / root utterance / confidence / unresolved fields / preview diff を保持する。
- [ ] user approval 後のみ canonical commit dispatch 経由で DB commit へ進む。
- [ ] create_export_job / draft_operation / commit_candidate / audit_log / runtime_event_log 以外の system-controlled write を追加していない。
- [ ] external_port_substrate の secure consumer dispatch lane とは関連するが同一 Bundle として混同していない。
- [ ] 関連 backend/frontend tests または `.agent/tests/*` が追加/更新されている。

---

## Bundle `job-scheduler-port-consumer`

**Status:** partial
**Roadmap/status SSOT:** `product.job_scheduler_port_consumer`
**SSOT:** `docs/design/runtime-bundle-job-scheduler-ssot.yaml`

PR#460 完了済み: external scheduler access_port / hook_port seed binding と、built-in scheduler の credential_kind=none seed row。
この TODO は **external scheduler ingress receiver** に縮退する。
RuntimeTimelineScheduler 本体、cron driver、run ledger、job execution lifecycle、job status projection は `product.scheduler_job_manifest_substrate` / SchedulerJobManifestSubstrate の責務であり、この port consumer TODO では扱わない。

残 todo:
- [ ] external scheduler hook/access receiver が active port record と credential_requirement を解決し、scheduler enqueue boundary へ渡すことを実装・検証する。
- [ ] external scheduler provider credential は external_port_substrate の port record attachment として扱い、provider-specific client / handler / selector を追加しない。
- [ ] built-in RuntimeTimelineScheduler path が port substrate に依存しないことの guard を維持する。
- [ ] external receiver failure は silent fallback せず、missing port / missing credential / invalid policy を explicit fail-close として記録する。

再分類済み / この TODO から除外:
- cron trigger driver loop: SchedulerJobManifestSubstrate / RuntimeTimelineScheduler side
- run ledger / input lease / job execution lifecycle: SchedulerJobManifestSubstrate
- scheduler evidence / job status projection: SchedulerJobManifestSubstrate projection/evidence surface
- execution_started / execution_completed / execution_failed lifecycle: job manifest run ledger / runtime_event_log side

対応資料:
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/scheduler-job-manifest-ssot.yaml`


---
