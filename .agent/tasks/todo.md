# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。

完了済み作業・PR修正履歴・旧方針の残骸は残さない。
未完了の task がある場合のみ、次の形式で追加する。

```md
## <Area>

- [ ] <具体的な未完了作業>
      → <理由・対象ファイル・次の判断点>
```

## Current TODO

## Runtime / Policy Variability

- [ ] [Claude] A4 fix: context_hub 系 DB CHECK 制約と policy variability の衝突を設計・移行方針へ落とす
      → 問題点: scope_limit / candidate_kind / feedback_kind が DB CHECK に固定され、function_parameters / registry policy の可変性と衝突し得る。
      → 目的: DB が semantic topology space である前提を保ちつつ、policy 可変値を過固定化しない。
      → 対象ファイル名: db/context_route_tables.sql, docs/design/context-route-recommendation.md, backend/repository/NpgsqlContextRouteRepository.cs
      → 対象関数名: context_hub_recommendation_current, context_hub_feedback_event, UpsertHubAttentionCurrentAsync, ApplyFeedbackWeightUpdateAsync
      → 次の判断点: 調査 → 設計方針化 → 必要なら migration 実装PRへ分離。

## Governance / CI Readiness

- [ ] [Codex] Issue #60 close readiness の remote CI pass を確認する
      → 問題点: backend-tests / frontend-types の remote CI pass 確認が残っている。
      → 目的: REQUIRED_NOT_EXECUTED を PASS 扱いせず、close可否を明確にする。
      → 対象: .agent/tasks/todo.md, GitHub Actions status
      → 次の判断点: CI pass なら close-ready、未完なら Remaining TODO 継続。

- [ ] [Codex] check-runtime-semantics.sh の remote CI equivalence を確認する
      → 問題点: local環境で dotnet / deno が無い場合、runtime semantics check が未確定になる。
      → 目的: remote CI equivalence または明示的 TODO で完了判定を管理する。
      → 対象ファイル名: .agent/tests/check-runtime-semantics.sh, .github/workflows/*
      → 次の判断点: CI確認。未確認なら未完了のまま保持。

## Documentation Boundary Hygiene

- [ ] [Codex] optional / future / implemented 境界のSSOT横断一覧を整備する
      → 問題点: 実装済み/未実装境界の可視化が分散し、監査コストと誤読余地が残る。
      → 目的: optional / future / implemented の境界を単一の参照面で追跡可能にする。
      → 対象ファイル候補: docs/design/*, docs/file-structure.yaml
      → 次の判断点: 既存ドキュメントへの統合位置を決め、一覧形式を確定する。

## Registry Tensor Continuity

- [ ] [Codex] registry tensor projection surface の実装乖離点検（runtime / endpoint / scheduler / function / UI topology）
      → 問題点: SSOT と監査Policyの明文化後、実装側で surface 間の意味連続性が崩れる余地がある。
      → 目的: projection/expansion surface ごとの drift を点検し、必要なら別PRで是正する。
      → 対象ファイル候補: docs/design/*, backend/runtime/*, frontend/*, db/*（点検のみ）
      → 次の判断点: 点検チェックリスト化の要否を判断。
