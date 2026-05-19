# Codex Governance Audit Report (2026-05-19)

## Scope
- .agent/protocols/policy-judgment.md
- .agent/protocols/completion.md
- .agent/protocols/reports-and-todos.md
- .agent/protocols/scenario-contract.md
- .agent/checklists/policy-judgment.md
- .agent/checklists/check-policy-judgment.sh

## Findings

### 1) Policy Judgment テンプレート直実行誤用の検出
- `check-policy-judgment.sh` は `Answer: ` 行を厳格に 15 件要求し、テンプレート未記入や欠損を V16/V15 として FAIL 化する。
- これによりテンプレートそのものを未回答で使う誤用は gate で検出される。

### 2) FAIL / NOT EXECUTED / queued CI の PASS 誤認防止
- protocol 側で `NOT EXECUTED ≠ PASS` と `queued/in_progress is blocking` を明示。
- completion sequence でも Remote CI Equivalence Gate により同条件を blocking と定義。

### 3) docs-only でも runtime/policy claim を含む場合の Policy Judgment 要否
- policy-judgment protocol の trigger に「docs or summaries that assert runtime or policy behavior」を明記。
- checklist 冒頭にも同趣旨が再掲され、claim-based 判定が成立。

### 4) PR Summary と diff の claim drift 監査
- completion sequence が full branch diff inspection を必須化。
- reports-and-todos protocol が PR-level report の配置と remaining TODO surface を分離し、claim/diff の照合前提を保持。

### 5) `.agent/tmp/tmp.txt` の create/verify/delete 順序
- scenario-contract protocol で create/delete コマンドを固定。
- completion sequence で diff照合・recursive verification 完了後に delete する順序を固定。

### 6) `check-structure.sh` を最後に実行する運用
- AGENTS.md と completion sequence の双方で最終実行を明示。

### 7) todo `[x]` 更新を Recursive Verification Gate 通過後に限定
- completion sequence step 11 と reports-and-todos に同条件を明示。

## Conclusion
- 上記 7 監査観点について、現行ドキュメント・チェック実装で検出/抑止の要件を満たしていることを確認した。
- 残ブロッカーは本 report では新規に検出されていない。既存の remote CI 依存 TODO は継続。
