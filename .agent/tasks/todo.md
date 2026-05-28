# Agent Task List — Remaining TODO

このファイルは agent task surface として使用する。

完了済み作業・PR修正履歴・旧方針の残骸は残さない。
未完了の implementation / design / SSOT / test-authoring task がある場合のみ、次の形式で追加する。

CI検証待ち、remote CI pass確認、local tool不足、未実行チェックの記録はこのファイルに追加しない。
それらはPRサマリ/完了レポートの verification / Required Check Scope に記載する。


作業中に既存TODOへ一時的な in-progress 印を付ける場合は、チェックボックス（`[x]`）ではなく HTML comment marker を使う。
- marker: `<!-- agent:in-progress -->`
- 使い方: 対象TODOの**直下に単独行**で一時的に付与する（inline付与はしない）
- 完了条件: 作業完了前に必ず marker 単独行を削除する（残存は構造チェック失敗）

```md
## <Area>

- [ ] <具体的な未完了作業>
      → <理由・対象ファイル・次の判断点>
```

## CI Attention guidance fragment identity residuals

- [ ] [schema/runtime] Include `authoring_surface` in CI Attention current projection identity
      → `logs.ci_attention_guidance_current` unique index / Npgsql upsert conflict target が SSOT `fragment_contract.source_identity` と不一致。surface別 fragment が current projection 上で上書きされないよう、identity を `authoring_surface` 含みに揃える（db/ci_attention_guidance_tables.sql, backend/repository/NpgsqlCiAttentionGuidanceRepository.cs）。

- [ ] [projection] Include `authoring_surface` in CI Attention SSE projection payload
      → `CiAttentionGuidanceGuidanceEventPayload` / Npgsql RETURNING / frontend parser / tests が source_identity の `authoring_surface` を projection payload として伝搬していない。frontend は read-only 表示境界を維持したまま identity を露出する（backend/schema/CiAttentionGuidanceContracts.cs, backend/repository/NpgsqlCiAttentionGuidanceRepository.cs, frontend/runtime/sseReceiver.ts, frontend/tests/ciAttentionGuidanceProjection.test.ts, backend/tests/Topolactor.Runtime.Tests/CiAttentionPromotionGateTests.cs）。

## Identity / authority completion bundle

- [ ] [runtime] Add/keep backend test evidence that manifest routing role axis authority is JWT claim at dispatch boundary (client body/context role must not override authenticated role).
      → Keep as bundle-level follow-up only if full end-to-end endpoint+dispatcher authority proof remains missing.

## UI/UX Primitive Executable Component Slice

運用整合チェック用の代表名（AutoCompleteInput / SearchCombobox / CandidateConfidenceBadge / InlineEditableField / PatchPreviewPanel / ApplyConfirmDialog / FacetedFilterBar / VirtualizedDataTable / LayoutDropZone / ComponentPlacementHandle / SnapGridOverlay / StyleTokenPicker / ThemePreviewPanel / DryRunResultPanel / ValidationErrorPanel）。
