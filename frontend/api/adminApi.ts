/**
 * Admin API types and client functions for:
 *   - context_route_config  (recommendation engine tuning parameters)
 *   - context_token_registry (hub Registry for discrete tokens)
 *
 * These are the SSOT registries for the context route recommendation runtime.
 * All values here correspond to ContextRouteConfig and ContextTokenRecord in
 * the backend schema layer.
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

export const defaultContextRouteConfig: ContextRouteConfig = {
  minSimilarity: 0.05,
  topK: 50,
  minNeighbors: 10,
  recentDays: 90,
  maxCandidatesShown: 5,
  baselineWeight: 0.5,
  neighborWeight: 0.5,
};

export async function fetchContextRouteConfig(): Promise<ContextRouteConfig> {
  try {
    const res = await fetch("/api/admin/context-route-config");
    if (!res.ok) return { ...defaultContextRouteConfig };
    return await res.json() as ContextRouteConfig;
  } catch {
    return { ...defaultContextRouteConfig };
  }
}

export async function saveContextRouteConfig(
  config: ContextRouteConfig,
): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await fetch("/api/admin/context-route-config", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(config),
    });
    const body = await res.json() as { ok: boolean; message: string };
    return body;
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { ok: false, message };
  }
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

export async function fetchContextTokens(): Promise<ContextToken[]> {
  try {
    const res = await fetch("/api/admin/context-token-registry");
    if (!res.ok) return [];
    return await res.json() as ContextToken[];
  } catch {
    return [];
  }
}

export async function createContextToken(
  token: Omit<ContextToken, "tokenId" | "status">,
): Promise<{ ok: boolean; message: string; tokenId?: string }> {
  try {
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
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { ok: false, message };
  }
}

export async function deprecateContextToken(
  tokenId: string,
): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await fetch(
      `/api/admin/context-token-registry/${tokenId}/deprecate`,
      { method: "POST" },
    );
    const body = await res.json() as { ok: boolean; message: string };
    return body;
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { ok: false, message };
  }
}
