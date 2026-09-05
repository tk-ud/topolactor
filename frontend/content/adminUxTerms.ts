/**
 * 管理画面の非開発者向け表示用語（URL・API 名は変更しない）。
 * 内部の manifest / schema 等は技術情報・詳細リファレンスに退避する。
 */
export const UX_CONTENTS = "新しいページを作る";
export const UX_CONTENTS_PAGE = "新しいページを作る画面";
export const UX_HUB_MANIFESTS = "ページ同士をつなぐ";
export const UX_HUB_MANIFESTS_PAGE = "ページ同士をつなぐ画面";
/** @deprecated 互換参照用 — 新規コードは UX_CONTENTS / UX_HUB_MANIFESTS を使用 */
export const UX_IMPORT_SETTINGS = UX_CONTENTS;
export const UX_IMPORT_SETTINGS_PAGE = UX_CONTENTS_PAGE;

export const UX_DATA_SHAPE = "データの形";
export const UX_UI_BUILDER = "画面づくり";
export const UX_RUNTIME_CHECK = "動作確認";
export const UX_ENUM_ROSTER = "enum 名簿";
export const UX_USER_ROSTER = "ユーザー名簿";

/** ステータスラベル（通常表示用） */
export const UX_STATUS_LABELS: Record<string, string> = {
  draft: "下書き",
  active: "有効",
  deprecated: "利用停止",
};

/** アクションラベル（通常表示用） */
export const UX_ACTION_LABELS: Record<string, string> = {
  validate: "内容を確認",
  promote: "有効化",
  deprecate: "利用停止",
  import: "取り込む",
  create: "追加",
  update: "更新",
};

/** 実行先ラベル（通常表示用） */
export const UX_RUNTIME_DESTINATION_LABELS: Record<string, string> = {
  topology_transform_runtime: "通常ルーティング",
  admin_runtime: "管理機能",
  sse_projection_runtime: "リアルタイム投影",
};

/**
 * Step 1 派生識別子プレビュー（SSOT: admin-console-workflow-ssot.yaml
 * canonical_sequential_authoring_pipeline steps[0].derived_on_step1 — route /
 * primary physical table / ui_builder_key は auto-shown preview として常時表示が
 * 要求されているため details 等で隠さない。ここではラベル文言のみ通常語彙にする）。
 */
export const UX_DERIVED_IDENTIFIER_ROUTE_LABEL = "ページのURL（自動生成）";
export const UX_DERIVED_IDENTIFIER_PRIMARY_TABLE_LABEL = "保存先テーブル名（自動生成）";
export const UX_DERIVED_IDENTIFIER_UI_BUILDER_KEY_LABEL = "画面づくり用キー（自動生成）";
/** 複製元の証跡（読み取り専用）ラベル */
export const UX_CLONE_SOURCE_EVIDENCE_TOPOLOGY_NAME_LABEL = "トポロジーID";
export const UX_CLONE_SOURCE_EVIDENCE_STATUS_LABEL = "状態";
export const UX_CLONE_SOURCE_EVIDENCE_DISPATCHER_LABEL = "実行先";
export const UX_CLONE_SOURCE_EVIDENCE_UPDATED_AT_LABEL = "最終更新";

/** 技術用語→業務語ヘルパー（詳細/debug のみで技術語を表示） */
export function toFriendlyLabel(technicalKey: string): string {
  const map: Record<string, string> = {
    manifest: "画面",
    schema: UX_DATA_SHAPE,
    runtime: UX_RUNTIME_CHECK,
    dispatch: "実行",
    hub_relation: "ナビ順序",
    sequence_position: "順序番号",
    topology_manifest_id: "画面群ID",
    related_hub_id: "遷移先",
    component: "部品",
    token: "スタイル設定",
  };
  return map[technicalKey] ?? technicalKey;
}

/** ContentsScreenDesignPanel フィールドラベル（通常表示用） */
export const UX_FIELD_NULLABLE = "空欄許可";

/** Step 2 column enum dictionary group binding (not exposed as enumGroupId). */
export const UX_FIELD_ENUM_GROUP = "候補グループ";
export const UX_FIELD_ENUM_GROUP_NONE = "（なし）";

/** 検索キー選択（通常表示）— internal column names must not be directly exposed */
export const UX_FIELD_SEARCH_KEY = "検索キー";
/** 集計キー（通常表示）— "group by" must not appear as primary vocabulary */
export const UX_FIELD_AGGREGATION_KEY = "集計キー";
/** 表示列（通常表示） */
export const UX_FIELD_DISPLAY_COLUMNS = "表示列";
/** 集計列（通常表示） */
export const UX_FIELD_AGGREGATION_COLUMNS = "集計列";
/** 集計関数（通常表示） */
export const UX_FIELD_AGGREGATION_FUNCTION = "集計関数";
/** 集計式（列＋関数の複数行） */
export const UX_FIELD_AGGREGATION_MEASURES = "集計式";
/**
 * Step 3 項目の使い分け: 操作ごとの対象＝UI投影、集計キー＝GROUP BY、表示列は保存用（作者は操作対象のみ編集）。
 */
export const UX_FIELD_STEP3_COLUMN_USAGE = "項目の使い分け";

/** 集計関数候補（topology intent; JsonbManifestRuntime 系と整合） */
export const AGGREGATION_FUNCTION_OPTIONS: readonly {
  value: string;
  label: string;
}[] = [
  { value: "", label: "— なし —" },
  { value: "sum", label: "合計 (sum)" },
  { value: "avg", label: "平均 (avg)" },
  { value: "max", label: "最大 (max)" },
  { value: "min", label: "最小 (min)" },
  { value: "count", label: "件数 (count)" },
];
/** サンプル表示（通常表示） */
export const UX_FIELD_SAMPLE_VIEWING = "サンプル表示";
/** 初期データ（通常表示） */
export const UX_FIELD_INITIAL_DATA = "初期データ";

/** Step 3 CSV/JSON 取り込み: 検証ルールは Step 2 の項目定義 */
export const UX_IMPORT_RULE_PAGE_COLUMNS = "取り込みルール";
export const UX_IMPORT_RULE_PAGE_COLUMNS_HINT =
  "CSV/JSON の列名は Step 2 で定義した項目名（例: employees.name）と一致させてください。プレビューではこのページの項目定義で型・必須を確認します。";
export const UX_IMPORT_RULE_PAGE_COLUMNS_EMPTY =
  "Step 2 で項目を定義すると、ここに取り込みルールが表示されます。";

/** スタンドアロン取り込み画面向け — schema_registry の説明（Step 3 では非表示） */
export const UX_IMPORT_SCHEMA_REGISTRY_HINT =
  "プラットフォーム登録済みのデータ形です。通常のページ作成（Step 3）では選びません。デモ・管理ランタイム用の登録が含まれます。";
export const UX_IMPORT_SCHEMA_LABELS: Record<string, string> = {
  default_schema: "汎用（label のみ・プラットフォーム既定）",
  admin_schema: "管理画面ランタイム用（通常は未使用）",
  demo_entity_schema: "デモ用（label / state / hub_id）",
};

export function uxImportSchemaOptionLabel(name: string): string {
  return UX_IMPORT_SCHEMA_LABELS[name] ?? name;
}

/** テーブル結合意図（任意、通常表示） */
export const UX_FIELD_RELATION_INTENT = "参照データの関連付け（任意）";

/** 行 ID（主キー）— relation 項目の保存値は id、表示はこのラベル */
export const UX_RELATION_ROW_ID_KEY = "id";
export const UX_RELATION_ROW_ID_LABEL = "id (record id)";
/** 操作ごとの対象項目（イベント時、通常表示） */
export const UX_FIELD_OPERATION_ENTITY = "操作ごとの対象項目";

/** Step 3: この画面で有効にする操作種別 */
export const UX_FIELD_OPERATION_KINDS = "この画面で有効にする操作";
export const UX_FIELD_OPERATION_KINDS_HINT =
  "チェックした操作だけがエンドユーザー画面に現れます。下の表で、各操作に関わる項目を選びます。";
export const UX_FIELD_OPERATION_KINDS_EMPTY_HINT =
  "操作を1つ以上有効にすると、操作ごとの対象項目を設定できます。";

/** 操作ごとの対象項目 — マトリクス説明 */
export const UX_FIELD_OPERATION_ENTITY_INTRO =
  "有効にした操作ごとに、画面で使う項目を選びます。列見出しの役割（表示／入力）を参考にしてください。検索の入力キーは別途「検索キー」で設定します。";

/** 操作ごとの対象項目 — 列ヘッダの req/res 役割ラベル */
export const UX_OPERATION_ENTITY_ROLE_RESPONSE = "表示（Res）";
export const UX_OPERATION_ENTITY_ROLE_REQUEST = "入力（Req）";
export const UX_OPERATION_ENTITY_ROLE_TARGET = "操作対象";

export const UX_OPERATION_ENTITY_BULK_COLUMN = "列を一括";
export const UX_OPERATION_ENTITY_BULK_ROW = "行を一括";
export const UX_OPERATION_ENTITY_BULK_ALL = "すべて";
export const UX_OPERATION_ENTITY_BULK_NONE = "解除";

export const UX_OPERATION_ENTITY_ROLE_LABELS: Record<string, string> = {
  response: UX_OPERATION_ENTITY_ROLE_RESPONSE,
  request: UX_OPERATION_ENTITY_ROLE_REQUEST,
  operation_target: UX_OPERATION_ENTITY_ROLE_TARGET,
};

export function uxOperationEntityRoleLabel(role: string): string {
  return UX_OPERATION_ENTITY_ROLE_LABELS[role] ?? role;
}

/** 検索条件（詳細表示） */
export const UX_FIELD_SEARCH_CONDITIONS = "検索条件";
/** 検索条件の段階開示 summary */
export const UX_FIELD_ADVANCED_SEARCH_CONDITIONS = "詳細条件を設定";
/** 条件未設定時の追加導線 */
export const UX_FIELD_ADD_SEARCH_CONDITION = "検索条件を追加";
/** 論理条件（プロ向け） */
export const UX_FIELD_LOGICAL_CONDITION = "論理条件";
/** 集計結果の絞り込み（詳細表示） */
export const UX_FIELD_HAVING_CONDITIONS = "集計後の絞り込み（詳細）";
/**
 * 表示列モード（ランタイム用）。Step 3 通常表示では操作ごとの対象から displayColumns を同期するため、
 * 作者向けの独立チェック UI には使わない。SSOT: admin-console-workflow-ssot.yaml column_roles_contract.
 */
export const UX_FIELD_DISPLAY_MODE = "表示列の範囲";

/** 表示列モード候補 */
export const DISPLAY_COLUMN_MODE_LABELS: Record<string, string> = {
  selected: "選んだ列のみ",
  all: "すべての列",
  none: "集計値のみ（列なし）",
};

/** 検索演算子候補（通常表示。SQL 語彙だが条件値として許可） */
export const SEARCH_OPERATOR_OPTIONS: readonly {
  value: string;
  label: string;
}[] = [
  { value: "=", label: "= (一致)" },
  { value: "!=", label: "≠ (不一致)" },
  { value: "<>", label: "<> (不一致 alias)" },
  { value: "like", label: "like (部分一致)" },
  { value: "ilike", label: "ilike (大小無視)" },
  { value: "not like", label: "not like (不含)" },
  { value: ">", label: "> (より大)" },
  { value: ">=", label: "≥ (以上)" },
  { value: "<", label: "< (より小)" },
  { value: "<=", label: "≤ (以下)" },
  { value: "between", label: "between (範囲)" },
  { value: "in", label: "in (リスト)" },
  { value: "not in", label: "not in (リスト除外)" },
  { value: "is null", label: "is null (空)" },
  { value: "is not null", label: "is not null (空でない)" },
];

/** 論理結合演算子（条件グループ接続） */
export const LOGICAL_CONNECTOR_OPTIONS: readonly {
  value: string;
  label: string;
}[] = [
  { value: "and", label: "AND" },
  { value: "or", label: "OR" },
  { value: "not", label: "NOT" },
];

/** HAVING 演算子候補 */
export const HAVING_OPERATOR_OPTIONS: readonly {
  value: string;
  label: string;
}[] = [
  { value: "=", label: "= (一致)" },
  { value: "!=", label: "≠ (不一致)" },
  { value: "<>", label: "<> (不一致 alias)" },
  { value: ">", label: "> (より大)" },
  { value: ">=", label: "≥ (以上)" },
  { value: "<", label: "< (より小)" },
  { value: "<=", label: "≤ (以下)" },
];

/**
 * カラム型の通常表示候補（SSOT: admin-console-workflow-ssot.yaml step3.column_type_UI.candidates）。
 * free-text は UX_COLUMN_TYPE_ADVANCED_LABEL 経由で隔離する。
 * 「group by」を含む集計語彙はここに追加しない。
 */
export const COLUMN_TYPE_NORMAL_VIEW_OPTIONS: readonly string[] = [
  "text",
  "integer",
  "bigint",
  "boolean",
  "numeric",
  "timestamp with time zone",
  "date",
  "jsonb",
  "uuid",
  "varchar",
  "enum",
];

/** Step 2/3: enum-backed columns use dataType "enum" and optional enumGroupId binding. */
export function isEnumBackedColumnDataType(dataType: string): boolean {
  return dataType.trim().toLowerCase() === "enum";
}

/** 項目型の通常表示ラベル。option value は DB/API 契約の型名を維持する。 */
export const UX_COLUMN_TYPE_LABELS: Record<string, string> = {
  text: "文字列",
  integer: "整数",
  bigint: "大きな整数",
  boolean: "はい / いいえ",
  numeric: "数値",
  "timestamp with time zone": "日時",
  date: "日付",
  jsonb: "自由形式データ",
  uuid: "識別子",
  varchar: "短い文字列",
  enum: "列挙（enum）",
};

/** 通常表示候補外のカスタム型を入力するための advanced/other オプションラベル */
export const UX_COLUMN_TYPE_ADVANCED_LABEL = "その他（詳細入力）";

/**
 * /admin/contents Step 1 entry mode 選択肢。
 * SSOT: admin-console-workflow-ssot.yaml admin_contents_step1_entry_modes.
 * 3 つの mode は可視的に分離する。replacement と clone-as-new-topology を混同させない。
 */
export type ContentsStep1EntryModeId =
  | "create_new_topology"
  | "clone_active_as_replacement_draft"
  | "clone_active_as_new_topology_draft"
  | "resume_existing_draft";

export const CONTENTS_STEP1_ENTRY_MODE_OPTIONS: readonly {
  id: ContentsStep1EntryModeId;
  label: string;
  description: string;
}[] = [
  {
    id: "create_new_topology",
    label: "新規トポロジを作成",
    description: "元の正本なし。新しいトポロジIDで空の下書きを作成します。",
  },
  {
    id: "clone_active_as_replacement_draft",
    label: "有効な正本を複製して『正本置き換え用』下書きを作成",
    description:
      "有効な正本を読み取り専用ソースとして複製し、置き換え用の下書きを作成します。" +
      "置き換えの成立はバックエンド権限のみ（証跡・検証・差分・競合チェック必須）。",
  },
  {
    id: "clone_active_as_new_topology_draft",
    label: "有効な正本を複製して『別トポロジ登録用』下書きを作成",
    description:
      "有効な正本を系譜（lineage）の証跡としてのみ参照し、新しいトポロジIDで別トポロジを登録します。" +
      "元の正本を置き換えることはできません。",
  },
  {
    id: "resume_existing_draft",
    label: "既存の下書きを再開",
    description:
      "保存済みの contents 下書きを選び、トポロジ名の再登録なしで Step 2 から編集を再開します。",
  },
];

/** clone lifecycle 表示ラベル（draft_origin / clone_mode をユーザー向けに可視化）。 */
export const CLONE_DRAFT_ORIGIN_LABELS: Record<string, string> = {
  manual_new: "新規作成",
  manual_clone_replacement: "正本置き換え用クローン",
  manual_clone_new_topology: "別トポロジ登録用クローン",
  sql_attention_candidate: "SQL Attention 候補",
};

export const CLONE_MODE_LABELS: Record<string, string> = {
  none: "クローンなし",
  replacement: "正本置き換え",
  new_topology: "別トポロジ登録",
};

/** clone source active 読み取り専用証跡の見出し。 */
export const UX_CLONE_SOURCE_EVIDENCE_HEADING = "複製元（有効な正本・読み取り専用）";

/** replacement merge はバックエンド権限のみ、という固定注記。 */
export const UX_CLONE_BACKEND_MERGE_AUTHORITY_NOTICE =
  "正本への置き換えはバックエンド権限のみで成立します。" +
  "この画面は意図の送信と証跡・ブロッカーの表示のみを行い、置き換え対象や競合の判定は行いません。";

/** 系譜のみ（置き換え権限ではない）という注記。 */
export const UX_CLONE_LINEAGE_ONLY_NOTICE =
  "複製元は系譜（lineage）の証跡としてのみ参照します。系譜の証跡だけでは正本を置き換えできません。";

/** replacement merge ブロッカーの見出し。 */
export const UX_CLONE_MERGE_BLOCKERS_HEADING = "置き換えブロッカー（バックエンド判定）";

/** 推奨フローの Step ラベル（canonical admin workflow のみ。ログインは prerequisite） */
export const UX_MAIN_FLOW_STEP_LABELS = [
  UX_CONTENTS,
  UX_UI_BUILDER,
  UX_HUB_MANIFESTS,
] as const;

/** /admin/contents パイプライン sub-step ラベル（管理トップステッパー用） */
export const UX_CONTENTS_PIPELINE_SUBSTEP_LABELS = [
  "空登録",
  "テーブル定義",
  "関連付け",
  "物理割当・ページ",
] as const;

/** 有効化タイミング（通常表示用。保存値は変更しない） */
export const UX_ACTIVATION_POLICY_LABELS: Record<string, string> = {
  manual: "手動で有効化",
  scheduled: "日時を指定",
  conditional: "条件を満たしたとき",
};

/** 通常導線に露出させない実装語彙。詳細・debug surface では表示可。 */
export const NORMAL_VIEW_BANNED_TERMS = [
  "manifest",
  "manifestid",
  "manifest_key",
  "topology_manifest",
  "hub_relation",
  "hub membership",
  "navigation ordering",
  "canonical",
  "projection",
  "runtime",
  "dispatcher",
  "payload",
  "backend",
  "db table",
  "column",
  "schema",
  "package",
  "component",
  "grouping intent",
  "raw",
  "silent fallback",
  "pipeline",
  "post-pipeline",
  "submit",
  "separate layout/design surface",
  "standalone design surface",
  "promote",
  "バケット",
  "add のみ",
] as const;

/** UI ビルダー canvas workspace の docked panel labels（タブではない）。 */
export const UX_UI_BUILDER_TAB_LABELS: Record<string, string> = {
  bucket: "部品カタログ（参照）",
  canvas: "canvas workspace",
  layerInspector: "レイヤーインスペクタ",
  designInspector: "デザインインスペクタ",
  css: "スタイル辞書",
  catalog: "部品カタログ（参照）",
  ci: "CI ガイダンス（参照）",
};

/** layout editor 空キャンバス — 主導線は左パネルカードの drag-to-canvas */
export const UX_EMPTY_CANVAS_DRAG_GUIDANCE =
  "左パネルのカードをドラッグしてキャンバスへ配置してください。";

/** layout editor — route 未選択時の案内 */
export const UX_ROUTE_KEY_REQUIRED_FOR_CANVAS =
  "先にページルートを選択または入力してください。パッケージは自動で用意されます。";

/** @deprecated use UX_ROUTE_KEY_REQUIRED_FOR_CANVAS */
export const UX_PACKAGE_REQUIRED_FOR_CANVAS = UX_ROUTE_KEY_REQUIRED_FOR_CANVAS;

/** component bucket panel — drag ヒント（drop で自動登録） */
export const UX_COMPONENT_BUCKET_CARD_DRAG_HINT =
  "ドラッグしてキャンバスへ配置（部品は自動でパッケージに追加されます）";

/** 右ドック — 操作イベント surface（SSOT: runtimeInteractions / layout_vs_design_split） */
export const UX_EVENT_AUTHORING_SECTION = "操作イベント";
/** SSOT: ui_events_trigger_and_target_configurable — 独立 2 軸ラベル */
export const UX_TRIGGER_UI_LABEL = "トリガUI指定";
export const UX_TARGET_UI_LABEL = "操作対象UI指定";
export const UX_RUNTIME_INTERACTION_SECTION_TITLE = "操作イベント（この部品がトリガ）";
export const UX_RUNTIME_INTERACTION_SECTION_HINT =
  "選択中の部品がトリガとなり、操作対象の部品または接続先へイベントを届けます。トリガUI指定と操作対象UI指定は独立して設定します。";
export const UX_RUNTIME_INTERACTION_TRIGGER_UI = "トリガ（この部品）";
export const UX_RUNTIME_INTERACTION_WHEN = "トリガUI指定";
export const UX_RUNTIME_INTERACTION_WHAT = "操作の種類";
export const UX_RUNTIME_INTERACTION_TARGET = "操作対象UI指定";
/** パッケージ単位の接続（内部APIレーン） — SSOT: route_navigation_wiring_preset + manifest_screenReadQueryWiring + PackageWiringEditor advanced */
export const UX_PACKAGE_WIRING_SECTION_TITLE = "内部API（パッケージ接続・ルート遷移）";
export const UX_PACKAGE_WIRING_SECTION_HINT =
  "このページ（パッケージ）単位の接続です。内部API（Contents Step 3 のトポロジ API）とルート遷移を通常画面で設定します。部品ごとのUI状態更新・外部API連携・外部インスタンス連携は「操作イベント」で設定します。";
export const UX_PACKAGE_WIRING_ADVANCED_LABEL = "上級: 配線 ID・接続先の直接編集";
export const UX_RUNTIME_INTERACTION_INTENT_OPEN = "開く";
export const UX_RUNTIME_INTERACTION_INTENT_CLOSE = "閉じる";
export const UX_RUNTIME_INTERACTION_INTENT_TOGGLE = "表示を切り替える";
export const UX_RUNTIME_INTERACTION_SURFACE_MODAL = "モーダル";
export const UX_RUNTIME_INTERACTION_SURFACE_DRAWER = "ドロワー";
export const UX_RUNTIME_INTERACTION_SURFACE_DIALOG = "ダイアログ";
export const UX_RUNTIME_INTERACTION_NO_OVERLAY_TARGETS =
  "モーダル／ドロワー部品がキャンバスにありません。開閉操作を追加するには先に配置してください。";
export const UX_RUNTIME_INTERACTION_ADD = "+ 動作を追加";
export const UX_RUNTIME_INTERACTION_ADD_OVERLAY = "+ モーダル／ドロワー操作";
export const UX_RUNTIME_INTERACTION_ADD_EXTERNAL = "+ 外部API連携";
export const UX_RUNTIME_INTERACTION_ADD_INSTANCE = "+ 外部インスタンス連携";
export const UX_RUNTIME_INTERACTION_ADD_EXTERNAL_COMMIT = "外部API連携を確定";
export const UX_RUNTIME_INTERACTION_ADD_INSTANCE_COMMIT = "外部インスタンス連携を確定";
export const UX_RUNTIME_INTERACTION_STAGING_CANCEL = "キャンセル";
/**
 * UI状態更新（ui_state_update）汎用ミューテーション — SSOT:
 * admin-console-workflow-ssot.yaml layout_node_props_contract.descriptor
 * actionType vocabulary（setState / setActiveKey / localStateMutation）。
 * モーダル／ドロワー等の開閉（overlay disclosure）以外の状態更新もここで
 * 設定・編集できるようにする、開閉操作とは別の追加行。
 */
export const UX_RUNTIME_INTERACTION_ADD_STATE_UPDATE = "+ 状態の更新";
export const UX_RUNTIME_INTERACTION_STATE_UPDATE_ACTION_LABEL = "更新の種類";
export const UX_RUNTIME_INTERACTION_STATE_UPDATE_ACTION_OPTIONS: readonly { value: string; label: string }[] = [
  { value: "setState", label: "値を設定（setState）" },
  { value: "setActiveKey", label: "選択中キーを設定（setActiveKey）" },
  { value: "localStateMutation", label: "ローカル状態を更新（localStateMutation）" },
];
export const UX_RUNTIME_INTERACTION_STATE_PATH_LABEL = "状態スロット（statePath）";
export const UX_RUNTIME_INTERACTION_STATE_VALUE_LABEL = "値（value）";
export const UX_RUNTIME_INTERACTION_LOCAL_STATE_TARGET_LABEL = "ローカル状態の対象（ui-local）";
export const UX_RUNTIME_INTERACTION_NO_STATE_UPDATE_TARGETS =
  "キャンバスに部品がありません。状態を更新する対象を先に配置してください。";
/**
 * 配線インスペクタ分類語彙（正本: frontend-ui-audit-bundle-semantic-frame.md /
 * SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml setting_category_taxonomy）。
 * frontend側: UI監視割当 / UI状態更新 / 副作用設定、backend側: 内部API / 外部API連携 / 外部インスタンス連携。
 */
export const UX_WIRING_CATEGORY_UI_WATCH_BINDING = "UI監視割当";
export const UX_WIRING_CATEGORY_UI_STATE_UPDATE = "UI状態更新";
export const UX_WIRING_CATEGORY_SIDE_EFFECT = "副作用設定";
export const UX_WIRING_CATEGORY_INTERNAL_API = "内部API";
export const UX_WIRING_CATEGORY_EXTERNAL_API = "外部API連携";
export const UX_WIRING_CATEGORY_EXTERNAL_INSTANCE = "外部インスタンス連携";
export const UX_RUNTIME_INTERACTION_CATEGORY_OVERLAY = UX_WIRING_CATEGORY_UI_STATE_UPDATE;
export const UX_RUNTIME_INTERACTION_CATEGORY_EXTERNAL = UX_WIRING_CATEGORY_EXTERNAL_API;
export const UX_RUNTIME_INTERACTION_CATEGORY_INSTANCE = UX_WIRING_CATEGORY_EXTERNAL_INSTANCE;
export const UX_EXTERNAL_INTEGRATION_SECTION_TITLE = UX_WIRING_CATEGORY_EXTERNAL_API;
export const UX_EXTERNAL_INTEGRATION_SECTION_HINT =
  "外部API連携レーン: DB 由来の active な external port 候補を操作対象として選びます。内部API（Contents Step 3 のトポロジ API 配線）とは別系統です。";
export const UX_INSTANCE_OPERATION_SECTION_TITLE = UX_WIRING_CATEGORY_EXTERNAL_INSTANCE;
export const UX_INSTANCE_OPERATION_SECTION_HINT =
  "外部インスタンス連携レーン: admin 承認済みの instance operation 候補を選びます。外部API連携と sibling で、内部API（Contents Step 3）とは別系統です。";
export const UX_EXTERNAL_PORT_EMPTY_HINT =
  "active な external port 候補がありません。外部API連携は内部API（Contents Step 3）とは別系統です。認証・外部接続設定で port を登録してください。";
/** UI監視割当 — 宣言（useState相当のstate slot割当。更新ではない） */
export const UX_WIRING_WATCH_BINDING_HINT =
  "この部品が持つ状態スロット（監視変数）を宣言します。ここは割当（宣言）であり、値の更新は「UI状態更新」、反応は「副作用設定」で行います。";
export const UX_WIRING_WATCH_BINDING_ADD = "+ 状態スロットを宣言";
export const UX_WIRING_WATCH_BINDING_EMPTY = "宣言済みの状態スロットはありません。";
/** 副作用設定 — 明示的な副作用なし選択（SSOT: side_effect_setting.explicit no-side-effect） */
export const UX_WIRING_SIDE_EFFECT_SECTION_HINT =
  "応答の受け取り先（outputProp）・データスコープ（payloadFrom）・対象部品への状態反映を設定します。副作用を持たない場合は明示的に「副作用なし」を選択します。";
export const UX_WIRING_SIDE_EFFECT_NONE_LABEL = "副作用なし（明示）";
/** lifecycle idempotency policy（SSOT: lifecycle_policy.idempotency_policy_values） */
export const UX_WIRING_IDEMPOTENCY_LABEL = "再実行ポリシー（idempotency）";
export const UX_WIRING_IDEMPOTENCY_OPTIONS: readonly { value: string; label: string }[] = [
  { value: "once_per_mount", label: "初期マウントごとに1回（再レンダーで再送出しない）" },
  { value: "refetch_on_route_enter", label: "ページ表示ごとに再取得" },
  { value: "dedupe_key", label: "重複排除キーで制御" },
];
export const UX_RUNTIME_INTERACTION_EXTERNAL_PORT_SELECT = "操作対象UI指定（external port）";
export const UX_RUNTIME_INTERACTION_INSTANCE_OPERATION_SELECT = "操作対象UI指定（instance operation）";
/**
 * admin_runtime 操作上書き（dispatchTargetRefByTrigger / dispatchPayloadFromByTrigger）—
 * SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml admin_runtime_target_ref_override_contract。
 * wiringKind="admin_runtime" のレイアウトが持つ既定の操作（1レイアウト=1操作）を、特定のトリガだけ
 * 別の manifest 操作へ切り替えるための上書きです。候補は manifest / dispatcher_mapping から生成され、
 * 画面固有のハードコード選択肢ではありません。
 */
export const UX_ADMIN_RUNTIME_OPERATION_OVERRIDE_SECTION_TITLE = "管理操作の上書き（admin_runtime）";
export const UX_ADMIN_RUNTIME_OPERATION_OVERRIDE_SECTION_HINT =
  "このレイアウトの既定の管理操作とは別に、特定のトリガだけ他の manifest 操作へ振り替えます。候補は登録済みの manifest / dispatcher_mapping から自動生成されます。";
export const UX_ADMIN_RUNTIME_OPERATION_OVERRIDE_SELECT = "操作対象UI指定（管理操作）";
export const UX_ADMIN_RUNTIME_OPERATION_OVERRIDE_EMPTY_HINT =
  "active な admin_runtime 操作候補がありません。/admin/contents 等で対象 manifest が active かつ dispatcher_mapping 登録済みか確認してください。";
export const UX_ADMIN_RUNTIME_OPERATION_OVERRIDE_ADD = "+ 管理操作の上書き";
export const UX_ADMIN_RUNTIME_OPERATION_OVERRIDE_ADD_COMMIT = "管理操作の上書きを確定";
/** round 18: このレイアウトの配線がadmin_runtimeでない場合の編集不可理由表示 */
export const UX_ADMIN_RUNTIME_OPERATION_OVERRIDE_NOT_ADMIN_RUNTIME_LAYOUT =
  "このレイアウトの配線はadmin_runtimeではないため、管理操作の上書きは追加・編集できません。";
/** @deprecated use UX_EXTERNAL_INTEGRATION_SECTION_TITLE */
export const UX_RUNTIME_INTERACTION_EXTERNAL_API_SECTION_TITLE = UX_EXTERNAL_INTEGRATION_SECTION_TITLE;
/** @deprecated use UX_EXTERNAL_INTEGRATION_SECTION_HINT */
export const UX_RUNTIME_INTERACTION_EXTERNAL_API_SECTION_HINT = UX_EXTERNAL_INTEGRATION_SECTION_HINT;
/** @deprecated use UX_INSTANCE_OPERATION_SECTION_TITLE */
export const UX_RUNTIME_INTERACTION_EXTERNAL_INSTANCE_SECTION_TITLE = UX_INSTANCE_OPERATION_SECTION_TITLE;
/** @deprecated use UX_INSTANCE_OPERATION_SECTION_HINT */
export const UX_RUNTIME_INTERACTION_EXTERNAL_INSTANCE_SECTION_HINT = UX_INSTANCE_OPERATION_SECTION_HINT;

const UX_RUNTIME_INTERACTION_TRIGGER_LABELS: Record<string, string> = {
  click: "クリック時",
  change: "値が変わったとき",
  submit: "送信時",
  toggle: "切り替え時",
  select: "選択時",
  input: "入力時",
  focus: "フォーカス時",
  blur: "フォーカスが外れたとき",
  // lifecycle triggers — SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml trigger_vocabulary
  // initial_mount は runtime synthetic lifecycle trigger（DOM onLoad / resource loaded ではない）
  initial_mount: "初期マウント時（ライフサイクル）",
  route_enter: "ページ表示時（ライフサイクル）",
  initial_display: "初期表示時（ライフサイクル）",
  // pointer triggers（高頻度）
  mouseon: "マウスが乗ったとき（高頻度）",
  mouseout: "マウスが離れたとき（高頻度）",
  hover_start: "ホバー開始時（高頻度）",
  hover_end: "ホバー終了時（高頻度）",
  // keyboard triggers
  keyon: "キー押下中（高頻度）",
  keydown: "キーを押したとき（高頻度）",
  keyup: "キーを離したとき（高頻度）",
  enter: "Enterキー押下時",
  escape: "Escapeキー押下時",
};

export function uxRuntimeInteractionTriggerLabel(trigger: string): string {
  return UX_RUNTIME_INTERACTION_TRIGGER_LABELS[trigger] ?? trigger;
}

/** canvas mode 切替 — SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml layout_mode / wiring_mode */
export const UX_CANVAS_MODE_LAYOUT = "レイアウト";
export const UX_CANVAS_MODE_WIRING = "配線";
export const UX_WIRING_MODE_HINT =
  "配線ビューは操作イベント（runtimeInteractions）の投影です。ここでの編集は下書きノードへの操作イベント追加として保存され、配線図そのものは保存されません。";
export const UX_WIRING_MODE_EMPTY =
  "操作イベントが設定された部品がありません。部品を選択して「操作イベント」から追加するか、部品を開閉対象部品へドラッグして配線してください。";
export const UX_WIRING_DROP_GUIDANCE =
  "部品をドラッグして開閉対象部品（モーダル／ドロワー／ダイアログ）へドロップすると配線を追加します。";
/** lifecycle / high-frequency policy — 明示警告文（SSOT: lifecycle_policy / high_frequency_policy） */
export const UX_WIRING_POLICY_HIGH_FREQUENCY_WARNING =
  "高頻度トリガでの外部/バックエンド送出には debounce（ミリ秒）の明示指定が必要です。";
export const UX_WIRING_POLICY_LIFECYCLE_WARNING =
  "ライフサイクルトリガでの外部/バックエンド送出は、明示的な確認が必要です。プレビューでは実行されません。";
export const UX_WIRING_POLICY_LIFECYCLE_CONFIRM_LABEL =
  "読み込み時の送出を理解して有効化する（再表示時にも再実行されます）";
export const UX_WIRING_DEBOUNCE_LABEL = "送出間隔（debounce, ミリ秒）";
/** 外部連携 credential authority 表示（port record context 内; SSOT: external-port-substrate-ssot.yaml admin_setting_projection） */
export const UX_EXTERNAL_PORT_CREDENTIAL_AUTHORITY_LABEL = "認証情報（port record）";

/** layout editor 右ドック — 配置インスペクタ見出し */
export const UX_LAYOUT_INSPECTOR_SECTION = "配置インスペクタ";

/** layout editor 右ドック — デザインインスペクタ見出し（選択ノードと連動） */
export const UX_DESIGN_INSPECTOR_SECTION = "デザインインスペクタ";

/** /admin/ui-builder は canvas workspace + docked inspectors の単一 authoring surface。 */
export const UX_LAYOUT_EDITOR_SURFACE = "canvas workspace";
export const UX_DESIGN_EDITOR_SURFACE = "デザインインスペクタ";

/** Route navigation default wiring preset — normal-view labels. */
export const UX_ROUTE_NAVIGATION_PRESET_LABEL = "クリック時に指定ルートへ移動";
export const UX_ROUTE_NAVIGATION_NONE_LABEL = "（移動しない）";
export const UX_ROUTE_NAVIGATION_ROUTE_SELECT_LABEL = "移動先ルートを選択";
export const UX_ROUTE_NAVIGATION_SAVE_LABEL = "ルート遷移の配線を保存";

/** Step 3 manifest dispatch wiring — normal-view topology API preset (contents_dispatch_ui_wiring_configurable) */
export const UX_MANIFEST_API_EVENT_PRESET_LABEL = "トポロジ API 設定";
export const UX_TOPOLOGY_API_SECTION_HINT =
  "Contents dispatch レーン: Step 3 で定義した read/query 配線を、このパッケージの dispatch 接続先として保存します。外部連携・インスタンス操作とは別です。";
export const UX_MANIFEST_API_EVENT_TOPOLOGY_CONTEXT = "編集中のページ（Contents Step 3）";
export const UX_MANIFEST_API_EVENT_PAGE_SELECT = "接続先ページ";
export const UX_MANIFEST_API_EVENT_WIRING_SELECT = "API イベント（Step 3 で設定した配線）";
export const UX_MANIFEST_API_EVENT_NONE = "（接続しない）";
export const UX_MANIFEST_API_EVENT_SAVE_LABEL = "API イベント配線を保存";
export const UX_MANIFEST_API_EVENT_TOPOLOGY_MISSING =
  "この画面に対応する Contents ページが見つかりません。/admin/contents で topology.name を登録してください。";
export const UX_MANIFEST_API_EVENT_NOT_CONTENTS_SCREEN =
  "このページは Contents の業務画面ではありません（認証管理などの bundle projection）。トポロジ API は /admin/contents で作った画面（例: employees）を UI Builder 上部で選んだときだけ候補が出ます。";
export const UX_MANIFEST_API_EVENT_NO_STEP3_WIRING =
  "Step 3 の配線候補がありません。/admin/contents で操作種別・検索・表示列を設定し Step 3 まで保存してください。";
export const UX_INSTANCE_OPERATION_EMPTY_HINT =
  "承認済みの instance operation 候補がありません。インスタンス操作は Contents Step 3 とは別系統です。/admin の認証・インスタンス設定で operation を登録・承認してください。";
export const UX_MANIFEST_API_EVENT_WIRING_FIELD_LABELS: Record<string, string> = {
  searchConditions: "検索条件",
  havingConditions: "集計フィルタ（HAVING）",
  aggregationMeasures: "集計・一覧",
  displayColumns: "表示列",
  displayColumnMode: "表示列モード",
};

/** Hub navigation — destination screen picker */
export const UX_HUB_NAV_DESTINATION_LABEL = "遷移先の画面";

/** 左 docked panel — 部品追加パネル見出し（SSOT: canvas_workspace_contract.left_panel）。 */
export const UX_COMPONENT_ADD_PANEL_LABEL = "部品追加";

/** 左 docked panel — ダッシュボード preset 候補タブラベル。 */
export const UX_DASHBOARD_PRESET_CANDIDATE_LABEL = "ダッシュボード preset 候補";

/**
 * ダッシュボード preset 候補の説明文。
 * dashboard_placement_candidate タグを持つ部品（preset / md_viewer 候補）。
 * DB バケット登録不要。主導線は /admin/team-dashboard。
 */
export const UX_DASHBOARD_PRESET_CANDIDATE_DESCRIPTION =
  "dashboard_placement_candidate — preset / md_viewer 候補。DB 登録不要。主導線: /admin/team-dashboard。";

/** design_inspector 上部固定アクション — 選択ノードのデザイン設定を保存。 */
export const UX_DESIGN_NODE_SAVE_LABEL = "選択ノードのデザインを保存";

/** layout_patch apply 成功後の次アクション案内 */
export const UX_LAYOUT_APPLIED_HANDOFF_TITLE = "配置の保存が完了しました";
export const UX_LAYOUT_APPLIED_HANDOFF_HINT =
  "配置（layout_patch）は DB に反映済みです。色・文言などのデザインは別途永続化が必要です。";
export const UX_LAYOUT_APPLIED_GO_DESIGN_SAVE = "デザインを永続化（右パネル）";
export const UX_LAYOUT_APPLIED_GO_INSPECTION =
  "投影インスペクションで確認（読み取り専用）";

// ---------------------------------------------------------------------------
// Step 3 — aggregate trigger structured authoring（通常表示用）
// SSOT: runtime-orchestration-ssot.yaml aggregate_trigger_step3_extension_contract
// payload キー・enum 値は変更しない。ラベルのみ日本語化する。
// ---------------------------------------------------------------------------

export const UX_AGGREGATE_TRIGGER_SECTION_TITLE = "集計トリガー（構造化入力）";

export const UX_AGGREGATE_TRIGGER_SECTION_INTRO =
  "Step 3 では構造化ペイロードの選択とプレビューのみを行います。" +
  "閾値判定・実体化・承認の最終判断はサーバー側ランタイムが行います。";

export const UX_AGGREGATE_TRIGGER_NO_TARGETS_ALERT =
  "Step 2 の論理テーブルまたは Step 2.5 の関連が保存されていないため、集計トリガーのペイロードは作成されません。";

export const UX_AGGREGATE_TRIGGER_INVALID_TARGET_ALERT =
  "未定義の対象が選択されているため、集計トリガーのペイロードは作成されません。";

export const UX_AGGREGATE_TRIGGER_NO_PROCESSING_FUNCTION =
  "（登録済みの処理関数がありません）";

export const UX_AGGREGATE_TRIGGER_NO_PROCESSING_FUNCTION_REGISTERED =
  "集計トリガー用の処理関数が未登録です。サーバー側の抽象関数登録が必要です。";

export const UX_AGGREGATE_TRIGGER_NO_PROCESSING_FUNCTION_LOAD_ERROR =
  "集計トリガー用の処理関数一覧を読み込めませんでした。";

export const UX_AGGREGATE_TRIGGER_PROCESSING_FUNCTION_HINT =
  "集計トリガー用に登録済みの抽象関数から選択します。未登録・無効・権限不一致はサーバーが拒否します。";

export const UX_AGGREGATE_TRIGGER_OPERATION_DEFINITION_HINT =
  "Step 1 のトポロジー ID からサーバーが導出する値です（ここでは編集できません）。";

export const UX_AGGREGATE_TRIGGER_NO_AGGREGATE_FIELDS =
  "集計対象に選択できる列がありません。";

export const UX_AGGREGATE_TRIGGER_ADD_COUNTER = "+ カウンターを追加";

export const UX_AGGREGATE_TRIGGER_ADD_PAYLOAD_ENTRY = "+ 実体化ペイロード行を追加";

export const UX_AGGREGATE_TRIGGER_PAYLOAD_ENTRY_REQUIRED =
  "実体化ペイロードは最低 1 行必要です。";

export const UX_AGGREGATE_TRIGGER_PREVIEW_HEADING = "構造化ペイロードのプレビュー";

export const UX_AGGREGATE_TRIGGER_MATERIALIZATION_TARGET_EMPTY =
  "下の一覧から選択";

export const UX_AGGREGATE_TRIGGER_MATERIALIZATION_TARGET_NONE =
  "利用できる列がありません";

export const UX_AGGREGATE_TRIGGER_COUNTER_NAME_PLACEHOLDER = "カウンター名";

export const UX_AGGREGATE_TRIGGER_SOURCE_FIELD_PLACEHOLDER = "宣言済みイベント項目";

export const UX_AGGREGATE_TRIGGER_SELECT_PLACEHOLDER = "（選択）";

export const UX_AGGREGATE_TRIGGER_FIELDS_WARNING =
  "Step 2 / 2.5 の対象に列定義がまだないため、競合キーと実体化ペイロードを構成できません。";

export function uxAggregateTriggerStep2TargetLabel(tableName: string): string {
  return `Step 2 論理テーブル: ${tableName}`;
}

export function uxAggregateTriggerStep25TargetLabel(
  localTableRef: string,
  joinTableRef: string,
): string {
  return `Step 2.5 関連: ${localTableRef} → ${joinTableRef}`;
}

/** enum 値は payload 契約のまま。表示は「日本語（value）」 */
export function uxAggregateTriggerEnumLabel(
  value: string,
  labels: Record<string, string>,
): string {
  const label = labels[value];
  return label ? `${label}（${value}）` : value;
}

export const UX_AGGREGATE_TRIGGER_FIELD_LABELS = {
  canonicalTriggerKind: "トリガー種別",
  triggerSourceDetailKind: "トリガー詳細種別",
  executionScope: "実行スコープ",
  transactionBoundary: "トランザクション境界",
  approvalPolicy: "承認ポリシー",
  aggregateTargetBinding: "集計対象の紐づけ",
  materializationTargetBinding: "実体化対象の紐づけ",
  processingFunctionId: "処理関数 ID",
  operationDefinitionId: "操作定義 ID",
  conflictKeyFields: "競合キー（集計対象の列）",
  deltaMap: "差分カウンター（イベント→集計）",
  thresholdPolicy: "閾値ポリシー",
  minimumTrialCount: "最小試行回数",
  targetRatio: "目標比率",
  ratioNumeratorField: "比率の分子フィールド",
  ratioDenominatorField: "比率の分母フィールド",
  comparisonOperator: "比較演算子",
  materializationPayloadMap: "実体化ペイロード対応",
  targetField: "出力先列",
  source: "値の出所",
  constantValue: "固定値",
  sourceField: "参照元項目",
} as const;

export const UX_AGGREGATE_TRIGGER_CANONICAL_KIND_LABELS: Record<string, string> = {
  cron: "定期実行",
  hook: "フック",
  client: "クライアント操作",
};

export const UX_AGGREGATE_TRIGGER_SOURCE_DETAIL_LABELS: Record<string, string> = {
  client_operation_event: "クライアント操作イベント",
  hook_event: "フックイベント",
  scheduled_cron_event: "定期実行イベント",
  runtime_function_event: "ランタイム関数イベント",
};

export const UX_AGGREGATE_TRIGGER_EXECUTION_SCOPE_LABELS: Record<string, string> = {
  single_event: "単一イベント",
  aggregate_key_partition: "集計キー単位",
  operation_instance: "操作インスタンス単位",
  tenant_or_workspace_partition: "テナント／ワークスペース単位",
};

export const UX_AGGREGATE_TRIGGER_TRANSACTION_BOUNDARY_LABELS: Record<
  string,
  string
> = {
  event_append_only: "イベント追記のみ",
  event_append_and_aggregate_upsert: "イベント追記＋集計更新",
  event_append_aggregate_upsert_and_materialization:
    "イベント追記＋集計更新＋実体化",
};

export const UX_AGGREGATE_TRIGGER_APPROVAL_POLICY_LABELS: Record<string, string> = {
  auto_materialize_when_threshold_passes: "閾値通過時に自動実体化",
  require_backend_approval_before_materialization:
    "実体化前にサーバー承認が必要",
  require_human_approval_before_materialization: "実体化前に人の承認が必要",
};

export const UX_AGGREGATE_TRIGGER_MATERIALIZATION_SOURCE_LABELS: Record<
  string,
  string
> = {
  function_input_event: "関数入力イベント",
  aggregate_current_row: "集計現在行",
  selected_step2_entity_fields: "Step 2 論理テーブルの列",
  selected_step2_5_relation_fields: "Step 2.5 関連の列",
  constant: "固定値",
  generated_value: "生成値",
  runtime_actor_source_metadata: "実行主体メタデータ",
};

export const UX_AGGREGATE_TRIGGER_GENERATED_VALUE_LABELS: Record<string, string> = {
  uuid: "UUID",
  current_timestamp: "現在日時",
  monotonic_sequence_within_scope: "スコープ内連番",
};

export const UX_AGGREGATE_TRIGGER_COMPARISON_OPERATOR_LABELS: Record<
  string,
  string
> = {
  ">": "より大",
  ">=": "以上",
  "<": "より小",
  "<=": "以下",
  "=": "等しい",
  "!=": "等しくない",
};

export const UX_AGGREGATE_TRIGGER_GATE_HINT =
  "「集計・サンプル」で集計キーまたは集計式を設定すると、集計トリガーの詳細設定が利用できます。";

export const UX_AGGREGATE_TRIGGER_OPEN_SETTINGS = "集計トリガーを設定…";

export const UX_AGGREGATE_TRIGGER_CONFIGURED_SUMMARY =
  "集計トリガーの設定があります（クリックして確認・編集）";

export const UX_AGGREGATE_TRIGGER_MODAL_CLOSE = "閉じる";

export const UX_AGGREGATE_TRIGGER_MODAL_DONE = "設定を閉じる";
