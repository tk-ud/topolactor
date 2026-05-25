# Topology Recommendation CI Runtime — 設計思想と取り扱い方針

対応 YAML: `topology-recommendation-ci-runtime.yaml`

---

## 概念

自己学習統計（context route recommendation / transition aggregates 等）から生成される
topology 更新候補（recommendation candidate）を、
C# package runtime の validation runner によって CI 検証し、
`checked=true` の候補のみを update / promote 対象とする設計。

この CI runtime も registry tensor（registry table semantic matrix 由来座標）の projection surface であり、
candidate 検証は DB / UI / endpoint / runtime / scheduler / function と同一 tensor continuity を
監査する操作として扱う。CI を個別実装層として分断しない。

```text
self_learning_statistics
→ generate topology_recommendation_candidate
→ cron excitation trigger → trigger context → Runtime excitation
→ resolve topology_recommendation_ci package
    (Manifest / Registry / function_parameters)
→ C# validation runner: candidate ごとに validation checks を実行
→ record recommendation_check_result (checked=true / checked=false)
→ checked=true  → ci_pass → admin 承認 / promote 対象
→ checked=false → ci_fail → reject / recommend delete / expire 対象
```

**CI / validation の主体は C# package runtime の validation runner である。**
shell script は必須ではなく、既存 local CI や外部検証器が必要な場合だけ
allowlisted external check adapter として C# runtime が明示的に呼び出す。

DB は実行コマンド文字列を保持しない。
silent fallback は存在しない。

---

## 候補種別 (Candidate Types)

対象 candidate type は SQL patch / physical table promotion に限定しない。
以下の種別を扱う。

| candidate_type | 説明 |
|---|---|
| `registry_addition` | Registry への新規エントリ追加（relation_registry / schema_registry 等） |
| `relation_registry_promotion` | JSONB 関連データを正規の relation_registry エントリに昇格 |
| `enum_axis_registration` | Enum 軸を context_token_registry に登録 |
| `schema_patch` | 既存 schema の定義変更 |
| `manifest_patch` | 既存 manifest の定義変更 |
| `package_binding` | 新規 package バインド定義の追加 |
| `generated_column` | 推奨される生成列の追加 |
| `index` | 推奨される index の追加 |
| `physical_table` | JSONB から物理テーブルへの promotion |
| `sql_patch` | 上記に分類されない DB 変更パッチ |

---

## DB エンティティ

### topology_recommendation_candidate

topology 更新候補を記録する。

| フィールド | 型 | 説明 |
|---|---|---|
| `candidate_id` | uuid (PK) | |
| `candidate_type` | enum | 上記 candidate_type 種別 |
| `source` | string | 生成元（例: `self_learning_statistics` / `admin_manual`） |
| `target_ref` | jsonb | 対象の識別情報（table_name / registry_id 等）。コマンド文字列不可 |
| `proposed_change` | jsonb | 変更内容の構造化記述。コマンド文字列不可 |
| `status` | enum | `pending` / `ci_pass` / `ci_fail` / `promoted` / `rejected` / `expired` |
| `generated_at` | timestamp | |
| `expires_at` | timestamp (nullable) | policy 上の有効期限 |
| `created_at` | timestamp | |
| `updated_at` | timestamp | |

**制約**: `proposed_change` と `target_ref` はコマンド文字列・シェルコマンドを含んではならない。
構造化された識別情報・変更内容のみを格納する。

### recommendation_check_result

C# validation runner が記録する CI 結果。

| フィールド | 型 | 説明 |
|---|---|---|
| `check_id` | uuid (PK) | |
| `candidate_id` | uuid (FK) | → topology_recommendation_candidate |
| `check_name` | string | 実行された validation 名 |
| `checker_package` | string | 実行した C# package 名 |
| `checked` | boolean | true = CI pass / false = CI fail |
| `result_detail` | jsonb | 構造化された検証結果。コマンド文字列不可 |
| `adapter_id` | uuid (nullable, FK) | 外部検証器を使用した場合の adapter 参照 |
| `checked_at` | timestamp | |

### external_check_adapter_registry

allowlisted external check adapter の登録テーブル。
DB はコマンド文字列を保持しない。adapter 識別子と enabled 状態のみを管理する。
コマンドの実装は C# package runtime 側に閉じる。

| フィールド | 型 | 説明 |
|---|---|---|
| `adapter_id` | uuid (PK) | |
| `adapter_name` | string | adapter 識別名（C# 実装の参照キー） |
| `adapter_type` | enum | `local_ci` / `external_validator` |
| `candidate_types` | string[] | この adapter が対象とする candidate_type リスト |
| `enabled` | boolean | |
| `created_at` | timestamp | |

### topology_policy_source (function_parameters)

CI policy は独立した設定テーブルを作らず、既存 `function_parameters` テーブルに格納する。

```text
function_name = 'topology_recommendation_ci_resolve'
parameter_key = 'default_policy'
```

policy JSON 構造:

```json
{
  "enabled": true,
  "allowed_candidate_types": [
    "registry_addition",
    "relation_registry_promotion",
    "enum_axis_registration",
    "schema_patch",
    "manifest_patch",
    "package_binding",
    "generated_column",
    "index",
    "physical_table",
    "sql_patch"
  ],
  "expiry_days": 30,
  "min_check_pass_count": 1,
  "max_candidates_per_run": 50
}
```

policy-missing → `MissingPolicy` を返す。production fallback 定数での継続は禁止。
resolver: `TopologyRepository.LoadFunctionParameterAsync`

---

## Package Dispatch フロー

```text
cron excitation trigger
→ trigger context { operation: "topology:recommendation:ci:run" }
→ Runtime excitation
→ resolve Manifest / Registry / function_parameters
→ select topology_recommendation_ci package
→ C# validation runner:
    → load pending candidates (policy: allowed_candidate_types / max_candidates_per_run)
    → for each candidate:
        → resolve applicable validation checks (from policy)
        → execute C# validation checks
        → if external check needed:
            → verify adapter_id is in external_check_adapter_registry AND enabled
            → call allowlisted external_check_adapter only
            → ExternalError if adapter fails or is not allowlisted
        → record recommendation_check_result (checked=true / checked=false)
        → update candidate.status (ci_pass / ci_fail)
→ checked=true  candidates → eligible for admin approval / promote
→ checked=false candidates → reject / recommend delete / expire
→ update audit log
```

Trigger は context を Runtime に渡すだけである。
「何を検証するか」「どの package を使うか」「有効かどうか」は topology データが決める。

---

## C# Validation Runner

CI / validation の主体は C# package runtime の validation runner である。

Validation runner の責務:

- `function_parameters` から CI policy を解決する（policy-missing → `MissingPolicy`）
- pending candidate を load する（`allowed_candidate_types` / `max_candidates_per_run` 準拠）
- candidate_type ごとの validation checks を実行する
- 外部検証器が必要な場合は `external_check_adapter_registry` で allowlisted かどうかを確認し、
  allowlisted かつ enabled な adapter のみを呼び出す
- `recommendation_check_result` を記録する
- candidate status を更新する（`ci_pass` / `ci_fail`）

shell script は必須ではない。
外部検証器（既存 local CI 等）が必要な場合のみ、allowlisted external check adapter として呼び出す。

---

## External Check Adapter

外部検証器が必要な場合（例: 既存 local CI が特定の candidate_type を検証する場合）:

- DB に adapter 識別子を登録する（`external_check_adapter_registry`）
- adapter の実装（コマンド呼び出し等）は C# package runtime 側に閉じる
- DB はコマンド文字列を保持しない
- allowlisted でない external adapter は実行禁止
- 未登録 adapter への呼び出しは `ExternalError` として記録する

---

## 明示 Status

candidate.status:

```text
pending    — 生成済み、CI 未検証
ci_pass    — CI 検証通過（checked=true）→ admin 承認 / promote 対象
ci_fail    — CI 検証失敗（checked=false）→ reject / recommend delete / expire 対象
promoted   — 実際に適用済み
rejected   — admin または policy によって棄却済み
expired    — policy 上の有効期限切れ
```

validation runner の dispatch status:

```text
Ok               — 正常完了
Disabled         — topology_recommendation_ci package が無効化されている
MissingPolicy    — function_parameters が未登録
MalformedPolicy  — policy データが破損または JSON パース不可
ValidationFailed — validation check 不合格（checked=false として記録）
ExternalError    — 外部 adapter からのエラー、または未 allowlisted adapter 呼び出し
```

`Disabled` は silent skip 禁止。明示 status として記録する。
`ExternalError` を飲み込んで正常完了として扱うことを禁止する。

---

## 取り扱い方針

### やってよいこと

- cron を context 生成のみに使用する
- C# validation runner が validation checks を policy から解決して実行する
- allowlisted external check adapter を通じて既存 local CI を呼び出す
- CI 結果を `recommendation_check_result` として記録する
- `checked=true` の candidate のみ admin 承認 / promote 対象にする
- `checked=false` の candidate を reject / recommend delete / expire 対象にする
- policy で明示的に無効化された場合も `Disabled` status を記録する

### やってはいけないこと

- DB にコマンド文字列・シェルコマンドを格納する
- allowlisted でない外部コマンドを C# runtime から実行する
- shell script を CI の必須主体として扱う（optional / allowlisted adapter のみ）
- `checked=false` を silent skip にする（reject / expire 候補として記録する）
- policy missing のとき production fallback 定数で継続する（`MissingPolicy` を返す）
- `Disabled` を silent skip にする（`Disabled` status を記録する）
- `ExternalError` を飲み込んで正常完了として扱う
- candidate_type を SQL patch / physical_table のみに限定する
- cron trigger に domain-specific logic や validation ロジックを置く
- `topology_recommendation_ci` 専用の独立した設定テーブルを作る（`function_parameters` に統合する）

---

## topolactor Runtime との接続

canonical route への接続位置:

```text
cron excitation trigger
→ trigger context { operation: "topology:recommendation:ci:run" }
→ Runtime excitation                          ← runtime-excitation-and-package-dispatch.md
→ stored_topology_data
→ trigger operation
→ operation_vector
→ attractor_resolve
→ structure_map_resolve
→ package_resolve                             ← topology_recommendation_ci package を選択
→ schema_resolve
→ component_expand → C# validation runner     ← candidate 検証・check_result 記録
→ emission_or_projection → audit log
```

- Trigger layer: context 生成のみ。domain-specific logic なし。
- Runtime layer: Manifest / Registry / function_parameters から package を選択。
- Package runtime layer: C# validation runner が candidate を検証し、`recommendation_check_result` を記録。

---

## 既存 Promotion Policy との接続

`framework-policy.yaml` の `promotion_policy` における `show_admin_candidates` / `admin_approval` の前段として、
topology recommendation CI が候補の事前検証（CI gate）を担う。

```text
self_learning_statistics
→ topology_recommendation_candidate 生成
→ [topology recommendation CI gate]  ← 本 SSOT のスコープ
    → checked=true  → ci_pass → show_admin_candidates（admin 承認候補）
    → checked=false → ci_fail → reject / recommend delete / expire
→ admin_approval
→ apply_index_or_generated_column_or_registry_or_physical_table
→ keep_rollback_plan
```

CI gate を通過した候補（`ci_pass`）だけが admin に提示される。
`ci_fail` 候補は admin 承認の前段で排除される。

---

## Runtime Excitation Trigger との接続

本 SSOT は `docs/design/runtime-excitation-and-package-dispatch.md` に定義する
**cron trigger → package dispatch** パターンに従う。

```text
cron trigger（定期実行）
→ trigger context { operation: "topology:recommendation:ci:run" }
→ Runtime excitation
→ resolve Manifest: select topology_recommendation_ci package
→ execute C# validation runner
→ policy check (function_parameters: enabled / allowed_candidate_types / expiry_days 等)
→ record recommendation_check_result / update candidate.status
→ update audit log
```

cron trigger は「いつ実行するか」という context を Runtime に渡すだけ。
「何を検証するか」「どの candidate type を対象とするか」「有効かどうか」は topology データが決める。

---

## System Operation CI — SQL Attention / Registry Continuity

### 概念

Topology Recommendation CI (上記) は、topology 更新候補の事前検証 (candidate CI gate) である。

**System Operation CI** はこれとは別の目的を持つ。

- **対象**: 稼働中の SQL Attention (hub attention current / evidence_json) と Registry Continuity (registry / relation / context_event) の意味連続性を、システム側の不変条件として継続検査する。
- **目的**: 推薦精度ではなく topology 連続性の invariant 検査。候補の事前承認とは分離される。
- **threshold / 検査種別は hardcode OK**: system CI の検査ルールはシステム不変条件であり、runtime policy ではない。

### System CI と runtime policy の境界

| 項目 | 格納場所 | 変更主体 |
|---|---|---|
| system CI 検査ルール | C# hardcode (system invariant) | 開発者 |
| EMA alpha / scope_limits / delta | function_parameters | 運用 policy |
| 推薦スコア threshold | function_parameters | 運用 policy |
| CI 候補種別 / expiry_days | function_parameters | 運用 policy |

**禁止**: system CI の検査ルールを function_parameters に外出しする (runtime policy と混同する)。

### 対象実装

- `backend/runtime/SystemOperationCiRuntime.cs` — 検査ロジック本体 (event-driven / cron 両系統)
- `backend/schema/SystemCiContracts.cs` — `SystemCiDiagnosticResult` / `SystemCiFinding` / `HubAttentionCiSummary` / `RegistryTokenCiSummary`
- `backend/repository/ContextRouteRepository.cs` — `LoadHubAttentionSummaryForCiAsync` / `CountContextEventsAsync` / `CountFeedbackEventsAsync` / `LoadRegistryTokenSummaryForCiAsync`
- `backend/runtime/ContextRouteRecommendationResolver.cs` — event-driven CI 接続点 (`RunTopologyVectorRuntimeExtensionAsync` 内で `InspectEvidenceIntegrity` / `InspectHubAttentionAfterUpdate` を呼び出す; Blocking → `TVR_EXTENSION_FAILED`)
- `backend/scheduler/SystemOperationCiScheduler.cs` — cron trigger 接続 (BackgroundService; `InspectHubAttentionContinuityAsync` / `InspectCurrentRebuildabilityAsync` / `InspectRegistryContinuityAsync` を定期呼び出し)
- `backend/tests/Topolactor.Runtime.Tests/SystemOperationCiRuntimeTests.cs` — event-driven / cron CI ユニットテスト
- `backend/tests/Topolactor.Runtime.Tests/SystemOperationCiSchedulerTests.cs` — スケジューラ統合テスト

### Event-driven inspection (EventDriven)

ランタイムイベント直後に in-memory で実行する軽量検査。追加の DB 往復なし。

**対象イベント**:
- hub attention current upsert 後 → `InspectHubAttentionAfterUpdate`
- evidence extraction 後 → `InspectEvidenceIntegrity`
- feedback event 構築後 → `InspectFeedbackEvents`

**検査観点 (hardcoded invariants)**:

| チェック名 | 種別 | 条件 |
|---|---|---|
| `HUB_ID_EMPTY` | Blocking | hub_id が Guid.Empty |
| `CANDIDATE_ID_EMPTY` | Blocking | candidate_id が Guid.Empty |
| `ATTENTION_SCORE_NOT_FINITE` | Blocking | attention_score が NaN / Infinity |
| `EMA_FAST_NOT_FINITE` | Blocking | ema_fast が NaN / Infinity |
| `EMA_SLOW_NOT_FINITE` | Blocking | ema_slow が NaN / Infinity |
| `EVIDENCE_JSON_NOT_PARSEABLE` | Gap | evidence_json が JSON array としてパース不可 |
| `MLP_FEATURE_JSON_NOT_PARSEABLE` | Gap | mlp_feature_json が JSON array としてパース不可 |
| `EVIDENCE_EMPTY_WITH_OPERATION` | Gap | operation が存在するが evidence が空 |
| `EVIDENCE_NEGATIVE_CONTRIBUTION` | Blocking | contribution_score < 0 |
| `FEEDBACK_DELTA_ZERO` | Blocking | delta_applied = 0 |
| `FEEDBACK_DELTA_SIGN_VIOLATION` | Blocking | delta 符号が feedback_kind に矛盾 |

### Cron-triggered inspection (CronContinuity)

定期実行で DB を読み取り、より重い連続性探索を行う。

**検査観点 (hardcoded invariants)**:

| チェック名 | 種別 | 条件 |
|---|---|---|
| `CRON_ATTENTION_SCORE_NOT_FINITE` | Blocking | DB 上の attention_score が NaN / Infinity |
| `CRON_EMA_FAST_NOT_FINITE` | Blocking | DB 上の ema_fast が NaN / Infinity |
| `CRON_EMA_SLOW_NOT_FINITE` | Blocking | DB 上の ema_slow が NaN / Infinity |
| `CRON_EVIDENCE_MISSING` | Gap | evidence_json が空 (HasEvidence=false) |
| `CURRENT_NOT_REBUILDABLE_NO_EVENTS` | Gap | hub attention records > 0 だが context_event / feedback_event が両方 0 件 |
| `CRON_ORPHANED_REGISTRY` | Gap | active token が hub attention record に参照されていない (孤立 token) |

**Cron trigger 接続**: 実装済み — `backend/scheduler/SystemOperationCiScheduler.cs` (BackgroundService) が
`SYSTEM_CI_STARTUP_DELAY_SECONDS` 秒の起動遅延後、`SYSTEM_CI_INTERVAL_HOURS` 時間間隔で以下を定期呼び出しする:

```text
cron trigger (SystemOperationCiScheduler BackgroundService)
→ SystemOperationCiRuntime.InspectHubAttentionContinuityAsync
→ SystemOperationCiRuntime.InspectCurrentRebuildabilityAsync
→ SystemOperationCiRuntime.InspectRegistryContinuityAsync
→ diagnostic result → structured logging (Pass: LogInformation / Gap: LogWarning / Blocking: LogError)
```

### 検査結果の扱い

```text
Blocking → fail-close / ExplicitError / caller must not proceed
Gap      → reportable diagnostic / caller logs warning and continues
Pass     → no action required
```

- silent fallback 禁止
- LogError-only 禁止 (Blocking の場合)
- Blocking は ExplicitError として接続する
- Gap は reportable diagnostic として記録する

### Admin UI callable diagnostic surface 参照

System Operation CI の診断意味（何を検査し、どの status を返すか）の正本は本章を含む既存 System Operation CI 設計面にある。  
一方、Admin UI からの到達経路（AdminRuntime 経由の callable diagnostic route）の正本は以下に委譲する。

- `docs/design/system-ci-admin-runtime-callable-surface.yaml`

この参照は **SSOT配線** であり、実装配線完了を意味しない。  
Admin UI / AdminRuntime は test runner / shell / CI process を product runtime lane として直接呼び出さない。
