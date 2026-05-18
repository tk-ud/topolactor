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

## Auth / Demo Login

- [ ] backend HTTP host と /auth/login HTTP route を実装する
      → `backend/endpoint/AuthEndpoint.cs` に認証ロジッククラスは存在するが、
         backend に HTTP ホスト (ASP.NET Core Program.cs / WebApplication 等) がなく、
         HTTP route バインドが未実装。
      → `frontend/routes/api/auth/login.ts` は `DEMO_BACKEND_URL/auth/login` に
         proxy しているが、この backend エンドポイントが稼働していない限り機能しない。
      → 実装時の対象候補: backend/Program.cs または backend/Host.cs,
         MinimalAPI または Controller routing, DEMO_BACKEND_URL 設定手順。
      → docs/demo-walkthrough.md に「backend HTTP route 未実装」と明記済み。

## Runtime Log Retention

- [ ] Codex 向け実装プロンプトに従い、LogRetentionExecutor / Scheduler / Repository を実装する
      → 要件定義完了済み。実装方針: aggregate-then-delete（context_transition_stats は保持、raw context_event を削除）
      → policy surface: function_parameters (function_name='context_event_retention', parameter_key='retention_policy')
      → policy に必要なフィールド: enabled / hot_days / cold_days / archive_strategy / batch_size / log_types
      → db/seed_empty.sql の既存シードに enabled・log_types を追加し、scheduler 間隔シードを追加する
      → 新規作成対象:
           backend/schema/RetentionContracts.cs
           backend/repository/RetentionRepository.cs (stub)
           backend/repository/NpgsqlRetentionRepository.cs (FK 削除順序: event_vector_cache → prefix_vector_cache → context_event → context_session)
           backend/runtime/LogRetentionExecutor.cs (hardcode なし; MissingPolicy / MalformedPolicy / Disabled を明示返却)
           backend/runtime/LogRetentionScheduler.cs (IHostedService + PeriodicTimer; business logic なし)
           backend/tests/Topolactor.Runtime.Tests/LogRetentionExecutorTests.cs (Ok / Disabled / MissingPolicy / MalformedPolicy の 4 ケース)
      → スコープ外: context_transition_stats 削除、admin UI、cold archive テーブル、ON DELETE CASCADE 追加
      → 完了条件: 4 テスト PASS + check-structure.sh PASS + Policy Judgment Checklist all green

## Infra / Demo Runtime

- [ ] nginx service を docker compose に接続する
      → 前提: frontend service と backend service が `infra/docker-compose.yml` に追加されること。
      → 現時点では postgres / adminer のみ。frontend/backend docker サービスが定義されるまで nginx upstream を compose に追加しない。
      → 追加時の対象: infra/docker-compose.yml (nginx service + depends_on 追加), docs/demo-walkthrough.md (nginx 有効化手順)。
      → infra/nginx.conf はすでに frontend:8000 / backend:5000 upstream を定義済み。