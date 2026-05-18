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

## Issue #60 — Topology Vector Runtime Completion

- [ ] TopologyVectorRuntime を canonical context route recommendation runtime に接続する
      → 理由: PR #61 / #62 で SSOT・contracts・DB・utility・Npgsql persistence は整備済みだが、`ContextRouteRecommendationResolver.ResolveAsync` はまだ従来の prefix vector / nearest prefix / transition stats 導線のまま。TopologyVectorRuntime の transition key evidence / Topology MLP / hub attention current が正規Runtime導線で呼ばれていない。
      → 改善方針: `ContextRouteRecommendationResolver.ResolveAsync` に TopologyVectorRuntime を接続し、policy enabled 時のみ transition key evidence extraction / MLP feature crossing / hub attention current load-upsert-rank recalculation を実行する。enabled=false / policy missing / invalid / repository unavailable は explicit status とし、silent fallback しない。
      → 対象ファイル: backend/runtime/ContextRouteRecommendationResolver.cs, backend/runtime/TopologyVectorRuntime.cs, backend/repository/ContextRouteRepository.cs, backend/repository/NpgsqlContextRouteRepository.cs, backend/schema/ContextRouteContracts.cs, backend/schema/ContextRoutePolicyContracts.cs, backend/tests/Topolactor.Runtime.Tests/*
      → 次の判断点: current 更新タイミングを context event append 後 / recommendation生成後 / feedback反映後のどこに固定するか。既存 Section 1 / Section 2 推薦導線を破壊しないこと。
      → 推奨担当: Codex

- [ ] Registrar validation flow に registry vector validation を接続する
      → 理由: SQL cosine neighbor search と `ValidateRegistryVectorAsync` は実装済みだが、Registrar の Draft → Validate refs → Preview → Promote flow にまだ接続されていない。Issue #60 の「registry 作成 / promote 前に意味重複候補を structured validation result として返す」条件が未充足。
      → 改善方針: registry draft / promote 前に `ValidateRegistryVectorAsync` を呼び、duplicate_vector / near_duplicate_vector / related_existing_registry / topology_outlier / zero_vector を backend structured validation result として返す。UI は判定せず projection のみ行う。
      → 対象ファイル: backend registrar validation service / repository files if present, backend/repository/NpgsqlContextRouteRepository.cs, backend/schema/*, docs/registrar-admin-ui-specification.md, frontend Registrar admin UI files if projection exists
      → 次の判断点: Registrar validation endpoint / service が未整備の場合、先に backend validation boundary を作るか、既存 validate flow に最小接続するか。
      → 推奨担当: Codex

- [ ] Registrar admin UI / frontend projection に topology vector validation result と attention evidence を表示する
      → 理由: Issue #60 は「Registrar UI が cosine 判定を持たず、backend structured validation result を projection する」ことを受け入れ条件にしている。現状、backend result / evidence のprojection導線は未完了。
      → 改善方針: Frontend は cosine / topology / MLP / feedback 判定を持たず、backend から返る structured validation result / evidence_json / mlp_feature_json を表示するだけにする。UI action identity / projection identity は scenario contract で確認する。
      → 対象ファイル: frontend Registrar admin UI files if present, frontend API proxy files, backend response contracts, docs/registrar-admin-ui-specification.md
      → 次の判断点: 既存UIに最小表示を追加するか、projection contract を先に固定するか。
      → 推奨担当: Codex

- [ ] Issue #60 close 前の acceptance audit を実施する
      → 理由: Issue #60 は複数PRに分割して進めているため、最後に SSOT / DB / contracts / Npgsql / runtime route / registrar validation / frontend projection の意味整合をまとめて確認する必要がある。
      → 改善方針: Issue #60 の受け入れ条件を一つずつ確認し、完了 / 未完了 / out-of-scope を明記する。旧仕様・fallback・magic number・矛盾コメント・テスト残骸が残る場合は削除または隔離する。
      → 対象ファイル: Issue #60 関連変更全体, docs/design/context-route-recommendation.md, docs/design/context-route-recommendation.yaml, docs/registrar-admin-ui-specification.md, README.md, backend/runtime/*, backend/repository/*, backend/schema/*, db/context_route_tables.sql, db/seed_empty.sql, frontend relevant files
      → 次の判断点: close可能か、追加PRが必要か。
      → 推奨担当: Codex

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

