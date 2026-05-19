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

## Registry Tensor Continuity

- [ ] [Claude] Context Route / Topology Vector Runtime の旧vector実装を DB topology observation runtime へ移行する
      → 実装完了 (branch: claude/process-todo-tasks-yXNvS, commit: 6db556c)。
      → 実施内容: BuildEventVector → BuildMultiHotVector (1.0f per token ID)、tokenValueMap 依存を除去、tokenIds proxy as relationIds を廃止、DDLコメント・UI文言・DTO名を multi-hot / rebuildable projection cache に更新。
      → 残タスク: backend-tests CI 検証 (backend-tests workflow は PR / main push でのみ起動。PR作成 or main mergeで CI pass を確認してから [x] 化すること)。
      → 完了条件: backend-tests remote CI PASS 確認後に [x]。

- [ ] [Codex] Implement package-generator runtime/endpoint wiring for ui_component_bucket -> ui_topology_tensor persistence (tracked after SSOT/schema alignment).
      → 実装完了 (branch: claude/process-todo-tasks-wPH6O)。
      → 実施内容: UiTopologyRepository (abstract + NpgsqlUiTopologyRepository), PackageGeneratorRuntime, PackageGeneratorEndpoint を追加。Program.cs に DI登録・ルート (GET /admin/ui-component-bucket, POST /admin/package-generator/generate) を追加。ユニットテスト (PackageGeneratorEndpointTests) 追加。
      → 残タスク: backend-tests remote CI PASS 確認 (dotnet ローカル不可 / REQUIRED_NOT_EXECUTED)。
      → 完了条件: backend-tests remote CI PASS 確認後に [x]。

- [x] [Codex] registry tensor projection continuity 軽量チェックリストを追加する
      → 問題点: registry tensor projection surface の定期点検観点（runtime / endpoint / scheduler / function / UI / DB の6面）が未定義で、drift 判定が属人的になる。
      → 目的: projection/expansion continuity の静的監査観点を軽量チェックリスト化し、routine/periodic audit の判定基準を安定化する。
      → 改善方針: checklist肥大化を避け、6面の存在確認・write/read surface・未実装境界・残TODO保存だけを確認する軽量ゲートにする。
      → 対象ファイル名: .agent/checklists/*, .agent/protocols/reports-and-todos.md
      → 対象関数名: なし
      → 実装完了: `.agent/checklists/registry-tensor-projection-continuity.md` と `check-registry-tensor-projection-continuity.sh --self-test` を追加。
      → 判断結果: check-policy-judgment.sh から分離した専用静的監査チェックとして実装済み。
