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

- [ ] `product.dynamic_support_nocode_loop` completion bundle は `product.sql_attention_recommendation_feedback_ux` の closure と M6 self-hosted admin authoring loop completion を前提とする。M6 は persistence scaffold 着手済みだが completion bundle は未完了のため partial を維持する。

## Self-hosted Admin Authoring M6 (Partial: persistence scaffold only)

- [ ] M6 self-hosted admin authoring completion bundle 群は partial へ遷移。
      → CSV/JSON import validate-preview-apply、JSONB label/value manifest、user-defined aggregation/display/binding policy、document canvas、DB/JSONB data binding、PDF export snapshot、intake snapshot + applied diff log を completion bundle 単位で管理する（implementation atom 分解はしない）。

      remaining completion bundles:
      - admin_csv_json_import_endpoint_and_upload_ui
      - manifest_validation_runtime
      - validate_preview_apply_runtime_boundary
      - canonical_apply_and_diff_log_boundary
      - document_canvas_template_runtime
      - data_binding_to_document_canvas
      - pdf_export_minimal_record

- [ ] Notion / Google Sheets / Slack / GitHub Issues / generic webhook / external REST API connector は future optional external surfaces として扱う。
      → M6 MVP completion condition には含めない。外部API connector contract SSOT 未定義は M6 MVP blocking gap にしない。
