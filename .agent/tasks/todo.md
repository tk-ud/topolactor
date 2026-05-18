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

- [ ] DB駆動の抽象 delete 関数を定期 scheduler で実行する
      → self-learning DB / recommendation runtime は append-only context event / operation history を蓄積するため、
         放置するとユーザー操作 log が肥大化する。
      → create 側は UI操作を起点に DB Registry / function_parameters / topology policy から抽象 create 関数を解決して実行する。delete 側も同じ Runtime 導線で対称に扱う。
      → OS cron 直結ではなく、独自 backend runtime の scheduled worker / scheduler として扱う。
         cron は excitation trigger として context を Runtime に渡すだけ。
         package selection / retention 期間 / 有効無効は policy surface（function_parameters / Manifest）で管理する。
         設計方針詳細: docs/design/runtime-excitation-and-package-dispatch.md 参照。
      → admin 管理の Manifest / Registry / function_parameters / structure_map policy で、対象log種別・retention期間・実行頻度・on/off を管理する。
      → scheduler は Manifest 上の抽象 delete 関数を解決し、DB上で具体的な delete / anonymize / aggregate promotion 処理として実行する。
      → cleanup は optional 機能として有効/無効を切り替えられるようにする。無効時も明示的に Disabled status を返し、silent fallback にしない。
      → 期限切れの raw user operation log を削除し、必要な transition aggregates / audit summary は保持方針に従って残す。
      → 実装時の対象候補: db schema / backend scheduled runtime / admin manifest UI / retention policy seed / docs/design/context-route-recommendation.md / local CI test。
      → 未解決点: raw log を完全削除するか、匿名化・集計済み状態へ promotion してから削除するかを決める。

## Promotion CI / SQL Patch Recommendation

- [ ] jsonb → DB化候補を CI 検証済み SQL patch candidate として扱う設計SSOTを追加する
      → 現状の promotion flow は jsonb / usage metrics / promotion manifest / admin approval を前提としているが、SQL patch candidate を CI で検証し、checked=true/false によって promote / reject / recommend delete へ分岐する導線が未定義。
      → cron excitation trigger は context を Runtime に渡すだけとし、Runtime が promotion_ci package を選択する。
      → DB は「何を検査するか」の Registry / Manifest / policy を持ち、C# Runtime が許可された C# 関数を実行し、その C# 関数が必要に応じて allowlisted sh を実行する。
      → CI 結果は promotion_check_result として記録し、checked=true の候補だけ update / promote 対象にする。checked=false は reject / recommend delete / expire 候補にする。
      → 直接 DB から任意 sh を実行しない。sh 実行は C# package runtime の明示関数に閉じる。
      → 対象候補: docs/design/promotion-ci-sql-patch-runtime.md, docs/framework-policy.yaml, .agent/tasks/todo.md。

## Infra / Demo Runtime

- [ ] nginx service を docker compose に接続する
      → 前提: frontend service と backend service が `infra/docker-compose.yml` に追加されること。
      → 現時点では postgres / adminer のみ。frontend/backend docker サービスが定義されるまで nginx upstream を compose に追加しない。
      → 追加時の対象: infra/docker-compose.yml (nginx service + depends_on 追加), docs/demo-walkthrough.md (nginx 有効化手順)。
      → infra/nginx.conf はすでに frontend:8000 / backend:5000 upstream を定義済み。