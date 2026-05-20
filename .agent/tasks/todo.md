# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。

完了済み作業・PR修正履歴・旧方針の残骸は残さない。
未完了の task がある場合のみ、次の形式で追加する。

```md
## <Area>

- [ ] <具体的な未完了作業>
      → <理由・対象ファイル・次の判断点>
```

## Runtime Orchestration SSOT 準拠 (SSOT: docs/design/runtime-orchestration-ssot.yaml)

点検レポート: `.agent/reports/2026-05-20-runtime-orchestration-ssot-inspection.md`

SSOT参照必読:
- `docs/design/runtime-orchestration-ssot.yaml` (manifest_dispatcher, scheduler_contract, minimal_event_shape, output_lanes)
- `docs/framework-core.yaml` (canonical route)
- `docs/framework-policy.yaml` (backend_flow)

### Backend Structural Gaps

- [ ] [Claude] Gap-4: Manifest DB テーブルを追加する
      → SSOT `manifest_contract.storage: db`。role_definition / route_definition / ui_projection_definition /
         dispatcher_mapping / runtime_mapping / topology_function_binding_mapping / projection_constructor_mapping
         の各テーブルが未実装。manifest_dispatcher と projection_constructor の前提となる DB 層。
      → 対象: `db/manifest_tables.sql` (新規)、`db/init.sql` (\i 追記)
      → 実装前に SSOT の manifest_contract.invariants を再確認必須。
      → Scenario Contract 必須 (DB スキーマ変更を伴う)。

- [ ] [Claude] Gap-5: `minimal_event_shape` フィールドを EndpointRequestDto / DispatchRequest に追加する
      → SSOT `minimal_event_shape.identity_fields`: trigger_kind, manifest_id, trace_id が欠如。
      → `EndpointRequestDto` に `trigger_kind` (cron|hook|client)、`manifest_id` (nullable Guid)、
         `trace_id` (nullable string) を追加。`OperationType` との関係を整理。
      → frontend `DispatchRequest` 型 (`frontend/api/dispatch.ts`) も同期して追加。
      → 対象: `backend/schema/Contracts.cs`、`frontend/api/dispatch.ts`
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

- [ ] [Claude] Gap-1: `manifest_dispatcher` を実装する
      → SSOT `backend_contract.flow`: endpoint → backend_scheduler → manifest_dispatcher → topology_transform_runtime
      → ManifestDispatcher は manifest DB の dispatcher_mapping / runtime_mapping を読んで
         runtime destination を決定する。RuntimeExecutor 内の target/layer/action ハードコード分岐を移管。
      → 前提: Gap-4 (manifest DB) が完了していること。
      → 対象: `backend/runtime/ManifestDispatcher.cs` (新規)、`backend/runtime/RuntimeExecutor.cs` (分岐移管)
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

- [ ] [Claude] Gap-3: `topology_function_binder` / `topology_function_interface` を実装する
      → SSOT `backend_contract.topology_function_binder`: function_name + where_condition + jsonb_array → interface_args
      → SSOT prohibited: entity_constructor / csharp_factory / domain_invariant_constructor
      → 関数バインディング層として Repository 内の直接 SQL 組み立てを整理し、
         TopologyFunctionInterface を経由する形に移行する。
      → 対象: `backend/runtime/TopologyFunctionBinder.cs` (新規)、`backend/schema/TopologyFunctionInterface.cs` (新規)
      → Scenario Contract 必須。

- [ ] [Claude] Gap-2: `runtime_timeline_scheduler` をクライアント dispatch パスに接続する
      → SSOT `scheduler_contract.input: cron_trigger | hook_trigger | client_trigger`
      → 現状 client trigger は RetentionScheduler 等を経由せず直接 RuntimeExecutor に到達する。
      → scheduler alignment (causal order, execution_boundary, collision_control) を client dispatch にも適用。
      → 前提: Gap-1 (manifest_dispatcher) が完了していること。
      → 対象: `backend/scheduler/RuntimeTimelineScheduler.cs` (新規)、`backend/Program.cs`
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

- [ ] [Claude] Gap-6: output lane `db_notify_emission` / `registry_attractor_update` を実装する
      → SSOT `backend_contract.output_lanes`: response_emission (実装済み) に加え
         db_notify_emission と registry_attractor_update lane が必要。
      → `notify_listen_contract` の db_notify + pg LISTEN インフラを実装。
      → 前提: Gap-1 (manifest_dispatcher) が完了していること。
      → 対象: `backend/runtime/OutputLaneRouter.cs` (新規)、`backend/repository/DbNotifyRepository.cs` (新規)
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

### Frontend Structural Gaps

- [ ] [Claude] Gap-7: SSE projection lane を実装する
      → SSOT `frontend_contract.lanes.projection_event_lane`:
         sse_receiver → frontend_scheduler → sse_dispatcher → projection_runtime → ui_projection
      → フロントエンドに SSE 受信インフラが存在しない。
      → 前提: Gap-6 (db_notify_emission) が完了していること。
      → 対象: `frontend/runtime/sseReceiver.ts` (新規)、`frontend/runtime/projectionRuntime.ts` (新規)
      → Scenario Contract 必須。

- [ ] [Claude] Gap-8: `projection_constructor` を実装する
      → SSOT `frontend_contract.projection_constructor`:
         json_key_value + projection_definition → form_inputs | component_projection | ui_projection
      → 現状 `renderEmission.ts` は ComponentRegistry から ComponentSpec を取得するのみ。
         projection_definition を参照する構築フローがない。
      → 前提: Gap-4 (manifest DB) の projection_constructor_mapping が必要。
      → 対象: `frontend/runtime/projectionConstructor.ts` (新規)
      → Scenario Contract 必須。

- [ ] [Claude] Gap-9: frontend admin routes を SSOT に合わせて追加する
      → SSOT `frontend_routes.admin`: /admin (実装済み), /admin/manifests, /admin/contents, /admin/ui-builder
      → skeleton ページで構わない。SSOT ルート定義との一致を確立する。
      → 対象: `frontend/routes/admin/manifests.tsx` / `contents.tsx` / `ui-builder.tsx` (新規)

- [ ] [Claude] Gap-10: `/login` ルートを SSOT の `/auth` に合わせるか SSOT を更新する
      → SSOT `frontend_routes.public: [/, /auth]` だが現状は `/login` ルートが存在する。
      → 選択肢:
        (a) `frontend/routes/login.tsx` → `frontend/routes/auth.tsx` にリネーム
        (b) SSOT を `/login` に更新する
      → SSOT が `/auth` を規定している理由を確認してから判断。対象: `frontend/routes/login.tsx` or SSOT
