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
2. Visual layout builder（Issue #89）— Issue #86 依存クリア済み、着手可能。

---

## UI/UX Primitive Executable Component Slice

note: representative slice (implemented context): AutoCompleteInput / SearchCombobox / CandidateConfidenceBadge / InlineEditableField / PatchPreviewPanel / ApplyConfirmDialog / FacetedFilterBar / VirtualizedDataTable / LayoutDropZone / ComponentPlacementHandle / SnapGridOverlay / StyleTokenPicker / ThemePreviewPanel / DryRunResultPanel / ValidationErrorPanel。

## Abstract Function Boundary Tests

- [ ] [abstract-function][unit-tests] function-backed primitive が参照する abstract function boundary tests を追加する
      → component rendering test ではなく、function boundary unit test として実装する。
      → 優先対象:
        candidate/search:
          - rank_candidate_results
          - explain_candidate_match
          - detect_duplicate_candidates
          - suggest_schema_promotion_candidates
        import/lookup:
          - import_rows_to_candidates
          - deduplicate_import_candidates
          - resolve_postal_address
          - resolve_address_postal
          - resolve_tel_candidate
        mutation/audit:
          - validate_candidate
          - preview_update_patch
          - apply_confirmed_update
          - append_diff_log
          - build_rollback_candidate
          - resolve_conflict_candidate
        calculation/topology:
          - calculate_attention_weight
          - calculate_rank_score
          - calculate_topology_distance
          - calculate_route_cost
          - validate_formula_contract
        layout/design token:
          - preview_layout_patch
          - validate_layout_constraint
          - apply_confirmed_layout_patch
          - resolve_style_token
          - preview_responsive_rule
          - validate_component_placement
        operation safety:
          - dry_run_operation
          - validate_mutation_boundary
          - explain_operation_risk
          - check_permission_for_operation
          - build_confirmable_operation
      → 検査境界:
        SQL Attention functions は observation/candidate surface only。
        mutation系は validate → explicit confirm → apply → append log の順序を崩さない。
        external lookup は candidate surface only で、canonical write しない。

## Admin Visual Layout Builder Issue #89

- [ ] [issue-89][db-runtime] layout tensor + css token reference persistence の DB/runtime保存導線を実装する
      → 対象:
        - cssTokenRefs
        - responsiveTokenRefs
        - layout tensor persistence
        - ui_layout_registry / ui_wiring_registry / ui_topology_tensor との保存境界
      → 完了条件:
        frontend projection は token ref を渡すだけで、DB/topology judgment は backend/runtime/DB 境界で行う。

- [ ] [issue-89][ui] mouse-driven layout editor / drag-drop UI island を実装する
      → selector候補や preview component ではなく、実際の mouse operation UI を作る。
      → drag/drop mutation は direct DB write ではなく、preview / validate / explicit apply route に接続する。

## Component Operation Event Log Integration

- [ ] [integration-test][component-operation-event-log] PostgreSQL-backed append/idempotency integration test を追加する
      → backend endpoint / repository boundary tests は実装済み。
      → 残りは real PostgreSQL-backed component_operation_event_log append/idempotency verification。
