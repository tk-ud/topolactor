/**
 * 管理画面の非開発者向け表示用語（URL・API 名は変更しない）。
 * 内部の manifest / schema 等は技術情報・詳細リファレンスに退避する。
 */
export const UX_CONTENTS = "ページ作成";
export const UX_CONTENTS_PAGE = "ページ作成画面";
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
export const UX_FIELD_IMPORT_SCHEMA = "取り込みデータ定義名";
export const UX_FIELD_NULLABLE = "空欄許可";

/** 推奨フローの Step ラベル（テストとステッパーで共有） */
export const UX_MAIN_FLOW_STEP_LABELS = [
  "ログイン",
  "新規 manifest 作成",
  UX_UI_BUILDER,
  UX_HUB_MANIFESTS,
] as const;
