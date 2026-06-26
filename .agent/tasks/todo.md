# Agent Task List

未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` / `acceptance_pending` のみ。Roadmap bundle 列は feature-bundle status index への対応であり、実装済み bundle や証跡台帳はここへ復活させない。

---

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | Roadmap bundle | 主 SSOT |
|-----------|------|--------|------|----------------|---------|
| `instance-port-substrate` | credential-backed instance connection / instance function call substrate | not_started | 1 | `product.instance_port_substrate` | `docs/design/instance-port-substrate-ssot.yaml` |
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `product.external_optional_surface_bundle_gate` | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `external-port-consumer-hardcode-reduction` | 外部連携 consumer hardcode reduction / abstract function・seed 拡張 | partial | 1 | - | `docs/design/external-port-substrate-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル方針 | not_started | 2 | `product.helper_manual_policy` | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | acceptance_pending | 1 | `product.dynamic_support_nocode_loop` | `docs/system-roadmap.yaml`（roadmap/status SSOT。実装完了判定は実コード・テスト確認が必要） |
| `cli-mcp-dispatch-secured-port` | CLI/MCP read/export/import-candidate port 親境界 | not_started | 5 subBundles | `product.external_port_substrate` / `product.core_runtime_route` | `docs/design/cli-model-context-protocols-port-ssot.yaml` |

注: 上記 consumer bundle は PR#460 により seed binding / credential_requirement / policy_steps が完了済み。client/UI consumer (email / audit_approval) は UI Builder portTargetRef 配線前提が完了済み。hook consumer (stripe / webhook_inbox) は hook_port seed binding が完了済み (UI Builder portTargetRef 配線ではない)。残作業は各 bundle consumer todo 参照。provider-specific runtime / client は追加しない。UI Builder form preset は docs/design/ui-builder-preset-ecosystem-ssot.yaml / db/physical_search_crud_aggregate_preset_seed.sql の CRUD preset seed の写像/派生であり、新規 UI runtime / 専用 component 実装ではない。

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

## Bundle `external-port-consumer-hardcode-reduction`

**Status:** partial
**Roadmap bundle:** `-`
**Primary SSOT:** `docs/design/external-port-substrate-ssot.yaml`

進捗メモ (この PR):
- `record_transfer_lifecycle_evidence` を generic `ExternalPortPolicyStepExecutor` から
  専用 `ExportSftpBundleStepHandler`（`IExternalPortBundleStepHandler` / SSOT
  consumer_bundle_step_handler_surface）へ退避済み。generic executor の
  `AllowedOperationKeys` / registry / lifecycle helper / 旧 dead `MarkOnly` を削減。
- entity_ref PascalCase switch の重複（executor `ResolveEntityId` +
  `EventLogPrimitiveAdapter.ResolveEntityRef`）を context 単一の
  `ResolveReferenceValue` へ統合。
- credential refresh compatibility path（`ExternalTokenRefresher.RefreshIfNeededAsync`）の
  hardcoded lease duration default (`TimeSpan.FromMinutes(5)`) を削除し、canonical
  `credential_acquire_lease` と同じく step/request config から解決・fail-close 化。
- compat absorption + shim（fail-close adapter）は既存どおり維持し regress させていない。
- evidence repository の tableRef allowlist は増やさず、raw dynamic SQL も導入していない。
- PR#513 audit 追従 (同一 PR 追加 scope):
  - (read側) entity_ref vocabulary を external_context binding（snake_case）へ統合し、seed
    `entity_ref_key`（seed_empty + absorption seed）を PascalCase→snake_case 移行、
    並行 PascalCase switch を除去。
  - (write側) result→external_context mapping を manifest-declared 化:
    `topology.abstract_function_steps.external_context_key`（CHECK 制約 +
    `db/external_port_result_context_mapping_seed.sql`）を runtime が authority 検証して適用、
    unknown / unauthorized / secret-bearing / type-mismatch を fail-close。
    `ApplyResultMirror` は固定 writable allowlist のみで generic projection engine ではない。
  - evidence append/load の C# tableRef switch を DB function
    （`topology.epce_append_evidence` / `topology.epce_load_projection`）へ退避。
    allowlist + active manifest binding validation は fail-close guard として維持、
    raw tableRef 動的 SQL なし。
  - `ExternalPortConsumerEvidenceRepositoryLiveDbTests` の sftp ケースは SFTP 前提
    （completed export_job + manifest + verified checksum）を seed するよう修正し 7/7 通過。
    新 DB function の sftp 分岐も end-to-end 検証済み（旧 pre-existing test-data gap は解消）。
  - 詳細 todo `.agent/tasks/external-port-substrate-implementation-todo.md` と整合済み。
- 残: `record_transfer_lifecycle_evidence` の分岐（success/mismatch/failed/retry +
  scheduler enqueue）の純 PostgreSQL function 完全 data 化は、AF manifest が条件分岐/
  scheduler enqueue を表現できないため bundle handler 境界として保留（理由を SSOT/handler doc に明記）。

問題点:
外部連携基板は Gate0 / parent completion として実装済みだが、generic executor / evidence repository / abstract function external context 周辺に、次 Bundle 追加時に C# 意味が増えやすい hardcode reduction 候補が残っている。

- `record_transfer_lifecycle_evidence` が `ExternalPortPolicyStepExecutor` にあり、Export/SFTP lifecycle 意味が generic executor 内へ混入している。
- `ExportJobId` / `FileArtifactId` / `ChecksumValue` / `AuthorizationKey` / `OutputProp` などの entity/result context key が C# switch として残っている。
- `NpgsqlExternalPortConsumerEvidenceRepository` に `AllowedTableRefs` / `InsertSql` / `SelectSql` の tableRef switch があり、manifest-bound validation はあるが projection constructor mapping が C# 側に残っている。
- compatibility operation は `abstract_function_key` absorption seed で吸収済みだが、active policy step の `operation_key` が legacy compatibility 名のまま残っている箇所があるため、可能なら `execute_abstract_function` への完全寄せを検討できる。
- credential refresh は canonical abstract primitive chain が存在する一方、compatibility path に lease duration 等の実装側値が残らないか確認・整理余地がある。

目的:
外部連携基板の runtime skeleton は維持しつつ、bundle lifecycle 意味・projection mapping 意味・entity ref 意味を seed / abstract function / DB function mapping に寄せ、今後の consumer bundle 追加で provider/bundle-specific C# hardcode が増えるのを防ぐ。

改善方針:
- `record_transfer_lifecycle_evidence` を generic `ExternalPortPolicyStepExecutor` から退避する。第一候補は `execute_abstract_function` + manifest-authorized PostgreSQL function / evidence projection mapping。純計算のみ handler に残す方針と整合させる。
- compatibility operation rows を、可能な範囲で active seed 上 `execute_abstract_function` + `abstract_function_key` へ寄せる。shim は旧 row 互換の fail-close adapter として縮小する。
- `event_log` / `append_runtime_event_log` の `entity_ref_key` は PascalCase runtime field 名 switch ではなく、abstract function input binding / external_context binding に寄せる。
- `ApplyResultToExternalContext` の固定 key switch は、manifest 側の result-to-external-context mapping へ寄せられるか検討する。
- evidence table append/load は C# table switch を増やさず、DB function または seed-defined mapping row + manifest binding validation へ寄せる。raw tableRef 動的 SQL は禁止する。
- credential refresh compatibility path は canonical primitive chain と矛盾しないよう整理し、method / lease duration / expires_at response key 等の C# default を増やさない。
- provider_kind / required_by_bundle による C# if/switch 分岐は禁止する。
- frontend payload / seed payload / projection / log に credential plaintext、endpoint 実体、signed URL、bucket、storage path 等を出さない。

対応資料:
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/abstract-function-primitive-registry-ssot.yaml`
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/pipeline-continuity-ssot.yaml`

対象ファイル名:
- `.agent/tasks/todo.md`
- `backend/runtime/ExternalPortCredentialRefresher.cs`
- `backend/runtime/ExternalPortPolicyStepExecutorCompatibilityShim.cs`
- `backend/runtime/AbstractFunctionRuntime.cs`
- `backend/repository/NpgsqlExternalPortConsumerEvidenceRepository.cs`
- `db/seed_empty.sql`
- `db/external_port_compat_absorption_seed.sql`
- `db/topology_tables.sql`
- `.agent/tests/check-external-port-compat-absorption.sh`
- `.agent/tests/check-external-port-parent-completion.sh`

対象関数名・境界:
- `ExternalPortPolicyStepExecutor.ExecutePolicyAsync`
- `ExternalPortPolicyStepExecutor.ExecuteAsync`
- `ExternalPortPolicyStepExecutor.ResolveEntityId`
- `ExternalPortPolicyStepExecutor.BuildRetrySchedulerRequest`
- `ExternalPortPolicyStepExecutor` の `record_transfer_lifecycle_evidence`
- `ExternalPortPolicyStepExecutorCompatibilityShim.ExecuteCompatibilityAsync`
- `AbstractFunctionExecutionContext.ApplyResultToExternalContext`
- `AbstractFunctionExecutionContext.ResolveExternalContext`
- `EventLogPrimitiveAdapter.ResolveEntityRef`
- `NpgsqlExternalPortConsumerEvidenceRepository.AppendEvidenceAsync`
- `NpgsqlExternalPortConsumerEvidenceRepository.LoadProjectionAsync`
- `NpgsqlExternalPortConsumerEvidenceRepository.InsertSql`
- `NpgsqlExternalPortConsumerEvidenceRepository.SelectSql`
- `NpgsqlExternalPortConsumerEvidenceRepository.ValidateTableRefBoundToActiveManifestAsync`

NG軸:
- 外部連携 parent completion / Gate0 implemented 状態を未実装扱いに戻す
- Roadmap (`docs/system-roadmap.yaml`) をこの Bundle 追加で更新する
- provider_kind / required_by_bundle による C# if/switch 分岐を増やす
- provider/bundle-specific runtime handler / client / repository method を追加する
- raw tableRef 動的 SQL を許可する
- manifest authority / seed-defined mapping を迂回する
- credential plaintext / endpoint 実体 / signed URL / bucket / storage path を frontend payload / seed payload / projection / log に露出する

受入条件:
- [x] `record_transfer_lifecycle_evidence` が generic executor から退避され（専用 `ExportSftpBundleStepHandler`）、Export/SFTP lifecycle 意味は generic executor から除去。evidence/event_log 書込は seed-directed・manifest-binding-validated repository 境界経由、event type / table ref / retry target は step_config（seed）由来の data-defined。AF manifest が条件分岐/scheduler enqueue を表現できないため、純 PostgreSQL function 化ではなく per-bundle handler 境界を採用（理由を SSOT/handler doc に明記）。
- [x] active compatibility policy steps は `abstract_function_key` 吸収 + shim 経由で `execute_abstract_function` に寄っており、shim は `abstract_function_key` 必須の fail-close adapter として維持（regress なし）。operation_key 文字列の完全 flip は未実施。
- [x] entity/result context key: (read側) entity_ref vocabulary を external_context binding（snake_case: export_job_id / file_artifact_id / checksum_value / authorization_key）へ統合し、seed entity_ref_key（seed_empty + absorption seed）も snake_case へ移行、並行 PascalCase switch を除去。(write側) result→external_context mapping を manifest-declared 化: `topology.abstract_function_steps.external_context_key`（CHECK 制約 + `db/external_port_result_context_mapping_seed.sql`）を runtime が authority 検証して適用し、unknown / unauthorized / secret-bearing / type-mismatch を fail-close（`ApplyResultMirror` は固定 writable allowlist のみ、generic projection engine ではない）。result_context_key とは decouple。
- [x] evidence append/load は C# tableRef switch を DB function（`topology.epce_append_evidence` / `topology.epce_load_projection`）へ退避し、allowlist + active manifest binding validation を fail-close guard として維持、raw tableRef 動的 SQL は導入していない。
- [x] credential refresh compatibility path は canonical chain と整合（lease duration の C# default を削除し config 由来 fail-close 化）、method / expires_at response key の C# default も増やしていない。
- [x] provider_kind / required_by_bundle の C# if/switch 分岐、provider-specific client、bundle-specific runtime handler（generic executor 内）を追加していない。`ExportSftpBundleStepHandler` は operation_key 駆動の SSOT 公認 consumer handler であり provider_kind 分岐ではない。
- [x] credential plaintext / endpoint 実体 / signed URL / bucket / storage path を frontend payload / seed payload / projection / log に出していない（変更なし）。
- [x] `.agent/tests/check-external-port-compat-absorption.sh` / `.agent/tests/check-external-port-parent-completion.sh`（stale 化していた lifecycle assertion を canonical shape へ更新 + 退避 guard 追加）/ runtime tests（`ExportSftpPortConsumerContractTests` 更新, full external-port runtime suite 168 件）/ live-DB integration が通過。

out_of_scope:
- Roadmap 更新
- 外部連携基板 parent completion の再判定・未実装化
- scheduler 本体の変更
- provider-specific scheduler/client/runtime 実装
- DB schema / backend runtime / frontend / test の今回実装

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

**Status:** not_started
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

**Status:** not_started
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
- [ ] read/search/aggregate/analyze/validate は user auth/authz → scope resolution → credential/capability requirement resolution → ManifestDispatcher/runtime dispatch → Data Reader/authorized read model を必ず通る。
- [ ] Data Reader は dispatch resolved request のみ受け付け、未解決 request を fail-close する。
- [ ] allowed tables / columns / filters / periods / roles / users / row scope が admin config 由来で解決される。
- [ ] direct DB connection / direct SQL / Core API direct call / dedicated handler bypass の guard または tests がある。
- [ ] audit_log / runtime_event_log 境界を skip しない。

out_of_scope:
- export_job 生成 / file generation / manifest/checksum 生成
- file stream / direct file download
- import_structured_output / draft_operation / commit_candidate
- DB commit / approval / delete / payment / email send
- browser UI automation

### SubBundle `cli-mcp-export-job-port`

**Status:** not_started
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
- [ ] export は必ず authorized read scope に基づく `export_job` として記録される。
- [ ] manifest_version / export_job_id / generated_at / generated_by / period / source_tables / source_record_ids / files / checksum を持つ manifest が生成される。
- [ ] csv/json/pdf/zip generation は Data Reader authorized request 由来の record set のみを対象にする。
- [ ] checksum と generated_files が export ledger に残る。
- [ ] unauthorized bulk export を fail-close する tests/guards がある。

out_of_scope:
- file stream permission / download API
- import-candidate / draft_operation / commit_candidate
- approval / DB commit / arbitrary mutation
- provider-specific external export runtime

### SubBundle `cli-mcp-file-stream-port`

**Status:** not_started
**対応SSOTファイル:** `docs/design/cli-model-context-protocols-port-ssot.yaml`
**対応SSOTセクション名:** `file_stream` / `mcp_surface.resources`

目的:
- `download_export_file`
- `get_export_status`
- export resource URI (`topolactor://exports/{export_job_id}/manifest.json`, `topolactor://exports/{export_job_id}/file`)
- file stream permission

対象ファイル名候補:
- `backend/Program.cs` (MCP/API file stream entry only; direct path exposure 不可)
- `backend/repository/*Export*`
- `backend/runtime/ManifestDispatcher.cs`
- `backend/runtime/RuntimeExecutor.cs`
- `frontend/api/adminApi.ts` (admin projection metadata only if needed)

NG軸:
- stream without export_job
- permission bypass
- direct file path exposure
- checksum/manifest metadata なしの stream

受入条件:
- [ ] file stream は API が許可した export_job に対してのみ開放される。
- [ ] export_job_id / source_record_ids / generated_by / generated_at / period / checksum / manifest_version を検証する。
- [ ] direct filesystem path や secret-bearing location を response/resource metadata に露出しない。
- [ ] `get_export_status` と resource URI は unauthorized user に fail-close する。

out_of_scope:
- export_job 作成 / csv/json/pdf/zip generation 本体
- read/search/aggregate/analyze/validate 本体
- import-candidate / approval / DB commit
- browser UI automation

### SubBundle `cli-mcp-import-candidate-port`

**Status:** not_started
**対応SSOTファイル:** `docs/design/cli-model-context-protocols-port-ssot.yaml` / `docs/design/runtime-bundle-audit-approval-ssot.yaml`
**対応SSOTセクション名:** `core_invariant.canonical_structured_input_lane` / `structured_input_import` / `core_invariant.dispatch_and_approval_boundary`

目的:
- `import_structured_output`
- `assign_business_object_candidate`
- `create_draft_operation`
- `create_commit_candidate`
- `preview_diff`
- `source_transcript_ref` / `root_utterance` / `confidence` / `unresolved_fields` / evidence handling

対象ファイル名候補:
- `backend/runtime/ManifestDispatcher.cs`
- `backend/runtime/RuntimeExecutor.cs`
- `backend/runtime/TopologyFunctionBinder.cs`
- `backend/repository/*Draft*` / `backend/repository/*CommitCandidate*`
- `backend/Program.cs` (MCP/API import-candidate entry only; approval execution route は不可)
- `db/topology_tables.sql` (draft_operation / commit_candidate tables only when SSOT-backed)
- `db/seed_empty.sql` (import_candidate admin config only when SSOT-backed)

NG軸:
- external AI structured output as SSOT
- DB commit by AI/MCP/CLI
- user approval bypass
- auto-confirming unresolved fields
- delete/payment/email send execution
- commit_candidate から canonical dispatch を迂回した DB 更新

受入条件:
- [ ] External AI structured output は evidence/input として扱われ、正本化されない。
- [ ] draft_operation / commit_candidate は source_transcript_ref / root_utterance / confidence / unresolved_fields / assigned_business_object_candidate / preview_diff を保持する。
- [ ] AI/MCP/CLI は draft_operation / commit_candidate 作成までで、approval execution / DB commit / arbitrary mutation を実行できない。
- [ ] user approval 後のみ canonical commit dispatch 経由で DB commit に進む境界が明記・検証される。
- [ ] unresolved_fields を自動確定せず、preview diff/evidence なし candidate を fail-close する。

out_of_scope:
- read/export/file stream 実装
- approval UI 実装
- canonical commit dispatch 本体の新規実装
- delete/payment/email send
- browser UI automation

### SubBundle `cli-mcp-surface-metadata-port`

**Status:** not_started
**対応SSOTファイル:** `docs/design/cli-model-context-protocols-port-ssot.yaml`
**対応SSOTセクション名:** `mcp_surface` / `admin_ui_configuration` / `explicitly_out_of_scope`

目的:
- MCP tools/resources generation
- admin config projection
- exposed resources/tools metadata
- out_of_scope operation を surface metadata から除外する

対象ファイル名候補:
- `backend/Program.cs` (MCP metadata entry only; dedicated runtime bypass 不可)
- `backend/runtime/ManifestDispatcher.cs`
- `backend/repository/*Mcp*` / `backend/repository/*Cli*`
- `db/seed_empty.sql` (MCP/admin config projection seed only when SSOT-backed)
- `frontend/islands/ContentsScreenDesignPanel.tsx` (admin config projection only if needed)
- `frontend/islands/ProjectionShell.tsx` (metadata projection only if needed)

NG軸:
- browser UI automation
- dedicated backend handler bypassing dispatch
- core API direct call
- operation execution beyond declared read/export/import-candidate surface
- out_of_scope operation の tool/resource 公開

受入条件:
- [ ] MCP tools/resources は admin config から生成される。
- [ ] tools は get_monthly_context / search_records / aggregate_records / validate_export / create_export_job / download_export_file / get_export_status / import_structured_output / assign_business_object_candidate / create_draft_operation / create_commit_candidate / get_preview_diff に限定される。
- [ ] resources は SSOT の `mcp_surface.resources` に沿い、permission/scope metadata と対応する。
- [ ] explicitly_out_of_scope operations が tools/resources として公開されない guard/tests がある。

out_of_scope:
- read/export/import-candidate の runtime body 実装
- backend route dedicated handler bypass
- core API direct call
- UI browser automation
- approval / commit / delete / payment / email send

---

---
