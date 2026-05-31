/**
 * 管理画面の非開発者向け表示用語（URL・API 名は変更しない）。
 * 内部の manifest / schema 等は技術情報・詳細リファレンスに退避する。
 */
export const UX_IMPORT_SETTINGS = "取り込み設定";
export const UX_DATA_SHAPE = "データの形";
export const UX_UI_BUILDER = "画面づくり";
export const UX_RUNTIME_CHECK = "動作確認";
export const UX_IMPORT_SETTINGS_PAGE = "取り込み設定画面";

/** 推奨フローの Step ラベル（テストとステッパーで共有） */
export const UX_MAIN_FLOW_STEP_LABELS = [
  "ログイン",
  UX_IMPORT_SETTINGS,
  "インポート",
  UX_UI_BUILDER,
  UX_RUNTIME_CHECK,
] as const;
