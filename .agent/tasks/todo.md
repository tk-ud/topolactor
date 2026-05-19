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

## Registry Tensor Continuity

- [ ] [Claude] Context Route / Topology Vector Runtime の旧vector実装を DB topology observation runtime へ移行する
      → 実装完了 (branch: claude/process-todo-tasks-yXNvS, commit: 6db556c)。
      → 実施内容: BuildEventVector → BuildMultiHotVector (1.0f per token ID)、tokenValueMap 依存を除去、tokenIds proxy as relationIds を廃止、DDLコメント・UI文言・DTO名を multi-hot / rebuildable projection cache に更新。
      → 残タスク: backend-tests CI 検証 (backend-tests workflow は PR / main push でのみ起動。PR作成 or main mergeで CI pass を確認してから [x] 化すること)。
      → 完了条件: backend-tests remote CI PASS 確認後に [x]。

- [x] [Codex] registry tensor projection surface の実装乖離点検（runtime / endpoint / scheduler / function / UI topology）
- [ ] [Codex] registry tensor projection surface の実装乖離点検（runtime / endpoint / scheduler / function / UI topology）
      → 問題点: SSOT と監査Policyの明文化後、実装側で surface 間の意味連続性が崩れる余地がある。
      → 目的: projection/expansion surface ごとの drift を点検し、必要なら別PRで是正する。
      → 対象ファイル候補: docs/design/*, backend/runtime/*, frontend/*, db/*（点検のみ）
      → 次の判断点: 点検チェックリスト化の要否を判断。

- [ ] [Codex] Implement package-generator runtime/endpoint wiring for ui_component_bucket -> ui_topology_tensor persistence (tracked after SSOT/schema alignment).
- [ ] [Codex] registry tensor projection continuity 軽量チェックリストを追加する
      → 問題点: registry tensor projection surface の定期点検観点（runtime / endpoint / scheduler / function / UI / DB の6面）が未定義で、drift 判定が属人的になる。
      → 目的: 実装完了判定ではなく、projection continuity の静的監査観点を軽量チェックリスト化する。
      → 改善方針: checklist肥大化を避け、6面の存在確認・write/read surface・未実装境界・残TODO保存だけを確認する軽量ゲートにする。
      → 対象ファイル名: .agent/checklists/*, .agent/protocols/reports-and-todos.md
      → 対象関数名: なし
      → todo: 後続PRで checklist 形式と必要なら self-test を追加する。
      → 次の判断点: check-policy-judgment.sh とは分離し、registry tensor projection continuity 専用の静的監査チェックにするか判断する。
