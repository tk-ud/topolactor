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

## TODO dependency map（execution order）

1. Runtime recommendation EMA/statistics integration（独立）
2. UI primitive component DB registration（Issue #86）
3. Visual layout builder（Issue #89, depends on #86）

---

## Runtime Recommendation Pipeline

- [x] statistics / EMA integration for topology projection recommendation を実装する
      → 依存関係: なし（単独着手可）。
      → 対象責務: recommendation candidate 並び替え・投影生成（runtime path）。
      → 対象ファイル: `backend/runtime/PackageResolver.cs`, `backend/runtime/EmissionBuilder.cs`。
      → 実装結果: recommendation_blend を policy (`function_parameters`) で導入し、EMA/trend/statistics の重み・scope_limit を外部化。EMA persistence は `context_hub_recommendation_current` を継続利用。
      → 監査役TODO: 本番 policy row に `topology_vector_runtime.recommendation_blend` を追加し、運用重みを確定。

## Runtime Orchestration SSOT 準拠 (SSOT: docs/design/runtime-orchestration-ssot.yaml)

SSOT参照必読:
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`

## Frontend UI Topology Tensor Registration (Issue #86)

- [ ] primitive component を UI topology tensor に DB 登録し drift を解消する
      → 依存関係: runtime recommendation TODO と独立（並行可）。
      → 対象責務: component topology の永続化・責務境界明記。
      → 対象ファイル: `db/ui_topology_tables.sql`, `docs/registrar-admin-ui-specification.md`。
      → 詳細:
        - Button / Input / Table / Card は frontend/components/ に code-only で存在（drift / GAP）。
        - 各 component を PackageGeneratorRuntime 経由で componentId / packageId 発行 → ui_topology_tensor に DB 保存する。
        - CRUD wiring / CanDI wiring の責務境界を `docs/registrar-admin-ui-specification.md` に明記する。
      → 完了条件: code-only component が 0 件（全て DB topology tensor に接続）。

## Admin Visual Layout Builder (Issue #89)

- [ ] visual layout builder の mouse 操作 UI と layout tensor DB 管理を実装する
      → 依存関係: **Issue #86 完了後に着手**（component DB registration が前提）。
      → 対象責務: layout tensor schema + drag/drop UI island 実装。
      → 対象ファイル: `db/ui_topology_tables.sql`, `frontend/islands/`, `docs/registrar-admin-ui-specification.md`。
      → 詳細:
        - LayoutBuilderSection は ui-builder.tsx に文書化済みだが UI 実装（drag/drop）は未着手。
        - `layoutId` / `styleTokenId` / `responsiveRuleId` の DB schema 未追加。
      → 完了条件: `docs/system-roadmap.yaml` の `admin_visual_layout_builder status=implemented`。
