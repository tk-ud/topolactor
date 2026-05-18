/**
 * Admin API types and client functions for:
 *   - context_token_registry (hub Registry for discrete tokens)
 *
 * Context route recommendation policy is NOT managed here.
 * Policy lives in function_parameters (topology data store) and is loaded
 * by the backend resolver via TopologyRepository.LoadFunctionParameterAsync.
 *
 * When DEMO_BACKEND_URL is not set, endpoints return 501 — functions return null.
 * When no JWT is available, endpoints return 401 — functions throw.
 * Callers must handle null (not bound) and errors (unauth / network) explicitly.
 */

const SESSION_TOKEN_KEY = "demo_jwt_token";

function getAuthHeaders(): Record<string, string> {
  const token =
    typeof globalThis.sessionStorage !== "undefined"
      ? sessionStorage.getItem(SESSION_TOKEN_KEY)
      : null;
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  return headers;
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
  const res = await fetch("/api/admin/context-token-registry", {
    headers: getAuthHeaders(),
  });
  if (res.status === 501) return null;
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return await res.json() as ContextToken[];
}

export async function createContextToken(
  token: Omit<ContextToken, "tokenId" | "status">,
): Promise<{ ok: boolean; message: string; tokenId?: string }> {
  const res = await fetch("/api/admin/context-token-registry", {
    method: "POST",
    headers: getAuthHeaders(),
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
    {
      method: "POST",
      headers: getAuthHeaders(),
    },
  );
  const body = await res.json() as { ok: boolean; message: string };
  return body;
}
