# Agent Task List

---
未処理は **bundle 単位**で実装・レビューする。掲載は `not_started` / `partial` のみ。

## 未処理 bundle 索引

| Bundle ID | 名称 | Status | 件数 | 主 SSOT |
|-----------|------|--------|------|---------|
| `future-external-bundle-gate` | 外部 surface bundle 実装ゲート | not_started | 1 | `docs/design/extended-runtime-bundle-registry-ssot.yaml` |
| `helper-manual` | ユーザー向けヘルプ / マニュアル | not_started | 3 | `docs/design/user-facing-helper-manual-ssot.yaml` |
| `product-nocode-loop-acceptance` | 製品手動受入 | not_started | 1 | `docs/system-roadmap.yaml`（参照のみ・正本ではない） |
| `aggregate-trigger-substrate` | 集計トリガー基盤 | not_started | 1 | `docs/design/runtime-orchestration-ssot.yaml` |
| `agent-readonly-repo-observation-tools-surface` | Agent read-only repo observation tools surface | partial | 4 | `docs/governance/agent-governance-routing-ssot.yaml` |

---

## Bundle `future-external-bundle-gate`

**Status:** not_started
**SSOT:** `docs/design/extended-runtime-bundle-registry-ssot.yaml`

- [ ] Notion/Sheets/Slack/GitHub/Webhook/REST-API-Connector/NoCode-Loop — 個別 SSOT 揃うまで実装しない

---

## Bundle `helper-manual`

**Status:** not_started
**SSOT:** `docs/design/user-facing-helper-manual-ssot.yaml`

- [ ] helper/manual category 候補の実装設計
- [ ] Desktop AI / CLI / MCP Reader 向けライティング方針
- [ ] ヘルプコンポーネント実装（SSOT カテゴリ構造ゲート）

---

## Bundle `product-nocode-loop-acceptance`

**Status:** not_started

- [ ] `product.dynamic_support_nocode_loop` 手動受入（roadmap 追従）

---

## Bundle `aggregate-trigger-substrate`

**Status:** in_progress
**SSOT:** `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/abstract-function-primitive-registry-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/ssot-map.yaml`

- [ ] 集計トリガー基盤の test / backend / frontend(admin/contents Step3) 実装（SSOT contract は設計確定済み）
  - 問題点: SSOT contract は確定済み。現状は backend runtime / validator / repository fixed-template contract / frontend structured authoring helper / DB DDL の部分実装とローカル contract test はあるが、Admin contents Step3 本体UI enforcement、live PostgreSQL適用、AdminRuntime定義保存経路、cron/hook/client scheduler-to-manifest 実DB経路の executable proof は未完了。
  - 目的: `aggregate-trigger-substrate` を公開基盤設備として定義し、admin/contents Step3 で構造化された処理関数を登録できるようにする。event は入力であり scope owner ではない。execution scope / transaction boundary / aggregate target / threshold policy / materialization target / approval policy は処理関数または operation definition 側が所有する。
  - 改善方針: UI は SQL / CASE / WHERE / 任意 table 名を保存しない。Step3 で、Step2 の logical entity または Step2.5 の relation definition から aggregate target / materialization target を選択し、conflict key、delta map、最低試行回数、比率分子/分母、比較演算子、target ratio、materialization policy、approval policy を構造化 payload として backend runtime に送る。repository は許可済み template に展開し、app-side read -> count++ -> update race を避けて atomic upsert / duplicate materialization guard を実装する。
  - 対応資料: `docs/framework-core.yaml`, `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/pipeline-continuity-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/abstract-function-primitive-registry-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/ssot-map.yaml`
  - 対象ファイル: `docs/design/runtime-orchestration-ssot.yaml`, `docs/design/admin-console-workflow-ssot.yaml`, `docs/design/db-schema.yaml`, `docs/design/abstract-function-primitive-registry-ssot.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/ssot-map.yaml`, `backend/runtime/AdminRuntime.cs`, `backend/runtime/RuntimeExecutor.cs`, `backend/runtime/ScreenDataShapeQueryRuntime.cs`, `backend/runtime/ScreenDataShapeQueryEvaluator.cs`, `backend/repository/ContextRouteRepository.cs`, `backend/repository/NpgsqlContextRouteRepository.cs`, `frontend/islands/**`, `frontend/components/**`, `frontend/tests/*.test.ts`, `backend/tests/**/*.cs`, `db/*.sql`
  - 対象関数/単位: `aggregate_trigger_definition`, `aggregate_trigger_event_evidence`, `aggregate_trigger_materialization_evidence`, `trigger_source`, `processing_function_scope`, `aggregate_target_binding`, `conflict_key_fields`, `delta_map`, `minimum_trial_count`, `ratio_numerator_field`, `ratio_denominator_field`, `comparison_operator`, `target_ratio`, `materialization_target_binding`, `materialization_payload_map`, `approval_policy`, `AggregateTriggerRuntime`, `AggregateTriggerRepository`, `AggregateTriggerDefinitionValidator`, `AggregateTriggerConditionEvaluator`
  - OK軸: SSOT が公開汎用設備として aggregate trigger を定義し、特定アプリケーション名・特定ユースケース名を正本化せず、Step3 が Step2/2.5 定義済み対象だけを選ばせ、backend が構造化定義を検証し、repository が fixed SQL template で idempotent event append / atomic upsert / minimum trial + ratio threshold / controlled materialization / duplicate prevention / evidence log を実装し、backend/frontend/test/proof manifest が event -> aggregate -> threshold -> materialize 経路を証明する。
  - NG軸: 特定アプリ専用の hardcode 実装、特定アプリケーション名・特定ユースケース名を substrate 要件へ混入、UI の raw SQL/CASE/WHERE 保存、任意 table 名入力、event 側を scope owner とする設計、Step2/2.5 未定義対象への登録、frontend persistence 判断、app-side read -> count++ -> update race、閾値超過時の二重 materialization、approval policy 未定義、projection aggregation のみで mutation/materialization 未証明、proof manifest の過剰主張。
  - SSOT確定済み: materialization payload map の初期表現は `function_input_event`, `aggregate_current_row`, `selected_step2_entity_fields`, `selected_step2_5_relation_fields`, `constant`, `generated_value`, `runtime_actor_source_metadata` を許可し、raw SQL / CASE / WHERE / 未宣言 json path は禁止する。


---

## Bundle `agent-readonly-repo-observation-tools-surface`

**Status:** partial
**SSOT:** `docs/governance/agent-governance-routing-ssot.yaml`, `docs/framework-policy.yaml`, `docs/file-structure.yaml`, `.agent/docs/structure-map.yaml`, `.agent/docs/required-paths.yaml`, `.agent/docs/ssot-map.yaml`, `.agent/docs/test-bundles.yaml`, `docs/design/test-proof-manifest-ssot.yaml`

- 目的: `.agent/tools` を Agent向け read-only repo observation 公式surfaceとして新設する。
- Bundle処理開始前に必ず読む:
  1. `AGENTS.md`
  2. `.agent/rules/rule.md`
  3. `.agent/README.md`
  4. `.agent/prompt/audit.md`
  5. `.agent/protocols/audit.md`
  6. `.agent/protocols/implementation-change.md`
  7. `docs/governance/agent-governance-routing-ssot.yaml`
  8. `docs/framework-policy.yaml`
  9. `docs/file-structure.yaml`
  10. `.agent/docs/structure-map.yaml`
  11. `.agent/docs/required-paths.yaml`
  12. `.agent/docs/ssot-map.yaml`
  13. `.agent/docs/test-bundles.yaml`
  14. `docs/design/test-proof-manifest-ssot.yaml`
  15. `.agent/scripts/emit-directory-tree-json.py`
  16. `.agent/tests/check-ssot-proof-surface-connectivity.sh`
  17. `.agent/scripts/check_ssot_proof_surface_connectivity.py`
  18. `.agent/tests/check-no-ruby-dependency.sh`
  19. `.agent/tests/check-structure.sh`
  20. `.agent/tasks/todo.md`
- 親Bundle完了条件: 子Bundle `agent-tools-governance-contract`、`agent-tools-core-readonly-observation`、`agent-tools-proof-and-structure-gate`、`agent-tools-advanced-surface-maps` が完了していること。
- 備考: `agent-tools-advanced-surface-maps` は future 子Bundle扱いでもよい。初期完了を狙う場合は、advanced surface maps を親Bundle初期完了条件から外す判断も許可する。ただし判断理由をtodo内に残す。

### 子Bundle `agent-tools-governance-contract`

**Worktype:** design_change
**Status:** implemented

- Scope: `.agent/tools` の正本上の位置づけを確定する。
- 問題点: `.agent/tools` の責務・禁止事項・`.agent/scripts` との境界・proof接続が未定義。このまま実装すると便利script置き場化し、SSOT authority / proof / semantic judgment と混同される。
- 目的: `.agent/tools` を Agent向け read-only repo observation surface として governance / policy / rule 上で定義する。
- 改善方針: `.agent/tools` は read-only observation interface。`.agent/scripts` は CI / gate / helper implementation body。tool output は SSOT authority / proof / completion judgment ではない。tool は semantic completion / implemented / partial 判定をしない。Python3 stdlib only policy を `.agent/tools` に接続する。
- 対応資料: `docs/governance/agent-governance-routing-ssot.yaml`, `.agent/README.md`, `.agent/docs/structure-map.yaml`, `docs/framework-policy.yaml`, `.agent/rules/rule.md`, `.agent/protocols/audit.md`
- 完了記録: `.agent/tools` を Agent向け read-only repo observation surface として governance / policy / rule / structure 上で正本化済み。`.agent/tools` と `.agent/scripts` の責務境界、read-only / no mutation / no semantic judgment、tool output が SSOT authority / proof / completion judgment ではない境界、Python3 stdlib only policy 接続、後続 `directory-map` / `ssot-map-query` / `proof-surface-map` 実装方針を定義済み。実装本体・初期tool・proof gate は後続子Bundle scope のまま。

### 子Bundle `agent-tools-core-readonly-observation`

**Worktype:** implementation_change
**Status:** implemented
**Depends on:** `agent-tools-governance-contract`

- Scope: 初期 read-only tool surface を追加する。
- 問題点: Agent向けに安定した repo observation command がない。
- 目的: 初期tool `directory-map`, `ssot-map-query`, `proof-surface-map` を追加する。
- 改善方針: `.agent/tools` は thin entrypoint。構造処理は `.agent/scripts` の Python3 stdlib 実装を再利用する。既存 `.agent/scripts/emit-directory-tree-json.py` は移動しない。`directory-map` では `--output` など file mutation option を露出しない、または拒否する。
- 対応資料: `.agent/scripts/emit-directory-tree-json.py`, `.agent/docs/ssot-map.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/scripts/check_ssot_proof_surface_connectivity.py`
- 完了記録: 初期 read-only observation tool surface として `.agent/tools/README.md`, `directory-map`, `ssot-map-query`, `proof-surface-map` を追加済み。`directory-map` は既存 `.agent/scripts/emit-directory-tree-json.py` を thin wrapper として再利用し、`.agent/tools` surface では `--output` など mutation/file-write option を拒否する。`ssot-map-query` は `.agent/docs/ssot-map.yaml` を JSON stdout で観測し、`proof-surface-map` は `docs/design/test-proof-manifest-ssot.yaml` と `.agent/docs/test-bundles.yaml` を read-only に観測する。各toolは Python3 stdlib only で、出力metadataに SSOT authority / proof completion / completion judgment / semantic audit judgment / implemented 判定ではない境界を含める。proof gate / required-paths / test-bundles / test-proof-manifest への本格接続は後続 `agent-tools-proof-and-structure-gate` scope のまま。

### 子Bundle `agent-tools-proof-and-structure-gate`

**Worktype:** implementation_change
**Status:** blocked_by_design
**Depends on:** `agent-tools-governance-contract`, `agent-tools-core-readonly-observation`

- Scope: `.agent/tools` の存在・禁止事項・依存境界を proof surface に接続する。
- 問題点: `.agent/tools` を追加しても required path / structure gate / proof manifest に接続されなければ governance surface として検出不能。
- 目的: `.agent/tools` を structure check / required paths / test bundle / proof manifest に接続する。
- 改善方針: dedicated check `check-agent-tools-surface.sh` を追加する。structured processing が必要なら Python3 stdlib script に委譲する。`check-structure.sh` から delegated subcheck として呼ぶ。
- 対応資料: `.agent/docs/required-paths.yaml`, `.agent/docs/test-bundles.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/tests/check-structure.sh`, `.agent/tests/check-no-ruby-dependency.sh`, `docs/framework-policy.yaml`

### 子Bundle `agent-tools-advanced-surface-maps`

**Worktype:** design_change -> implementation_change
**Status:** future
**Depends on:** `agent-tools-governance-contract`, `agent-tools-core-readonly-observation`, `agent-tools-proof-and-structure-gate`

- Scope: 初期tool外の高度観測mapを後続実装する。
- 対象tool候補: `change-impact-map`, `dependency-surface-map`, `orphan-surface-map`
- 問題点: impact / dependency / orphan 系は semantic judgment と誤認されやすく、初期toolと同時実装すると責務境界が崩れやすい。
- 目的: 高度観測mapを後続Bundleとして分離し、input / output schema と禁止事項を定義してから実装する。
- 改善方針: 各toolの JSON schema を先に定義する。“orphan” は unused / dead code 判定ではなく unindexed candidate observation として扱う。dependency / impact は観測補助であり、設計影響確定や completion 判定をしない。
- 対応資料: `docs/governance/agent-governance-routing-ssot.yaml`, `.agent/docs/ssot-map.yaml`, `docs/design/test-proof-manifest-ssot.yaml`, `.agent/docs/test-bundles.yaml`
