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
2. Visual layout builder（Issue #89）— 実装完了（mouse-driven editor / drag-drop / preview-validate-apply 接続）。

---

## UI/UX Primitive Executable Component Slice

note: representative slice (implemented context): AutoCompleteInput / SearchCombobox / CandidateConfidenceBadge / InlineEditableField / PatchPreviewPanel / ApplyConfirmDialog / FacetedFilterBar / VirtualizedDataTable / LayoutDropZone / ComponentPlacementHandle / SnapGridOverlay / StyleTokenPicker / ThemePreviewPanel / DryRunResultPanel / ValidationErrorPanel。

## Abstract Function Boundary Tests

- [ ] [abstract-function][unit-tests] function-backed primitive が参照する abstract function boundary tests を追加する
      → repo 実態分類（2026-05-27確認）:
        - 実装済み: なし（対象32関数すべて、実コード上の実装/エクスポート未検出）
        - 未実装: rank_candidate_results / explain_candidate_match / detect_duplicate_candidates / suggest_schema_promotion_candidates / import_rows_to_candidates / deduplicate_import_candidates / resolve_postal_address / resolve_address_postal / resolve_tel_candidate / validate_candidate / preview_update_patch / apply_confirmed_update / append_diff_log / build_rollback_candidate / resolve_conflict_candidate / calculate_attention_weight / calculate_rank_score / calculate_topology_distance / calculate_route_cost / validate_formula_contract / preview_layout_patch / validate_layout_constraint / apply_confirmed_layout_patch / resolve_style_token / preview_responsive_rule / validate_component_placement / dry_run_operation / validate_mutation_boundary / explain_operation_risk / check_permission_for_operation / build_confirmable_operation
        - stub: なし（同名stub/dummy/pass-through未検出）
        - scope外: なし（全32関数は本TODOの調査対象として維持）
      → 残対象・理由・対象ファイル・必要SSOT・次判断点:
        - 残対象: 上記32関数の実装追加と function boundary unit test 追加
        - 理由: 実装本体が存在しないため、境界unit testを追加できない（推測実装禁止）
        - 対象ファイル: docs/design/abstract-function-primitive-registry-ssot.yaml, docs/design/ui-ux-primitive-catalog-ssot.yaml（参照定義）
        - 必要SSOT: docs/design/abstract-function-primitive-registry-ssot.yaml, docs/design/ui-ux-primitive-catalog-ssot.yaml, docs/system-roadmap.yaml
        - 次判断点:
          1) 32関数を frontend runtime helper として実装するか、backend contract 経由で実装するかを決定
          2) 各関数の I/O 契約を SSOT で確定（入力shape・失敗shape・副作用禁止）
          3) validate → explicit confirm → apply → append log 境界を破らない test fixture を先に定義
          4) SQL Attention / external lookup / layout token の各境界を unit test の fail 条件として明文化
      → 検査境界:
        SQL Attention functions は observation/candidate surface only。
        mutation系は validate → explicit confirm → apply → append log の順序を崩さない。
        external lookup は candidate surface only で、canonical write しない。

## Component Operation Event Log Integration

- [ ] [integration-test][component-operation-event-log] PostgreSQL-backed append/idempotency integration test を追加する
      → backend endpoint / repository boundary tests は実装済み。
      → 残りは real PostgreSQL-backed component_operation_event_log append/idempotency verification。
