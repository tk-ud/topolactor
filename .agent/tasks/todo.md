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

## UX改善 — 非専門管理者向け /admin/ui-builder

監査対象: `frontend/islands/UiBuilderAdmin.tsx`（3104行）
判断基準: 内部語・英語ライフサイクル語が管理者に見えている箇所を全て非専門語に置換し、
失敗時は「なぜ失敗したか」「どの部品が問題か」「どのタブで直すか」を具体的に示す。

### [UX-1] ステータスバッジ・ライフサイクル語の日本語化

- [ ] `resolveBucketStatus()` の返却ラベルを日本語化する
      → 現状 `"bucketed"/"packaging"/"promoted"/"未登録"` が混在して StatusBadge に渡される。
         翻訳マップ: `bucketed` → `"部品登録済み（準備中）"`, `packaging` → `"パッケージ化中"`,
         `promoted` → `"配置可能（登録完了）"`, `"未登録"` → `"未登録（使用不可）"`.
         対象: `UiBuilderAdmin.tsx:383-396` `resolveBucketStatus()` の返却 label 値。

- [ ] PrimitiveCatalog の `lifecycleStatus` 表示を非専門語に置換する
      → `"code_only_drift"` → `"未登録（コードのみ）"` として StatusBadge へ渡す。
         対象: `UiBuilderAdmin.tsx:1226-1229`.

- [ ] LayoutPatchSummaryPanel の英語バッジ・用語を日本語化する
      → `"valid"/"invalid"` → `"問題なし"/"エラーあり"`,
         `"blocking errors"` → `"修正が必要なエラー"`,
         `"ドラフトのみ: N 件"` → `"まだ使えない部品: N 件"`.
         対象: `UiBuilderAdmin.tsx:674-693`.

### [UX-2] テーブル列名・ボタンラベルの非専門語化

- [ ] PrimitiveCatalog の headerMap を管理者向け表記に統一する
      → 現状 `component_key/"コンポーネントキー"`, `lifecycle_status/"ライフサイクル"`,
         `runtime_connected/"ランタイム接続"`, `registration_required/"登録必須"`,
         `capability_tags/"ケイパビリティタグ"`, `source_path/"ソースパス"` が露出。
         変更案: `component_key` → `"部品名"`, `kind` → `"種別"`,
         `lifecycle_status` → `"状態"`, `runtime_connected` → `"DB連携"`,
         `registration_required` → `"登録要"`, `capability_tags` → `"機能タグ"`,
         `source_path` は列から除外して advanced 折りたたみへ移動。
         対象: `UiBuilderAdmin.tsx:1184-1196`.

- [ ] BucketSection カタログ選択テーブルの列ヘッダーを翻訳する
      → `"componentKey"` → `"部品名"`, `"kind"` → `"種別"`, `"sourcePath"` を列から除外
         （technical detail は advanced セクションへ）。
         対象: `UiBuilderAdmin.tsx:1480`.

- [ ] generate / promote ボタンのラベルから英語ライフサイクル語を除去する
      → `"生成 (bucketed → packaging)"` → `"パッケージ化する"`,
         `"プロモート (packaging → promoted)"` → `"配置可能にする"`.
         AdminActionHint の説明も「componentId 等を発行して packaging 状態へ」ではなく
         「部品をシステムに正式登録し、レイアウトで使えるようにします」に変更。
         対象: `UiBuilderAdmin.tsx:1609-1626`.

- [ ] TABS のタブヒント文言を非専門語に置換する
      → `"Step 1: bucket → generate → promote"` → `"部品を選んで登録 → 配置可能にする"`,
         `"Step 2: canvas 配置 → preview → validate → apply"` → `"キャンバスに配置 → 確認 → 保存反映"`.
         対象: `UiBuilderAdmin.tsx:3063-3068`.

- [ ] 操作成功メッセージを管理者向け表記にする
      → `"バケットアイテムを作成しました: [key]"` → `"[部品名] を登録しました"`,
         `"生成完了: [key] → packaging"` → `"[部品名] のパッケージ化が完了しました"`,
         `"プロモート完了: route=[route]"` → `"[部品名] が配置可能になりました（ルート: [route]）"`.
         対象: `UiBuilderAdmin.tsx:1353, 1407, 1431`.

### [UX-3] 失敗時修復導線の強化

- [ ] ActionableValidationErrorPanel に「タブへ移動」ボタンを追加する
      → 現状 `DRAFT_ONLY_NODES` エラーの suggestion が
         「「バケット管理」タブで対象コンポーネントをプロモートしてください」
         というテキストのみで、ボタンがない。
         `ActionableValidationErrorPanel` に `onNavigate?: (tab: TabId) => void` を追加し、
         `DRAFT_ONLY_NODES` → `onNavigate("bucket")` ボタン「→ 部品登録タブへ移動」,
         `CSS_TOKEN_INVALID` → `onNavigate("css")` ボタン「→ CSS設定タブへ移動」を表示。
         対象: `UiBuilderAdmin.tsx:536-575`（`ActionableValidationErrorPanel`）および
         呼び出し元 `UiBuilderAdmin.tsx:3041-3046`。
         `TabId` は `UiBuilderFlowStepper.tsx` から import 可能。

- [ ] BucketSection の generate / promote 失敗時に ActionableValidationErrorPanel を使う
      → 現状 `handleGenerate` / `handlePromote` は失敗時に `ValidationErrorPanel`
         （生エラーリスト）のみ表示し、原因・修正方法・タブ導線がない。
         `BUCKET_CREATE_FAILED` / `GENERATE_FAILED` / `PROMOTE_FAILED` コードを
         `ERROR_CODE_FIX` マップに追加し、`ActionableValidationErrorPanel` で表示する。
         追加すべき cause/suggestion 例:
           `BUCKET_CREATE_FAILED`: 原因「部品の登録に失敗しました」, 提案「すでに登録済みでないか確認してください」
           `GENERATE_FAILED`: 原因「パッケージ化に失敗しました」, 提案「バックエンド接続を確認し、ルートキーが正しいか再確認してください」
           `PROMOTE_FAILED`: 原因「配置可能化に失敗しました」, 提案「先にパッケージ化を完了してから実行してください」
         対象: `UiBuilderAdmin.tsx:307-333`（ERROR_CODE_FIX）および
         `UiBuilderAdmin.tsx:1357-1358, 1411, 1436-1437`（setErrors 呼び出し箇所）。

- [ ] apply ブロック時のエラー表示を強化する — 部品名リスト + タブ移動ボタン
      → 現状 `callLayoutPatch("apply")` のドラフトノード検出時 (line 2527-2537) は
         `setPatchErrors(draftOnlyNodes.map(...))` でエラーをセットするが、
         "どの部品が問題か" が `friendlyComponentLabel(n.componentKey)` で表示される一方、
         「バケット管理タブへ移動」ボタンが存在しない。
         このエラーブロックのメッセージを `"まだ使えない部品が X 件あります — 先に登録してください"` に変更し、
         `ActionableValidationErrorPanel` の `onNavigate` 経由でタブ移動ボタンを表示する。
         対象: `UiBuilderAdmin.tsx:2526-2537`.

- [ ] ApplyReadinessPanel の各チェック失敗項目に「修正する」ボタンを追加する
      → 現状 `draftOnlyCount > 0` の行は
         `{draftOnlyCount} 件 — バケット → プロモートを先に完了してください（適用はブロック）`
         というテキストのみ。ここに `onNavigate: (tab) => void` prop を受け取り、
         チェックが ✗ のとき `<button onClick={() => onNavigate("bucket")}>部品登録タブで修正する →</button>`
         を表示する。
         同様に `canPatch === false` → `onNavigate("bucket")` ボタン「ルートが未選択 — 部品登録タブで確認する」。
         対象: `UiBuilderAdmin.tsx:598-671`（`ApplyReadinessPanel`）および
         呼び出し元 `UiBuilderAdmin.tsx:2999-3006`（`setActiveTab` を prop として渡す）。

- [ ] LifecycleStepIndicator の applied_fail 時に修正アクションリンクを表示する
      → 現状 ✗ と赤表示のみで次のアクションが示されていない。
         `phase === "applied_fail"` 時に `LifecycleStepIndicator` の下に
         `<p role="alert">「エラー — 修正方法」を確認してください。まだ使えない部品がある場合は部品登録タブへ戻ってください。</p>`
         を表示する。対象: `UiBuilderAdmin.tsx:473-525`.

### [UX-4] 技術説明文の非専門語化・非表示化

- [ ] LayoutBuilderSection の「投影サーフェス境界」バナーを除去または折りたたむ
      → `UiBuilderAdmin.tsx:2850-2853` の `alert-warn` バナー
         「投影サーフェス境界: フロントエンドはドラフト状態・visual preview・intent 送信のみ担当…」
         は開発者向け注記で管理者 UX を阻害する。
         `<details>` 折りたたみ「技術情報」または完全削除に変更する。

- [ ] CssTokenSelectorSection の説明文から内部パス・技術キーを除去する
      → `UiBuilderAdmin.tsx:1674-1677` の説明
         「セレクター候補は `docs/design/css-dictionary-ssot.yaml` 派生…`cssTokenRefs`…」を
         「見た目の設定（色・余白・フォント）を選択できます。選んだ設定はレイアウト保存時に適用されます。」
         に置換する。

- [ ] PrimitiveCatalog の説明文を非専門語に変更する
      → `UiBuilderAdmin.tsx:1200-1204` の
         「UI topology DB に登録（パッケージ生成経由）されて初めてトポロジーテンソルエンティティになります。
           コードのみのコンポーネントは drift/GAP として扱われます。」を
         「ここに表示されている部品をレイアウトで使うには、バケット管理タブで「登録済み」状態にする必要があります。」
         に置換する。

- [ ] AdvancedManualOverride のタイトル既定値を日本語化する
      → title prop デフォルト `"manual override / unsafe / advanced"` → `"上級者向け設定（通常は不要）"`.
         対象: `UiBuilderAdmin.tsx:577-595`.

### [UX-5] パレット・キャンバスの未登録部品表示を改善する

- [ ] LayoutPalette の draft-only 表示ラベルを改善する
      → `UiBuilderAdmin.tsx:2317-2319` の `"⚠未登録"` スパンを
         `"⚠ まだ使えません"` に変更し、title 属性に
         `"この部品はまだ登録されていません。部品登録タブでパッケージ化してから使用してください。"`
         を追加する。

- [ ] VisualLayoutNode の draft-only メッセージにタブ移動ヒントを加える
      → `UiBuilderAdmin.tsx:1811-1813` の
         `"⚠ 未登録 — 適用ブロック"` を
         `"⚠ まだ使えない部品 — 適用不可"` に変更し、
         title 属性に `"部品登録タブでプロモートしてから apply してください"` を追加する。

## Dynamic Support Nocode Loop — manual acceptance

- [ ] `product.dynamic_support_nocode_loop` の manual acceptance / hand-debug verification を実施し、authoring guidance・SQL Attention feedback・M6 self-hosted admin authoring loop が同一UX導線として受入可能か確認する。
      → 残理由は implementation gap ではない。M6 self-hosted admin authoring loop、SQL Attention SQLA-1..5、SQL Attention live DB E2E、roadmap/test-bundles 正規化は完了済みで、M6/M7 core runtime production-ready 判定は維持する。future optional external connector surfaces は M6/M7 blocker ではない。
