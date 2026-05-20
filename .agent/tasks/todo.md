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

- [ ] [Claude] Gap-1 partial: `manifest_dispatcher` の `runtime_mapping` による `runtime_destination` 分岐を接続する
      → ManifestDispatcher はマニフェスト解決まで実装済み。解決後の runtime_mapping エントリを使った runtime_destination 選択が未接続 (skeleton)。
      → 対象: `backend/runtime/ManifestDispatcher.cs`, `backend/repository/NpgsqlManifestRepository.cs`
      → docs/system-roadmap.yaml: backend.manifest_dispatcher = partial

- [ ] [Claude] Gap-2 partial: `runtime_timeline_scheduler` の client trigger を統一キューに整列させる
      → 現在 client trigger は ManifestDispatcher に直接同期呼び出し (HTTP response contract 保持のための意図的例外)。
      → cron/hook/client の 3 トリガ全てを同一 Channel で整列する完全実装は未達。
      → 判断点: HTTP response contract を壊さずに統一整列を実現できるか設計が必要。
      → 対象: `backend/scheduler/RuntimeTimelineScheduler.cs`
      → docs/system-roadmap.yaml: backend.runtime_timeline_scheduler = partial

- [ ] [Claude] Gap-6 partial: `OutputLaneRouter` を `RuntimeExecutor` パイプラインから呼び出す
      → OutputLaneRouter / NpgsqlDbNotifyRepository は実装済みだが RuntimeExecutor pipeline に未接続。
      → db_notify_emission / registry_attractor_update lane が実際には通っていない。
      → 対象: `backend/runtime/OutputLaneRouter.cs`, `backend/runtime/RuntimeExecutor.cs`
      → docs/system-roadmap.yaml: backend.output_lanes = partial

- [ ] [Claude] Gap-7 partial: SSE end-to-end integration test を実装する (Issue #123)
      → SseEventBroadcaster / DbNotifyListener / SseEndpoint は実装済みだが E2E テストが未実装。
      → backend → pg_notify → DbNotifyListener → SseEventBroadcaster → SseEndpoint → client の通し確認が未実施。
      → 対象: `backend/tests/`, `docs/design/pipeline-continuity-ssot.yaml`
      → docs/system-roadmap.yaml: backend.sse_emitter = partial (known_gap_ref: Issue #123)

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
