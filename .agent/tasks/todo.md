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
2. Frontend component event runtime（Issue #86 前提）
3. UI primitive component DB registration（Issue #86）
4. Visual layout builder（Issue #89, depends on #86）

---

## Runtime Recommendation Pipeline

- [x] statistics / EMA integration for topology projection recommendation を実装する
      → 依存関係: なし（単独着手可）。
      → 対象責務: recommendation candidate 並び替え・投影生成（runtime path）。
      → 対象ファイル: `backend/runtime/ContextRouteRecommendationResolver.cs`, `backend/schema/ContextRoutePolicyContracts.cs`, `backend/tests/Topolactor.Runtime.Tests/ContextRouteRecommendationResolverTests.cs`, `backend/tests/Topolactor.Runtime.Tests/ContextRoutePolicyTestFixtures.cs`。
      → 実装結果: operation候補は neighbor/baseline のみを維持。token候補のみ recommendation_blend 対象として、EMA/trend/statistics の重み・scope_limit を policy (`function_parameters`) へ外部化。EMA persistence は `context_hub_recommendation_current` を継続利用。
      → 監査役TODO: operation候補へ blend 適用が必要な場合は、`candidate_kind="operation"` の current row 設計（key/ID整合・生成/読取導線分離）を別TODOとして起票する。
      → 監査役TODO: 本番 policy row に `topology_vector_runtime.recommendation_blend` を追加し、運用重みを確定。

## Runtime Orchestration SSOT 準拠 (SSOT: docs/design/runtime-orchestration-ssot.yaml)

SSOT参照必読:
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`

## Frontend Component Event Runtime (Issue #86 前提)

- [ ] component 操作イベントを frontend runtime queue に逐次送信し、約10秒ごとにバックグラウンドでDB永続化APIへflushする
      → 依存関係: UI primitive component DB registration の前提。component catalog がAPI直書き分岐を持たないための runtime boundary。
      → 対象責務: component操作イベントの集約・順序保持・定期flush・明示失敗。
      → 対象ファイル候補: `frontend/runtime/`, `frontend/components/`, `frontend/routes/api/`, backend event-log intake endpoint / repository / schema 関連。
      → 設計方針:
        - 各componentは操作イベントを frontend runtime へ逐次emitするだけにする。
        - componentからDB保存APIを直接叩かない。
        - frontend runtime が queue / scheduler を持ち、約10秒間隔で batch flush する。
        - flush対象は componentId / packageId / layoutId / event_type / payload / actor_or_source / occurred_at / idempotency_key を含む component operation event log。
        - click / change / select / toggle / expand / collapse / submit / focus / blur / drag / drop を正規化イベントとして扱う。
        - debounce / throttle / batch_size / flush_interval_seconds / retry_policy / explicit error を runtime policy または parameter として外部化できるようにする。
        - offline / API失敗時は silent drop せず、queue保持・明示エラー・retry境界を定義する。
        - backend側は受け取ったevent batchをDBへappendし、後続の学習・推薦・監査で使える形にする。
      → local cache fallback:
        - 通常は in-memory queue を正規一次queueとして扱う。
        - scheduler停止 / flush失敗 / offline / page lifecycle interruption 時は、未送信event batchを localStorage fallback cache に退避できるようにする。
        - localStorage cache は送信成功ACKを受けた event から順に解放する。
        - idempotency_key で重複送信をDB側/endpoint側で排除できる前提にする。
        - localStorage fallback は永久保存ではなく、max_events / max_bytes / ttl / schema_version を持つ bounded cache として扱う。
        - 機密値・巨大payload・秘密情報は localStorage に保存しない。event payload はcomponent操作ログに必要な最小情報へ制限する。
      → 完了条件: component操作が frontend runtime queue に集約され、直接API分岐なしで定期batch永続化できる設計・実装・テストが揃うこと。

## Frontend UI Topology Tensor Registration (Issue #86)

- [ ] primitive component を UI topology tensor に DB 登録し drift を解消する
      → 依存関係: Frontend Component Event Runtime の責務境界確定後に着手。
      → 対象責務: component topology の永続化・責務境界明記。
      → 対象ファイル: `db/ui_topology_tables.sql`, `docs/registrar-admin-ui-specification.md`, `frontend/components/`, `frontend/routes/admin/ui-builder.tsx`, backend UI topology repository / package generator runtime 関連。
      → 詳細:
        - Button / Input / Table / Card は frontend/components/ に code-only で存在（drift / GAP）。
        - 各 component を PackageGeneratorRuntime 経由で componentId / packageId 発行 → ui_topology_tensor に DB 保存する。
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
