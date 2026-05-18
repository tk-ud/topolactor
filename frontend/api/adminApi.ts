/**
 * Admin API types and client functions for:
 *   - context_token_registry (hub Registry for discrete tokens)
 *   - registry vector validation (Registrar draft/promote flow)
 *
 * Context route recommendation policy is NOT managed here.
 * Policy lives in function_parameters (topology data store) and is loaded
 * by the backend resolver via TopologyRepository.LoadFunctionParameterAsync.
 *
 * When DEMO_BACKEND_URL is not set, endpoints return 501 — fetchContextTokens returns null.
 * When no JWT is available, endpoints return 401 — all functions throw "HTTP 401".
 * Callers must handle null (not bound) and errors (unauth / network / conflict) explicitly.
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

/** Returns null when the registry endpoint is not yet bound (501). Throws on other errors including 401. */
export async function fetchContextTokens(): Promise<ContextToken[] | null> {
  const res = await fetch("/api/admin/context-token-registry", {
    headers: getAuthHeaders(),
  });
  if (res.status === 501) return null;
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return await res.json() as ContextToken[];
}

/** Throws "HTTP 401" when unauthenticated. Returns response body for other statuses including errors. */
export async function createContextToken(
  token: Omit<ContextToken, "tokenId" | "status">,
): Promise<{ ok: boolean; message: string; tokenId?: string; errorCode?: string }> {
  const res = await fetch("/api/admin/context-token-registry", {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(token),
  });
  if (!res.ok && res.status === 401) throw new Error(`HTTP ${res.status}`);
  const body = await res.json() as {
    ok: boolean;
    message: string;
    tokenId?: string;
    errorCode?: string;
  };
  return body;
}

/** Throws "HTTP 401" when unauthenticated. Returns response body for other statuses including errors. */
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
  if (!res.ok && res.status === 401) throw new Error(`HTTP ${res.status}`);
  const body = await res.json() as { ok: boolean; message: string };
  return body;
}

// ---------------------------------------------------------------------------
// Registry vector validation
// ---------------------------------------------------------------------------

export type RegistryVectorNeighbor = {
  registryId: string;
  name: string;
  cosineScore: number;
  matchedIds: string[];
  reason: string;
};

export type RegistryVectorValidationResult = {
  validationClass:
    | "pass"
    | "related_existing_registry"
    | "near_duplicate_vector"
    | "duplicate_vector"
    | "zero_vector"
    | "explicit_error";
  isBlocking: boolean;
  neighbors: RegistryVectorNeighbor[];
  statusDetail?: string;
};

/**
 * Validates a candidate registry ID array against existing registry rows.
 * Returns null when the backend is not configured (501).
 * Throws "HTTP 401" when unauthenticated.
 */
export async function validateRegistryVector(
  registryTable: string,
  queryIds: string[],
): Promise<RegistryVectorValidationResult | null> {
  const res = await fetch("/api/admin/registry-vector-validate", {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify({ registryTable, queryIds }),
  });
  if (res.status === 501) return null;
  if (!res.ok && res.status === 401) throw new Error(`HTTP ${res.status}`);
  return await res.json() as RegistryVectorValidationResult;
}
