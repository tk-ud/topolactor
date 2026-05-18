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

## Product Completion Roadmap (暫定)

- [ ] frontend demo から stub / skeleton / synthetic 表示を排除し、backend runtime emission の投影へ一本化する
      → 目的: demoを「説明用の静的表示」ではなく、DB seed + runtime resolver + emission を観測するプロダクト導線にする。
      → 改善方針: /demo の defaultStructureMap / demoTokens / synthetic Emission を正規runtime導線から外し、必要なら /demo-static 等へ隔離する。
      → 対象ファイル: frontend/routes/demo.tsx, frontend/components/ProjectionView.tsx, frontend/components/EmissionView.tsx, frontend/api/dispatch.ts, docs/demo-walkthrough.md
      → 次の判断点: /demo を runtime専用ページへ昇格するか、静的構造説明ページを別routeに分離するか。

- [x] ログイン済みユーザーが dispatch panel から backend /dispatch を実行できる導線を閉じる
      → 完了: frontend/routes/api/dispatch.ts 新規作成、LoginPanel が sessionStorage に JWT を保存、OperationPanel が token を読み取り Bearer 送信、未ログイン時は明示案内表示。Fresh単体 localhost:8000 でも /api/dispatch proxy を提供。

- [x] context_route recommendation を demo で観測可能な状態まで閉じる
      → 完了: demo_seed.sql を固定UUID event IDに変更し context_event_vector_cache / context_prefix_vector_cache をseed。
         demo_policy の min_neighbors を 3→1 に変更（ON CONFLICT DO UPDATE）。
         OperationPanel に Context フィールド（Session ID / Token IDs）を追加。
         backend JSON ポリシーを CamelCase property + SnakeCaseLower enum に統一。
         currentOperation を vector.Action ではなく vector.AttractorKey（完全キー形式）に修正。
         LoadRecentPrefixVectorsAsync の tableName filter を null（全候補対象）に修正。
         frontend/api/dispatch.ts の status union を snake_case に統一（RecommendationPanel と一致）。
         docs/demo-walkthrough.md に Scenario E 追加・route identity 定義を明記。
         dispatch panel で target=demo/layer=hub/action=overview + demo session ID + token_active で recommendation ok 結果が得られる。

- [ ] admin / registry 操作を skeleton 受付からDB永続化へ移行する
      → 理由: context_token_registry admin は画面とAPI shapeがあるが、未接続時501やスケルトン説明が残る。
      → 改善方針: context_token_registry のGET/POST/deprecateをbackendまたはFresh API経由でDBへ接続し、seed表示ではなく実データを編集する。
      → 対象ファイル: frontend/routes/admin/context-token-registry.tsx, frontend/islands/ContextTokenRegistryEditor.tsx, frontend/routes/api/admin/context-token-registry.ts, frontend/routes/api/admin/context-token-registry/[tokenId]/deprecate.ts, backend/repository/NpgsqlContextRouteRepository.cs
      → 次の判断点: admin API をbackendへ集約するか、Fresh API route をbackend proxyとして維持するか。

- [ ] DB-backed application runtimeとしての最小状態遷移ループを定義し、demo seed から実データ入力ループへ移行する
      → 目的: topolactor を単なる runtime scaffold ではなく、任意ドメインの application state loop を持つ runtime へ進める。
      → 改善方針: state transition、event/diff履歴、list/detail projection を含む最小schema/package/componentを定義し、runtime route に載せる。
      → 対象ファイル: db/schema.sql, db/topology_tables.sql, db/demo_seed.sql, backend/runtime/*, frontend/routes/*, frontend/components/*, docs/demo-walkthrough.md
      → 次の判断点: initial use case を単一ドメインへ絞るか、複数ドメインに流用可能な master/detail/diff 基盤を先に閉じるか。

- [ ] production運用に必要な環境変数・secret・起動手順・失敗時表示を整理する
      → 理由: Docker Compose demo は立つが、DEMO_JWT_SECRET / DEMO_BACKEND_URL / DATABASE_URL / nginx経由などの正規導線が混在しやすい。
      → 改善方針: local dev / docker compose / production-like の3導線を分離し、未設定時は明示エラー、設定済み時は同一runtimeへ到達するようにする。
      → 対象ファイル: infra/docker-compose.yml, infra/.env.example, frontend/routes/api/*, backend/Program.cs, docs/demo-walkthrough.md, README.md
      → 次の判断点: demo用authを残すか、本番auth境界を別SSOTとして切るか。

- [ ] CI/テストを「構造確認」から「runtime意味確認」へ拡張する
      → 理由: structure/backend/db のチェックはあるが、ログイン→dispatch→emission、policy変更→runtime反映、registry更新→推薦変化の意味テストが不足している。
      → 改善方針: backend unit/integration、frontend type/API proxy、DB seed smoke、demo runtime smoke を分けて追加する。
      → 対象ファイル: .agent/tests/*, backend/tests/Topolactor.Runtime.Tests/*, frontend/*, db/demo_seed.sql, docs/demo-walkthrough.md
      → 次の判断点: Docker Compose を使うE2E smokeをCIに入れるか、ローカル任意チェックに留めるか。

## Demo Runtime Dispatch

- [x] Public scaffold demo のログイン→dispatch→backend runtime emission 導線を閉じる
      → 完了: /api/dispatch proxy 実装、JWT sessionStorage 保存/読み取り、未ログイン時明示案内、docs/demo-walkthrough.md にログイン→dispatch フロー追記。
      → 残課題: recommendation cold-start 表示の扱い（次の判断点）は未解決。context_route recommendation TODO に委譲。
