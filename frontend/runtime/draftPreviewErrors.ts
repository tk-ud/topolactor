/**
 * Maps backend error codes from /api/draft-preview/* to user-facing Japanese messages.
 * No generic fallback — all unknown codes are surfaced with the code name to avoid
 * hiding specificity.
 */
export const DRAFT_PREVIEW_ERROR_MESSAGES: Readonly<Record<string, string>> = {
  LAYOUT_NOT_FOUND: "指定されたレイアウトが存在しません（管理画面で作成・保存してください）",
  MANIFEST_TOPOLOGY_AMBIGUOUS: "同一 topologySystemName に複数の draft manifest があります（曖昧さは許可されません）",
  LAYOUT_HAS_NO_NODES: "このレイアウトにはノードがありません（UIビルダーで部品を配置してからApplyしてください）",
  LAYOUT_NODES_NOT_FOUND: "レイアウトの tensor 行が見つかりません（UIビルダーで部品を配置可能化し、Applyしてください）",
  LAYOUT_NODES_AMBIGUOUS_SELECTOR: "同一 layoutId に複数の tensor 行があります。UIビルダーで正しい routeKey / packageId を選び直してください",
  PROJECTION_FAILED: "コンテンツのスロット投影に失敗しました（レイアウト構造またはドラフトデータを確認してください）",
  UNAUTHORIZED: "認証が必要です（セッションを更新してください）",
  MALFORMED_LAYOUT_ID: "レイアウトIDの形式が不正です（有効なレイアウトを選択してください）",
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
