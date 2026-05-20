# Runtime Orchestration SSOT 実装点検レポート (2026-05-20)

## 対象 SSOT
`docs/design/runtime-orchestration-ssot.yaml` v0.1.0

## 点検スコープ
- backend: `Program.cs`, `DispatchEndpoint.cs`, `RuntimeExecutor.cs`, `scheduler/*`, `schema/Contracts.cs`
- frontend: `runtime/*.ts`, `api/dispatch.ts`, `routes/*`
- DB: `db/*.sql`

---

## ギャップ一覧

### Gap-1: `manifest_dispatcher` 未実装 【Structural】
- SSOT `backend_contract.flow`: `endpoint → backend_scheduler → manifest_dispatcher → topology_transform_runtime → topology_function_binder → topology_output_lanes`
- 現状: `DispatchEndpoint → RuntimeExecutor` の直結。ManifestDispatcher が存在しない。
- Dispatcher は manifest DB の `dispatcher_mapping` / `runtime_mapping` / `role_definition` を参照してルーティングを行う設計だが、現在は RuntimeExecutor 内にハードコードされた target/layer/action 分岐が存在する。
- 対象: `backend/runtime/ManifestDispatcher.cs` (新規)、`backend/runtime/RuntimeExecutor.cs`

### Gap-2: `runtime_timeline_scheduler` がクライアント dispatch パスに存在しない 【Structural】
- SSOT `scheduler_contract.input`: `cron_trigger | hook_trigger | client_trigger`
- 現状: `RetentionScheduler` / `SystemOperationCiScheduler` はいずれも cron-only BackgroundService。client trigger (POST /dispatch) はスケジューラーを経由せず直接 RuntimeExecutor に到達する。
- SSOT が定義する `trigger_kind` の scheduler alignment (causal order, execution_boundary, collision_control) がクライアント操作には適用されていない。
- 対象: `backend/scheduler/RuntimeTimelineScheduler.cs` (新規)、`backend/Program.cs`

### Gap-3: `topology_function_binder` / `topology_function_interface` 未実装 【Structural】
- SSOT `backend_contract.topology_function_binder`: `function_name + where_condition + jsonb_array → topology_function_interface_args`
- SSOT `function_binding_and_constructor_contract.prohibited`: `entity_constructor`, `csharp_factory`, `domain_invariant_constructor`
- 現状: 関数バインディング層が存在せず、データアクセスは各 Repository 内で直接組み立てられている。
- 対象: `backend/runtime/TopologyFunctionBinder.cs` (新規)、`backend/schema/TopologyFunctionInterface.cs` (新規)

### Gap-4: Manifest DB テーブル未実装 【Structural】
- SSOT `manifest_contract.storage: db` — `role_definition`, `route_definition`, `ui_projection_definition`, `dispatcher_mapping`, `runtime_mapping`, `topology_function_binding_mapping`, `projection_constructor_mapping`
- 現状: manifest 系テーブルが DB に存在しない。dispatcher/role のマッピングが RuntimeExecutor にハードコードされている。
- 対象: `db/manifest_tables.sql` (新規)

### Gap-5: `minimal_event_shape` フィールド不足 【Contract】
- SSOT `minimal_event_shape.identity_fields`: `trigger_kind`, `target`, `layer`, `action`, `manifest_id`, `trace_id`
- 現状 `EndpointRequestDto`: `OperationType`, `Target`, `Layer`, `Action`, `IdOrHubId`, `Payload`, `Context` — `trigger_kind`, `manifest_id`, `runtime_id`, `trace_id` が存在しない。
- `OperationType` は `trigger_kind` に近い概念だが別物 (SSOT は `cron|hook|client` 固定 enum)。
- 対象: `backend/schema/Contracts.cs`, `frontend/api/dispatch.ts`

### Gap-6: output lane `db_notify_emission` / `registry_attractor_update` 未実装 【Structural】
- SSOT `backend_contract.output_lanes`: `response_emission`, `db_notify_emission`, `registry_attractor_update`
- 現状: `response_emission` (HTTP レスポンス) のみ。`db_notify_emission` / `registry_attractor_update` lane が存在しない。
- 関連: `notify_listen_contract` 全体が未実装 (db_notify + LISTEN インフラなし)。
- 対象: `backend/runtime/OutputLaneRouter.cs` (新規)、`backend/repository/DbNotifyRepository.cs` (新規)

### Gap-7: SSE projection lane 未実装 【Frontend】
- SSOT `frontend_contract.lanes.projection_event_lane`: `sse_receiver → frontend_scheduler → sse_dispatcher → projection_runtime → ui_projection`
- 現状: フロントエンドに SSE インフラなし。`frontend/runtime/` に SSE 受信・スケジューラ・ディスパッチャ相当なし。
- 対象: `frontend/runtime/sseReceiver.ts` (新規)、`frontend/runtime/projectionRuntime.ts` (新規)

### Gap-8: `projection_constructor` 未実装 【Frontend】
- SSOT `frontend_contract.projection_constructor`: `json_key_value + projection_definition → form_inputs | component_projection | ui_projection`
- 現状: `renderEmission.ts` は ComponentRegistry から ComponentSpec を取得するが、`projection_definition` を参照する構築フローがない。
- 対象: `frontend/runtime/projectionConstructor.ts` (新規)

### Gap-9: frontend admin routes 不足 【Frontend Routes】
- SSOT `frontend_routes.admin`: `/admin`, `/admin/manifests`, `/admin/contents`, `/admin/ui-builder`
- 現状: `/admin`, `/admin/context-token-registry`, `/admin/registry-vector-validate` のみ。
- 不足: `/admin/manifests`, `/admin/contents`, `/admin/ui-builder`
- 対象: `frontend/routes/admin/manifests.tsx`, `frontend/routes/admin/contents.tsx`, `frontend/routes/admin/ui-builder.tsx` (新規)

### Gap-10: `/auth` vs `/login` ルート名不一致 【Frontend Routes】
- SSOT `frontend_routes.public`: `[/, /auth]`
- 現状: `frontend/routes/login.tsx` → `/login` ルート
- SSOT の命名と一致しない。
- 対象: `frontend/routes/login.tsx` → `frontend/routes/auth.tsx` へのリネーム (または SSOT 側の確認)

---

## 深刻度分類

| Gap | 深刻度 | 理由 |
|---|---|---|
| Gap-1 manifest_dispatcher | High | SSOT の中核ルーティング設計が未実装 |
| Gap-4 Manifest DB テーブル | High | manifest_dispatcher / projection_constructor の前提 |
| Gap-5 minimal_event_shape | High | イベント identity 識別フィールドが contract 未整合 |
| Gap-2 scheduler dispatch path | Medium | client trigger の scheduler alignment なし |
| Gap-3 topology_function_binder | Medium | 関数バインディング層の SSOT 準拠なし |
| Gap-6 output lanes | Medium | db_notify / registry_attractor lane なし |
| Gap-7 SSE lane | Medium | projection_event_lane 全体が未実装 |
| Gap-8 projection_constructor | Medium | frontend projection 構築フロー欠如 |
| Gap-9 admin routes | Low | UI ルートが SSOT 定義と不一致 |
| Gap-10 /auth vs /login | Low | ルート名の SSOT 不一致 |

---

## 完了済み実装（点検対象外として確認）
- POST /dispatch シングルエンドポイント ✓
- GET /health ✓
- POST /auth/login ✓
- RuntimeExecutor canonical pipeline ✓
- RetentionScheduler (cron) ✓
- SystemOperationCiScheduler (cron) ✓
- ui_component_bucket / ui_topology_tensor DB テーブル ✓
- bootstrap 検証 ✓
