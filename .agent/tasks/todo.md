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

## Non-blocking cleanup / hardening carry-over

- [ ] [coverage] Expand CI Attention guidance projection coverage beyond `admin_ui_builder` to additional authoring surfaces
      → Current scope is intentionally ui-builder-first; authority/visibility/promotion completion bundle is closed, so remaining work is surface-expansion only.

- [ ] [hardening] Add deeper manifest-activation and audit-evidence visibility scenario tests for authority boundary regression prevention
      → Keep backend runtime as exclusive promotion authority and frontend as display-only while expanding negative-case assertions.

## UI/UX Primitive Catalog and Abstract Function Registry

## TODO dependency map（execution order）

1. Frontend Component Event Runtime — canonical route 閉鎖済み（frontend queue / flush / localStorage fallback / `/api/component-events/append` / backend append endpoint / idempotency / frontend・backend tests）。残作業は surface expansion / hardening（non-blocking、上記参照）。
2. Visual layout builder（Issue #89）— 実装完了（mouse-driven editor / drag-drop / preview-validate-apply 接続 / draft-only apply guard + backend fail-close validation / promoted component-package palette identity projection）。

---

## UI/UX Primitive Executable Component Slice

note: representative slice (implemented context): AutoCompleteInput / SearchCombobox / CandidateConfidenceBadge / InlineEditableField / PatchPreviewPanel / ApplyConfirmDialog / FacetedFilterBar / VirtualizedDataTable / LayoutDropZone / ComponentPlacementHandle / SnapGridOverlay / StyleTokenPicker / ThemePreviewPanel / DryRunResultPanel / ValidationErrorPanel。


