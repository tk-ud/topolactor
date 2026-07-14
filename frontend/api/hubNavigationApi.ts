/**
 * hubNavigationApi.ts — read-only navigation link-list fallback for authenticated surfaces with
 * no business projection of their own (e.g. Normal Dashboard Home). Backed by the existing
 * hub_relation authority (HubNavigationResolver); never a frontend-hardcoded link list.
 */
import type { AuthError } from "./authApi.ts";

export type HubRelationNavigationLink = {
  relatedHubId: string;
  relatedHubLabel: string;
  sequencePosition: number;
  targetManifestId?: string | null;
};

export type HubRelationNavigationLinksResponse = {
  success: boolean;
  links: HubRelationNavigationLink[];
  errors?: AuthError[];
};

/** GET /api/hub-navigation/relations — any authenticated JWT. */
export async function fetchHubRelationNavigationLinks(
  token: string,
): Promise<HubRelationNavigationLinksResponse> {
  const response = await fetch("/api/hub-navigation/relations", {
    method: "GET",
    credentials: "include",
    headers: { Authorization: `Bearer ${token}` },
  });
  const json = await response.json();
  if (typeof json === "object" && json !== null && "success" in json) {
    return json as HubRelationNavigationLinksResponse;
  }
  return { success: false, links: [], errors: [{ message: "unexpected response shape" }] };
}
