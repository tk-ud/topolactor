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

- [ ] [Claude] Gap-1: `manifest_dispatcher` を完全実装する
      → スケルトン実装済み: `backend/runtime/ManifestDispatcher.cs` (RuntimeExecutor への同期 delegate)。
      → 残り: role+target+layer+action で active manifest を DB から解決する manifest-driven routing。
      → `db/manifest_tables.sql` / `db/init.sql` の表面追加は skeleton として存在するが、
         `role_definition` / `route_definition` / `dispatcher_mapping` / `runtime_mapping` を
         active manifest 解決に利用できる contract completeness へ整理する必要がある。
      → RuntimeExecutor 内の target/layer/action ハードコード分岐を移管。
      → 対象: `backend/runtime/ManifestDispatcher.cs`、`backend/runtime/RuntimeExecutor.cs`
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

- [ ] [Claude] Gap-3: `topology_function_binder` / `topology_function_interface` を実装する
      → SSOT `backend_contract.topology_function_binder`: function_name + where_condition + jsonb_array → interface_args
      → 対象: `backend/runtime/TopologyFunctionBinder.cs` (新規)、`backend/schema/TopologyFunctionInterface.cs` (新規)
      → Scenario Contract 必須。

- [ ] [Claude] Gap-2: `runtime_timeline_scheduler` のトリガ統合を完全実装する
      → スケルトン実装済み: `backend/scheduler/RuntimeTimelineScheduler.cs` — client trigger は
         DispatchEndpoint → RuntimeTimelineScheduler → ManifestDispatcher → RuntimeExecutor の経路に接続済み。
      → 残り: cron (RetentionScheduler) / hook (SystemOperationCiScheduler) / client の3トリガを
         同一キューで整列する unified queue 実装。前提: Gap-1 (manifest_dispatcher) 完了後。
      → 対象: `backend/scheduler/RuntimeTimelineScheduler.cs`、`backend/Program.cs`
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

- [ ] [Claude] Gap-6: output lane `db_notify_emission` / `registry_attractor_update` を実装する
      → SSOT `backend_contract.output_lanes`: response_emission (実装済み) + db_notify_emission + registry_attractor_update。
      → db_notify payload: table_id, table_registry_id, manifest_id。
      → pg LISTEN は BackgroundService で保持し hook_trigger として backend_scheduler キューへ。
      → 前提: Gap-1 (manifest_dispatcher) 完了後。
      → 対象: `backend/runtime/OutputLaneRouter.cs` (新規)、`backend/repository/DbNotifyRepository.cs` (新規)
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

### Frontend

- [ ] [Claude] Gap-7: SSE projection lane を完全実装する
      → スケルトン実装済み:
         - `backend/endpoint/SseEndpoint.cs` (keep-alive ping のみ)
         - `frontend/runtime/sseReceiver.ts` (EventSource connect/disconnect skeleton)
         - `frontend/runtime/sseDispatcher.ts` (handler-map skeleton)
         - `frontend/routes/api/sse.ts` (proxy skeleton)
      → 残り: 実際の projection/Attention イベント送信・受信・projection_runtime への接続。
         SSE receiver が hook_trigger として frontend_scheduler キューへ送る (backend と対称構造)。
      → Issue #123 残件として、`docs/design/pipeline-continuity-ssot.yaml` に記載の
         SSE end-to-end integration test (real server push through full lane) 未実装をここへ包含して完了させる。
      → 前提: Gap-6 (db_notify_emission) 完了後。
      → 対象: `backend/endpoint/SseEndpoint.cs`、`frontend/runtime/sseReceiver.ts`、
         `frontend/runtime/projectionRuntime.ts` (新規)
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

## Seed Import/Export Runtime (Issue #84)

- [ ] [Claude] seed Runtime の SSOT 位置づけを確定し、save / validate / preview / import 導線を実装する
      → Issue #84: UI-managed `/storage/seed.json` を正規導線とする seed Runtime。
      → まず seed Runtime を SSOT (runtime-orchestration-ssot / registrar-admin-ui-specification) 上のどの境界に置くか明記する。
      → `/storage/seed.json` の save / load / validate / preview / import を段階分離して実装する。import 失敗は silent fallback 禁止。
      → docker-compose.yml に `/storage` volume mount を追加する。
      → 前提: manifest_dispatcher / topology_function_binder Gap 解消後が望ましいが、SSOT位置づけ明記は先行可能。
      → 対象: `backend/runtime/`、`backend/repository/`、`frontend/routes/admin/`、`docs/registrar-admin-ui-specification.md`、`infra/docker-compose.yml`
      → Scenario Contract + Runtime Boundary Failure Matrix 必須。

## Frontend UI Component System (Issue #86)

- [ ] [Claude] Tailwind ベース primitive/packaged component system と UI topology DB 登録導線を実装する
      → Issue #86: primitive component → componentId/packageId 発行 → UI topology DB 保存 → CRUD/CanDI wiring。
      → code-only component は drift/GAP 扱い。component / package は必ず DB 上の UI topology tensor に接続する。
      → primitive (Button/Input/Table/Card 等) と packaged/composite component の境界を明確化する。
      → CRUD wiring / CanDI wiring の責務境界を定義する。frontend が runtime/topology 判定を持たないことを明記する。
      → 対象: `frontend/components/`、`db/ui_topology_tables.sql`、`docs/registrar-admin-ui-specification.md`、`docs/file-structure.yaml`
      → Scenario Contract 必須。

## Admin Visual Layout Builder (Issue #89)

- [ ] [Claude] admin visual layout builder と layout tensor / variable CSS 管理導線を設計・実装する
      → Issue #89: admin 画面で layout を mouse 操作で構成し、layoutId / styleTokenId / responsiveRuleId を DB に保存する。
      → Tailwind を画面ごとの直書きにせず、layout token / style token として DB 管理する。
      → components bucket → package generator → visual layout builder → UI topology DB の接続を明記する。
      → frontend adapter は固定 projection surface とし、仕様追加は registry tensor / UI topology data で表現する。
      → 前提: Issue #86 (component system) の方針定義後。
      → 対象: `db/ui_topology_tables.sql`、`frontend/routes/admin/`、`frontend/islands/`、`docs/registrar-admin-ui-specification.md`
      → Scenario Contract 必須。

## Demo — Auth Guard パターン例示

- [ ] [Claude] demo article ページを作成し、返信 UI にコンポーネント auth ガードを実装する
      → 目的: 全面 auth ガード (/auth ページ) と コンポーネント単位 auth ガード の両パターンをデモで示す。
      → 構成:
        - `frontend/routes/demo-article.tsx` — 記事本文は未認証でも閲覧可能
        - `frontend/islands/ReplyPanel.tsx` — 返信フォーム。未認証時は「ログインして返信する → /auth」を表示、
          認証済み時は返信フォームを表示するコンポーネント level auth ガード
      → sessionStorage の demo_jwt_token 有無で認証状態を判定。
      → 対象: `frontend/routes/demo-article.tsx` (新規)、`frontend/islands/ReplyPanel.tsx` (新規)
