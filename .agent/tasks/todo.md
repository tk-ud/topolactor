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

## Frontend Runtime Scheduler Completion (Gap-13)

- [ ] `frontend.runtime_scheduler` completion bundle を closure する。
      → `queueClientCommand` は現状 immediate pass-through。ordering / async execution / collision control / rollback boundary を scheduler runtime module 内で完結させ、M1 completion_condition を満たす。

## Topology Repository Production Hardening (Gap-11)

- [ ] `backend.topology_repository` completion bundle を closure する。
      → `NpgsqlTopologyRepository` には demo shortcut route が残るため、production registry resolution 寄せ・恒久境界化・gap close の検証を完了する。

## Admin Contracts Finalization (Gap-12)

- [ ] `backend.admin_contracts` completion bundle を closure する。
      → `AdminContracts.cs` temporary placeholder field を validated runtime schema へ寄せ、API/runtime contract の確定と検証を完了する。

## CI Attention User Guidance Feedback Closure

- [ ] `ci_attention_guidance_fragment` completion bundle の残件を closure する。
      → dynamic support / nocode loop feedback と SQL Attention user-visible loop を結合し、user-guidance feedback loop の残留未完了境界を閉じる。

## External Integration M6 (Deferred Start)

- [ ] M6 external integration bundle 群は `not_started` を維持する。
      → Notion / Sheets / Slack / webhook / CSV import は開始時に validate-preview-apply boundary 単位で roadmap/TODO を起票し、implementation atom ではなく completion bundle で管理する。
