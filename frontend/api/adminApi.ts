/**
 * Admin API types and client functions for:
 *   - context_route_config  (recommendation engine tuning parameters)
 *   - context_token_registry (hub Registry for discrete tokens)
 *
 * These are the SSOT registries for the context route recommendation runtime.
 * All values here correspond to ContextRouteConfig and ContextTokenRecord in
 * the backend schema layer.
 *
 * When an endpoint returns 501 (not yet bound to backend), functions return null
 * rather than hardcoded fallback data.  Callers must handle null explicitly.
 */

// ---------------------------------------------------------------------------
// context_route_config
// ---------------------------------------------------------------------------

export type ContextRouteConfig = {
  minSimilarity: number;
  topK: number;
  minNeighbors: number;
  recentDays: number;
  maxCandidatesShown: number;
  baselineWeight: number;
  neighborWeight: number;
};

/** Returns null when the registry endpoint is not yet bound (501). */
export async function fetchContextRouteConfig(): Promise<ContextRouteConfig | null> {
  const res = await fetch("/api/admin/context-route-config");
  if (res.status === 501) return null;
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return await res.json() as ContextRouteConfig;
}

export async function saveContextRouteConfig(
  config: ContextRouteConfig,
): Promise<{ ok: boolean; message: string }> {
  const res = await fetch("/api/admin/context-route-config", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(config),
  });
  const body = await res.json() as { ok: boolean; message: string };
  return body;
}

// ---------------------------------------------------------------------------
// context_token_registry
// ---------------------------------------------------------------------------

export type ContextToken = {
  tokenId: string;
  label: string;
  group: string | null;
  value: number;
  status: "active" | "deprecated";
};

/** Returns null when the registry endpoint is not yet bound (501). */
export async function fetchContextTokens(): Promise<ContextToken[] | null> {
  const res = await fetch("/api/admin/context-token-registry");
  if (res.status === 501) return null;
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return await res.json() as ContextToken[];
}

export async function createContextToken(
  token: Omit<ContextToken, "tokenId" | "status">,
): Promise<{ ok: boolean; message: string; tokenId?: string }> {
  const res = await fetch("/api/admin/context-token-registry", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(token),
  });
  const body = await res.json() as {
    ok: boolean;
    message: string;
    tokenId?: string;
  };
  return body;
}

export async function deprecateContextToken(
  tokenId: string,
): Promise<{ ok: boolean; message: string }> {
  const res = await fetch(
    `/api/admin/context-token-registry/${tokenId}/deprecate`,
    { method: "POST" },
  );
  const body = await res.json() as { ok: boolean; message: string };
  return body;
}
