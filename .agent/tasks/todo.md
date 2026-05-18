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
      → 完了: ContextRouteRecommendationResolver.ResolveAsync に TVR 拡張を接続。context event append 後に evidence extraction / MLP feature crossing / hub attention current upsert-rank recalculation を実行。enabled=false / tokenIds空はスキップ。非致命的ラッパー（try-catch）でエラー時も推薦結果に影響なし。

- [x] Registrar validation flow に registry vector validation を接続する
      → 完了: RegistrarValidationService 新規作成。function_parameters から registry_validation policy を読み込み、TopologyVectorRuntime.ValidateRegistryVectorAsync を呼び出す。AdminEndpoint.HandleValidateRegistryVectorAsync / POST /admin/registry-vector-validate 追加。policy missing / invalid / DB unavailable は全て ExplicitError（blocking）。

- [x] Registrar admin UI / frontend projection に topology vector validation result と attention evidence を表示する
      → 完了: RegistryVectorValidator island 新規作成。/admin/registry-vector-validate ページ追加。/api/admin/registry-vector-validate proxy route 追加。adminApi.ts に validateRegistryVector / 型定義追加。fresh.gen.ts manifest 更新。frontend は backend structured result を projection するのみ（cosine/MLP判定なし）。

- [x] Issue #60 close 前の acceptance audit を実施する
      → 完了: Tasks 1-3 / 5 実装完了。TVR接続 / Registrar validation / frontend projection / topology_edit_log 分離 が全て完了。check-runtime-semantics.sh は dotnet/deno 非利用環境のため NOT EXECUTED（Remote CI equivalence 要確認）。

## Runtime Persistence Completion

- [x] 汎用編集 diff log を state transition log と分離して定義・永続化する
      → 完了: topology_edit_log テーブル追加（db/topology_tables.sql）。DiffLogRepository.AppendEditAsync 新規メソッド（base: logger出力）。NpgsqlDiffLogRepository override で topology_edit_log に INSERT。RuntimeExecutor を AppendEditAsync に変更。demo_state_transitions と意味境界を分離済み。

## Runtime Meaning Check Verification

- [ ] check-runtime-semantics.sh を dotnet / deno 利用可能環境で実行し、runtime意味チェックの実行結果を確定する
      → NOT EXECUTABLE: dotnet / deno が本環境で利用不可。Remote CI equivalence（backend-tests.yml / frontend-types.yml）の pass 確認が必要。
      → 対象ファイル: .agent/tests/check-runtime-semantics.sh
      → 次の判断点: GitHub Actions CI が pass したことを確認してから close。

