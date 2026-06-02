/** Static copy for /admin help panels — aligned with docs/registrar-admin-ui-specification.md */

import {
  UX_CONTENTS,
  UX_CONTENTS_PAGE,
  UX_DATA_SHAPE,
  UX_HUB_MANIFESTS,
  UX_HUB_MANIFESTS_PAGE,
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
    `${UX_CONTENTS} → ${UX_UI_BUILDER} → ${UX_HUB_MANIFESTS} の canonical admin workflow を案内します。直接 DB を編集する画面ではありません。`,
  prerequisites: [
    "/auth でログイン（管理画面利用の前提）",
    "DB 永続化する場合: infra 起動と DATABASE_URL / DEMO_BACKEND_URL の設定",
  ],
  howToSteps: [
    `/admin/contents で新規 manifest の下書きを作成し、内容確認・プレビュー・有効化を行う`,
    "/admin/ui-builder で部品登録・レイアウトを準備し、保存反映する",
    `/admin/manifests で既存 manifest の relation / hub 操作を行う`,
    "動作確認が必要な場合は canonical admin workflow 外の /demo を利用する",
  ],
  inputs: [
    "各 canonical admin 画面で説明される登録対象",
    "操作前に /auth でログイン",
  ],
  actions: [
    "canonical route registry に沿って新規 manifest 作成、UI 準備、既存 manifest relation / hub 操作を進める",
  ],
  outputs: [
    "成功時: backend が検証したうえで canonical topology に反映",
    "失敗時: 画面のエラーパネルに code / message — silent fallback はしません",
  ],
  boundaryNotes: [
    "Frontend = projection / intent submission。意味判断・永続化の正本は DB + backend runtime",
    "Preview は read-only。Apply / Promote は明示操作のみ",
  ],
};

export const ADMIN_IMPORT_GUIDE: AdminGuide = {
  title: "Legacy / Debug: インポート（CSV/JSON）",
  purpose:
    `CSV/JSON ファイルを、${UX_CONTENTS}で定義した${UX_DATA_SHAPE}に沿って取り込む画面です。`,
  prerequisites: [
    "/auth でログイン済みであること",
    `先に /admin/contents で画面の内容と${UX_DATA_SHAPE}を定義していること`,
    "インポート対象の画面が一覧に出ない場合: topology_manifest 投影ギャップの可能性あり（.agent/tasks/todo.md 参照）",
  ],
  howToSteps: [
    `「1. 画面と${UX_DATA_SHAPE}を選択」で、取り込み先と各行の形を選ぶ`,
    "「2. CSV または JSON ファイルをアップロード」でファイルを選ぶ（拡張子で csv/json を自動判定）",
    "「3. プレビュー（バリデート）」を押す — backend が各行を検証し、DB にはまだ書かない",
    "プレビュー表で invalid 行があれば validationErrors を直し、ファイルを差し替えて再プレビュー",
    "validCount > 0 を確認したら「4. 適用」を押す — snapshotId 経由で valid レコードのみ DB 反映",
    "applyLogId が表示されたら /demo で取り込み結果を確認する",
  ],
  inputs: [
    `画面 — ${UX_CONTENTS}で定義した取り込み先`,
    `${UX_DATA_SHAPE} — CSV/JSON の各行に必要な項目の定義`,
    "CSV または JSON ファイル — 1 行または 1 オブジェクト = 1 レコード想定",
  ],
  actions: [
    "プレビュー（バリデート）— backend で画面+schema に照合。DB には書かない",
    "適用 — preview の snapshotId の valid レコードのみ DB へ明示反映",
  ],
  outputs: [
    "プレビュー成功: snapshotId、valid/invalid 件数、行ごとの validationErrors",
    "適用成功: applyLogId",
  ],
  nextSteps: [
    "UI 投影が必要なら /admin/ui-builder へ",
    "推薦トークンは /dev/admin/context-token-registry へ",
  ],
  boundaryNotes: [
    "Frontend はファイル送信と結果表示のみ。検証・永続化は backend",
  ],
  errorGuide: [
    "invalid 行あり → 表のエラー列を修正して再プレビュー。validCount=0 では適用不可",
    "backend 未接続 → DEMO_BACKEND_URL・DATABASE_URL・JWT",
    "missing ref → 画面/schema の存在とフィールド名の一致を確認",
    "画面一覧が空 → /admin/manifests で画面を作成。有効化済みでも一覧が空なら canonical 投影ギャップ（.agent/tasks/todo.md 参照）",
  ],
  caution:
    "適用は DB へ書き込みます。プレビューで valid 件数と内容を必ず確認してください。",
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
    "成功メッセージ（importedCount）のあと /demo で runtime が解決されるか確認",
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
    "大量レコード取り込み helper は legacy/debug 隔離済み",
    "UI 登録は /admin/ui-builder",
  ],
  boundaryNotes: [
    "seed.json は候補ファイル。正本は import 成功後の DB",
  ],
  errorGuide: [
    "ファイルなし → 先に保存するか backend storage を確認",
    "validation 失敗 → errors[].code に従い JSON 修正",
  ],
  caution:
    "インポートは DB へ runtime 宣言を書き込みます。バリデートとプレビューを省略しないでください。",
};

export const ADMIN_UI_BUILDER_GUIDE: AdminGuide = {
  title: "UI ビルダー（コンポーネント & レイアウト）",
  purpose:
    "プリミティブコンポーネントを UI topology DB に登録し、layout patch でグリッド配置を draft → validate → apply する画面です。",
  prerequisites: [
    "/auth 済み、UI topology DB 接続可能",
    `${UX_CONTENTS}で画面の内容と${UX_DATA_SHAPE}が定義されていること`,
    "データは /admin/contents で登録済みであること（推奨）",
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
    "/demo で投影確認",
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
    "/dev/admin/registry-vector-validate（登録前重複チェック）",
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
  title: "Legacy / Debug: ランタイム検証（dispatch）",
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
    "登録が足りない → /dev/admin/import, /dev/admin/seed, /admin/ui-builder",
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
  title: "新規 manifest 作成",
  purpose:
    `新しい manifest の下書きを作成し、内容と${UX_DATA_SHAPE}を整え、内容確認・プレビュー後に明示的に有効化する画面です。既存 manifest の relation / hub 操作は ${UX_HUB_MANIFESTS_PAGE} で行います。`,
  prerequisites: [
    "/auth 済み、DATABASE_URL / DEMO_BACKEND_URL 設定済み",
    "seed に content_bundle 管理用 admin route（layer=content_bundle）が active 登録されていること",
  ],
  howToSteps: [
    "新規 manifest が扱う hub / entity / relation を選ぶ",
    "Entity Draft Editor で state と label を入力し、ドラフトを作成する",
    "ドラフト更新後は内容確認を再実行する",
    "内容確認 → プレビュー → 有効化の順で進める",
    `既存 manifest の relation / hub 操作は /admin/manifests へ`,
  ],
  inputs: [
    "新規 manifest の hub selector / relation / state / label",
    "Advanced: entityJsonb JSON（debug のみ）",
  ],
  actions: [
    "content_bundle:create_entity_draft / update_entity_draft / validate_draft / preview_draft / promote_draft",
  ],
  outputs: [
    "新規 manifest draft detail",
    "structured validation issues（blocking / warning）",
    "preview summary + canPromote",
    "promote 結果 + created entity_id + readback",
  ],
  nextSteps: [
    "/admin/ui-builder",
    "/admin/manifests",
  ],
  boundaryNotes: [
    "direct DB 編集は行いません — backend validation / persistence boundary 経由のみ",
    "validation 未実行では promote を有効化しません",
    "draft は runtime visible active state ではありません",
    "frontend は topology 意味判断をしません",
  ],
};

export const ADMIN_MANIFESTS_GUIDE: AdminGuide = {
  title: UX_HUB_MANIFESTS,
  purpose:
    `既存 manifest の relation / hub 操作画面です。登録済み topology_manifest 一覧と relation / hub の遷移・並び順を同じ canonical route で扱います。promote 前の draft hub 割当は扱いません。新規 manifest 作成は ${UX_CONTENTS_PAGE} で行います。`,
  prerequisites: [
    "/auth でログイン済みであること",
    `${UX_CONTENTS} で新規 manifest が作成済みであること`,
  ],
  howToSteps: [
    "登録済み topology_manifest 一覧を確認する",
    "同じ画面内の relation / hub 操作で遷移先を追加・編集・並び替える",
    "promote 前の draft hub 割当はこの画面では扱わない",
  ],
  inputs: [
    "既存 topology_manifest の relation / navigation intent",
  ],
  actions: [
    "既存 topology_manifest 一覧表示",
    "relation 追加 / 編集 / 並び替え / 利用停止",
  ],
  outputs: [
    "既存 topology_manifest 一覧",
    "relation / hub 操作結果",
  ],
  boundaryNotes: [
    "Frontend は操作送信のみ — 内容の正しさはサーバー側で検証",
    "silent fallback 禁止 — バックエンド未接続は明示表示",
  ],
  caution: "relation / hub 操作は DB の状態を変更します。",
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
    href: "/admin/contents",
    label: "新規 manifest 作成",
    purpose: "新しい manifest のドラフト作成・内容確認・プレビュー・有効化",
    relation: "canonical workflow Step 1",
    howToSummary: [
      "新規ドラフトを作成",
      "内容確認 → プレビュー",
      "明示的に有効化",
    ],
  },
  {
    href: "/admin/ui-builder",
    label: UX_UI_BUILDER,
    purpose: "画面部品の登録とレイアウトの準備",
    relation: "canonical workflow Step 2",
    howToSummary: [
      "部品を登録",
      "レイアウトを配置",
      "プレビュー → 検証 → 保存反映",
    ],
  },
  {
    href: "/admin/manifests",
    label: UX_HUB_MANIFESTS,
    purpose: "既存 manifest の relation / hub 操作",
    relation: "canonical workflow Step 3",
    howToSummary: [
      "既存 manifest を確認",
      "relation を追加・編集・並び替え",
    ],
  },
];

export const ADMIN_HUB_NAVIGATION_GUIDE: AdminGuide = {
  title: "ナビ順序設定",
  purpose: "設定単位のページ遷移順序を設定します。" +
    "画面間ナビゲーションの順序を管理する画面です。",
  prerequisites: [
    "/auth でログイン済みであること",
    "先に /admin/manifests で画面群（topology_manifest）が登録されていること",
    `${UX_CONTENTS} で画面の内容が定義されていること`,
  ],
  howToSteps: [
    "「画面選択」で対象の topology_manifest を選ぶ",
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
    nextLabel: "新規 manifest 作成へ",
  },
  {
    step: 2,
    label: "新規 manifest 作成",
    href: "/admin/contents",
    purpose: "新しい manifest の下書き・内容確認・プレビュー・有効化",
    completionSign: "新規 manifest が有効化されていること",
    nextLabel: `${UX_UI_BUILDER}へ`,
  },
  {
    step: 3,
    label: UX_UI_BUILDER,
    href: "/admin/ui-builder",
    purpose: "表示・操作 UI（component / layout / style）",
    completionSign: "レイアウトの保存反映が完了していること",
    nextLabel: `${UX_HUB_MANIFESTS}へ`,
  },
  {
    step: 4,
    label: UX_HUB_MANIFESTS,
    href: "/admin/manifests",
    purpose: "既存 manifest の relation / hub 操作",
    completionSign: "必要な relation / hub 並び順操作が完了していること",
  },
];

export const ACCEPTANCE_FLOW_STEPS: AcceptanceFlowStep[] = ADMIN_MAIN_FLOW_STEPS
  .map((step) => ({ ...step }));

/** 受入チェックリスト — 完了判定の正本ではなく確認観点のリスト */
export type AcceptanceCheckItem = {
  label: string;
  href: string;
  checks: string[];
};

export const ACCEPTANCE_CHECKLIST: AcceptanceCheckItem[] = [
  {
    label: "新規 manifest 作成",
    href: "/admin/contents",
    checks: [
      "ドラフトを作成できること",
      "validation 未実行では有効化できないこと",
      "内容確認 → プレビュー → 有効化ができること",
    ],
  },
  {
    label: UX_UI_BUILDER,
    href: "/admin/ui-builder",
    checks: [
      "部品を登録できること",
      "レイアウトのプレビュー・検証・保存反映ができること",
    ],
  },
  {
    label: UX_HUB_MANIFESTS,
    href: "/admin/manifests",
    checks: [
      "既存 manifest 一覧が表示されること",
      "relation / hub 操作を同じ画面で行えること",
    ],
  },
];
