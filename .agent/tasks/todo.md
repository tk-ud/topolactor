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

## 担当エージェント方針

- AI駆動OS, 小規模修正 = Codex
- data駆動OS, 大規模リファクタ Runtime構築 = Claude

## Issue #60 — Acceptance & Close Readiness

- [ ] [Codex] Issue #60 close 前の acceptance audit を実施する
      → 残ブロッカー: remote CI (backend-tests / frontend-types) の pass 確認が必要。
      → 対象ファイル: .agent/tasks/todo.md
      → 次の判断点: CI pass を確認し、close 可否を判定する。

## Runtime Meaning Check Verification

- [ ] [Codex] check-runtime-semantics.sh を dotnet / deno 利用可能環境で実行し、runtime意味チェックの実行結果を確定する
      → NOT EXECUTABLE: dotnet / deno が本環境で利用不可。Remote CI equivalence（backend-tests.yml / frontend-types.yml）の pass 確認が必要。
      → 対象ファイル: .agent/tests/check-runtime-semantics.sh
      → 次の判断点: GitHub Actions CI が pass したことを確認してから close。

## Codex Governance Audit Experiments

- [x] [Codex] Policy Judgment のテンプレート直実行誤用を検出する監査を実施する
      → 理由: 手順逸脱により gate 判定が形骸化するリスクがある。
      → 対象ファイル: .agent/protocols/policy-judgment.md, .agent/checklists/check-policy-judgment.sh
      → 次の判断点: テンプレート利用と実行手順の差分を監査報告に明記できるか確認する。

- [x] [Codex] Policy Judgment FAIL / NOT EXECUTED / queued CI を PASS と誤認しないか監査する
      → 理由: completion 判定と実 CI 状態の claim drift を防止する必要がある。
      → 対象ファイル: .agent/protocols/completion.md, .agent/protocols/policy-judgment.md
      → 次の判断点: FAIL/未実行/保留を明示的に blocking 扱いできているか検証する。

- [x] [Codex] docs-only 変更でも runtime / policy behavior claim を含む場合に Policy Judgment を要求するか監査する
      → 理由: ファイル種別ベースの例外で判断漏れが起きる可能性がある。
      → 対象ファイル: .agent/protocols/policy-judgment.md, .agent/protocols/completion.md
      → 次の判断点: claim ベース判定の適用条件を監査で再現できるか確認する。

- [x] [Codex] PR Summary と実際の diff の claim drift を監査する
      → 理由: 報告内容と変更実体の乖離は Recursive Verification Gate 破綻につながる。
      → 対象ファイル: .agent/protocols/reports-and-todos.md, .agent/protocols/completion.md
      → 次の判断点: summary 各主張が diff の根拠へトレース可能か点検する。

- [x] [Codex] `.agent/tmp/tmp.txt` の作成・検証・削除順序を守るか監査する
      → 理由: 一時契約のライフサイクル崩れは gate の証跡不整合を生む。
      → 対象ファイル: .agent/protocols/scenario-contract.md, .agent/protocols/completion.md
      → 次の判断点: 生成→照合→削除の順序違反を検出可能か確認する。

- [x] [Codex] `bash .agent/tests/check-structure.sh` を最後に実行する運用を監査する
      → 理由: required local checks の終端順序が崩れると completion gate を満たせない。
      → 対象ファイル: AGENTS.md, .agent/tests/check-structure.sh
      → 次の判断点: 実行ログ上で最終コマンドとして確認できるか検証する。

- [x] [Codex] todo.md の `[x]` 更新が Recursive Verification Gate 通過後に限定されているか監査する
      → 理由: 先行完了マークは未解決ブロッカーの隠蔽につながる。
      → 対象ファイル: .agent/tasks/todo.md, .agent/protocols/completion.md
      → 次の判断点: gate 通過前の完了更新を検出する監査観点を固定化できるか確認する。

## Claude Implementation Boundary Audit Experiments

- [ ] [Claude] context_event append が silent failure にならないか監査する
      → 理由: append failure の握りつぶしは runtime 観測性を失わせる。
      → 対象ファイル: runtime / persistence 関連実装（監査時に確定）
      → 次の判断点: failure が explicit result として surface されるか検証する。

- [ ] [Claude] transition stats / context event / TVR extension の失敗を「継続可能」にしてよい境界を監査する
      → 理由: recoverable 境界の誤設定で policy と実装の整合が崩れる。
      → 対象ファイル: runtime executor / recommendation runtime 関連実装（監査時に確定）
      → 次の判断点: 継続可否の判定根拠を policy surface に照合できるか確認する。

- [ ] [Claude] function_parameters 由来にすべき値を runtime 定数化しないか監査する
      → 理由: policy 可変値の定数化は SSOT 逸脱を起こす。
      → 対象ファイル: runtime policy 読み取り / registrar validation 関連実装（監査時に確定）
      → 次の判断点: 可変パラメータのソースが function_parameters に統一されているか確認する。

- [ ] [Claude] DB CHECK / column name / seed policy が policy可変性と衝突していないか監査する
      → 理由: schema 固定と policy 変更余地の矛盾が運用停止を誘発する。
      → 対象ファイル: db/*.sql, seed policy 関連定義（監査時に確定）
      → 次の判断点: 変更可能前提の項目が DB 制約で過固定化されていないか確認する。

- [ ] [Claude] append-only log と rebuildable materialized current を混同しないか監査する
      → 理由: 役割混同は履歴完全性と再構築可能性を同時に損なう。
      → 対象ファイル: log/current テーブル定義と repository 実装（監査時に確定）
      → 次の判断点: append-only と current projection の責務境界が明示されているか確認する。

- [ ] [Claude] frontend に topology / cosine / MLP / feedback 判定を漏らさないか監査する
      → 理由: 判定ロジックの frontend 流出は境界違反となる。
      → 対象ファイル: frontend projection / admin UI 関連実装（監査時に確定）
      → 次の判断点: frontend は structured result の投影のみに留まっているか検証する。

- [ ] [Claude] hub identity / target_table / candidate_kind / candidate_id / scope_limit の境界キー欠落を監査する
      → 理由: 境界キー欠落は集計歪み・誤関連付けを起こす。
      → 対象ファイル: runtime dispatch / repository key mapping 関連実装（監査時に確定）
      → 次の判断点: 各キーが write/read 両経路で保持されるか確認する。

- [ ] [Claude] SQL Attention を単なる推薦UI実装へ矮小化しないか監査する
      → 理由: DB-backed attention の意味境界を UI 層へ誤転写するリスクがある。
      → 対象ファイル: recommendation runtime / SQL attention 関連実装（監査時に確定）
      → 次の判断点: attention の責務が runtime+persistence 側に維持されているか確認する。

- [ ] [Claude] optional / future extension を runtime implemented と誤記しないか監査する
      → 理由: 実装済み主張の先走りは completion 報告の信頼性を損なう。
      → 対象ファイル: docs/design/*.md, reports/todos 関連ドキュメント（監査時に確定）
      → 次の判断点: 実装済み・未実装・将来拡張のラベル整合を確認する。

- [ ] [Claude] exploration slot を完全ランダム候補として実装しないか監査する
      → 理由: 推薦境界条件を失い、policy 期待と乖離する。
      → 対象ファイル: recommendation candidate selection 関連実装（監査時に確定）
      → 次の判断点: exploration 条件が policy/runtime 定義に沿っているか検証する。

- [ ] [Claude] happy path のみのテストで boundary failure matrix を省略しないか監査する
      → 理由: failure matrix 欠落は運用時障害の未検出を招く。
      → 対象ファイル: tests / protocols / completion artifacts（監査時に確定）
      → 次の判断点: success 以外の failure 系ケースが網羅されているか確認する。
