# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。

完了済み作業・PR修正履歴・旧方針の残骸は残さない。
未完了の task がある場合のみ、次の形式で追加する。

```md
## <Area>

- [ ] <具体的な未完了作業>
      → <理由・対象ファイル・次の判断点>
```

## Current TODO

- [x] [Codex] Validate db/init.sql compose bootstrap on fresh postgres volume in docker-enabled environment
      → 実装完了 (branch: claude/process-todo-tasks-LkGVp)。
      → PostgreSQL 16 ローカル起動で全 SQL ファイル (schema.sql → topology_tables.sql → promotion_tables.sql → context_route_tables.sql → ui_topology_tables.sql → seed_empty.sql → demo_seed.sql) を ON_ERROR_STOP=1 で実行。
      → `ui_component_bucket` (8列) / `ui_topology_tensor` (12列) 作成確認済み。
      → 診断レポート: `.agent/reports/2026-05-20-db-init-compose-bootstrap-validation.md`

## Architecture Fix — Single Dispatch Endpoint (SSOT: framework-policy.yaml `backend_flow.style = vector_runtime_and_dispatcher_based`)

SSOT参照必読:
- `docs/framework-policy.yaml` (backend_flow, runtime_boundary_failure_matrix)
- `docs/framework-core.yaml` (canonical route, frontend_to_backend_dispatch flow)
- `docs/file-structure.yaml` (backend.principle: endpoint_is_thin_boundary)
- `docs/registrar-admin-ui-specification.md` (Section 8: Backend Boundary Policy)

違反現状:
- Program.cs に 7 本の HTTP route が存在。正しくは POST /dispatch 1本 + GET /health + POST /auth/login のみ。
- AdminEndpoint / PackageGeneratorEndpoint が Repository を直接呼び出し、canonical route を経由しない。
- frontend/routes/api/admin/ に複数の proxy ファイルが存在。正しくは /api/dispatch 1本に集約。

---

- [x] [Claude] Step 1: Program.cs の JWT ベアラートークン抽出を `ExtractBearerToken` ヘルパー関数に抽出 (behavior-preserving refactor)
      → 対象: `backend/Program.cs`。全 7 route handler の3行抽出パターンを1行呼び出しに置換。

- [x] [Claude] Step 2: RuntimeExecutor に admin 操作ハンドラーを追加
      → 対象: `backend/runtime/RuntimeExecutor.cs`。
      → `context_token_registry` / `registry_vector_validate` / `ui_component_bucket` / `package_generator` の
         operation target を canonical route で dispatch できるよう case を追加。
      → AdminEndpoint / PackageGeneratorEndpoint のロジックを RuntimeExecutor 内ハンドラーへ移管。
      → SSOT: docs/framework-core.yaml `frontend_to_backend_dispatch.flow`、
               docs/framework-policy.yaml `backend_flow.processing_flow`
      → 事前読み必須: backend/runtime/RuntimeExecutor.cs、backend/runtime/OperationVectorResolver.cs、
                       backend/endpoint/AdminEndpoint.cs、backend/endpoint/PackageGeneratorEndpoint.cs
      → Scenario Contract 更新必須 (canonical route 変更を伴うため)
      → Runtime Boundary Failure Matrix (全10項目) を checklist に記入必須

- [x] [Claude] Step 3: Program.cs の admin/package-generator 専用 route を削除し、POST /dispatch に集約
      → 対象: `backend/Program.cs`。
      → 削除対象 route: GET|POST /admin/context-token-registry、POST /admin/context-token-registry/{id}/deprecate、
                         POST /admin/registry-vector-validate、GET /admin/ui-component-bucket、POST /admin/package-generator/generate。
      → AdminEndpoint / PackageGeneratorEndpoint の DI 登録も削除 (Step 2 完了後)。
      → 残留: GET /health、POST /auth/login、POST /dispatch の3本のみ。
      → SSOT: docs/framework-policy.yaml `backend_flow.style = vector_runtime_and_dispatcher_based`
      → Scenario Contract + Runtime Boundary Failure Matrix 必須

- [x] [Claude] Step 4: frontend admin proxy ファイルを削除し /api/dispatch に集約
      → 対象削除: `frontend/routes/api/admin/context-token-registry.ts`、
                  `frontend/routes/api/admin/context-token-registry/[tokenId]/deprecate.ts`、
                  `frontend/routes/api/admin/registry-vector-validate.ts`。
      → /api/dispatch.ts は既存のまま維持 (変更不要)。
      → admin UI 側の fetch 呼び出しを /api/admin/* → /api/dispatch に書き換え。
      → SSOT: docs/file-structure.yaml `frontend.directories.api: backend_contract_client`、
               docs/framework-core.yaml `runtime_wiring.frontend_to_backend_dispatch`
      → Scenario Contract + Runtime Boundary Failure Matrix (frontend proxy 項目) 必須

## System Operation CI (Issue #83)

- [x] [Claude] SystemOperationCiRuntime の backend-tests CI 検証
      → 実装完了 (branch: claude/issue-83-tasks-PbiMy, PR #104 wiring 含む)。
      → 対象: 全 backend/runtime/*, backend/repository/*, backend/tests/.../*.cs
      → remote CI (backend-tests workflow) PASS 確認済み (PR #104)。

- [x] [Claude] SystemOperationCiRuntime の event-driven CI 接続 (RunTopologyVectorRuntimeExtensionAsync)
      → 実装完了 (branch: claude/issue-83-tasks-PbiMy, PR #104)。
      → InspectEvidenceIntegrity: evidence extraction 後に呼び出し。Blocking → throw → TVR_EXTENSION_FAILED。
      → InspectHubAttentionAfterUpdate: hub attention record 構築後 (upsert 前) に呼び出し。Blocking → throw → TVR_EXTENSION_FAILED。
      → Gap → LogWarning + recommendation 継続。
      → SystemOperationCiRuntime を Program.cs に DI 登録済み。
      → テスト追加: StubBlockingEvidenceCiRuntime / StubGapEvidenceCiRuntime / NanEmaFastExistingRepository。

- [x] [Claude] Registry 連続性探索 (orphaned registry detection) の実装
      → 実装完了 (branch: claude/issue-83-tasks-PbiMy, PR #104)。
      → InspectRegistryContinuityAsync: LoadRegistryTokenSummaryForCiAsync → CRON_ORPHANED_REGISTRY (Gap)。
      → RegistryTokenCiSummary を SystemCiContracts.cs に追加。
      → NpgsqlContextRouteRepository.LoadRegistryTokenSummaryForCiAsync: context_token_registry で孤立 token カウント。
      → テスト追加: StubRegistryCiRepository + InspectRegistryContinuityAsync 3テスト。

- [x] [Claude] Cron trigger 接続 (background worker / scheduled job)
      → 実装完了 (branch: claude/process-todo-tasks-Ns7fy)。
      → SystemOperationCiScheduler (BackgroundService) を追加。InspectHubAttentionContinuityAsync /
         InspectCurrentRebuildabilityAsync / InspectRegistryContinuityAsync を定期呼び出し。
      → Program.cs に AddHostedService<SystemOperationCiScheduler>() 登録済み。
      → 診断結果レポート: .agent/reports/2026-05-19-system-operation-ci-scheduler.md
      → remote CI (backend-tests workflow) PASS 確認済み (PR #108)。

## Registry Tensor Continuity

- [x] [Claude] Context Route / Topology Vector Runtime の旧vector実装を DB topology observation runtime へ移行する
      → 実装完了 (branch: claude/process-todo-tasks-yXNvS, commit: 6db556c)。
      → 実施内容: BuildEventVector → BuildMultiHotVector (1.0f per token ID)、tokenValueMap 依存を除去、tokenIds proxy as relationIds を廃止、DDLコメント・UI文言・DTO名を multi-hot / rebuildable projection cache に更新。
      → remote CI (backend-tests workflow) PASS 確認済み (PR #93)。

- [x] [Codex] Implement package-generator runtime/endpoint wiring for ui_component_bucket -> ui_topology_tensor persistence (tracked after SSOT/schema alignment).
      → 実装完了 (branch: claude/process-todo-tasks-wPH6O)。
      → 実施内容: UiTopologyRepository (abstract + NpgsqlUiTopologyRepository), PackageGeneratorRuntime, PackageGeneratorEndpoint を追加。Program.cs に DI登録・ルート (GET /admin/ui-component-bucket, POST /admin/package-generator/generate) を追加。ユニットテスト (PackageGeneratorEndpointTests) 追加。
      → CI同期: PR #99 merge 後の main 最新 CI で backend-tests / Structure Check / default-entity-search の success を確認済み。
      → サマリ: package-generator runtime/endpoint wiring は完了。残TODOは本ファイルの未チェック項目のみ。

- [x] [Codex] registry tensor projection continuity 軽量チェックリストを追加する
      → 問題点: registry tensor projection surface の定期点検観点（runtime / endpoint / scheduler / function / UI / DB の6面）が未定義で、drift 判定が属人的になる。
      → 目的: projection/expansion continuity の静的監査観点を軽量チェックリスト化し、routine/periodic audit の判定基準を安定化する。
      → 改善方針: checklist肥大化を避け、6面の存在確認・write/read surface・未実装境界・残TODO保存だけを確認する軽量ゲートにする。
      → 対象ファイル名: .agent/checklists/*, .agent/protocols/reports-and-todos.md
      → 対象関数名: なし
      → 実装完了: `.agent/checklists/registry-tensor-projection-continuity.md` と `check-registry-tensor-projection-continuity.sh --self-test` を追加。
      → 判断結果: check-policy-judgment.sh から分離した専用静的監査チェックとして実装済み。
