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

## Public Demo Scaffold v1（Hub DB / Registry / Package / Component / Recommendation）

- [ ] 0. 前提条件の成立確認（着手ゲート）
      → PR #19 の scope 整合が完了済みであることを確認し、未完了なら本デモ実装は開始しない。確認結果は PR 説明または作業ログに明示する。
      → 判断点: 「整合済み」をどの証跡（PR link / merge commit / issue close）で扱うか。

- [ ] 1. デモ方針と制約の固定（設計ガード）
      → topolactor を CRUD/MVC 化しない、canonical runtime route（operation → vector → attractor → structure_map → package → schema → emission）を迂回しない、frontend は projection のみに徹する、silent fallback を入れない、を実装前に明文化する。
      → 対象: README.md, docs/demo-walkthrough.md（方針記述）, 実装全体レビュー観点。
      → 判断点: route は `/demo` か `/admin/demo` か（public exposure と運用境界のトレードオフ）。

- [ ] 2. Runtime policy 値の配置方針を先に確定
      → 設定値・閾値・候補数・優先度など Runtime behavior へ影響する値は hardcode せず、DB Registry / Manifest / structure_map / package-schema parameter へ寄せる。
      → 対象: db/context_route_tables.sql, db/demo_seed.sql, frontend/package/*, frontend/schema/*。
      → 判断点: 既存テーブルで表現可能か、追加カラム/追加registryが必要か。未実装なら missing-policy を明示する。

- [ ] 3. DB デモシード追加（public fake data 限定）
      → `db/demo_seed.sql` を追加し、実ビジネスデータを含まない fake/demo data のみで、hub / entities / topology registry / structure_map / package-schema-component refs / context token / recommendation policy / context events / prefix vector examples の関係が追えるようにする。
      → コメントで「どの registry 変更が runtime 解決へどう効くか」を追跡可能にする。
      → SQL 実行可能性より既存 schema 整合を優先し、矛盾がある場合は理由を記録する。
      → 対象: db/demo_seed.sql, db/README.md（seed適用意図の追記）, 必要に応じて db/context_route_tables.sql。

- [ ] 4. Frontend demo components 追加（projection 専用）
      → 以下の component を追加し、runtime resolved data の表示に徹する（ローカル業務ロジックを持たせない）。
      → `frontend/components/HubOverviewCard.tsx`（resolved hub summary）
      → `frontend/components/EntityTableProjection.tsx`（demo entity list projection）
      → `frontend/components/RecommendationPanel.tsx`（context_route_recommendation projection）
      → `frontend/components/ContextTokenBadgeList.tsx`（context token registry badge 表示）
      → 判断点: 既存 `renderEmission` / component registry 経由で差し込むか、route 側 composition 最小化で差し込むか。

- [ ] 5. Demo package/schema 追加（component 直結回避）
      → `demo_hub_overview_package` / `demo_entity_registry_package` / `demo_recommendation_package` と対応 schema を追加し、route で component を固定直結しすぎず、structure_map → package → schema → component expansion の可視性を担保する。
      → 対象: frontend/package/*, frontend/schema/*, frontend/registry/componentRegistry.ts, frontend/runtime/renderEmission.ts（必要最小限の接続）。
      → 判断点: 命名規約を既存 package/schema と揃えるか、demo 専用 prefix を採用するか。

- [ ] 6. Demo route 追加（public scaffold 明示）
      → `/demo` または `/admin/demo` に demo route を追加し、「これは public scaffold demo であり production UI ではない」旨を明記する。
      → 画面上で「DB Registry / Hub 変更 → Runtime 解決変化 → UI projection 変化 → context recommendation 変化」が追える導線を用意する。
      → 対象: frontend/routes/demo.tsx または frontend/routes/admin/demo.tsx。

- [ ] 7. Walkthrough / README の最小導線整備
      → `docs/demo-walkthrough.md` を追加し、初見ユーザー向けに「何を変更し、どこを見ると価値が分かるか」を step-by-step で記述する。
      → `README.md` に短い Demo セクションを追加し、walkthrough への入口を作る。
      → 必須説明: fake/demo data であること、production business data 非依存、runtime canonical route を維持していること。

- [ ] 8. 構造チェック・ローカルCIゲート通過
      → 必須: `bash .agent/tests/check-structure.sh`。
      → DB/SQL を変更した場合: `bash .agent/tests/check-db-schema.sh`。
      → Frontend（Fresh/Deno/Preact）を変更した場合: `bash .agent/tests/check-frontend-types.sh`。
      → いずれか失敗時は修正→再実行。CI red のまま commit/push しない。

- [ ] 9. 受け入れ確認（デモ価値の判定）
      → 以下が一目で確認できることを受け入れ条件とする。
      → (a) DB/Registry を変えると Runtime 解決が変わる。
      → (b) Runtime 解決が変わると UI projection が変わる。
      → (c) context recommendation が変わる。
      → 判断点: 最小変更シナリオ（例: token/priority/route policy 1点変更）で差分が説明可能か。

- [ ] 10. 対象ファイル計画の棚卸し（実装前チェックリスト）
      → DB: `db/demo_seed.sql`, `db/README.md`, `db/context_route_tables.sql`
      → Frontend: `frontend/components/HubOverviewCard.tsx`, `frontend/components/EntityTableProjection.tsx`, `frontend/components/RecommendationPanel.tsx`, `frontend/components/ContextTokenBadgeList.tsx`, `frontend/package/*`, `frontend/schema/*`, `frontend/routes/demo.tsx` or `frontend/routes/admin/demo.tsx`, `frontend/runtime/renderEmission.ts`
      → Docs: `docs/demo-walkthrough.md`, `README.md`
      → 実際の変更対象は最小化し、未変更ファイルは理由付きで除外判断を残す。

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

- [x] `infra/docker-compose.yml` を追加する
      → demo 用 Postgres / Adminer を `topolactor_demo` 固定credentialで起動する最小 compose を追加済み。
      → 本番用途ではない。公開 scaffold のローカル確認用。
      → 作成済み: `infra/docker-compose.yml`。

- [ ] docker compose の seed 適用順と実行確認を行う
      → `docker compose -f infra/docker-compose.yml up` で schema / topology / promotion / context route / seed_empty が適用されることを確認する。
      → `db/demo_seed.sql` 追加後は compose init mount に含めるか、walkthrough 側で明示実行にするか判断する。
      → 対象: infra/docker-compose.yml, db/README.md, docs/demo-walkthrough.md。
