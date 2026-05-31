/** Static copy for /admin help panels — aligned with docs/registrar-admin-ui-specification.md */

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
  title: "Registrar 管理 UI（入口）",
  purpose:
    "トポロジー登録の意図を backend 経由で送る controlled registration boundary です。ランタイム実行画面・汎用 CRUD・直接 DB エディタではありません。",
  prerequisites: [
    "デモで DB 永続化する場合: infra を起動し DATABASE_URL / DEMO_BACKEND_URL を設定",
    "/auth でログインし JWT を取得（sessionStorage に demo_jwt_token）",
    "ランタイム dispatch は /admin/runtime（本インデックスは登録専用）。demo preset は /demo",
  ],
  howToSteps: [
    "管理トップ（このページ）で全体の流れと各画面の役割を確認する",
    "初回データ取り込みなら /admin/import で CSV/JSON を preview → apply する",
    "デモ用 runtime 宣言なら /admin/seed で seed.json を validate → preview → import する",
    "UI コンポーネント・レイアウト登録は /admin/ui-builder で bucket → promote → layout patch（preview → validate → apply）",
    "推薦トークン追加は /admin/context-token-registry。登録前の重複確認は /admin/registry-vector-validate",
    "各画面で DB 書き込み（apply / promote / import）の前に必ず preview または validate を通す",
    "登録後は /admin/runtime で dispatch・emission を確認（demo 専用は /demo）",
  ],
  inputs: [
    "各画面で説明される manifest / schema / seed / bucket / layout / token などの登録対象",
    "操作前に /auth で JWT を取得（DB 永続化が必要な画面）",
  ],
  actions: [
    "下のリンクから目的の画面へ移動し、画面内の Preview → Validate → Apply / Promote / Import の順で確認する",
  ],
  outputs: [
    "成功時: backend が検証したうえで DB（canonical topology）または /storage に反映",
    "失敗時: 画面のエラーパネルに code / message — silent fallback はしません",
  ],
  nextSteps: [
    "推奨受入フロー: Auth → Import または Contents → UI Builder → Runtime確認",
    "全画面共通: preview / validate を必ず通してから apply / promote / import を実行する",
  ],
  boundaryNotes: [
    "Frontend = projection / intent submission。意味判断・永続化の正本は DB + backend runtime",
    "Preview は read-only。Apply / Promote / Import は明示操作のみ",
  ],
};

export const ADMIN_IMPORT_GUIDE: AdminGuide = {
  title: "インポート（CSV/JSON）",
  purpose:
    "外部ファイルのレコードを、選択した import manifest と schema に沿って topology 登録データへ取り込むための画面です。",
  prerequisites: [
    "/auth 済み、DEMO_BACKEND_URL 設定済み",
    "取り込み先の import manifest と schema が DB に既に登録されていること",
  ],
  howToSteps: [
    "「1. マニフェストとスキーマを選択」で、取り込みルール（manifest）と行の形（schema）を選ぶ",
    "「2. CSV または JSON ファイルをアップロード」でファイルを選ぶ（拡張子で csv/json を自動判定）",
    "「3. プレビュー（バリデート）」を押す — backend が各行を検証し、DB にはまだ書かない",
    "プレビュー表で invalid 行があれば validationErrors を直し、ファイルを差し替えて再プレビュー",
    "validCount > 0 を確認したら「4. 適用」を押す — snapshotId 経由で valid レコードのみ DB 反映",
    "applyLogId が表示されたら /admin/runtime または /demo で取り込み結果を確認する",
  ],
  inputs: [
    "マニフェスト — 取り込みルール・ターゲットの定義（DB 登録済み一覧から選択）",
    "スキーマ — 各行/オブジェクトのフィールド型・必須 ref の契約",
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
  title: "context_token_registry",
  purpose:
    "推薦エンジンと SQL Attention が参照する離散トークン辞書を管理します。value がコサイン類似度の意味方向成分です。",
  prerequisites: [
    "/auth 済み、DEMO_BACKEND_URL + DATABASE_URL 設定済み",
  ],
  howToSteps: [
    "ページ読み込みでアクティブトークン一覧が表示される（空ならこれから追加）",
    "「新規トークン追加」: label（必須）、group（任意）、value を -1.0〜1.0 で入力",
    "「トークン追加」を押す — backend が検証し DB に create、一覧に行が増える",
    "不要になったトークンは行の「非推奨にする」— 物理削除ではなく status=deprecated",
    "新規 registry 登録前に /admin/registry-vector-validate で ID 重複がないか確認することを推奨",
  ],
  inputs: [
    "label, group, value ∈ [-1.0, 1.0]",
  ],
  actions: [
    "トークン追加（create）",
    "非推奨（deprecate）",
  ],
  outputs: [
    "create → tokenId 発行",
    "deprecate → 一覧で非推奨表示",
  ],
  nextSteps: [
    "/admin/registry-vector-validate",
    "推薦パラメータは function_parameters（本 UI では編集不可）",
  ],
  boundaryNotes: [
    "Frontend は意図送信のみ。永続化は backend",
  ],
  errorGuide: [
    "501 → Docker + DEMO_BACKEND_URL",
    "401 → /auth",
    "value 範囲外 → -1〜1 に修正",
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
  title: "レジストリベクター検証",
  purpose:
    "登録候補の registry ID 配列が既存レジストリと重複・近似していないかを事前検証します（DB には書きません）。",
  prerequisites: [
    "/auth 済み",
    "検証したい UUID のリストを用意",
  ],
  howToSteps: [
    "registryTable に対象テーブル名を入力（例: relation_registry）",
    "query IDs に UUID を改行・カンマ・スペース区切りで貼り付け",
    "「レジストリベクター検証」を押す",
    "結果の validationClass と isBlocking を確認 — blocking なら neighbors 表で既存 ID とスコアを見る",
    "duplicate / near_duplicate なら query を統合・差別化して再検証",
    "pass または非 blocking なら、Import / UI Builder / Token 登録など本登録操作へ進む",
  ],
  inputs: [
    "registryTable, query IDs（UUID 列）",
  ],
  actions: [
    "検証実行のみ（read-only）",
  ],
  outputs: [
    "validationClass, isBlocking, neighbors[]",
  ],
  nextSteps: [
    "blocking → ID 修正後に再検証",
    "pass → 登録画面へ",
  ],
  boundaryNotes: [
    "閾値は function_parameters — 本 UI では変更不可",
    "DB 書き込みなし",
  ],
  errorGuide: [
    "duplicate_vector → neighbors を参照して ID 見直し",
    "zero_vector → 有効なベクトルを持つ ID を選ぶ",
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
  title: "マニフェスト管理",
  purpose:
    "DB 上の manifest を管理する画面です。Runtime wiring（dispatcher_mapping / runtime_mapping / projection_constructor_mapping）と、別 boundary の promotion metadata（disclosure / campaign intent）を typed sub-mode で編集します。runtime dispatch の意味判断は backend が行います。",
  prerequisites: [
    "/auth 済み、DATABASE_URL / DEMO_BACKEND_URL 設定済み",
    "seed に manifest 管理用 admin route（layer=manifest / promotion_manifest）が active 登録されていること",
  ],
  howToSteps: [
    "「Runtime wiring」タブで DB から active / draft / deprecated manifest を一覧する",
    "行をクリックして wiring detail — dispatcher axes と runtime_destination を確認する",
    "draft の場合は axes + runtime_destination を編集して「ドラフト更新」",
    "新規 wiring は「新規ドラフト」→ axes 入力 → 「ドラフト作成」（status=draft のみ）",
    "「バリデート」で backend 検証 — missing mapping / unknown destination / duplicate active axes は explicit error",
    "optional advanced: projection_constructor_mapping（constructorKey / outputKind / packageIds + nested JSON）",
    "valid な draft のみ「プロモート (draft → active)」— 自動昇格なし",
    "「Promotion metadata」タブで promotion_metadata_mapping 付き manifest を一覧・編集",
    "promotion 新規: wiring タブで draft 作成 → promotion タブで manifest_id を指定して metadata 追加",
    "promotion「バリデート」— disclosure / refs / placementKey 解決は backend 正本",
    "promotion 付き manifest の lifecycle promote は wiring タブの「プロモート」（promotion validate も promote 前に実行）",
  ],
  inputs: [
    "wiring: dispatcher axes role, target, layer, action + runtime_destination",
    "optional advanced: projection_constructor_mapping — constructorKey, outputKind, packageIds, fieldDefs JSON",
    "promotion: manifestKey, versionLabel, disclosureText, placementKey, projectionSurfaceType, activationPolicy, targetTopologyRefs",
  ],
  actions: [
    "manifest: list / get / validate / create_draft / update_draft / promote / deprecate",
    "promotion_manifest: list / get / validate / update_draft",
  ],
  outputs: [
    "一覧・詳細・structured validation result",
    "promote/deprecate は lifecycle response（ok / errorCode）",
  ],
  nextSteps: [
    "/admin/runtime で dispatch 経路を確認",
    "CSV/JSON import manifest 選択は /admin/import",
  ],
  boundaryNotes: [
    "Frontend は intent submission のみ — manifest / promotion validity は backend validate",
    "ManifestDispatcher の production path は変更しない",
    "promotion metadata と runtime wiring は同一 manifest topology 配列に共存可能",
    "silent fallback 禁止 — backend unavailable は明示表示",
  ],
  caution: "Promote / Deprecate は DB status を変更します。",
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
    href: "/admin/runtime",
    label: "ランタイム検証",
    purpose: "user dispatch → emission 投影（登録後の動作確認）",
    relation: "canonical runtime route / 開発者検証",
    howToSummary: [
      "ログイン済みを確認",
      "preset を選んで実行",
      "emission / エラーを確認",
    ],
  },
  {
    href: "/admin/import",
    label: "インポート",
    purpose: "CSV/JSON → manifest+schema で validate → preview → apply",
    relation: "topology データ一括登録 / M6",
    howToSummary: [
      "manifest + schema を選択 → ファイル選択",
      "プレビューで invalid 行をゼロに",
      "適用で DB 反映",
    ],
    caution: "Apply で DB 書き込み",
  },
  {
    href: "/admin/seed",
    label: "シード",
    purpose: "/storage/seed.json の save/load/validate/preview/import",
    relation: "demo runtime 宣言候補 → DB",
    howToSummary: [
      "JSON 編集 or ロード",
      "バリデート → プレビュー",
      "インポート",
    ],
    caution: "Import で DB 書き込み",
  },
  {
    href: "/admin/ui-builder",
    label: "UI ビルダー",
    purpose: "bucket → generate → promote → layout patch",
    relation: "UI topology DB / Issue #86/#89",
    howToSummary: [
      "バケット登録 → プロモート",
      "レイアウトタブで配置",
      "preview → validate → apply",
    ],
    caution: "Promote・layout Apply で DB 書き込み",
  },
  {
    href: "/admin/context-token-registry",
    label: "Context Token Registry",
    purpose: "推薦・SQL Attention 用トークン辞書",
    relation: "推薦エンジン / context route",
    howToSummary: [
      "label + value を入力",
      "トークン追加",
      "不要時は非推奨",
    ],
  },
  {
    href: "/admin/registry-vector-validate",
    label: "Registry Vector Validate",
    purpose: "registry ID 配列の重複・近傍検証",
    relation: "登録前チェック",
    howToSummary: [
      "table + UUID 列を入力",
      "検証実行",
      "blocking なら ID 修正",
    ],
  },
  {
    href: "/admin/contents",
    label: "コンテンツ",
    purpose: "hub / entity / relation browse、entity draft → validate → preview → promote",
    relation: "content_bundle admin route / hub topology 管理",
    howToSummary: [
      "browse タブで hub / entity / relation を検索・確認",
      "Entity Draft Editor でドラフト作成 → Validate → Preview",
      "Promote で active entity として DB 登録",
    ],
  },
  {
    href: "/admin/manifests",
    label: "マニフェスト",
    purpose: "manifest wiring の一覧・detail・validate・promote/deprecate",
    relation: "M2 manifest admin management surface",
    howToSummary: [
      "一覧 → detail → validate",
      "draft 編集 → promote（明示）",
      "active → deprecate",
    ],
    caution: "Promote / Deprecate で DB status 変更",
  },
];

/** 推奨受入フロー — admin index の "推奨受入フロー" セクションで使用 */
export type AcceptanceFlowStep = {
  step: number;
  label: string;
  href: string;
  purpose: string;
  completionSign: string;
  nextLabel?: string;
  boundaryNote?: string;
};

export const ACCEPTANCE_FLOW_STEPS: AcceptanceFlowStep[] = [
  {
    step: 1,
    label: "認証 (Auth)",
    href: "/auth",
    purpose: "JWT を取得し sessionStorage に demo_jwt_token を保存する。バックエンド API を叩く画面はすべて事前ログインが必要。",
    completionSign: "ログイン済み表示。sessionStorage に demo_jwt_token が存在すること。",
    nextLabel: "Import または Contents へ",
  },
  {
    step: 2,
    label: "インポート (Import)",
    href: "/admin/import",
    purpose: "CSV/JSON ファイルを manifest+schema に照合し topology データとして DB へ取り込む。プレビューで valid 件数を確認してから適用する。",
    completionSign: "適用成功後に applyLogId が表示されること。invalidCount = 0 が理想。",
    nextLabel: "Contents またはUI Builder へ",
    boundaryNote: "preview/apply の正本は backend。frontend はファイル送信と結果表示のみ。",
  },
  {
    step: 3,
    label: "コンテンツ (Contents)",
    href: "/admin/contents",
    purpose: "hub / entity / relation を browse で確認し、entity draft → validate → preview → promote の流れで active content として登録する。",
    completionSign: "promote 成功後の readback に entity_id が返ること。Browse に新規エンティティが現れること。",
    nextLabel: "UI Builder または Runtime確認へ",
    boundaryNote: "promote 可否判定は backend。frontend は draft 状態の表示と intent 送信のみ。",
  },
  {
    step: 4,
    label: "UI ビルダー (UI Builder)",
    href: "/admin/ui-builder",
    purpose: "コンポーネントを bucket → generate → promote し、layout canvas に配置して preview → validate → apply する。",
    completionSign: "パレットにプロモート済みコンポーネントが現れ、layout apply が valid 完了すること。ドラフトのみノードが 0 件であること。",
    nextLabel: "Runtime確認へ",
    boundaryNote: "topology 意味判断は backend。frontend はドラフト状態の表示と intent 送信のみ。",
  },
  {
    step: 5,
    label: "Runtime確認 (Runtime)",
    href: "/admin/runtime",
    purpose: "promoted topology に対して dispatch を発行し emission を確認する。登録不足なら対応する画面へ戻る。",
    completionSign: "dispatch が成功し emission が返ること。ATTRACTOR_RESOLVE_FAILED などのエラーが解消されること。",
    boundaryNote: "dispatch 経路・emission は backend / DB が正本。frontend は projection のみ。",
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
    label: "Import preview/apply",
    href: "/admin/import",
    checks: [
      "manifest と schema を選択できること",
      "プレビューで validCount > 0 になること",
      "適用で applyLogId が返ること",
    ],
  },
  {
    label: "Content draft/validate/preview/promote",
    href: "/admin/contents",
    checks: [
      "Browse で hub / entity / relation 一覧が表示されること",
      "ドラフト作成 → Validate で blocking issues なしになること",
      "Preview で canPromote: true になること",
      "Promote 後 readback に entity_id が返ること",
    ],
  },
  {
    label: "UI Builder bucket/promote/layout apply",
    href: "/admin/ui-builder",
    checks: [
      "バケットにコンポーネントを登録できること",
      "generate → promote でパレットに追加されること",
      "layout preview が valid になること",
      "layout validate が通ること",
      "draft-only ノードなしで layout apply が成功すること",
    ],
  },
  {
    label: "Runtime dispatch確認",
    href: "/admin/runtime",
    checks: [
      "preset を選択して dispatch が成功すること",
      "emission が返ること（ATTRACTOR_RESOLVE_FAILED なし）",
    ],
  },
];
