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

## Dynamic Support Nocode Loop — manual acceptance

- [ ] `product.dynamic_support_nocode_loop` の manual acceptance / hand-debug verification を実施し、authoring guidance・SQL Attention feedback・M6 self-hosted admin authoring loop が同一UX導線として受入可能か確認する。
      → 残理由は implementation gap ではない。M6 self-hosted admin authoring loop、SQL Attention SQLA-1..5、SQL Attention live DB E2E、roadmap/test-bundles 正規化は完了済みで、M6/M7 core runtime production-ready 判定は維持する。future optional external connector surfaces は M6/M7 blocker ではない。

## Demo Stepper UX — 一般ユーザー向け体験の残課題

- [ ] `UserDemoResultCard` に推薦候補の実値を表示する。
      → `contextRouteRecommendation.nextOperations[].value` は取得できているが `toUserFacingResult` で捨てている。`UserFacingResult` に `recommendationItems?: string[]` を追加し、result card に「おすすめ候補: ...」を見せる。対象: `frontend/runtime/emissionSummary.ts`, `frontend/components/UserDemoResultCard.tsx`

- [ ] `UserDemoNextActions` を「試していない全シナリオを列挙する」形に改善する。
      → 現在の `relatedScenario()` は 1 件だけ循環提示する。currentScenarioId を除いた残り全シナリオ（最大 2 件）を並べると「次に何ができるか」が明確になる。対象: `frontend/components/UserDemoNextActions.tsx`

- [ ] 未認証時のシナリオカードにインライン誘導を追加する。
      → 現状はバナー警告のみでカードはクリック可能 → step 3 でエラー。token が null のとき各カードボタンに「ログイン後に試せます →」を副テキストで表示し、クリックで `/auth` へ誘導するか dispatch を抑制する。対象: `frontend/islands/UserDemoStepper.tsx`

- [ ] step 3 エラー時に「同じシナリオをもう一度試す」ボタンを追加する。
      → 現在は「別のシナリオを試す」のみ。ログイン後に戻ってきたユーザーが同じシナリオを再試行できるよう、エラー result のときだけ `onRetry` とは別の「再試行」ボタンを `UserDemoNextActions` に追加する。対象: `frontend/components/UserDemoNextActions.tsx`, `frontend/islands/UserDemoStepper.tsx`

- [ ] Step 1 に戻る導線を明示する。
      → StepBar は現状クリック不可で、step 3 から step 1 に戻るには「別のシナリオを試す」に気づく必要がある。StepBar の「目的を選ぶ」をクリック可能にする（step > 1 のとき `onReset` を呼ぶ）か、step 3 見出し近くに「← やり直す」ボタンを置く。対象: `frontend/islands/UserDemoStepper.tsx`

- [ ] `emission.data` のプレビューを result card に追加する。
      → entity list シナリオは `emission.data` に実エンティティデータが返るが現状カウントしか表示していない。`UserFacingResult` に `dataPreview?: Record<string, unknown>` を追加し、result card にコンパクトな JSON または key 一覧を表示する（詳細は `/demo/debug` に委ねる）。対象: `frontend/runtime/emissionSummary.ts`, `frontend/components/UserDemoResultCard.tsx`
