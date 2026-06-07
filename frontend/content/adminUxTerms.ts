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
/** テーブル結合意図（任意、通常表示） */
export const UX_FIELD_RELATION_INTENT = "参照データの関連付け（任意）";

/** 行 ID（主キー）— relation 項目の保存値は id、表示はこのラベル */
export const UX_RELATION_ROW_ID_KEY = "id";
export const UX_RELATION_ROW_ID_LABEL = "id (record id)";
/** 操作ごとの対象項目（イベント時、通常表示） */
export const UX_FIELD_OPERATION_ENTITY = "操作ごとの対象項目";

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
