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


## SQL Attention Logs schema contract 後の次フェーズ

- [ ] refresh logs.current / l2 norm watch function implementation を実装する
      → top3 norm-level watch、membership/order/level/delta 変動検知、threshold 解決（Manifest / function_parameters / policy table）と return/exploration-candidate 分岐を実装する。policy値の magic number 化は禁止。

- [ ] scheduler/runtime hub-attractor exploration を実装する
      → exploration 実行責務 (scheduler vs runtime)、vector permutation 上限、hub-attractor topK を実装し、evidence 保存は独立 TODO（write_logs_attention 境界）を参照する。

- [ ] [Claude] logs.attention evidence persistence / write_logs_attention boundary を実装する
      → SQL Attention 完了境界は hub-attractor exploration の実行ではなく、`logs.attention` に evidence row が保存されることとする。
      → `current_id` linkage を必須にし、`statistics_json` / `ema_score`、`l2_norm` / `vector_json` / `neighbor_score`、`phase_vector_json`、`evidence_json` を evidence meaning 別に保持する。
      → statistics / Attention / Phase Attention を単一scoreへ collapse しない。
      → append-only / archive-required とし、phase_vector から registry mutation / migration / column promotion は実行しない。
      → 対象: future `write_logs_attention` 相当 responsibility, `logs.attention` schema/write path, scheduler/runtime exploration save boundary

- [ ] phase_vector generation implementation を行う
      → phase_vector は `logs.attention.vector_json` から始まる post-main auxiliary evidence transform として実装し、logs.attention.phase_vector_json に evidence として保存する。
      → `w = l2_norm`、`x/y/z = hub-side record-count bases`、`i/j/k = axis movement amounts` の意味境界を維持し、phase movement は manifest / policy cap 由来ではないことを明示する。
      → phase_vector から自動 mutation/migration/promotion は行わない。

- [ ] statistics / EMA integration for topology projection recommendation を実装する
      → hit hub から投影される topologys 意味空間の提示順/候補強度を統計・EMA・履歴・利用頻度で扱う recommendation basis を実装する。

- [ ] refresh logs.hub_current / attractor current function implementation を実装する
      → hub-side attractor current と axis z-score を算出・更新する function contract を実装し、phase_vector 移動距離計算に必要な母数を提供する。

- [ ] physical tableid 対応の実装判断を行う
      → topology_edit_log を logs.diff として流用する場合の physical table identity 追加/写像方式を決定する。現状は domain scope identifier であり canonical logs.diff ではない。

## Runtime Orchestration SSOT 準拠 (SSOT: docs/design/runtime-orchestration-ssot.yaml)

SSOT参照必読:
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`

### Backend

- [ ] [Claude] Gap-7 残: SSE E2E test の live DB 経路と scheduler routing を実装する (Issue #123)
      → DbNotifyListener.HandleNotificationPayload の unit test 追加済み (live DB 不要, DbNotifyListenerPayloadTests)。
      → 残: DbNotifyListener → pg_notify → broadcaster の live DB 経路テスト (live DB 必要)。
      → 残: hook_or_db_notify_event_enters_scheduler_before_sse_emission 完了条件。現在 DbNotifyListener が broadcaster に直接 Broadcast しており scheduler を経由していない。scheduler routing 変更は設計判断が先決。
      → 対象: `backend/scheduler/DbNotifyListener.cs`, `backend/tests/`
      → docs/system-roadmap.yaml: backend.sse_emitter = partial (known_gap_ref: Gap-7)

- [ ] [Claude] Gap-14 残: `runtime_timeline_scheduler` の queue durability / overflow / cancellation boundary hardening
      → cron/hook/client の unified queue alignment は実装済み (Gap-2 完了扱い)。
      → 残: queue persistence 未実装、queue full (overflow/backpressure) 時の明示境界、cancellation 時の再実行・中断境界の仕様/実装を確定する。
      → 完了条件: queue_persistence_and_overflow_or_cancellation_boundary_not_finalized を解消し `backend.runtime_timeline_scheduler` を implemented へ昇格可能な状態にする。
      → 対象: `backend/scheduler/RuntimeTimelineScheduler.cs`, `docs/system-roadmap.yaml`
      → docs/system-roadmap.yaml: backend.runtime_timeline_scheduler known_gap_ref: Gap-14

- [ ] [Claude] Gap-15 残: output lane full connection / live verification の実装とゲート接続
      → 残: `OutputLaneRouter.RouteAsync` / `AdminRuntime.ExecuteDataAsync` / db_notify output lane の live 実行検証を Runtime Environment Test Gate と接続する。
      → Gap-14 と分離し、timeline alignment 完了後の output lane 接続・検証ギャップとして追跡する唯一の管理ポイントとする。
      → 対象: `.agent/tests/check-runtime-environment.sh`, `backend/runtime/OutputLaneRouter.cs`, `backend/runtime/AdminRuntime.cs`, `backend/repository/*Notify*.cs`
      → docs/system-roadmap.yaml: backend.runtime_timeline_scheduler known_gap_ref: Gap-15, milestones.M3_unified_timeline_and_output_lanes.blocking_gaps: Gap-15

## Seed Import/Export Runtime (Issue #84)

- [ ] [Claude] SeedRuntime.ImportAsync の canonical DB write 実装を完了する
      → save / load / validate / preview は実装済み。残は import の skeleton 解消。
      → SeedRuntime.ImportAsync を canonical route で DB write まで到達する実装にする。
      → 完了条件: seed_import_export_runtime status=implemented (docs/system-roadmap.yaml)
      → 対象: `backend/runtime/SeedRuntime.cs` (ImportAsync の skeleton 部分)、`backend/runtime/ManifestDispatcher.cs`

- [ ] [Codex] SeedRuntime.ImportAsync canonical DB write confirmation contract を実装する
      → 現在 import は canonical route dispatch までは実施するが、runtime handler から `canonical_db_write_applied=true` が返る契約が未整備なため fail-close になる。
      → seed import declaration ごとに canonical write 完了を machine-check できる runtime output contract と test fixture を追加する。
      → 対象: `backend/runtime/SeedRuntime.cs`, `backend/runtime/AdminRuntime.cs`, `backend/tests/Topolactor.Runtime.Tests/*`

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
