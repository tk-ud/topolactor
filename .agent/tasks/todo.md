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

- [ ] JWT login scaffold を追加する
      → public demo / admin demo の最小ログイン導線として実装する。実ビジネス認証ではなく demo scaffold 用。
      → JWT secret / issuer / expiry など Runtime/Auth behavior に影響する値は hardcode せず、環境変数または demo policy surface に寄せる。
      → 対象候補: backend/endpoint/*, backend/guard/*, backend/schema/*, frontend/routes/*, frontend/api/*。

- [ ] password hash を bcrypt 系で扱う
      → demo user の password は平文保存しない。demo credential は公開用でも hash 済み seed / config とする。
      → bcrypt 採用可否、C# 側 library、seed 生成方法を確認する。
      → 対象候補: db/demo_seed.sql, backend auth endpoint, auth repository/guard。

## Infra / Demo Runtime

- [ ] nginx service を docker compose に接続する
      → frontend / backend service が `infra/docker-compose.yml` に追加されたタイミングで nginx service を有効化する。
      → 現時点では Postgres / Adminer のみなので、未定義 upstream を compose に接続しない。
      → 対象: infra/docker-compose.yml, infra/nginx.conf, docs/demo-walkthrough.md。

- [ ] docker compose の seed 適用順と実行確認を行う
      → `docker compose -f infra/docker-compose.yml up` で schema / topology / promotion / context route / seed_empty が適用されることを確認する。
      → `db/demo_seed.sql` 追加済み。compose init mount に含めるか、walkthrough 側で明示実行にするか判断する。
      → 対象: infra/docker-compose.yml, db/README.md（手順記載済み）, docs/demo-walkthrough.md。

