import type { ContentBundleListItem } from "../api/adminApi.ts";

/**
 * Hub-relation destination options for /admin/manifests.
 * Author-facing label is page summary (fallback: label), not raw hub id.
 */
export function hubDestinationPickerOptions(
  hubs: ContentBundleListItem[],
): ContentBundleListItem[] {
  return hubs.filter(
    (h) => h.summary.trim().length > 0 || h.label.trim().length > 0,
  );
}

export function hubDestinationOptionLabel(hub: ContentBundleListItem): string {
  const summary = hub.summary.trim();
  if (summary.length > 0) return summary;
  return hub.label.trim();
}

export type HubNavigationErrorLike = { code?: string; message?: string };

/**
 * Friendly, normal-view Japanese text for a known hub_navigation lifecycle error code (create /
 * update / deprecate / reorder). Raw backend messages interpolate internal vocabulary
 * (related_hub_id, source hub_id, hub_relations, topology manifest) not meant for normal-view
 * primary display -- those, and any unmapped/codeless error (network failures, unexpected
 * response shapes), stay reachable only through the raw code/message inside an explicit
 * technical disclosure, never here.
 */
const HUB_NAVIGATION_ERROR_FRIENDLY_TEXT: Record<string, string> = {
  MANIFEST_NOT_FOUND: "選択した設定が見つかりませんでした。画面を再読み込みしてください。",
  HUB_NOT_FOUND: "遷移先の画面が見つかりませんでした。選び直してください。",
  SELF_LOOP: "自分自身への遷移は登録できません。別の画面を選択してください。",
  SEQUENCE_CONFLICT: "この順序番号はすでに使われています。別の番号を指定してください。",
  HUB_RELATION_NOT_FOUND: "対象のナビ遷移が見つからないか、すでに無効化されています。",
  HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST:
    "この設定に残る最後のナビ遷移は削除できません（画面の遷移先が無くなってしまいます）。",
};

const HUB_NAVIGATION_ERROR_GENERIC_FALLBACK_TEXT = "処理に失敗しました。時間をおいて再度お試しください。";

export function hubNavigationErrorFriendlyText(e: HubNavigationErrorLike): string {
  return (e.code && HUB_NAVIGATION_ERROR_FRIENDLY_TEXT[e.code]) ?? HUB_NAVIGATION_ERROR_GENERIC_FALLBACK_TEXT;
}

export type HubNavigationLifecycleAction = "create" | "update" | "deprecate";

/**
 * Friendly, normal-view Japanese text for a hub_navigation lifecycle success (create / update /
 * deprecate). The raw backend carrier message ("Hub relation created."/"updated."/"deprecated.")
 * is untranslated English using the backend's own "hub relation" vocabulary, not this surface's
 * user-facing "ナビ遷移" term -- callers already know which action just succeeded, so this maps
 * directly from that action rather than pattern-matching the raw text. The raw message stays
 * reachable via an explicit technical disclosure at the call site, never deleted.
 */
const HUB_NAVIGATION_SUCCESS_FRIENDLY_TEXT: Record<HubNavigationLifecycleAction, string> = {
  create: "ナビ遷移を登録しました。",
  update: "ナビ遷移を更新しました。",
  deprecate: "ナビ遷移を無効化しました。",
};

export function hubNavigationSuccessFriendlyText(action: HubNavigationLifecycleAction): string {
  return HUB_NAVIGATION_SUCCESS_FRIENDLY_TEXT[action];
}
