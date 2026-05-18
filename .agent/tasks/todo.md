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

## Runtime Log Retention

- [ ] archive_strategy="archive" を実装する（context_event を cold table へ移動）
      → 現時点では archive_strategy="delete" のみ対応。"archive" が指定された場合は
         LogRetentionRuntime が MalformedPolicy を返す（silent fallback なし）。
      → cold table スキーマ設計と NpgsqlContextRouteRepository への archival メソッド追加が必要。
      → 対象: db/context_route_tables.sql (cold table), backend/runtime/LogRetentionRuntime.cs,
               backend/repository/NpgsqlContextRouteRepository.cs

- [ ] hot_days ウィンドウ内の event を context_event に保持する最適化を実装する
      → seed_empty.sql の retention_policy に hot_days フィールドは定義済みだが、
         v1 では ContextEventRetentionPolicy に含まれておらず実行時に無視される。
      → 実装時は ContextEventRetentionPolicy に HotDays を追加し、
         LogRetentionRuntime で hot_days < created_at 条件を DELETE 句に反映する。
      → 対象: backend/schema/RetentionContracts.cs, backend/runtime/LogRetentionRuntime.cs
