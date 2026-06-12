/**
 * UIBuilder read-only candidate search provider.
 *
 * Purpose: provide debounce_backend_readonly_search candidates for
 * autocomplete_input.primitive / suggest_input.primitive (AutoCompleteInput / SuggestInput).
 *
 * Boundary: read-only only. No mutation / DB write / apply.
 * Uses existing /api/dispatch → admin_runtime canonical route (ui_topology:layout_candidates).
 * No new HTTP endpoints. Typing triggers this provider; no mutations during typing.
 *
 * SSOT: docs/design/ui-ux-primitive-catalog-ssot.yaml
 *   - AutoCompleteInput: candidate_source_boundary: debounce_backend_readonly_search
 *   - SuggestInput:      candidate_source_boundary: debounce_backend_readonly_search
 * SSOT: docs/design/admin-console-workflow-ssot.yaml
 *   - frontend does not own topology judgment
 *   - frontend does not write to DB
 *   - /api/dispatch / ManifestDispatcher is the canonical route for backend calls
 *
 * Differentiated from SearchCombobox (uiBuilderAutocompleteCandidates.ts):
 *   - SearchCombobox: local_derivation_only, no backend fetch
 *   - AutoCompleteInput / SuggestInput: this provider (debounce + backend read-only)
 */

import { dispatchOperation } from "../api/dispatch.ts";

export type SearchCandidateResult =
  | { ok: true; candidates: string[] }
  | { ok: false; reason: string };

/**
 * Search route key / topology name candidates for autocomplete/suggest in UIBuilder authoring.
 * Calls ui_topology:layout_candidates (read-only, existing admin_runtime action; no mutation; no DB write).
 * Caller must debounce before calling — no mutations happen inside this function.
 *
 * Returns filtered candidates matching query (case-insensitive substring).
 * Returns explicit failure state on error — no silent fallback (SSOT: explicit_failure invariant).
 */
export async function searchRouteKeyCandidates(
  query: string,
  token?: string,
): Promise<SearchCandidateResult> {
  const resp = await dispatchOperation(
    {
      operationType: "admin",
      target: "admin",
      layer: "ui_topology",
      action: "layout_candidates",
      triggerKind: "client",
    },
    token,
  );

  if (!resp.success) {
    const errMsg =
      resp.errors?.[0]?.message ??
      resp.errors?.[0]?.Message ??
      "候補検索に失敗しました。backend dispatch に失敗しています。";
    return { ok: false, reason: errMsg };
  }

  const data = resp.emission?.data;
  if (!data) {
    return { ok: false, reason: "候補データがありません。" };
  }

  const candidates = extractRouteKeyCandidatesFromData(data, query);
  if (candidates.length === 0 && query.trim()) {
    return {
      ok: false,
      reason: `"${query}" に一致するルートキー候補が見つかりませんでした。`,
    };
  }
  if (candidates.length === 0) {
    return { ok: false, reason: "ルートキー候補が見つかりませんでした。" };
  }
  return { ok: true, candidates };
}

function extractRouteKeyCandidatesFromData(
  data: Record<string, unknown>,
  query: string,
): string[] {
  // ui_topology:layout_candidates returns an object whose layout entries
  // contain routeKey fields. Support both array and object shapes defensively.
  const arr = Array.isArray(data.layouts)
    ? data.layouts
    : Array.isArray(data.candidates)
    ? data.candidates
    : Array.isArray(data.items)
    ? data.items
    : [];

  const normalizedQuery = query.trim().toLowerCase();
  const seen = new Set<string>();
  const results: string[] = [];

  for (const item of arr) {
    if (typeof item !== "object" || item === null) continue;
    const r = item as Record<string, unknown>;
    const routeKey = typeof r.routeKey === "string" ? r.routeKey : null;
    if (!routeKey) continue;
    if (seen.has(routeKey)) continue;
    seen.add(routeKey);
    if (!normalizedQuery || routeKey.toLowerCase().includes(normalizedQuery)) {
      results.push(routeKey);
    }
  }

  return results.sort();
}
