import type { DispatchRequest, Emission, ValidationError } from "./dispatch.ts";

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

async function callAdminDispatch(request: DispatchRequest): Promise<Emission> {
  const res = await fetch("/api/dispatch", {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(request),
  });

  if (!res.ok && res.status === 401) throw new Error(`HTTP ${res.status}`);

  const body = await res.json() as { success?: boolean; emission?: Emission | null; errors?: ValidationError[] };
  if (res.status === 501) {
    const code = body.errors?.[0]?.code ?? body.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return { data: null } as Emission;
  }

  if (!res.ok || !body.success || !body.emission) {
    const msg = body.errors?.[0]?.message ?? body.errors?.[0]?.Message ?? `HTTP ${res.status}`;
    throw new Error(msg);
  }

  return body.emission;
}

export type ContextToken = {
  tokenId: string;
  label: string;
  group: string | null;
  value: number;
  status: "active" | "deprecated";
};

export async function fetchContextTokens(): Promise<ContextToken[] | null> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "context_token_registry",
    action: "list",
  });
  return (emission.data ?? null) as ContextToken[] | null;
}

export async function createContextToken(
  token: Omit<ContextToken, "tokenId" | "status">,
): Promise<{ ok: boolean; message: string; tokenId?: string; errorCode?: string }> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "context_token_registry",
    action: "create",
    payload: token,
  });
  return emission.data as { ok: boolean; message: string; tokenId?: string; errorCode?: string };
}

export async function deprecateContextToken(
  tokenId: string,
): Promise<{ ok: boolean; message: string }> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "context_token_registry",
    action: "deprecate",
    idOrHubId: tokenId,
  });
  return emission.data as { ok: boolean; message: string };
}

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

export async function validateRegistryVector(
  registryTable: string,
  queryIds: string[],
): Promise<RegistryVectorValidationResult | null> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "registry_vector",
    action: "validate",
    payload: { registryTable, queryIds },
  });
  return (emission.data ?? null) as RegistryVectorValidationResult | null;
}
