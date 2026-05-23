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


## SQL Attention observation runtime follow-up

- [ ] Concept SSOT に Runtime / Frontend / DB relation / SQL Attention の責務境界を短文追加する設計TODOを整理する
      → Runtime = 状態正本、Frontend = projection surface、DB relation / SQL Attention = 業務導線の decision / support surface を明記し、画面遷移・次タスク判断・業務フローを Frontend / 固定REST / if分岐 / 旧fallback に逃がさない設計境界を SSOT 追記候補として定義する。
      → 対象候補: `docs/design/runtime-orchestration-ssot.yaml`, `docs/framework-core.yaml`, `docs/framework-policy.yaml` (本文更新は本TODOでは実施しない)。

- [ ] route と attention の責務境界を SSOT / schema contract として定義する
      → `Attention = 推薦・近傍探索`, `Route = 固定導線・業務上必須遷移` を明示し、SQL Attention を固定 route の代替にしない。
      → resolver 優先順候補を `fixed route → topology enum/package recommend → SQL Attention relation recommend → explicit gap/error` として残し、業務必須遷移は route 優先で判定する。

- [ ] `hubs.relation_route` schema / SSOT contract を設計する
      → hub / relation をまたぐ固定業務導線 (例: 受付 → task生成 → inventory変動 → 請求) を Attention score で上書き不可な route として扱う contract を整理する。
      → 必須導線 / 任意導線 / 補助導線の区分、Attention が補助してよい範囲、Runtime resolver が route を優先する条件を未実装TODOとして明確化する。

- [ ] `topology.package_route` schema / SSOT contract を設計する
      → 同一 topology 内の状態遷移 (例: 受付済 → 作業中 → 完了 → 請求待ち) について、fixed package route と enum/package recommend の責務差分を定義する。
      → 推薦可能遷移と必須遷移の分離、UI projection emission 反映方針、topology 内状態束 route の schema 保持方式を設計TODOとして整理する。

- [ ] phase_vector generation implementation を行う
      → phase_vector は `logs.attention.vector_json` から始まる post-main auxiliary evidence transform として実装し、logs.attention.phase_vector_json に evidence として保存する。
      → `w = l2_norm`、`x/y/z = hub-side record-count bases`、`i/j/k = axis movement amounts` の意味境界を維持し、phase movement は manifest / policy cap 由来ではないことを明示する。
      → phase_vector から自動 mutation/migration/promotion は行わない。

- [ ] statistics / EMA integration for topology projection recommendation を実装する
      → hit hub から投影される topologys 意味空間の提示順/候補強度を統計・EMA・履歴・利用頻度で扱う recommendation basis を実装する。

- [ ] refresh logs.hub_current / attractor current function implementation を実装する
      → hub-side attractor current と axis z-score を算出・更新する function contract を実装し、phase_vector 移動距離計算に必要な母数を提供する。


## Runtime Orchestration SSOT 準拠 (SSOT: docs/design/runtime-orchestration-ssot.yaml)

SSOT参照必読:
- `docs/design/runtime-orchestration-ssot.yaml`
- `docs/framework-core.yaml`
- `docs/framework-policy.yaml`

## Frontend UI Component System (Issue #86)

- [ ] [Claude] primitive component を UI topology tensor に DB 登録し drift を解消する
      → Button / Input / Table / Card は frontend/components/ にコードのみ存在 (drift / GAP 状態)。
      → 各 component を PackageGeneratorRuntime 経由で componentId / packageId 発行 → ui_topology_tensor に DB 保存する。
      → CRUD wiring / CanDI wiring の責務境界を registrar-admin-ui-specification.md に明記する。
      → 完了条件: code-only component が 0 件になる (全て DB topology tensor に接続)
      → 対象: `db/ui_topology_tables.sql` (component 登録 surface 追加)、`docs/registrar-admin-ui-specification.md`

## Admin Visual Layout Builder (Issue #89)

- [ ] [Claude] visual layout builder の mouse 操作 UI と layout tensor DB 管理を実装する
      → LayoutBuilderSection は ui-builder.tsx に文書化済みだが UI 実装 (drag/drop) は未着手。
      → layoutId / styleTokenId / responsiveRuleId の DB schema 未追加。
      → 前提: Issue #86 component DB 登録完了後。
      → 完了条件: admin_visual_layout_builder status=implemented (docs/system-roadmap.yaml)
      → 対象: `db/ui_topology_tables.sql` (layout token schema)、`frontend/islands/` (drag/drop UI island)、`docs/registrar-admin-ui-specification.md`
