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
