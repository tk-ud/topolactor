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

## 担当エージェント方針

- AI駆動OS, 小規模修正 = Codex
- data駆動OS, 大規模リファクタ Runtime構築 = Claude

## Implementation Phase — Claude Runtime Boundary Fixes

- [x] [Claude] A1/A2 fix: recoverable boundary と explicit result surface の方針を実装可能な形へ整理・反映する
      → 完了: LogError-only continuing を削除し、AppendContextEventAsync / transition stats / TVR extension の失敗を ExplicitError に統一した。
      → 対象ファイル名: backend/runtime/ContextRouteRecommendationResolver.cs
      → 実装PR: claude/fail-close-context-route-runtime-QuuDb

- [ ] [Claude] A4 fix: context_hub 系 DB CHECK 制約と policy variability の衝突を設計・移行方針へ落とす
      → 問題点: scope_limit / candidate_kind / feedback_kind が DB CHECK に固定され、function_parameters / registry policy の可変性と衝突し得る。
      → 目的: DBがsemantic topology spaceである前提を保ちつつ、policy可変値を過固定化しない。
      → 改善方針: すぐ制約削除するのではなく、Runtime正規導線・migration影響・参照整合を調査して方針化する。
      → 対象ファイル名: db/context_route_tables.sql, docs/design/context-route-recommendation.md, backend/repository/NpgsqlContextRouteRepository.cs
      → 対象関数名: context_hub_recommendation_current, context_hub_feedback_event, UpsertHubAttentionCurrentAsync, ApplyFeedbackWeightUpdateAsync
      → todo: 調査→設計方針→必要なら migration 実装PRへ分離。

- [x] [Claude] A11 fix: AppendContextEventAsync / TVR extension failure の境界失敗テストを追加する
      → 完了: fail-close 方針に基づく A11 failure path tests を追加した。
      → 対象ファイル名: backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs
      → 追加テスト: ResolveAsync_AppendContextEventThrows_ReturnsExplicitError, ResolveAsync_TransitionStatsThrows_ReturnsExplicitError, ResolveAsync_TvrExtensionHubAttentionUpdateThrows_ReturnsExplicitError
      → 実装PR: claude/fail-close-context-route-runtime-QuuDb

## Implementation Phase — Codex Governance / Close Readiness

- [ ] [Codex] Issue #60 close readiness の remote CI pass を確認する
      → 問題点: backend-tests / frontend-types の remote CI pass 確認が残っている。
      → 目的: NOT EXECUTED を PASS 扱いせず、close可否を明確にする。
      → 対象ファイル名: .agent/tasks/todo.md, GitHub Actions status
      → todo: CI確認。pass なら close-ready、未完なら Remaining TODO 継続。

- [ ] [Codex] check-runtime-semantics.sh の remote CI equivalence を確認する
      → 問題点: local環境で dotnet / deno が無い場合、runtime semantics check が未確定になる。
      → 目的: remote CI equivalence または明示的TODOで完了判定を管理する。
      → 対象ファイル名: .agent/tests/check-runtime-semantics.sh, .github/workflows/*
      → todo: CI確認。未確認なら未完了のまま保持。

## Deferred Audit Experiments

- [ ] [Codex] Audit Gap Response Gate / Failure Triage / Required Check Scope の運用継続確認
      → 方針: 実装PRごとの completion report で自然に検証し、todo では増殖させない。
      → 参照: .agent/reports/2026-05-19-claude-boundary-audit-reaudit.md
      → 次の判断点: 実装PRで gate 違反が再発した場合のみ、新規未完了TODOとして再掲する。

- [ ] [Codex] 監査フェーズ成果の参照維持（再監査の再実行は現時点で不要）
      → 方針: 完了済み監査実験を current work に戻さず、必要時はレポート参照で追跡する。
      → 参照: .agent/reports/2026-05-19-claude-boundary-audit-reaudit.md
      → 次の判断点: Claude 実装境界修正後に、必要最小限の再監査TODOだけ追加する。
