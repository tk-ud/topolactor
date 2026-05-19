# Negative Consistency Checklist

Run before completion eligibility decision.

## Q1. タスク目的と実装差分が意味的にズレているように見えるが、問題ないか？
Answer:
Evidence:
Remaining risk:

## Q2. AGENTS.md / policy / scenario contract / boundary contract に違反しているように見えるが、問題ないか？
Answer:
Evidence:
Remaining risk:

## Q3. 成功扱いになっているが、未完了・未検証・部分失敗が残っているように見えるが、問題ないか？
Answer:
Evidence:
Remaining risk:

Completion-Eligibility:

Allowed Completion-Eligibility values: `PASS` / `BLOCKING`

Eligibility rule:
- Any Q1/Q2/Q3 = `問題あり` => Completion-Eligibility must be `BLOCKING`.
- `Completion-Eligibility: PASS` is allowed only when Q1/Q2/Q3 are all `問題なし`.
