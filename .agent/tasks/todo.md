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

- [ ] [Codex] Validate db/init.sql compose bootstrap on fresh postgres volume in docker-enabled environment
      → 対象: `db/init.sql`, `infra/docker-compose.yml`。`ui_component_bucket` / `ui_topology_tensor` 作成確認まで実施し、確認後に削除/完了化。

## System Operation CI (Issue #83)

- [ ] [Claude] SystemOperationCiRuntime の backend-tests CI 検証
      → 実装完了 (branch: claude/issue-83-tasks-PbiMy)。
      → 対象: `backend/runtime/SystemOperationCiRuntime.cs`, `backend/tests/.../SystemOperationCiRuntimeTests.cs`
      → dotnet が環境にないため local 実行不可。remote CI (backend-tests workflow) で確認後に [x] 化。

- [ ] [Claude] SystemOperationCiRuntime の event-driven CI 接続 (RunTopologyVectorRuntimeExtensionAsync)
      → 現状: InspectHubAttentionAfterUpdate / InspectEvidenceIntegrity / InspectFeedbackEvents は定義済み。
      → 未完: ContextRouteRecommendationResolver.RunTopologyVectorRuntimeExtensionAsync から呼び出しを追加する。
      → Blocking → ExplicitError("TVR_EXTENSION_FAILED") / Gap → LogWarning and continue。
      → 完了条件: resolver に SystemOperationCiRuntime を DI し、event 後に呼び出す実装 + テスト追加。

- [ ] [Claude] Cron trigger 接続 (background worker / scheduled job)
      → InspectHubAttentionContinuityAsync / InspectCurrentRebuildabilityAsync は定義済み。
      → cron trigger → Runtime excitation → SystemOperationCiRuntime 呼び出し導線が未実装。
      → 完了条件: cron dispatch endpoint / background worker から呼び出す実装 + .agent/reports/ への診断結果書き込み。

- [ ] [Claude] Registry 連続性探索 (orphaned registry detection) の実装
      → 孤立 registry (どの hub からも参照されない registry) の DB 検査クエリが未実装。
      → 対象: ContextRouteRepository に LoadOrphanedTokenSummaryForCiAsync などを追加する。
      → 完了条件: CRON_ORPHANED_REGISTRY finding を持つ InspectRegistryContinuityAsync を実装・テスト。

## Registry Tensor Continuity

- [ ] [Claude] Context Route / Topology Vector Runtime の旧vector実装を DB topology observation runtime へ移行する
      → 実装完了 (branch: claude/process-todo-tasks-yXNvS, commit: 6db556c)。
      → 実施内容: BuildEventVector → BuildMultiHotVector (1.0f per token ID)、tokenValueMap 依存を除去、tokenIds proxy as relationIds を廃止、DDLコメント・UI文言・DTO名を multi-hot / rebuildable projection cache に更新。
      → 残タスク: backend-tests CI 検証 (backend-tests workflow は PR / main push でのみ起動。PR作成 or main mergeで CI pass を確認してから [x] 化すること)。
      → 完了条件: backend-tests remote CI PASS 確認後に [x]。

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
