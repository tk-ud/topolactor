# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `cli-mcp-dispatch-secured-read-export-port` | CLI/MCP dispatch-secured read/export/import-candidate port 実装 | not_started | 1 | `product.external_port_substrate` / `product.core_runtime_route` | `docs/design/cli-model-context-protocols-port-ssot.yaml` |
| `email-port-consumer` | email_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-email-ssot.yaml` |
| `stripe-port-consumer` | stripe_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-stripe-ssot.yaml` |
| `job-scheduler-port-consumer` | job_scheduler_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-job-scheduler-ssot.yaml` |
| `sql-attention-key-expansion-draft-lane-implementation` | SQL Attention key expansion draft lane 実装 | not_started | 1 | - | `docs/design/sql-attention-logs-ssot.yaml` |
| `audit-approval-port-consumer` | audit_approval_bundle port substrate 接続実装 | partial | 1 | - | `docs/design/runtime-bundle-audit-approval-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。scheduler consumer (job_scheduler) は built-in/external port seed binding が完了済み (内蔵 scheduler は port substrate 非依存)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

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

## Bundle `email-port-consumer`

**Status:** partial
**SSOT:** `docs/design/runtime-bundle-email-ssot.yaml`

PR#460 完了済み: response_port (smtp) seed binding / credential_requirement / policy_steps / UI Builder portTargetRef 配線前提。
残作業は physical table / approval evidence / delivery evidence / projection 接続。SMTP provider-specific client / runtime は追加しない。
既存レーン参照: `docs/design/external-port-substrate-ssot.yaml#secure_consumer_dispatch_lane`

残 todo:
- [ ] email_draft / approval_record / delivery_evidence physical table 接続実装
- [ ] physical table manifest binding (email manifest / screen_data_shape)
- [ ] UI Builder form preset seed（CRUD preset 派生）/ portTargetRef action wiring (UI approval → response_port connect)
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
- [ ] generic hook lane seed/wiring: hook_path / route_key resolution → port record resolution → scheduler enqueue boundary（Stripe 専用 handler/runtime 新設なし）
- [ ] evidence / runtime_event_log: webhook_received / verification_success / verification_failure / payment_state_projected
- [ ] projection response: payment state / verification evidence projection

対応資料:
- `docs/design/runtime-bundle-stripe-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`

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
- [ ] cron trigger boundary 接続整理・evidence/projection 接続 (RuntimeTimelineScheduler 本体・in-memory queue は変更しない; built-in scheduler は port substrate に依存しないこと)
- [ ] hook trigger intake wiring (外部スケジューラー hook のみ port substrate 使用)
- [ ] evidence / runtime_event_log: trigger_received / scheduler_enqueued / execution_started / execution_completed / execution_failed
- [ ] projection response: job status projection
- [ ] built-in scheduler path が port substrate に依存しないことの test / guard 追加

対応資料:
- `docs/design/runtime-bundle-job-scheduler-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`

---


## Bundle "sql-attention-key-expansion-draft-lane-implementation"

**Status:** not_started
**SSOT:** "docs/design/sql-attention-logs-ssot.yaml" / "docs/design/sql-attention-logs-ssot.md"

問題点:
SQL Attention manifest topology key expansion draft lane の SSOT は定義されたが、実装側にはまだ "logs.attention" SQLAT evidence から高圧な離散値 Key を抽出し、registered manifest topology 全空間へ SQL 横断検索し、"source=sql_attention" の draft candidate JSONB と Markdown projection を insert する lane がない。現状のままだと、SQL Attention evidence は観測・推薦証跡に留まり、manifest topology / screen_data_shape / logical table / logical column / enum/discrete metadata を使った draft candidate 生成へ接続されない。

目的:
SQL Attention で近傍探索された "hubs.hub_relations" 群を Key discovery space として扱い、そこで得た高圧な離散値 Key を registered manifest topology 全空間へ展開して、relationship_axis_candidate / meaning_projection_candidate / aggregate_projection_candidate を draft candidate JSONB として insert する。Markdown は人間向け projection / review surface とし、runtime authority / topology promotion authority / UI placement authority にはしない。DB notify / SSE は draft 作成通知のみを担い、採用・昇格・配置は明示操作へ残す。

実装方針:

- [ ] "logs.attention" の SQLAT evidence を source として読み、source evidence refs / source tag "sql_attention" を保持する。
- [ ] SQLAT-explored hub relation neighborhood は Key extraction space に限定し、candidate completion space として扱わない。
- [ ] enum value / status / category / type / kind / state / boolean / low-cardinality value などの高圧 discrete Key を SQL query/function で抽出する。
- [ ] 抽出 Key をもとに、topology manifest JSONB / screen data shape JSONB / logical tables / logical columns / enum group or discrete value metadata / physical table manifest bindings を SQL 横断検索する。
- [ ] hit 集合から same-name axis / same-type axis / same-name-and-same-type common axis / enum group match / value overlap / logs.diff pressure / logs.attention pressure / table-ref reuse / manifest reuse を再集計する。
- [ ] raw count だけで scoring せず、routine high-frequency value の dampening、lift / pressure delta、ID column の axis/dimension 扱い、generic column dampening を入れる。
- [ ] "source=sql_attention"、"candidate_lane=manifest_topology_key_expansion_draft_lane"、"status=draft" を持つ draft candidate JSONB を insert-only で保存する。
- [ ] draft payload には candidate_id / candidate_type / source / candidate_lane / source_evidence_refs / high_pressure_key / hit_manifest_refs / hit_table_refs / common_axis_candidates / candidate_columns / score / status を保持する。
- [ ] Markdown projection は SQL で生成してよいが、authority は draft candidate JSONB とし、Markdown body は human-readable review / search / dashboard projection に限定する。
- [ ] Markdown projection には raw HTML / island markup / CSS class authority / executable script / promotion instruction as authority を含めない。
- [ ] insert 後の DB NOTIFY / SSE payload は structured JSON とし、event_type / source / candidate_id / candidate_lane / markdown_projection_id などを渡す。UI placement は SQL で決めない。
- [ ] C# は scheduler / orchestration / SQL execution / notification bridge までに限定し、candidate inference 本体を C# switch / hardcoded heuristic として実装しない。

対応資料:

- "docs/design/sql-attention-logs-ssot.yaml"
- "docs/design/sql-attention-logs-ssot.md"
- "docs/design/team-markdown-dashboard-saved-view-ssot.yaml"
- "docs/design/runtime-orchestration-ssot.yaml"
- "docs/design/pipeline-continuity-ssot.yaml"
- "docs/design/db-schema.yaml"

対象ファイル名:

- "db/sql_attention_logs_tables.sql"
- "db/topology_tables.sql"
- "db/seed_empty.sql"
- "backend/scheduler/*"
- "backend/repository/*"
- "backend/runtime/*"
- "backend/endpoint/SseEndpoint.cs"
- "backend/Program.cs"
- "frontend/runtime/sseReceiver.ts"
- "frontend/runtime/sseDispatcher.ts"
- "frontend/islands/*"
- "frontend/routes/admin/*"
- "frontend/tests/*"
- "backend/tests/*"
- ".agent/tests/check-sql-attention-ssot.sh"
- ".agent/tests/check-structure.sh"（必要なら語彙/境界 guard 追加のみ）

対象関数名またはruntime境界名:

- SQL function: "extract_sql_attention_high_pressure_keys" 相当
- SQL function: "compile_sql_attention_manifest_topology_draft_candidates" 相当
- SQL function / trigger: "insert_sql_attention_draft_candidate" 相当
- SQL trigger: "notify_sql_attention_draft_candidate_created" 相当
- "SqlAttentionScheduler" / SQL Attention scheduled lane
- "RuntimeTimelineScheduler"
- "SseEndpoint"
- "SseReceiver"
- "SseDispatcher"
- "Team Markdown Dashboard / saved markdown projection boundary"
- "Draft candidate insert-only boundary"
- "DB NOTIFY / SSE projection signal boundary"

NG軸:

- hub relation neighborhood だけで candidate completion すること
- SQL Attention evidence なしの full-space schema mining
- C# 側 candidate inference 必須化
- active manifest / topology registry / hub relation / runtime route の自動 mutation
- auto-apply / auto-promote
- Markdown を runtime SSOT / topology promotion authority として扱うこと
- SQL から HTML / island markup / UI placement を生成すること
- raw count only scoring
- ID column を primary display text として扱うこと
- "/admin/contents" の Step 2.5 / Step 3 をこの candidate generation lane の write target として扱うこと

受入条件:

- [ ] SQLAT evidence から discrete Key extraction → manifest topology full-space expansion → draft candidate JSONB insert までが SQL-driven lane として動作する。
- [ ] draft candidate JSONB が source evidence refs、source="sql_attention"、candidate_lane、high_pressure_key、hit refs、common axis candidates、candidate columns、score、status を保持する。
- [ ] Markdown projection は human-readable projection として保存/通知され、authority は draft candidate JSONB に残る。
- [ ] DB NOTIFY / SSE payload は structured JSON であり、SQL が UI placement / HTML / island markup を決めていない。
- [ ] hub relation neighborhood only completion、schema mining without SQLAT evidence、auto mutation、auto promote を防ぐ test / guard がある。
- [ ] C# implementation は orchestration / SQL execution / notification bridge に留まり、candidate inference logic を C# hardcode として持たない。
- [ ] 関連 backend/frontend tests または ".agent/tests/*" が追加/更新されている。

---

