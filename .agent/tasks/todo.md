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

boundary note: runtime/catalog/factory 境界は implemented 維持（`runtime adapter -> runtime registry -> runtime factory -> component`、`runtimeConnected:true` は factory/constructor reachable を意味）。

- [ ] representative UI/UX executable component slice を completion bundle 単位で昇格する
      → 単位は `componentKind selector -> RuntimeComponentFactory interface registration -> catalog sourcePath promotion -> runtimeConnected:true promotion -> static/type/deno による interface reachability check` を 1 bundle とする。

- [ ] UI/UX primitive catalog の実行可能昇格を段階実装する
      → 初回 representative slice は interface/thin-wrapper 中心で実施し、個別 UI behavior test 必須化ではなく catalog 昇格 invariant を主軸に進める。
      → representative names（昇格対象一覧）: AutoCompleteInput / SearchCombobox / CandidateConfidenceBadge / InlineEditableField / PatchPreviewPanel / ApplyConfirmDialog / FacetedFilterBar / VirtualizedDataTable / LayoutDropZone / ComponentPlacementHandle / SnapGridOverlay / StyleTokenPicker / ThemePreviewPanel / DryRunResultPanel / ValidationErrorPanel。

- [ ] 実装済み primitive の catalog.ts sourcePath を CATALOG_SSOT から実ファイルへ昇格する
      → 未実装 primitive は sourcePath: CATALOG_SSOT のまま維持し、実装完了時のみ昇格する。

- [ ] 実装済み primitive の runtimeConnected を false から true へ昇格する
      → factory registration + runtimePrimitiveRenderer/runtimeComponentAdapter 到達性証明後のみ true。

- [ ] component slice reachability check bundle を追加する
      → catalog -> componentKind -> factory/interface reachability を static check / TypeScript check / Deno test で検証する（個別 UI behavior test 必須化はしない）。
      → primitive に付随する abstract function / calculation / validation / external lookup / mutation boundary / patch generation / layout collision は UI component ではなく runtime function boundary として unit test 対象にする。

- [ ] 未実装 primitive は catalog_definition_only / runtimeConnected:false / registrationRequired:true のまま維持する
      → 誤昇格防止。未実装 primitive を implemented 扱いしない。

- [ ] roadmap status を実装実態に合わせて更新する
      → frontend.ui_ux_executable_component_slice を実装進捗に合わせて not_started/partial/implemented へ更新する。

## Abstract Function / Heavy Primitive Function Boundary

- [ ] [abstract-function][unit-tests] function-backed primitive が使う heavy function boundary に unit tests を追加する
      → 対象: calculation / validation / patch generation / external lookup / layout collision / mutation boundary / ranking explanation。
      → component rendering の単体テストではなく、付加処理関数の unit test として扱う。
      → promoted primitive がこれらの function を invoke する場合、UI component behavior test ではなく function boundary test を追加する。

## Admin Visual Layout Builder (Issue #89)

- [ ] [issue-89][db-runtime] layout tensor + css token reference persistence の DB/runtime保存導線を実装する
      → CSS dictionary SSOT に沿って `cssTokenRefs` / `responsiveTokenRefs`（または同等の token reference persistence）を保存境界として実装する。
      → `styleTokenId` / `responsiveRuleId` の列名・IDモデルや専用registryが必要な場合は、先にSSOT更新を完了条件に含める。

- [ ] [issue-89][ui] mouse-driven layout editor / drag-drop UI island を実装する
      → selector候補表示のみで止めず、layout editor 本体の mouse 操作 UI を実装する。

## Component Operation Event Log PostgreSQL Integration (non-blocking hardening carry-over)

- [ ] [integration-test][pr-220][non-blocking-hardening] component_operation_event_log の PostgreSQL 実体 integration test を追加する
      → backend endpoint / repository boundary tests は実装済み。残りは real PostgreSQL-backed component_operation_event_log append/idempotency verification。
