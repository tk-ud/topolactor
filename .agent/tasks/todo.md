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

## hub_navigation — 設計判断待ち（②〜⑤）

- [ ] **②** hub_navigation runtime 投影層の実装
      → 現状: admin 書き込み層（hub_navigation:create/update/deprecate）のみ。hub_relations.sequence_position を runtime が読んでページ遷移を制御する consumer 層が未実装。DB に sequence_position が登録されても遷移ロジックがないため「死んでいる」データになっている。
      設計判断: runtime destination `hub_navigation_runtime` を新設するか、既存 `topology_transform_runtime` の中で hub_relations を参照させるか？ どの dispatch action が sequence を読んで遷移先 hub を解決し、frontend にどの projection 形式で返すか？

- [ ] **③** sequence_position swap の UI 対応（UNIQUE 制約問題）
      → 現状: UNIQUE(topology_manifest_id, sequence_position) があるため、位置の入れ替えがアトミックにできない（SEQUENCE_CONFLICT になる）。HubNavigationAdmin.tsx の UI はこれを扱えていない。
      設計判断: a) `hub_navigation:reorder` 専用 action を backend に追加してトランザクション内で複数 UPDATE を実行、b) DB の UNIQUE を DEFERRABLE INITIALLY DEFERRED に変更、c) UI でドラッグ順序を一括送信してバッチ更新、のいずれか？

- [ ] **④** content_bundle:list_hub_relations との二重アクセスパス整理
      → 現状: `content_bundle:list_hub_relations`（全件リスト）と `hub_navigation:get_hub_relations`（manifest スコープ）が同テーブルへの別アクセスパス。ContentsAdmin の KIND_FILTERS に "hub_relation" があるが編集 UI がないため実質ブラウズのみ。
      設計判断: content_bundle 側は読み取り専用 browse 用として残すか、hub_navigation に一本化するか？ ContentsAdmin の hub_relation browse 表示も hub_navigation ページに移管するか？

- [ ] **⑤** 自己ループ防止バリデーション
      → 現状: CreateHubRelationAsync で `related_hub_id ≠ source_hub_id`（topology_manifests.hub_id 経由）のチェックがない。循環ナビゲーション（A→B→A）も登録できてしまう。
      設計判断: 単純な自己ループ（A→A）のみ禁止するか、n ホップの循環（A→B→A）まで検出して禁止するか？ 循環が業務上許容されるユースケースはあるか？

## Dynamic Support Nocode Loop — manual acceptance

- [ ] `product.dynamic_support_nocode_loop` の manual acceptance / hand-debug verification を実施し、authoring guidance・SQL Attention feedback・M6 self-hosted admin authoring loop が同一UX導線として受入可能か確認する。
      → 残理由は implementation gap ではない。M6 self-hosted admin authoring loop、SQL Attention SQLA-1..5、SQL Attention live DB E2E、roadmap/test-bundles 正規化は完了済みで、M6/M7 core runtime production-ready 判定は維持する。future optional external connector surfaces は M6/M7 blocker ではない。
