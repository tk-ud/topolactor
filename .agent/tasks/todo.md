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

## Infra / Demo Runtime

- [ ] nginx service を docker compose に接続する
      → 前提: frontend service と backend service が `infra/docker-compose.yml` に追加されること。
      → 現時点では postgres / adminer のみ。frontend/backend docker サービスが定義されるまで nginx upstream を compose に追加しない。
      → 追加時の対象: infra/docker-compose.yml (nginx service + depends_on 追加), docs/demo-walkthrough.md (nginx 有効化手順)。
      → infra/nginx.conf はすでに frontend:8000 / backend:5000 upstream を定義済み。
