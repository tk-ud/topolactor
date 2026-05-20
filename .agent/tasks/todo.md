# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。

完了済み作業・PR修正履歴・旧方針の残骸は残さない。
未完了の implementation / design / SSOT / test-authoring task がある場合のみ、次の形式で追加する。

CI検証待ち、remote CI pass確認、local tool不足、未実行チェックの記録はこのファイルに追加しない。
それらはPRサマリ/完了レポートの verification / Required Check Scope に記載する。

```md
## <Area>

- [ ] <具体的な未完了作業>
      → <理由・対象ファイル・次の判断点>
```

## Runtime Orchestration SSOT 準拠 (SSOT: docs/design/runtime-orchestration-ssot.yaml)

SSOT参照必読:
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`

### Backend

- [ ] [Claude] Gap-5: `EndpointRequestDto` / `DispatchRequest` に `trigger_kind` / `role` を追加する
      → SSOT `minimal_event_shape.fields`: trigger_kind (cron|hook|client), role (jwt_token_claim)。
      → `EndpointRequestDto` に trigger_kind / role を追加。OperationType との関係を整理。
      → frontend `DispatchRequest` 型 (`frontend/api/dispatch.ts`) も同期して追加。
      → 対象: `backend/schema/Contracts.cs`、`frontend/api/dispatch.ts`
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

- [ ] [Claude] Gap-1: `manifest_dispatcher` を実装する
      → SSOT `backend_contract.flows`: endpoint → backend_scheduler → manifest_dispatcher → topology_transform_runtime
      → API: role+target+layer+action で active manifest を解決。role は JWT claim から取得。
      → RuntimeExecutor 内の target/layer/action ハードコード分岐を移管。
      → 対象: `backend/runtime/ManifestDispatcher.cs` (新規)、`backend/runtime/RuntimeExecutor.cs`
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

- [ ] [Claude] Gap-3: `topology_function_binder` / `topology_function_interface` を実装する
      → SSOT `backend_contract.topology_function_binder`: function_name + where_condition + jsonb_array → interface_args
      → 対象: `backend/runtime/TopologyFunctionBinder.cs` (新規)、`backend/schema/TopologyFunctionInterface.cs` (新規)
      → Scenario Contract 必須。

- [ ] [Claude] Gap-2: `runtime_timeline_scheduler` をクライアント dispatch パスに接続する
      → SSOT `scheduler_contract`: cron / hook / client の3トリガを同一キューで整列。
      → 現状 client trigger はスケジューラを経由せず直接 RuntimeExecutor に到達する。
      → 前提: Gap-1 (manifest_dispatcher) 完了後。
      → 対象: `backend/scheduler/RuntimeTimelineScheduler.cs` (新規)、`backend/Program.cs`
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

- [ ] [Claude] Gap-6: output lane `db_notify_emission` / `registry_attractor_update` を実装する
      → SSOT `backend_contract.output_lanes`: response_emission (実装済み) + db_notify_emission + registry_attractor_update。
      → db_notify payload: table_id, table_registry_id, manifest_id。
      → pg LISTEN は BackgroundService で保持し hook_trigger として backend_scheduler キューへ。
      → 前提: Gap-1 (manifest_dispatcher) 完了後。
      → 対象: `backend/runtime/OutputLaneRouter.cs` (新規)、`backend/repository/DbNotifyRepository.cs` (新規)
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

### Frontend

- [ ] [Claude] Gap-7: SSE projection lane を実装する
      → SSOT `frontend_contract.lanes.projection_event_lane`:
         sse_receiver → frontend_scheduler → sse_dispatcher → projection_runtime → ui_projection
      → SSE receiver が hook_trigger として frontend_scheduler キューへ送る (backend と対称構造)。
      → 前提: Gap-6 (db_notify_emission) 完了後。
      → 対象: `frontend/runtime/sseReceiver.ts` (新規)、`frontend/runtime/projectionRuntime.ts` (新規)
      → Scenario Contract 必須。

- [ ] [Claude] Gap-8: `projection_constructor` を実装する
      → SSOT `frontend_contract.projection_constructor`:
         json_key_value + projection_definition → form_inputs | component_projection | ui_projection
      → manifest から projection_constructor_mapping を取得して構築する。
      → 対象: `frontend/runtime/projectionConstructor.ts` (新規)
      → Scenario Contract 必須。

- [ ] [Claude] Gap-9: frontend admin routes skeleton を追加する
      → SSOT `frontend_routes.admin`: /admin/manifests, /admin/contents, /admin/ui-builder が未実装。
      → 対象: `frontend/routes/admin/manifests.tsx` / `contents.tsx` / `ui-builder.tsx` (新規 skeleton)

## Demo — Auth Guard パターン例示

- [ ] [Claude] demo article ページを作成し、返信 UI にコンポーネント auth ガードを実装する
      → 目的: 全面 auth ガード (/auth ページ) と コンポーネント単位 auth ガード の両パターンをデモで示す。
      → 構成:
        - `frontend/routes/demo-article.tsx` — 記事本文は未認証でも閲覧可能
        - `frontend/islands/ReplyPanel.tsx` — 返信フォーム。未認証時は「ログインして返信する → /auth」を表示、
          認証済み時は返信フォームを表示するコンポーネント level auth ガード
      → sessionStorage の demo_jwt_token 有無で認証状態を判定。
      → 対象: `frontend/routes/demo-article.tsx` (新規)、`frontend/islands/ReplyPanel.tsx` (新規)