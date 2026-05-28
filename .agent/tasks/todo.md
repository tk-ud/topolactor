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

## CI Attention Dynamic Support Nocode Loop (Partial — remaining bundles)

- [ ] `product.dynamic_support_nocode_loop` completion bundle は `product.sql_attention_recommendation_feedback_ux` の closure と M6 non-IT authoring loop completion を前提とする。M6 deferred start 維持のため、この bundle は M6 開始まで未着手のまま維持する。

## External Integration M6 (Deferred Start)

- [ ] M6 external integration bundle 群は `not_started` を維持する。
      → Notion / Sheets / Slack / webhook / CSV import は開始時に validate-preview-apply boundary 単位で roadmap/TODO を起票し、implementation atom ではなく completion bundle で管理する。

## SQL Attention Recommendation Feedback UX

- [ ] SQLA-6 completion bundleのproduction seed境界を閉じる（`sql_attention_topology_projection/default_policy` を seed に追加し、runtime explicit failure/missing policy 境界テストを更新）。
      → 対象: db/seed_empty.sql, seed関連テスト, docs/system-roadmap.yaml の production_ready 判定根拠。
