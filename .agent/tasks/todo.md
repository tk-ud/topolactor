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

## Runtime Stub / Dummy / Skeleton Residue Audit

- [x] runtime正規導線と静的・テスト専用 scaffold の境界を再点検し、stub / dummy / skeleton 残骸を削除または明確隔離する
      → 理由: /demo と Program.cs の正規導線は backend runtime / DB-backed に寄っている一方、runtime-status・frontend default registry/map/schema/package・base TopologyRepository・DiffLogRepository・SemanticMapper に skeleton/dummy/stub 表記または挙動が残っており、完了済みruntime化との意味整合が崩れる。
      → 改善方針: runtime-status は実runtime検証表示へ移すか静的診断ページとして明示隔離する。frontend defaultStructureMap/defaultPackage/defaultSchema/defaultComponentRegistry は runtime結果ではないことを再確認し、正規導線で参照されない形へ隔離する。TopologyRepository base の dummy path はテスト専用境界を明示するか削除し、NpgsqlTopologyRepositoryをruntime正規導線として固定する。DiffLogRepository.AppendAsync は永続化diff log未完了として実装方針を切る。SemanticMapper の stub 表記は実態に合わせて削除または未完了扱いにする。
      → 対象ファイル: frontend/routes/runtime-status.tsx, frontend/structure_map.ts, frontend/package/defaultPackage.ts, frontend/schema/defaultSchema.ts, frontend/registry/componentRegistry.ts, backend/repository/TopologyRepository.cs, backend/repository/NpgsqlTopologyRepository.cs, backend/repository/DiffLogRepository.cs, backend/mapper/SemanticMapper.cs, backend/Program.cs, docs/demo-walkthrough.md
      → 完了: runtime-statusを静的チェックページとして明示し、default map/package/schema/registry と TopologyRepository の説明を runtime結果と誤認されない test fixture 境界へ更新。DiffLogRepository は非永続の現状態を明記し、SemanticMapper の stub 表記を実態に合わせて修正。
      → 推奨担当: Codex

## Product Completion Roadmap (暫定)

- [x] frontend demo から stub / skeleton / synthetic 表示を排除し、backend runtime emission の投影へ一本化する
      → 完了: /demo を runtime dispatch page へ昇格 (OperationPanel island + POST /api/dispatch)。
         defaultStructureMap / demoTokens / synthetic Emission を /demo から除去。
         静的構造説明ページを /demo-static として分離 (明示的に "not a runtime result" ラベル付き)。
         ProjectionView.tsx の "skeleton" 言語、EmissionView.tsx の "before real projection components are wired up" 言語を削除。
         docs/demo-walkthrough.md を新導線 (/demo = runtime, /demo-static = static diagram) に一致させた。
         Scenario E の手順を /demo への直接リンクに更新。

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
         ResolveAsync の処理順を変更: 全読み取り → append → status return の順序を確立。
         prefix LATERAL JOIN 汚染なし (OrderTrackingRepository) を含む3テストで検証。
         NO_CONTEXT_HISTORY / INSUFFICIENT_CONTEXT_HISTORY でも append が実行されることをテストで検証。
         docs/demo-walkthrough.md に Scenario E 追加・route identity・ordering guarantee を明記。
         dispatch panel で target=demo/layer=hub/action=overview + demo session ID + token_active で
         recommendation status:ok + nextOperations:[{value:demo:entity:list, score:0.6}] が安定して得られる。

- [x] admin / registry 操作を skeleton 受付からDB永続化へ移行する
      → 完了 (PR #47 + 追加修正込み):
         backend/endpoint/AdminEndpoint.cs 新規追加、backend/schema/AdminContracts.cs 追加。
         ContextRouteRepository に ListAllContextTokensAsync / CreateContextTokenAsync / DeprecateContextTokenAsync を追加。
         NpgsqlContextRouteRepository で DB 実装。
         CreateContextTokenAsync 戻り型を Guid? → CreateTokenResult(Code, TokenId) に変更し、
         UNIQUE(label,"group") 違反 (Postgres 23505) を Code.Conflict として明示返却 → HTTP 409。
         AdminEndpoint.HandleCreateTokenAsync が Code switch で LABEL_REQUIRED / VALUE_OUT_OF_RANGE /
         DUPLICATE_LABEL_GROUP / NOT_CONNECTED を個別 ErrorCode として返す。
         Program.cs に GET/POST /admin/context-token-registry および POST /admin/.../deprecate を JWT ガード付きで追加。
         frontend/routes/api/admin/context-token-registry.ts をbackend proxy に変更。
         frontend/routes/api/admin/context-token-registry/[tokenId]/deprecate.ts をbackend proxy に変更。
         frontend/api/adminApi.ts に JWT Bearer ヘッダー送信 + create/deprecate の 401 throw を追加。
         frontend/islands/ContextTokenRegistryEditor.tsx に未ログイン時の明示案内および
         handleAdd/handleDeprecate 中の 401 throw → setNotAuthed(true) を追加。
         AdminEndpointTests.cs: list/create/deprecate の success/failure/conflict/boundary/idempotent テスト追加。
         frontend/tests/adminApi.test.ts: 501/401/200/409/422/404 の応答マッピングテスト追加。

- [x] DB-backed application runtimeとしての最小状態遷移ループを定義し、demo seed から実データ入力ループへ移行する
      → 目的: topolactor を単なる runtime scaffold ではなく、任意ドメインの application state loop を持つ runtime へ進める。
      → 改善方針: state transition、event/diff履歴、list/detail projection を含む最小schema/package/componentを定義し、runtime route に載せる。
      → 対象ファイル: db/schema.sql, db/topology_tables.sql, db/demo_seed.sql, backend/runtime/*, frontend/routes/*, frontend/components/*, docs/demo-walkthrough.md
      → 次の判断点: initial use case を単一ドメインへ絞るか、複数ドメインに流用可能な master/detail/diff 基盤を先に閉じるか。

- [x] production運用に必要な環境変数・secret・起動手順・失敗時表示を整理する
      → 理由: Docker Compose demo は立つが、DEMO_JWT_SECRET / DEMO_BACKEND_URL / DATABASE_URL / nginx経由などの正規導線が混在しやすい。
      → 改善方針: local dev / docker compose / production-like の3導線を分離し、未設定時は明示エラー、設定済み時は同一runtimeへ到達するようにする。
      → 対象ファイル: infra/docker-compose.yml, infra/.env.example, frontend/routes/api/*, backend/Program.cs, docs/demo-walkthrough.md, README.md
      → 次の判断点: demo用authを残すか、本番auth境界を別SSOTとして切るか。

- [x] CI/テストを「構造確認」から「runtime意味確認」へ拡張する
      → 理由: structure/backend/db のチェックはあるが、ログイン→dispatch→emission、policy変更→runtime反映、registry更新→推薦変化の意味テストが不足している。
      → 改善方針: backend unit/integration、frontend type/API proxy、DB seed smoke、demo runtime smoke を分けて追加する。
      → 対象ファイル: .agent/tests/*, backend/tests/Topolactor.Runtime.Tests/*, frontend/*, db/demo_seed.sql, docs/demo-walkthrough.md
      → 完了: .agent/tests/check-runtime-semantics.sh を追加し、backend runtime/integration と frontend API proxy 意味テストを統合。Docker Compose E2E smoke は現時点ではローカル任意チェックとして docs に明記。
      → 推奨担当: Codex

## Demo Runtime Dispatch

- [x] Public scaffold demo のログイン→dispatch→backend runtime emission 導線を閉じる
      → 完了: /api/dispatch proxy 実装、JWT sessionStorage 保存/読み取り、未ログイン時明示案内、docs/demo-walkthrough.md にログイン→dispatch フロー追記。
      → 残課題: recommendation cold-start 表示の扱い（次の判断点）は未解決。context_route recommendation TODO に委譲。
