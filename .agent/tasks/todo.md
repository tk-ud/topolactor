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

## Temporary Planning Surface

- [ ] agent が任意で使える一時方針メモ `.agent/tmp/tmp.txt` の運用と配線を追加する
      → 目的: 作業量が多いと agent が判断した場合に、初期構想時の制約だけを `.agent/tmp/tmp.txt` へ短く記録し、Policy Judgment Checklist 前に最終差分との scope drift を確認する。
      → tmp は永続成果物ではなく、初期方針の一時参照点として扱う。長い reasoning log やPR summary代替にしない。
      → `.agent/tmp/tmp.txt` は commit 対象にしない。作業開始時に必要なら `create-tmp.sh` で作成し、Policy Judgment Checklist 前の確認後に `delete-tmp.sh` で削除する。
      → checklist 前に tmp が存在する場合は、tmp の初期制約と `git diff main...HEAD` の最終差分を比較し、方針通りなら tmp を削除して local CI へ進む。ズレがある場合は修正するか、意図的な方針変更理由を completion report / PR summary に明記する。
      → local CI は tmp 確認と削除が完了した後に実行する。`bash .agent/tests/check-structure.sh` は従来通り最後に実行する。
      → `check-structure.sh` は `.agent/tmp/tmp.txt` が残存している場合に fail させる。tmp 未使用時は pass する。
      → 配線対象候補: AGENTS.md, .agent/rules/rule.md, .gitignore, .agent/scripts/create-tmp.sh, .agent/scripts/delete-tmp.sh, .agent/tmp/.gitkeep, .agent/tests/check-structure.sh, .agent/docs/required-paths.yaml。
      → 受け入れ条件: tmp 利用は任意、tmp は commit されない、tmp 残存は structure check で検出される、completion report で tmp 使用有無と削除済み状態を報告できる。

## Infra / Demo Runtime

- [ ] nginx service を docker compose に接続する
      → 前提: frontend service と backend service が `infra/docker-compose.yml` に追加されること。
      → 現時点では postgres / adminer のみ。frontend/backend docker サービスが定義されるまで nginx upstream を compose に追加しない。
      → 追加時の対象: infra/docker-compose.yml (nginx service + depends_on 追加), docs/demo-walkthrough.md (nginx 有効化手順)。
      → infra/nginx.conf はすでに frontend:8000 / backend:5000 upstream を定義済み。