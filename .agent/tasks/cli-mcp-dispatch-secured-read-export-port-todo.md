# Bundle `cli-mcp-dispatch-secured-read-export-port`

**Status:** not_started
**Roadmap/status SSOT:** `product.external_port_substrate` / `product.core_runtime_route`
**SSOT:** `docs/design/cli-model-context-protocols-port-ssot.yaml` / `docs/design/cli-mcp-port-implementation-ssot.yaml`

## 問題点

CLI/MCP Port の read/export 境界、Context API、Data Reader、export_job、audit_log は定義済みだが、MCP/CLI access が必ず runtime dispatch 解決を通る security-critical lane として弱い。Core API 直叩き、未認証アクセス、dispatch 迂回、AI/CLI/MCP による DB 直接改変、外部AI構造化結果の正本扱いを閉じる必要がある。

## 目的

MCP/CLI client → MCP API port → user auth/authz → cli_reader_port scope resolution → credential/capability requirement resolution → ManifestDispatcher/runtime dispatch → Data Reader/authorized read model → physical DB read/export job/audit log → CLI/MCP response を正本レーンとして固定する。

さらに External AI structured output → MCP API port → user auth/authz → import_candidate scope resolution → credential/capability requirement resolution → ManifestDispatcher/runtime dispatch → business object assignment → draft_operation/commit_candidate creation → preview diff → user approval → canonical commit dispatch → DB commit → audit/runtime_event_log を draft/candidate lane として実装する。

## 実装方針

- [ ] MCP API port 入口で user auth/authz を fail-close し、response 後 validation や downstream 任せにしない。
- [ ] cli_reader_port / import_candidate scope resolution を Context API だけで完了扱いにせず、ManifestDispatcher/runtime dispatch 解決済み request のみ Data Reader / business object assignment へ渡す。
- [ ] credential/capability requirement resolution は plaintext credential 渡しではなく、credential requirement / capability availability / policy step requirement の解決として実装する。
- [ ] create_export_job / audit_log / runtime_event_log / draft_operation / commit_candidate creation は system-controlled DB operation として限定許可し、record commit/delete/approval/payment/email send/arbitrary mutation は CLI/MCP out_of_scope として閉じる。
- [ ] 外部AI構造化出力は evidence/input として扱い、root utterance / source transcript / confidence / unresolved fields / preview diff を保持した draft_operation / commit_candidate だけを作成する。
- [ ] user approval 前の DB mutation を禁止し、approval 自体を AI/MCP/CLI から実行できないようにする。approval 後のみ canonical commit dispatch 経由で DB commit へ進める。
- [ ] external_port_substrate と混同せず、外部連携出入口と AI/CLI 安全 read/export/import-candidate 出入口を別 Bundle 境界として扱う。

## 対応資料

- `docs/design/cli-model-context-protocols-port-ssot.yaml`
- `docs/design/cli-mcp-port-implementation-ssot.yaml`
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/design/external-port-substrate-ssot.yaml`
- `docs/design/runtime-bundle-file-storage-ssot.yaml`
- `docs/design/runtime-bundle-audit-approval-ssot.yaml`
- `docs/design/runtime-bundle-export-sftp-ssot.yaml`

## 対象ファイル名

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

## 対象関数名またはruntime境界名

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

## NG軸

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

## 受入条件

- [ ] read/export は必ず user auth/authz → scope resolution → credential/capability requirement resolution → ManifestDispatcher/runtime dispatch → Data Reader/authorized read model を通る。
- [ ] Context API / Data Reader / import candidate assignment の dispatch bypass test / guard がある。
- [ ] AI/MCP/CLI は draft_operation / commit_candidate 作成までで、DB commit / approval execution / arbitrary mutation を実行できない。
- [ ] commit_candidate は source transcript / root utterance / confidence / unresolved fields / preview diff を保持する。
- [ ] user approval 後のみ canonical commit dispatch 経由で DB commit へ進む。
- [ ] create_export_job / draft_operation / commit_candidate / audit_log / runtime_event_log 以外の system-controlled write を追加していない。
- [ ] external_port_substrate の secure consumer dispatch lane とは関連するが同一 Bundle として混同していない。
- [ ] 関連 backend/frontend tests または `.agent/tests/*` が追加/更新されている。
