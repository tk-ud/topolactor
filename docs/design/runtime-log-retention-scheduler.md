# Runtime Log Retention Scheduler — 設計思想と SSOT 方針

対応 YAML: `runtime-log-retention-scheduler.yaml`

---

## 目的

self-learning 系ログ（`context_event`, `operation_log` など）を無制限に蓄積せず、
**topology データで定義された retention policy** に基づいて定期実行で整理する。

本機能は optional capability として扱い、未設定時は silent fallback せず
明示 status を返す。

---

## SSOT 境界

```
function_parameters  ← retention scheduler の既定 policy（JSON）
structure_maps       ← 実行対象の抽象 delete / anonymize 関数参照
operation_log        ← scheduler の実行記録（監査用）
```

- Runtime は **executor** であり、保持期間や対象種別を定数で持たない。
- policy 値は `function_parameters` の
  `function_name='runtime_log_retention_scheduler_run'` / `parameter_key='default_policy'`
  で管理する。

---

## canonical route への接続

実行系は既存 Runtime 導線を踏襲する。

```
operation
→ vector
→ attractor
→ structure_map
→ package
→ schema
→ emission
```

scheduler は「外部 cron が直接 SQL delete を叩く仕組み」ではなく、
backend runtime worker が抽象 operation を発火して解決する。

---

## policy 例（概念）

- `enabled`: true/false
- `schedule`: `daily|hourly|manual`
- `targets`: `context_event|operation_log|...`
- `retention_days`: 90 など
- `mode`: `delete|anonymize|aggregate_then_delete`

上記は設計意図を示すキー例であり、実装時は
`runtime-log-retention-scheduler.yaml` の契約を SSOT とする。

---

## status 規約

- `Ok` — 実行成功
- `Disabled` — policy で無効化
- `PolicyMissing` — 必須 policy 未登録
- `PolicyInvalid` — policy 形式不正
- `RouteResolveFailed` — attractor / structure_map / package / schema 解決失敗

Broken refs は explicit error。silent fallback は禁止。

---

## 非目標

- OS cron / DB job による直接 purge を標準経路にしない
- frontend に削除ロジックを持ち込まない
- Runtime コードへ保持日数の固定値を埋め込まない
