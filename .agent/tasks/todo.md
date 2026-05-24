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

## SSOT Wiring Audit CI 次段階計画

- [ ] sh CI は AI / Agent 用の軽量運用 guard として維持し、アプリ本体の設計逸脱監査を C# / dotnet test CI へ分離する
      → 対象責務: sh は governance 構造検査、C# CI は SSOT 配線設計の逸脱監査。

- [ ] C# / dotnet test による SSOT wiring audit CI を 4系統で設計する
      → 1) Topology Registration CI（topology/package/schema/relation/component ref と linking/binding の逸脱監査）
      → 2) Hub Registration CI（hub registration / relation route / hub_current / attention logs の逸脱監査）
      → 3) Scheduler / Runtime CI（RuntimeTimelineScheduler / ManifestDispatcher / RuntimeExecutor の canonical route 逸脱監査）
      → 4) Component Registration CI（component_definition / ComponentDataHub / ui_topology_tensor 接続の逸脱監査）

- [ ] 初期段階 C# CI は判定面を diagnostics / evidence / eligibility に限定し、DB書き込み・自動昇格を scope out として固定する
      → 対象責務: staging → active 昇格判定や registry promotion の根拠面を先に整備し、実データ変更は後続に分離する。

- [ ] 後続タスクとして C# test skeleton / SSOT YAML loader / fixtures / diagnostics evidence DTO を分離起票する
      → 対象ファイル候補: backend test project, docs/design SSOT readers, fixture surface, promotion decision evidence contract.

- [ ] C# direct semantic tests for `OutputLaneRouter.RouteAsync` / `AdminRuntime.ExecuteDataAsync` を追加する
      → 対象責務: live E2E ではなく、dispatcher / output lane / admin runtime の意味境界を fixture で直接検証する。
      → 対象ファイル候補: `backend/tests/Topolactor.Runtime.Tests/`, `backend/runtime/OutputLaneRouter.cs`, `backend/runtime/AdminRuntime.cs`。
      → 完了条件: `.agent/tests/check-unified-test-gate.sh` の NOT_COVERED から該当2関数を削除できること。


## Non-blocking cleanup / hardening carry-over

- [ ] [cleanup][pr-220] `ContextRouteRepository.cs` の XML comment / indentation cleanup
      → Approve可能な非ブロッカー残件。実装意味やSSOT completion conditionを変えない範囲で整備する。

- [ ] [surface-expansion][pr-220] `OperationPanel` 以外の主要 component / projection surface への emit-only 配線拡張
      → Approve可能な非ブロッカー残件。frontend runtime event emit 面の適用対象拡張。

- [ ] [integration-test][pr-220] `component_operation_event_log` の PostgreSQL 実体 integration test 追加
      → Approve可能な非ブロッカー残件。append-only永続化境界の実DB検証を追加する。

## TODO dependency map（execution order）

1. Frontend Component Event Runtime（Issue #86 前提）
2. UI primitive catalog bucket投入/promote（Issue #86）
3. Visual layout builder（Issue #89, depends on #86）
4. Runtime Recommendation operation blend 判断
5. seed済み recommendation_blend の本番運用値確定 / seed以外の反映面確認

---

## Runtime Recommendation Pipeline

- [ ] recommendation_blend を operation 候補にも適用するか判断し、必要なら `candidate_kind="operation"` current row 設計を起票/実装する
      → 対象責務: operation候補に対する blend 適用要否の判断と、必要時の key/ID整合・生成/読取導線分離。
      → 対象ファイル候補: `backend/runtime/ContextRouteRecommendationResolver.cs`, `backend/schema/ContextRoutePolicyContracts.cs`, recommendation current row 設計資料。

- [ ] seed/demo seed に追加済みの `topology_vector_runtime.recommendation_blend` について、本番運用値を確定し seed以外の反映面（manifest/policy row）を確認する
      → 対象責務: 追加済みpolicyの運用値確定・環境別適用面の確認・反映手順の整備。
      → 対象ファイル候補: `db/seed_empty.sql`, `db/demo_seed.sql`, policy manifest surfaces, `backend/runtime/ContextRouteRecommendationResolver.cs`。

## Frontend Component Event Runtime (Issue #86 前提)

- [ ] Frontend Component Event Runtime の残scopeを完了する（Issue #86 前提）
      → 実装済み面: frontend queue/flush/localStorage fallback、`/api/component-events/append` route、backend append endpoint、idempotency境界、frontend/backend tests は存在する。
      → 残作業: OperationPanel 以外を含む全component emit配線、component registration 依存の接続、実DB/live verification、運用hardening（retry/監視/失敗運用）を完了境界まで詰める。
      → 対象ファイル候補: `frontend/runtime/frontendScheduler.ts`, `frontend/routes/api/component-events/append.ts`, `backend/endpoint/ComponentEventAppendEndpoint.cs`, `frontend/tests/frontendComponentEventRuntime.test.ts`, `backend/tests/Topolactor.Runtime.Tests/FrontendComponentEventLogLaneTests.cs`, `frontend/components/`。
      → SSOT参照: `docs/design/runtime-orchestration-ssot.yaml`, `docs/framework-core.yaml`, `docs/framework-policy.yaml`。

## Frontend UI Primitive Catalog Bucket/Promote (Issue #86)

- [ ] primitive catalog の bucket投入/promote 残作業を完了し、UI topology tensor 登録 drift を解消する
      → 依存関係: Frontend Component Event Runtime の責務境界確定後に着手。
      → 対象責務: primitive catalog entry を bucket投入し promote で componentId/packageId を確定する運用残作業。
      → 対象ファイル: `db/ui_topology_tables.sql`, `docs/registrar-admin-ui-specification.md`, `frontend/components/`, `frontend/routes/admin/ui-builder.tsx`, backend UI topology repository / package generator runtime 関連。
      → 詳細:
        - Button / Input / Table / Card は frontend/components/ に code-only で存在（drift / GAP）。
        - 各 component の残作業は PackageGeneratorRuntime 経由 bucket投入 → promote で componentId / packageId 発行し ui_topology_tensor へ反映すること。
        - CRUD wiring / CanDI wiring の責務境界を `docs/registrar-admin-ui-specification.md` に明記する。
        - 登録対象は既存4種だけで止めず、UI primitive catalog として一般的な component を網羅する。
        - componentは操作イベントを直接APIへ送らず、Frontend Component Event Runtime へemitする。
      → 登録対象 primitive catalog:
        - action: Button, IconButton, LinkButton, ToggleButton, SplitButton。
        - form_input: Input, Textarea, Select, Combobox, Checkbox, Radio, Switch, Slider, DatePicker, TimePicker, FileInput, SearchInput。
        - navigation: Tabs, Tab, TabList, TabPanel, Breadcrumb, Menu, DropdownMenu, SidebarNav, Pagination, Stepper。
        - disclosure_structure: Accordion, Tree, TreeItem, Collapse, Details, Panel, Card, Section。
        - feedback_status: Alert, Notice, Caution, Warning, ErrorMessage, Toast, Banner, Spinner, Progress, Skeleton。
        - display_labeling: Label, Badge, StatusBadge, CountBadge, DotBadge, PillBadge, Chip, Tag, Avatar, Tooltip, EmptyState, HelpText。
        - icon_symbol: Icon, IconBadge, IconLabel, IconButton, StatusIcon, SeverityIcon, LeadingIcon, TrailingIcon, ExpandIcon, CloseIcon, DragHandleIcon。
        - data_display: Table, DataGrid, List, DescriptionList, KeyValueList, Timeline。
        - overlay: Modal, Dialog, Drawer, Popover, TooltipOverlay。
        - layout_primitive: Container, Stack, Grid, Divider, Spacer, ScrollArea。
      → 可変 component 方針:
        - ToggleButton は selected / pressed / disabled / size / tone / icon / label / group_role などの引数で状態・見た目を可変にし、状態差分ごとに別component乱立させない。
        - Tabs / Tab は orientation / activeKey / variant / size / lazyMount / tabItems / panelBinding などの引数で可変にし、タブ数や選択状態をDB topology tensor側の component parameters として表現する。
        - Badge / Icon 系は semantic_role と visual_role を分け、status/severity/count/category/navigation/action の用途差分を variant / token / argument で表現する。
      → 今回PRで完了した細分化TODO単位（親Issue #86は未完了のまま）:
        - runtime primitive renderer の interactive emit coverage を拡張（input focus/blur, table select, card click optional bind）。
        - primitive component props に runtime event hook (onFocus/onBlur/onRowClick/onClick) を追加し、direct API call なしの emit-only 経路を維持。
        - runtime alias catalog coverage を `textarea/search_input/panel/section/data_grid/list` まで拡張。
        - projection constructor / runtime adapter / primitive renderer への alias 接続を完了。
        - SSE projection runtime lane の単体導線（`projection_runtime -> renderRuntimeComponents`）テストを追加。
        - package_generator:generate の ID返却契約 unit test（tensorId/componentId/packageId/layoutId/wiringId）を追加。
        - NotBucketed explicit error mapping unit test を追加。
      → 親Issue #86 に対する残scope（implemented 判定は不可）:
        - 実DB integration で `ui_component_bucket -> package_generator -> promote` 永続化連続性を確認する。
        - catalog対象 component の bucket/generate/promote 登録を進め、componentId/packageId/layoutId/wiringId を DB topology 側へ接続する。
        - code-only component/package drift を解消、または catalog単位で明示残TODO化する。
        - alias扱いと専用primitive化の境界（textarea/list/panel/section/data_grid/search_input）を整理する。
      → runtime component adapter 方針:
        - primitiveごとに個別frontend実装を増殖させるのではなく、原則として単一の runtime component adapter が `ui_topology_tensor` / component parameter の jsonb を展開し、既存の型付き interface / props に注入する。
        - jsonb payload は `component_kind`, `semantic_role`, `visual_role`, `parameter_schema`, `default_parameters`, `event_binding` を持つDB側component definitionとして扱う。
        - frontendはjsonbから展開された props を描画し、操作イベントを Frontend Component Event Runtime へemitする projection surface であり、topology判断・SQL Attention判断・API送信判断の所有者にしない。
        - parameter_schema で表現できる variant / state / icon / label / binding 差分は component catalog entry と props 展開で吸収し、別component乱立を避ける。
      → 登録時の分類軸:
        - component_kind / semantic_role / interaction_role / data_binding_role / accessibility_role / visual_role / parameter_schema を明示する。
        - code-only 実装が残る場合は drift として残し、DB topology tensor 未接続を完了扱いしない。
        - #89 layout builder が使う前提 component surface として、layoutId ではなく componentId / packageId 登録までを #86 の完了境界にする。
      → 完了条件: code-only component が 0 件、または未登録 component が component catalog drift として明確に残TODO化されること。

## Admin Visual Layout Builder (Issue #89)

- [ ] visual layout builder の mouse 操作 UI と layout tensor DB 管理を実装する
      → 依存関係: **Issue #86 完了後に着手**（component DB registration が前提）。
      → 対象責務: layout tensor schema + drag/drop UI island 実装。
      → 対象ファイル: `db/ui_topology_tables.sql`, `frontend/islands/`, `docs/registrar-admin-ui-specification.md`。
      → 詳細:
        - LayoutBuilderSection は ui-builder.tsx に文書化済みだが UI 実装（drag/drop）は未着手。
        - `layoutId` / `styleTokenId` / `responsiveRuleId` の DB schema 未追加。
      → 完了条件: `docs/system-roadmap.yaml` の `admin_visual_layout_builder status=implemented`。
