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
export const UX_FIELD_TABLE_REF = "参照テーブル名";
export const UX_FIELD_IMPORT_SCHEMA = "取り込みルール名";
export const UX_FIELD_NULLABLE = "空欄許可";

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
/** Step 3 項目の使い分け（検索・操作・表示を1表に） */
export const UX_FIELD_STEP3_COLUMN_USAGE = "項目の使い分け";

/** 集計関数候補（topology intent; JsonbManifestRuntime 系と整合） */
export const AGGREGATION_FUNCTION_OPTIONS: readonly { value: string; label: string }[] = [
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
];

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
] as const;
