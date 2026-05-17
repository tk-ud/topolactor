/**
 * Admin API types and client functions for:
 *   - context_token_registry (hub Registry for discrete tokens)
 *
 * Context route recommendation policy is NOT managed here.
 * Policy lives in function_parameters (topology data store) and is loaded
 * by the backend resolver via TopologyRepository.LoadFunctionParameterAsync.
 *
 * When an endpoint returns 501 (not yet bound to backend), functions return null
 * rather than hardcoded fallback data.  Callers must handle null explicitly.
 */

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
export async function fetchContextTokens(): Promise<ContextToken[]> {
  const res = await fetch("/api/admin/context-token-registry");
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
