/**
 * Maps backend error codes from /api/draft-preview/* to user-facing Japanese messages.
 * No generic fallback — all unknown codes are surfaced with the code name to avoid
 * hiding specificity.
 */
export const DRAFT_PREVIEW_ERROR_MESSAGES: Readonly<Record<string, string>> = {
  LAYOUT_NOT_FOUND: "指定されたレイアウトが存在しません（管理画面で作成・保存してください）",
  DRAFT_NOT_FOUND: "指定されたドラフトが存在しません（管理画面でドラフトを登録してください）",
  ROUTE_KEY_MISMATCH: "レイアウトとドラフトのルートキーが一致しません（対応するレイアウトとドラフトを選んでください）",
  LAYOUT_HAS_NO_NODES: "このレイアウトにはノードがありません（UIビルダーで部品を配置してからApplyしてください）",
  LAYOUT_NODES_NOT_FOUND: "このレイアウトにはノードがありません（UIビルダーで部品を配置してからApplyしてください）",
  PROJECTION_FAILED: "コンテンツのスロット投影に失敗しました（レイアウト構造またはドラフトデータを確認してください）",
  UNAUTHORIZED: "認証が必要です（セッションを更新してください）",
  MALFORMED_LAYOUT_ID: "レイアウトIDの形式が不正です（有効なレイアウトを選択してください）",
  MALFORMED_DRAFT_ID: "ドラフトIDの形式が不正です（有効なドラフトを選択してください）",
};

/**
 * Returns a specific Japanese error message for a known error code,
 * falling back to the API message or a labeled unknown-code message.
 * Never returns a generic "failed" without the code.
 */
export function resolvePreviewErrorMessage(code: string, fallback?: string): string {
  const known = DRAFT_PREVIEW_ERROR_MESSAGES[code];
  if (known) return known;
  if (fallback) return `${fallback}（コード: ${code}）`;
  return `プレビュー失敗: ${code}`;
}
