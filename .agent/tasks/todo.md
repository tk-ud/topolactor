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

## Runtime Persistence Completion

- [ ] 汎用編集 diff log を state transition log と分離して定義・永続化する
      → 理由: `demo_state_transitions` は状態遷移ログであり、編集ログではない。現状、値の編集差分を append-only に永続化する汎用 edit diff log が未作成で、runtime persistence / audit / recommendation feedback の正規入力として扱えない。
      → 改善方針: state transition log と edit diff log を意味境界で分離し、編集ログは `target_table`, `target_id`, `operation`, `before_json`, `after_json`, `diff_json`, `actor`, `created_at` を持つ append-only audit として定義する。`DiffLogRepository` は ILogger 出力ではなく DB-backed 永続化へ移行する。
      → 対象ファイル: backend/repository/DiffLogRepository.cs, backend/repository/NpgsqlTopologyRepository.cs, db/schema.sql, db/topology_tables.sql, backend/tests/Topolactor.Runtime.Tests/*, docs/demo-walkthrough.md
      → 次の判断点: edit diff log を `topology_edit_log` として topology runtime 全体に共通化するか、entity edit log として domain_data 側に寄せるか。
      → 推奨担当: Codex

## Runtime Meaning Check Verification

- [ ] check-runtime-semantics.sh を dotnet / deno 利用可能環境で実行し、runtime意味チェックの実行結果を確定する
      → 理由: check-runtime-semantics.sh は追加済みだが、実行環境で dotnet / deno 不在の場合は未実行となるため、導線追加と実行確認を分ける必要がある。
      → 改善方針: dotnet / deno が利用可能な環境で backend runtime tests / integration tests / frontend API proxy tests を実行し、失敗時は原因を修正する。
      → 対象ファイル: .agent/tests/check-runtime-semantics.sh, backend/tests/Topolactor.Runtime.Tests/*, backend/tests/Topolactor.Integration.Tests/*, frontend/tests/*
      → 次の判断点: Docker Compose E2E smoke を次段階でCI必須に昇格するか、ローカル任意のまま維持するか。
      → 推奨担当: Codex

