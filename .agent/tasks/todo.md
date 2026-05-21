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

- [ ] [Claude] Gap-1 残: `target_layer_action_destination_selection` の ManifestDispatcher への完全移管
      → RuntimeExecutor 内の demo/admin target dispatch 分岐を TargetDispatchOverride クラスに隔離済み (RuntimeExecutor_target_layer_action_dispatch_branches_are_removed_or_explicitly_isolated 充足)。
      → 残: TargetDispatchOverride → ManifestDispatcher への完全移管 (manifest-driven routing)。これには ManifestDispatcher への依存注入変更が必要。設計判断が先決。
      → 完了条件: target_layer_action_destination_selection_is_moved_to_manifest_dispatcher
      → 対象: `backend/runtime/TargetDispatchOverride.cs`, `backend/runtime/ManifestDispatcher.cs`
      → docs/system-roadmap.yaml: backend.manifest_dispatcher, backend.runtime_executor = partial

- [ ] [Claude] Gap-2 partial: `runtime_timeline_scheduler` の client trigger を統一キューに整列させる
      → 現在 client trigger は ManifestDispatcher に直接同期呼び出し (HTTP response contract 保持のための意図的例外)。
      → cron/hook/client の 3 トリガ全てを同一 Channel で整列する完全実装は未達。
      → 判断点: HTTP response contract を壊さずに統一整列を実現できるか設計が必要。
      → 対象: `backend/scheduler/RuntimeTimelineScheduler.cs`
      → docs/system-roadmap.yaml: backend.runtime_timeline_scheduler = partial

- [ ] [Claude] Gap-7 残: SSE E2E test の live DB 経路と scheduler routing を実装する (Issue #123)
      → DbNotifyListener.HandleNotificationPayload の unit test 追加済み (live DB 不要, DbNotifyListenerPayloadTests)。
      → 残: DbNotifyListener → pg_notify → broadcaster の live DB 経路テスト (live DB 必要)。
      → 残: hook_or_db_notify_event_enters_scheduler_before_sse_emission 完了条件。現在 DbNotifyListener が broadcaster に直接 Broadcast しており scheduler を経由していない。scheduler routing 変更は設計判断が先決。
      → 対象: `backend/scheduler/DbNotifyListener.cs`, `backend/tests/`
      → docs/system-roadmap.yaml: backend.sse_emitter = partial (known_gap_ref: Gap-7)

## Seed Import/Export Runtime (Issue #84)

- [ ] [Claude] Seed import の Gap-1 依存部分を完全実装する (manifest-driven routing 確立後)
      → save / load / validate / preview は実装済み。import は skeleton (Gap-1 依存)。
      → Gap-1 (target_layer_action_destination_selection → ManifestDispatcher 移管) 解消後に ImportAsync を完全実装する。
      → 完了条件: seed_import_export_runtime status=implemented (docs/system-roadmap.yaml)
      → 対象: `backend/runtime/SeedRuntime.cs` (ImportAsync の skeleton 部分)、`backend/runtime/ManifestDispatcher.cs`

## Frontend UI Component System (Issue #86)

- [ ] [Claude] primitive component を UI topology tensor に DB 登録し drift を解消する
      → Button / Input / Table / Card は frontend/components/ にコードのみ存在 (drift / GAP 状態)。
      → 各 component を PackageGeneratorRuntime 経由で componentId / packageId 発行 → ui_topology_tensor に DB 保存する。
      → CRUD wiring / CanDI wiring の責務境界を registrar-admin-ui-specification.md に明記する。
      → 完了条件: code-only component が 0 件になる (全て DB topology tensor に接続)
      → 対象: `db/ui_topology_tables.sql` (component 登録 surface 追加)、`docs/registrar-admin-ui-specification.md`

## Admin Visual Layout Builder (Issue #89)

- [ ] [Claude] visual layout builder の mouse 操作 UI と layout tensor DB 管理を実装する
      → LayoutBuilderSection は ui-builder.tsx に文書化済みだが UI 実装 (drag/drop) は未着手。
      → layoutId / styleTokenId / responsiveRuleId の DB schema 未追加。
      → 前提: Issue #86 component DB 登録完了後。
      → 完了条件: admin_visual_layout_builder status=implemented (docs/system-roadmap.yaml)
      → 対象: `db/ui_topology_tables.sql` (layout token schema)、`frontend/islands/` (drag/drop UI island)、`docs/registrar-admin-ui-specification.md`

## Runtime Environment Test Gate

- [ ] [Claude] Runtime Environment Test Gate の env / volume / live API-route E2E を完全化する
      → PR #149 で `check-runtime-environment.sh` を追加し、GitHub Actions 上の runtime-environment-gate として docker compose PostgreSQL 起動、healthcheck 待機、DB接続確認、required schema relations (`public.manifest`, `public.topology_edit_log`) 検証、live DB 向け integration test 実行、down -v 後始末までは追加済み。
      → 残: env 検証、volume / seed storage read-write 検証、backend コンテナ起動を含む live API-route E2E、dispatch/auth/sse 経路、db_notify / output lane / scheduler routing の実環境検証。
      → `OutputLaneRouter.RouteAsync` と `AdminRuntime.ExecuteDataAsync` は `check-unified-test-gate.sh` 上で NOT_COVERED として残っているため、直接またはAPI経由のカバー強化が必要。
      → 対象: `.agent/tests/check-runtime-environment.sh`, `.github/workflows/unified-test-gate.yml`, `infra/docker-compose.yml`, `backend/tests/Topolactor.Integration.Tests/*.cs`, `backend/runtime/OutputLaneRouter.cs`, `backend/runtime/AdminRuntime.cs`, `backend/scheduler/DbNotifyListener.cs`
      → 完了条件: env / volume / live API-route E2E の実検証が追加される / `OutputLaneRouter.RouteAsync` または `AdminRuntime.ExecuteDataAsync` の未カバー状態が改善される / missing tool・skipped test・起動失敗が pass にならない / 失敗時に docker logs / DB logs 等の原因調査ログが残る / `unified-test-gate` と `check-structure.sh` が green になる
