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

- [x] TopologyVectorRuntime を canonical context route recommendation runtime に接続する
      → TVR接続完了: ContextRouteRecommendationResolver.ResolveAsync に TVR 拡張を接続。evidence extraction / MLP feature crossing / hub attention EMA-upsert-rank を実行。enabled=false / tokenIds空はスキップ。非致命的ラッパー。
      → hub attention identity 移行完了: OperationVector.IdOrHubId 追加。hub attention current write は hubId.Value で行う。IdOrHubId なしの dispatch は hub attention をスキップ（hub-entity-scoped）。

- [x] Registrar validation flow に registry vector validation を接続する
      → 完了: RegistrarValidationService 新規作成。function_parameters から registry_validation policy を読み込み、TopologyVectorRuntime.ValidateRegistryVectorAsync を呼び出す。AdminEndpoint.HandleValidateRegistryVectorAsync / POST /admin/registry-vector-validate 追加。policy missing / invalid / DB unavailable は全て ExplicitError（blocking）。

- [x] Registrar admin UI / frontend projection に topology vector validation result と attention evidence を表示する
      → 完了: RegistryVectorValidator island 新規作成。/admin/registry-vector-validate ページ追加。/api/admin/registry-vector-validate proxy route 追加。adminApi.ts に validateRegistryVector / 型定義追加。fresh.gen.ts manifest 更新。frontend は backend structured result を projection するのみ（cosine/MLP判定なし）。

- [ ] Issue #60 close 前の acceptance audit を実施する
      → 残ブロッカー: remote CI (backend-tests / frontend-types) の pass 確認が必要。
      → hub attention identity 移行は完了（上記参照）。CI pass 確認後に close 可。

- [x] Hub attention の hub identity を session-scoped 暫定から hub-entity-scoped へ移行する
      → 完了: OperationVector.IdOrHubId 追加。OperationVectorResolver が EndpointRequestDto.IdOrHubId を転送。RunTopologyVectorRuntimeExtensionAsync は hubId.Value で hub attention DB calls を実行。hubId null の場合は hub attention をスキップ。テスト追加: WithHubId / WithoutHubId の2ケース。

## Runtime Persistence Completion

- [x] 汎用編集 diff log を state transition log と分離して定義・永続化する
      → 完了: topology_edit_log テーブル追加（db/topology_tables.sql）。DiffLogRepository.AppendEditAsync 新規メソッド（base: logger出力）。NpgsqlDiffLogRepository override で topology_edit_log に INSERT。RuntimeExecutor を AppendEditAsync に変更。demo_state_transitions と意味境界を分離済み。

## Runtime Meaning Check Verification

- [ ] check-runtime-semantics.sh を dotnet / deno 利用可能環境で実行し、runtime意味チェックの実行結果を確定する
      → NOT EXECUTABLE: dotnet / deno が本環境で利用不可。Remote CI equivalence（backend-tests.yml / frontend-types.yml）の pass 確認が必要。
      → 対象ファイル: .agent/tests/check-runtime-semantics.sh
      → 次の判断点: GitHub Actions CI が pass したことを確認してから close。

