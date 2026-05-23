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

- [ ] phase_vector generation implementation を行う
      → 対象ファイル: `db/sql_attention_logs_tables.sql`, `backend/runtime/AttractorResolver.cs` / 対象関数: AttractorResolver の attention evidence 更新系。理由: phase_vector 保存と evidence transform 実装が未完。次の判断点: phase_vector_json 更新タイミングを runtime emission 前後どちらに固定するか。
      → `w = l2_norm`、`x/y/z = hub-side record-count bases`、`i/j/k = axis movement amounts` の意味境界を維持し、phase movement は manifest / policy cap 由来ではないことを明示する。
      → phase_vector から自動 mutation/migration/promotion は行わない。

- [ ] statistics / EMA integration for topology projection recommendation を実装する
      → 対象ファイル: `backend/runtime/PackageResolver.cs`, `backend/runtime/EmissionBuilder.cs` / 対象関数: recommendation candidate 並び替え・投影生成。理由: EMA/履歴を候補提示に統合する実装が未完。次の判断点: EMA の window/persistence を function_parameters 由来でどこまで外部化するか。

- [ ] refresh logs.hub_current / attractor current function implementation を実装する
      → 対象ファイル: `db/sql_attention_logs_tables.sql`, `backend/runtime/AttractorResolver.cs` / 対象関数: hub_current refresh と z-score 算出更新。理由: logs.hub_current 更新関数が未完。次の判断点: batch/trigger/client のどの経路で refresh を必須化するか。



- [ ] Runtime jump event implementation を実装する
      → 対象ファイル: `backend/runtime/RuntimeExecutor.cs`, `backend/runtime/EmissionBuilder.cs`, `backend/schema/Contracts.cs` / 対象関数: route missing / user_action jump event 生成と emission。理由: SSOT の jump contract は定義済みだが Runtime 実装が未完。次の判断点: jump payload の最小必須フィールド（scope/from/to/reason）を既存 emission 形式へどう統合するか。

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
