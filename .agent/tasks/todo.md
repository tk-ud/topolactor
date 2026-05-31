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

## Dynamic Support Nocode Loop — manual acceptance

- [ ] `product.dynamic_support_nocode_loop` の manual acceptance / hand-debug verification を実施し、authoring guidance・SQL Attention feedback・M6 self-hosted admin authoring loop が同一UX導線として受入可能か確認する。
      → 残理由は implementation gap ではない。M6 self-hosted admin authoring loop、SQL Attention SQLA-1..5、SQL Attention live DB E2E、roadmap/test-bundles 正規化は完了済みで、M6/M7 core runtime production-ready 判定は維持する。future optional external connector surfaces は M6/M7 blocker ではない。


## Frontend projection surface — general UX acceptance

- [ ] `product.frontend_projection_surface_ux_acceptance` completion bundle: DB authority / frontend projection surface 境界を維持したまま、`UXA_frontend_projection_surface_general_ux` を受入可能にする。
      → Roadmap SSOT: `docs/system-roadmap.yaml` の `ux_acceptance_gates.frontend_projection_surface_general_ux`。runtime `production_ready` 判定とは独立した UX acceptance gate として扱う。frontend は local draft / preview / intent submission / backend result projection のみを所有し、design/layout/wiring authority、topology judgment、promotion judgment、persistence judgment、SQL Attention judgment を所有しない。direct DB write は追加せず、`layout_patch:preview → validate → apply` を維持する。
      → 未実装境界（完全列挙）:
        1. `lifecycle_state_visibility`: draft / validated / applied / failed / persisted を単一のユーザー向け状態モデルとして区別する。現状は `ApplyReadinessPanel` と `LayoutPatchSummaryPanel` の局所表示までで、backend persisted の明示と状態遷移履歴が不足。
        2. `recovery_navigation`: Undo/Redo、または最低限の cancel / reset / last persisted state への revert 導線を追加する。既存 primitive の `UndoTimeline` / `RollbackCandidatePanel` は UI builder 導線に未接続。
        3. `actionable_validation_errors`: 内部 code を advanced 情報として保持しつつ、原因・対象 node・一般語彙での修正候補を表示する。現状 `ValidationErrorPanel` と `projectLayoutPatchSummary` は message / generic next action 中心。
        4. `progressive_disclosure_vocabulary`: 通常表示の `slotKey` / `parentNodeId` / `gridCol` / `gridRow` / raw ref を一般語彙へ翻訳し、raw 値は advanced view に限定する。既存 `AdvancedManualOverride` は UUID/key 手入力の一部のみ。
        5. `non_pointer_operation`: palette add、node move、resize、reorder、delete に keyboard または button 操作を追加する。現状 canvas node / resize handle / drag-drop は pointer 中心。
        6. `accessibility_observability`: focus order、focus visible、target size、status message を反復確認可能にする。canvas node と resize handle の focusable control 化、visible focus、minimum target size、preview / validate / apply / persisted の live status 投影、a11y check を追加する。
        7. `first_run_guidance`: empty state に template / example flow と非 drag の初回 CTA を追加する。現状 how-to/help と「パレットからドラッグ」のみ。
        8. `css_token_visual_diff`: CSS token 選択に visual preview と before/after selection diff を接続する。現状 searchable/filterable candidate list、selected chip、selected count まで。
      → 実装対象候補: `frontend/islands/UiBuilderAdmin.tsx` (`LayoutBuilderSection`, `ApplyReadinessPanel`, `LayoutPatchSummaryPanel`, `LayoutPalette`, `CanvasInspector`, `LayerTree`, `VisualLayoutNode`, `ResizeHandle`, `VisualLayoutCanvas`, `CssTokenPicker`)、`frontend/components/ValidationErrorPanel.tsx`、必要に応じて既存 `UndoTimeline` / `RollbackCandidatePanel` / `EmptyStateActionPanel` / `ThemePreviewPanel` / `CssVariablePreview` adapter、`frontend/runtime/cssDictionary.ts`、projection component adapter / primitive renderer 周辺。
      → test-authoring 対象: `frontend/tests/visualLayoutBuilder.test.ts` に helper 単体境界を追加し、必要に応じて UI interaction test を追加する。`.agent/tests/check-system-roadmap.sh` は acceptance criteria と authority boundary の SSOT 構造を検査し、`.agent/tests/check-css-dictionary.sh` と `.agent/tests/check-ui-ux-executable-component-slice.sh` は既存 dictionary / primitive reachability を継続検査する。focus order / focus visible / target size / status message は automation または反復可能な manual acceptance check に接続する。
