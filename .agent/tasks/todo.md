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

## UI/UX Primitive Catalog and Abstract Function Registry

## TODO dependency map（execution order）

1. Frontend Component Event Runtime — canonical route 閉鎖済み（frontend queue / flush / localStorage fallback / `/api/component-events/append` / backend append endpoint / idempotency / frontend・backend tests）。残作業は surface expansion / hardening（non-blocking、上記参照）。
2. Visual layout builder（Issue #89）— 実装完了（mouse-driven editor / drag-drop / preview-validate-apply 接続 / draft-only apply guard + backend fail-close validation）。promoted component/package palette integration（DB登録済み identity を palette source にする）は follow-up 課題として残す。

---

## UI/UX Primitive Executable Component Slice

note: representative slice (implemented context): AutoCompleteInput / SearchCombobox / CandidateConfidenceBadge / InlineEditableField / PatchPreviewPanel / ApplyConfirmDialog / FacetedFilterBar / VirtualizedDataTable / LayoutDropZone / ComponentPlacementHandle / SnapGridOverlay / StyleTokenPicker / ThemePreviewPanel / DryRunResultPanel / ValidationErrorPanel。

## Abstract Function Boundary Tests

実装・テスト完了（2026-05-27）:
- 実装ファイル: frontend/runtime/abstractFunctions.ts（31関数すべて実装済み）
- テストファイル: frontend/tests/abstractFunctionBoundary.test.ts（107テスト、全パス）
- 実装済み31関数: rank_candidate_results / explain_candidate_match / detect_duplicate_candidates / suggest_schema_promotion_candidates / import_rows_to_candidates / deduplicate_import_candidates / resolve_postal_address / resolve_address_postal / resolve_tel_candidate / validate_candidate / preview_update_patch / apply_confirmed_update / append_diff_log / build_rollback_candidate / resolve_conflict_candidate / calculate_attention_weight / calculate_rank_score / calculate_topology_distance / calculate_route_cost / validate_formula_contract / preview_layout_patch / validate_layout_constraint / apply_confirmed_layout_patch / resolve_style_token / preview_responsive_rule / validate_component_placement / dry_run_operation / validate_mutation_boundary / explain_operation_risk / check_permission_for_operation / build_confirmable_operation
- count note: タスク記載の「32関数」はSSOT実数31関数の誤記（SSOT 7カテゴリ合計31関数で整合）
- completion_condition: primitive_attached_processing_functions_are_unit_tested_as_runtime_function_boundaries → 達成

