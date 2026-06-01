/** Static copy for /admin help panels — aligned with docs/registrar-admin-ui-specification.md */

import {
  UX_DATA_SHAPE,
  UX_IMPORT_SETTINGS,
  UX_IMPORT_SETTINGS_PAGE,
  UX_RUNTIME_CHECK,
  UX_UI_BUILDER,
} from "./adminUxTerms.ts";

export type AdminGuide = {
  title: string;
  purpose: string;
  howToSteps: string[];
  prerequisites?: string[];
  inputs: string[];
  actions: string[];
  outputs: string[];
  nextSteps?: string[];
  boundaryNotes?: string[];
  errorGuide?: string[];
  caution?: string;
};

export const ADMIN_INDEX_GUIDE: AdminGuide = {
  title: "管理 UI（入口）",
  purpose:
    `${UX_IMPORT_SETTINGS}の作成 → データ取り込み → 画面準備 → ${UX_RUNTIME_CHECK}まで進める管理デモの入口です。直接 DB を編集する画面ではありません。`,
  prerequisites: [
    "/auth でログイン（管理画面利用の前提）",
    "DB 永続化する場合: infra 起動と DATABASE_URL / DEMO_BACKEND_URL の設定",
    `推奨順: ${UX_IMPORT_SETTINGS} → インポート → ${UX_UI_BUILDER} → ${UX_RUNTIME_CHECK}`,
  ],
  howToSteps: [
    `上の「作業の流れ」に沿って進める（まず${UX_IMPORT_SETTINGS}、次にインポート）`,
    `/admin/manifests で取り込み・表示・実行先の定義（${UX_IMPORT_SETTINGS}）を作成する`,
    "/admin/import で CSV/JSON をプレビューしてから取り込む",
    "/admin/ui-builder で部品登録・レイアウトを準備し、保存反映する",
    "/admin/runtime で登録済み設定の動作を確認する",
    "（任意）/admin/seed — デモ用 seed.json、/admin/contents — コンテンツ登録、トークン辞書は別画面",
  ],
  inputs: [
    "各画面で説明される登録対象（取り込み設定 / データの形 / シード / 部品 / レイアウト / スタイル設定 など）",
    "操作前に /auth でログイン（DB 永続化が必要な画面）",
  ],
  actions: [
    "下のリンクから目的の画面へ移動し、画面内の「プレビュー → 検証 → 保存反映 / 有効化 / 取り込み」の順で確認する",
  ],
  outputs: [
    "成功時: backend が検証したうえで DB（canonical topology）または /storage に反映",
    "失敗時: 画面のエラーパネルに code / message — silent fallback はしません",
  ],
  nextSteps: [
    `推奨順: ログイン → ${UX_IMPORT_SETTINGS} → インポート → ${UX_UI_BUILDER} → ${UX_RUNTIME_CHECK}`,
    "書き込み操作の前に、各画面のプレビューまたは検証を必ず通す",
  ],
  boundaryNotes: [
    "Frontend = projection / intent submission。意味判断・永続化の正本は DB + backend runtime",
    "Preview は read-only。Apply / Promote / Import は明示操作のみ",
  ],
};

export const ADMIN_IMPORT_GUIDE: AdminGuide = {
  title: "インポート（CSV/JSON）",
  purpose:
    `CSV/JSON ファイルを、選択した${UX_IMPORT_SETTINGS}と${UX_DATA_SHAPE}に沿って取り込む画面です。`,
  prerequisites: [
    "/auth でログイン済みであること",
    `先に /admin/manifests で${UX_IMPORT_SETTINGS}を作成・有効化していること`,
    `取り込み用の${UX_DATA_SHAPE}が登録されていること（未登録なら${UX_IMPORT_SETTINGS_PAGE}で整える）`,
  ],
  howToSteps: [
    `「1. ${UX_IMPORT_SETTINGS}と${UX_DATA_SHAPE}を選択」で、取り込みルールと各行の形を選ぶ`,
    "「2. CSV または JSON ファイルをアップロード」でファイルを選ぶ（拡張子で csv/json を自動判定）",
    "「3. プレビュー（バリデート）」を押す — backend が各行を検証し、DB にはまだ書かない",
    "プレビュー表で invalid 行があれば validationErrors を直し、ファイルを差し替えて再プレビュー",
    "validCount > 0 を確認したら「4. 適用」を押す — snapshotId 経由で valid レコードのみ DB 反映",
    "applyLogId が表示されたら /admin/runtime または /demo で取り込み結果を確認する",
  ],
  inputs: [
    `${UX_IMPORT_SETTINGS} — 何をどこへ取り込むかのルール（登録済み一覧から選択）`,
    `${UX_DATA_SHAPE} — CSV/JSON の各行に必要な項目の定義`,
    "CSV または JSON ファイル — 1 行または 1 オブジェクト = 1 レコード想定",
  ],
  actions: [
    "プレビュー（バリデート）— backend で manifest+schema に照合。DB には書かない",
    "適用 — preview の snapshotId の valid レコードのみ DB へ明示反映",
  ],
  outputs: [
    "プレビュー成功: snapshotId、valid/invalid 件数、行ごとの validationErrors",
    "適用成功: applyLogId",
  ],
  nextSteps: [
    "UI 投影が必要なら /admin/ui-builder へ",
    "推薦トークンは /admin/context-token-registry へ",
  ],
  boundaryNotes: [
    "Frontend はファイル送信と結果表示のみ。検証・永続化は backend",
  ],
  errorGuide: [
    "invalid 行あり → 表のエラー列を修正して再プレビュー。validCount=0 では適用不可",
    "backend 未接続 → DEMO_BACKEND_URL・DATABASE_URL・JWT",
    "missing ref → manifest/schema の存在とフィールド名の一致を確認",
  ],
  caution: "適用は DB へ書き込みます。プレビューで valid 件数と内容を必ず確認してください。",
};

export const ADMIN_SEED_GUIDE: AdminGuide = {
  title: "シード（/storage/seed.json）",
  purpose:
    "デモ用のランタイム宣言候補を /storage/seed.json として編集し、検証後に canonical DB 側へ import する画面です。",
  prerequisites: [
    "/auth 済み（import 時に backend dispatch が必要）",
    "seed JSON の version と runtimes 配列が契約どおりであること",
  ],
  howToSteps: [
    "エディターに seed JSON を入力するか、「/storage からロード」で既存 seed.json を読み込む",
    "（任意）「/storage に保存」でディスク上の seed.json だけ更新する — まだ DB は変わらない",
    "「バリデート」で構文・必須フィールドを確認する。errors が出たら JSON を修正",
    "「プレビュー」で宣言される runtime 一覧（name/target/layer/action）を read-only 確認",
    "内容に問題がなければ「インポート」を押す — backend が DB authority へ反映",
    "成功メッセージ（importedCount）のあと /admin/runtime または /demo で runtime が解決されるか確認",
  ],
  inputs: [
    "Seed JSON（version + runtimes 配列）",
    "各 runtime: name, target, layer, action など",
  ],
  actions: [
    "保存 / ロード — /storage のみ（DB 不変）",
    "バリデート / プレビュー — 検証・一覧表示（DB 不変）",
    "インポート — DB へ反映",
  ],
  outputs: [
    "validate → valid または errors[]",
    "preview → runtime テーブル",
    "import → importedCount",
  ],
  nextSteps: [
    "大量レコードは /admin/import を検討",
    "UI 登録は /admin/ui-builder",
  ],
  boundaryNotes: [
    "seed.json は候補ファイル。正本は import 成功後の DB",
  ],
  errorGuide: [
    "ファイルなし → 先に保存するか backend storage を確認",
    "validation 失敗 → errors[].code に従い JSON 修正",
  ],
  caution: "インポートは DB へ runtime 宣言を書き込みます。バリデートとプレビューを省略しないでください。",
};

export const ADMIN_UI_BUILDER_GUIDE: AdminGuide = {
  title: "UI ビルダー（コンポーネント & レイアウト）",
  purpose:
    "プリミティブコンポーネントを UI topology DB に登録し、layout patch でグリッド配置を draft → validate → apply する画面です。",
  prerequisites: [
    "/auth 済み、UI topology DB 接続可能",
    "componentKey / sourcePath は frontend/components の実在パスと一致させる",
  ],
  howToSteps: [
    "タブ「バケット」: componentKey・sourcePath・componentKind を入力し「バケットに登録」→「バケット再ロード」で一覧確認",
    "バケット行を選択し routeKey（既存または新規）を指定 →「生成」で packaging →「プロモート」で DB に componentId/packageId/layoutId/wiringId を発行",
    "タブ「レイアウト」: 「プロモート済みパレット」をロードし、キャンバスへドラッグ（ドラフトのみ＝オレンジは apply 不可）",
    "layoutId / routeKey を選び、必要なら topology layout class を指定",
    "「1. プレビュー」で解決結果のみ確認（DB 不変）→「2. バリデート」で ref 整合 → 問題なければ「3. 適用」",
    "ドラフトのみノードが残っていると適用はブロック — プロモートするかキャンバスから削除",
    "（任意）タブ「CI」で break_boundary ガイダンスを確認してから promote/apply",
  ],
  inputs: [
    "バケット候補、routeKey、layoutId、グリッド上のノード配置",
  ],
  actions: [
    "bucket → generate → promote",
    "layout patch: preview → validate → apply",
  ],
  outputs: [
    "プロモート: 発行 ID と promoted パレット",
    "apply: UI topology DB に layout 永続",
  ],
  nextSteps: [
    "/admin/runtime または /demo で投影確認",
    "推薦連携は Context Token Registry",
  ],
  boundaryNotes: [
    "code-only = drift。promote 完了まで topology エンティティではない",
    "直接 DB 編集は不可",
  ],
  errorGuide: [
    "パレット空 → バケット登録とプロモートを先に",
    "apply blocked → draft-only ノードを解消",
  ],
  caution: "プロモートと layout の「適用」は DB 書き込みです。",
};

export const ADMIN_CONTEXT_TOKEN_GUIDE: AdminGuide = {
  title: "推薦トークン辞書",
  purpose:
    "推薦エンジンが参照するトークン辞書を管理します。ここに登録されたトークンIDの存在が推薦の観測シグナルになります。",
  prerequisites: [
    "/auth でログイン済みであること",
  ],
  howToSteps: [
    "ページ読み込みで有効なトークン一覧が表示される（空ならこれから追加）",
    "「新規トークン追加」: ラベル（必須）、グループ（任意）、値を -1.0〜1.0 で入力",
    "「トークン追加」を押す — サーバーで検証し登録、一覧に行が増える",
    "不要になったトークンは行の「非推奨にする」— データは保持され、推薦対象から外れます",
    "新規登録前に「登録前重複チェック」でID重複がないか確認することを推奨",
  ],
  inputs: [
    "ラベル（必須）、グループ（任意）、値 -1.0〜1.0",
  ],
  actions: [
    "トークン追加",
    "非推奨にする",
  ],
  outputs: [
    "追加 → 一覧に反映",
    "非推奨 → 一覧で非推奨表示",
  ],
  nextSteps: [
    "/admin/registry-vector-validate（登録前重複チェック）",
  ],
  boundaryNotes: [
    "登録・変更はサーバー側で管理されます",
  ],
  errorGuide: [
    "サーバー未接続 → 環境設定を確認してください",
    "ログインが必要 → /auth でログインしてください",
    "値の範囲外 → -1.0〜1.0 の値を入力してください",
  ],
};

export const ADMIN_RUNTIME_GUIDE: AdminGuide = {
  title: "ランタイム検証（dispatch）",
  purpose:
    "Registrar 登録 UI とは別の、promoted topology に対する user operation dispatch の検証面です。operationType=admin ではなく通常 runtime 経路（POST /api/dispatch）で emission を投影します。本番運用コンソールではありません。",
  prerequisites: [
    "/auth 済み（JWT が sessionStorage にあること）",
    "DEMO_BACKEND_URL 設定済み",
    "検証したい attractor / 操作が DB 上に promote 済みであること（未登録なら先に /admin の登録画面）",
  ],
  howToSteps: [
    "下のシナリオカードから観測したい操作 preset を選ぶ（例: default entity search）",
    "必要なら Advanced で target / layer / action を調整（開発者向け）",
    "「選択した操作を実行」で POST /api/dispatch → backend RuntimeExecutor → emission 投影",
    "結果要約で success / エラーを確認し、Debug で raw JSON を見る",
    "失敗時は AUTH_TOKEN_MISSING・ATTRACTOR_RESOLVE_FAILED 等の code を確認し、登録不足なら /admin へ戻る",
    "demo 専用 preset は /demo を使用（本画面は汎用・default 系）",
  ],
  inputs: [
    "preset または Advanced の operation vector（target, layer, action, payload）",
    "Authorization: Bearer JWT",
  ],
  actions: [
    "シナリオ選択 → 実行（user dispatch）",
    "（任意）SQL Attention パネル — runtime dispatch とは別経路の投影確認",
  ],
  outputs: [
    "Emission（DB 由来データの投影）と validation errors",
    "silent fallback なし — 失敗は明示エラー",
  ],
  nextSteps: [
    "登録が足りない → /admin/import, /admin/seed, /admin/ui-builder",
    "demo トポロジ → /demo",
    "接続問題 → /runtime-status",
  ],
  boundaryNotes: [
    "Registrar admin（import/seed/ui-builder）は登録意図のみ — runtime を直接実行しない（SSOT）",
    "本画面は登録後の検証用。/ は入口のみ、dispatch はここに集約",
    "Frontend は projection のみ。topology 意味の正本は DB + backend",
  ],
  errorGuide: [
    "AUTH_TOKEN_MISSING → /auth",
    "ATTRACTOR_RESOLVE_FAILED / missing ref → 先に seed/import で topology 登録",
    "backend 未接続 → DEMO_BACKEND_URL・infra",
  ],
};

export const ADMIN_REGISTRY_VECTOR_GUIDE: AdminGuide = {
  title: "登録前重複チェック",
  purpose:
    "登録する予定のIDが既存の登録と重複・類似していないか事前に確認します（データベースへの書き込みはしません）。",
  prerequisites: [
    "/auth でログイン済みであること",
    "確認したいIDのリストを用意",
  ],
  howToSteps: [
    "確認対象のテーブル名を入力（例: relation_registry）",
    "確認するIDを改行・カンマ・スペース区切りで貼り付け",
    "「重複チェックを実行」を押す",
    "結果を確認 — 問題あり（ブロッキング）の場合は類似登録の一覧で既存IDを確認する",
    "重複・類似が見つかった場合はIDを修正・統合して再確認",
    "問題なければ登録画面（インポート・画面づくり・トークン追加等）へ進む",
  ],
  inputs: [
    "テーブル名、確認するID（改行・カンマ区切り）",
  ],
  actions: [
    "重複チェック実行（確認のみ）",
  ],
  outputs: [
    "確認結果、類似する既存登録の一覧",
  ],
  nextSteps: [
    "問題あり → ID修正後に再確認",
    "問題なし → 登録画面へ",
  ],
  boundaryNotes: [
    "データベースへの書き込みはしません",
  ],
  errorGuide: [
    "重複 → 類似する既存登録を参照してIDを見直してください",
    "無効なID → 有効なIDを入力してください",
  ],
};

export const ADMIN_CONTENTS_GUIDE: AdminGuide = {
  title: "コンテンツ（topology content 管理）",
  purpose:
    "hub / entity / relation の browse、entity draft 登録、ref 検証、preview、explicit promote を行う Registrar 境界画面です。direct DB editor ではありません。",
  prerequisites: [
    "/auth 済み、DATABASE_URL / DEMO_BACKEND_URL 設定済み",
    "seed に content_bundle 管理用 admin route（layer=content_bundle）が active 登録されていること",
    "demo データ利用時は db/demo_seed.sql 適用済みであること",
  ],
  howToSteps: [
    "hub / entity / relation / hub_relation タブ検索で detail を確認",
    "Entity Draft Editor で hub / relation / state を selector から選び label を入力",
    "ドラフト作成 → 更新（任意）→ Validate → Preview（read-only）→ Promote の順で explicit 操作",
    "Advanced / Debug でのみ entityJsonb raw edit や UUID override が可能",
    "promote 成功後 readback で persisted entity を確認",
  ],
  inputs: [
    "keyword / kind / state filter（browse）",
    "hub selector / relation checkbox / state selector / label（draft）",
    "Advanced: entityJsonb JSON（debug のみ）",
  ],
  actions: [
    "content_bundle:search / list_hubs / list_entities / list_relations / list_hub_relations / get_entity / get_hub / get_relation",
    "content_bundle:create_entity_draft / update_entity_draft / validate_draft / preview_draft / promote_draft",
  ],
  outputs: [
    "browse 結果（hub / entity / relation list items）",
    "entity detail / draft detail",
    "structured validation issues（blocking / warning）",
    "preview summary + canPromote",
    "promote 結果 + created entity_id + readback",
  ],
  nextSteps: [
    "/admin/import",
    "/admin/ui-builder",
    "/admin/registry-vector-validate",
  ],
  boundaryNotes: [
    "direct DB 編集は行いません — backend validation / persistence boundary 経由のみ",
    "draft は runtime visible active state ではありません",
    "promote は明示操作 — 自動 promote なし",
    "frontend は topology 意味判断をしません",
  ],
};

export const ADMIN_MANIFESTS_GUIDE: AdminGuide = {
  title: `${UX_IMPORT_SETTINGS}（取り込み・表示・実行先）`,
  purpose:
    `CSV/JSON の取り込み方、画面への反映、実行先を登録・有効化する画面です。データの流れと公開・案内の設定をタブで分けて編集します。内容の正しさはサーバー側で確認します。`,
  prerequisites: [
    "/auth でログイン済みであること",
    "デモ環境では DATABASE_URL / DEMO_BACKEND_URL が設定されていること",
  ],
  howToSteps: [
    "「データの流れ」タブで登録済みの設定一覧を確認する",
    "行を選び、取り込み先・実行のつながりを確認・編集する",
    "下書きの場合は内容を直して「下書きを保存」",
    "新規は「新規下書き」→ 必要項目を候補から選択 → 「下書きを作成」",
    "「内容を確認」でサーバー検証 — 不足や重複はエラー表示（黙って通過しない）",
    "問題なければ下書きのみ「有効化」— 自動では有効になりません",
    "「公開・案内」タブで、案内文やキャンペーン情報を編集する（必要な場合）",
    "公開・案内を付ける場合: 先にデータの流れで下書き作成 → 公開・案内タブで対象を指定",
    "取り込みへ進む前に /admin/import で設定を選べることを確認する",
  ],
  inputs: [
    "取り込み・実行のつながり: 役割、対象、層、操作、実行先（候補から選択）",
    "公開・案内（任意）: 案内文、版ラベル、有効化ポリシーなど",
    "上級者向け設定（通常は不要）: 出力の追加定義",
  ],
  actions: [
    "一覧 / 詳細表示 / 内容確認 / 下書き作成 / 下書き更新 / 有効化 / 利用停止",
    "公開・案内: 一覧 / 詳細 / 確認 / 下書き更新",
  ],
  outputs: [
    "一覧・詳細・検証結果",
    "有効化/利用停止は結果コードで通知",
  ],
  nextSteps: [
    "/admin/runtime で動作を確認",
    "CSV/JSON 取り込みは /admin/import",
  ],
  boundaryNotes: [
    "Frontend は操作送信のみ — 内容の正しさはサーバー側で検証",
    "有効化の正本は DB + backend — frontend は結果を表示するのみ",
    "silent fallback 禁止 — バックエンド未接続は明示表示",
  ],
  caution: "有効化/利用停止は DB の状態を変更します。",
};

/** Index page: per-route cards with short how-to */
export const ADMIN_ROUTE_CARDS: {
  href: string;
  label: string;
  purpose: string;
  relation: string;
  howToSummary: string[];
  caution?: string;
}[] = [
  {
    href: "/admin/manifests",
    label: UX_IMPORT_SETTINGS,
    purpose: "取り込み・表示・実行先の定義を作成する（インポートの前提）",
    relation: "推奨フロー Step 2",
    howToSummary: [
      "一覧で既存の設定を確認",
      "新規下書き → 内容を候補から選択",
      "内容確認 → 有効化",
    ],
    caution: "有効化で DB の状態が変わります",
  },
  {
    href: "/admin/import",
    label: "インポート",
    purpose: "CSV/JSON をプレビューしてから取り込む",
    relation: `推奨フロー Step 3 — ${UX_IMPORT_SETTINGS}登録後`,
    howToSummary: [
      `${UX_IMPORT_SETTINGS}と${UX_DATA_SHAPE}を選択`,
      "ファイルを選びプレビュー",
      "問題なければ適用",
    ],
    caution: "適用で DB に書き込みます",
  },
  {
    href: "/admin/ui-builder",
    label: UX_UI_BUILDER,
    purpose: "画面部品の登録とレイアウトの準備",
    relation: "推奨フロー Step 4",
    howToSummary: [
      "部品を登録 → 配置できる状態にする",
      "レイアウトタブで配置",
      "プレビュー → 検証 → 保存反映",
    ],
    caution: "保存反映で DB に書き込みます",
  },
  {
    href: "/admin/hub-navigation",
    label: "ナビ順序設定",
    purpose: "画面間の遷移順序を設定する",
    relation: "推奨フロー Step 5",
    howToSummary: [
      "設定を選択",
      "遷移先を選んで追加",
      "▲▼で順序を調整",
    ],
  },
  {
    href: "/admin/runtime",
    label: UX_RUNTIME_CHECK,
    purpose: "登録済み設定の動作確認",
    relation: "推奨フロー Step 6",
    howToSummary: [
      "ログイン済みを確認",
      "シナリオを選んで実行",
      "結果を確認",
    ],
  },
  {
    href: "/admin/seed",
    label: "シード（任意）",
    purpose: "デモ用 seed.json の編集と取り込み",
    relation: "デモ runtime 宣言 — 通常フローとは別",
    howToSummary: [
      "JSON 編集 or ロード",
      "バリデート → プレビュー",
      "インポート",
    ],
    caution: "インポートで DB に書き込みます",
  },
  {
    href: "/admin/context-token-registry",
    label: "推薦トークン辞書",
    purpose: "推薦エンジンが参照するトークン辞書を管理する",
    relation: "推薦エンジン連携",
    howToSummary: [
      "ラベルと値を入力",
      "トークン追加",
      "不要時は非推奨",
    ],
  },
  {
    href: "/admin/registry-vector-validate",
    label: "登録前重複チェック",
    purpose: "登録前にIDが既存の登録と重複していないか確認する",
    relation: "登録前チェック",
    howToSummary: [
      "確認対象のテーブルとIDを入力",
      "重複チェックを実行",
      "問題があればIDを修正",
    ],
  },
  {
    href: "/admin/contents",
    label: "コンテンツ",
    purpose: "ページ・項目・関係を閲覧し、下書き作成から登録まで行う",
    relation: "コンテンツ管理",
    howToSummary: [
      "一覧タブでページ・項目・関係を検索・確認",
      "下書き作成 → 内容確認 → プレビュー",
      "登録で有効なコンテンツとして反映",
    ],
  },
];

export const ADMIN_HUB_NAVIGATION_GUIDE: AdminGuide = {
  title: "ナビ順序設定",
  purpose:
    "設定単位のページ遷移順序を設定します。" +
    "画面間ナビゲーションの順序を管理する画面です。",
  prerequisites: [
    "/auth でログイン済みであること",
    "先に /admin/manifests で取り込み設定が登録・有効化されていること",
    "遷移先の画面が /admin/contents で登録されていること",
  ],
  howToSteps: [
    "「設定選択」で対象の取り込み設定を選ぶ",
    "ナビ遷移が未登録なら「追加」フォームが自動表示される",
    "遷移先の画面を選んで「登録」（順序は自動で末尾に追加されます）",
    "既存エントリの「編集」でナビ遷移先を変更できる",
    "順序の変更は▲▼ボタンで行う",
    "不要なエントリは「削除」で無効化する（記録は保持）",
  ],
  inputs: [
    "設定 — 一覧から選択",
    "遷移先の画面 — 一覧から選択",
    "順序 — 末尾に自動追加（▲▼で変更可能）",
  ],
  actions: [
    "追加 — ナビ遷移を新規追加（有効）",
    "編集 — 遷移先を変更",
    "▲▼ — 順序を並び替え",
    "削除 — ナビ遷移を無効化（記録は保持）",
  ],
  outputs: [
    "成功時: ナビ遷移が登録され一覧に反映される",
    "失敗時: 競合・設定未発見などのエラーが表示される",
  ],
  boundaryNotes: [
    "Frontend は操作送信のみ。書き込みはサーバー側で管理される",
    "順序番号は設定スコープで一意です — 重複は自動回避されます",
  ],
};

/** 推奨受入フロー — admin index の "推奨受入フロー" セクションで使用 */
export type AcceptanceFlowStep = {
  step: number;
  label: string;
  href: string;
  purpose: string;
  completionSign: string;
  nextLabel?: string;
  boundaryNote?: string;
  subSteps?: { label: string; href: string }[];
};

/** 管理トップのコンパクトステッパー — admin-console-workflow-ssot.yaml canonical_workflow 準拠 */
export const ADMIN_MAIN_FLOW_STEPS: AcceptanceFlowStep[] = [
  {
    step: 1,
    label: "ログイン",
    href: "/auth",
    purpose: "管理画面利用の前提",
    completionSign: "ログイン済みであること",
    nextLabel: `${UX_IMPORT_SETTINGS}へ`,
  },
  {
    step: 2,
    label: UX_IMPORT_SETTINGS,
    href: "/admin/manifests",
    purpose: "取り込み・表示・実行先の定義を作る（DB系 / 配線系 / hub系）",
    completionSign: `有効な${UX_IMPORT_SETTINGS}が 1 件以上あること`,
    nextLabel: "データ取り込みへ",
  },
  {
    step: 3,
    label: "データ取り込み",
    href: "/admin/import",
    purpose: "CSV/JSON インポート または コンテンツ手動登録",
    completionSign: "データが DB に反映されていること",
    nextLabel: `${UX_UI_BUILDER}へ`,
    subSteps: [
      { label: "インポート", href: "/admin/import" },
      { label: "手動登録", href: "/admin/contents" },
    ],
  },
  {
    step: 4,
    label: UX_UI_BUILDER,
    href: "/admin/ui-builder",
    purpose: "画面部品とレイアウトを準備する",
    completionSign: "レイアウトの保存反映が完了していること",
    nextLabel: "ナビ順序設定へ",
  },
  {
    step: 5,
    label: "ナビ順序設定",
    href: "/admin/hub-navigation",
    purpose: "画面間の遷移順序を設定する",
    completionSign: "必要なナビ遷移が有効な状態で登録されていること",
    nextLabel: `${UX_RUNTIME_CHECK}へ`,
  },
  {
    step: 6,
    label: UX_RUNTIME_CHECK,
    href: "/admin/runtime",
    purpose: "登録済み設定が動くか確認する",
    completionSign: "実行が成功し結果が返ること",
  },
];

export const ACCEPTANCE_FLOW_STEPS: AcceptanceFlowStep[] = [
  {
    step: 1,
    label: "ログイン",
    href: "/auth",
    purpose: "管理画面を使う前に認証します。未ログインでは各 /admin/* 画面は利用できません。",
    completionSign: "ログイン済み表示。管理トップ以降の画面に進めること。",
    nextLabel: `${UX_IMPORT_SETTINGS}へ`,
  },
  {
    step: 2,
    label: UX_IMPORT_SETTINGS,
    href: "/admin/manifests",
    purpose: `取り込み・表示・実行先の定義を作成します（DB系 / 配線系 / hub系）。インポートにはこの${UX_IMPORT_SETTINGS}が先に必要です。`,
    completionSign: `有効な${UX_IMPORT_SETTINGS}が 1 件以上あり、/admin/import の選択肢に現れること。`,
    nextLabel: "データ取り込みへ",
    boundaryNote: "検証・有効化の正本は backend。frontend は入力と結果表示のみ。",
  },
  {
    step: 3,
    label: "データ取り込み",
    href: "/admin/import",
    purpose: `CSV/JSON インポート（/admin/import）または手動コンテンツ登録（/admin/contents）でデータを DB に反映します。`,
    completionSign: "データが DB に反映されていること（applyLogId または promote 完了）。",
    nextLabel: `${UX_UI_BUILDER}へ`,
    boundaryNote: "プレビュー・適用の正本は backend。frontend はファイル送信と結果表示のみ。",
    subSteps: [
      { label: "インポート", href: "/admin/import" },
      { label: "手動登録", href: "/admin/contents" },
    ],
  },
  {
    step: 4,
    label: UX_UI_BUILDER,
    href: "/admin/ui-builder",
    purpose: "部品を登録し、レイアウトを組んで保存反映します。",
    completionSign: "配置用パレットに部品が表示され、レイアウトの保存反映が完了すること。未登録のみの配置が残っていないこと。",
    nextLabel: "ナビ順序設定へ",
    boundaryNote: "topology 意味判断は backend。frontend はドラフト表示と操作送信のみ。",
  },
  {
    step: 5,
    label: "ナビ順序設定",
    href: "/admin/hub-navigation",
    purpose: "画面間の遷移順序を設定します。多画面ナビゲーションが必要な場合に実施します。",
    completionSign: "必要なナビ遷移が有効な状態で登録されていること。",
    nextLabel: `${UX_RUNTIME_CHECK}へ`,
    boundaryNote: "ナビ遷移の書き込みはサーバー側で管理します。frontend は操作送信のみ。",
  },
  {
    step: 6,
    label: UX_RUNTIME_CHECK,
    href: "/admin/runtime",
    purpose: "登録済み設定に対して操作を実行し、結果を確認します。不足があれば該当画面へ戻ります。",
    completionSign: "実行が成功し結果が返ること。登録不足エラーが解消されていること。",
    boundaryNote: "dispatch / emission の正本は backend + DB。frontend は結果の表示のみ。",
  },
];

/** 受入チェックリスト — 完了判定の正本ではなく確認観点のリスト */
export type AcceptanceCheckItem = {
  label: string;
  href: string;
  checks: string[];
};

export const ACCEPTANCE_CHECKLIST: AcceptanceCheckItem[] = [
  {
    label: `${UX_IMPORT_SETTINGS}の作成`,
    href: "/admin/manifests",
    checks: [
      `${UX_IMPORT_SETTINGS}の一覧が表示されること`,
      "下書き作成 → 内容確認 → 有効化ができること",
      `インポート画面で${UX_IMPORT_SETTINGS}を選べること`,
    ],
  },
  {
    label: "インポート（プレビュー・適用）",
    href: "/admin/import",
    checks: [
      `${UX_IMPORT_SETTINGS}と${UX_DATA_SHAPE}を選択できること`,
      "プレビューで有効件数 > 0 になること",
      "適用で applyLogId が返ること",
    ],
  },
  {
    label: "コンテンツ（任意）",
    href: "/admin/contents",
    checks: [
      "hub / entity / relation の一覧が表示されること",
      "ドラフト → 検証 → プレビュー → 登録の流れが通ること",
    ],
  },
  {
    label: "UI Builder（部品・レイアウト）",
    href: "/admin/ui-builder",
    checks: [
      "部品を登録できること",
      "配置用パレットに部品が現れること",
      "レイアウトのプレビュー・検証が通ること",
      "未登録のみの配置がなく保存反映できること",
    ],
  },
  {
    label: UX_RUNTIME_CHECK,
    href: "/admin/runtime",
    checks: [
      "シナリオを選んで実行が成功すること",
      "結果が返ること（登録不足エラーがないこと）",
    ],
  },
];
