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

async function callAdminDispatch(request: DispatchRequest): Promise<Emission | null> {
  const res = await fetch("/api/dispatch", {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(request),
  });

  if (!res.ok && res.status === 401) throw new Error(`HTTP ${res.status}`);

  const body = await res.json() as { success?: boolean; emission?: Emission | null; errors?: ValidationError[] };
  if (res.status === 501) {
    const code = body.errors?.[0]?.code ?? body.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
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
  if (emission === null) return null;
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
  if (emission === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
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
  if (emission === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
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
  if (emission === null) return null;
  return (emission.data ?? null) as RegistryVectorValidationResult | null;
}

// ---------------------------------------------------------------------------
// Admin CSV/JSON Import — M6 validate-preview-apply
// ---------------------------------------------------------------------------

export type AdminImportManifestItem = {
  manifestId: string;
  status: string;
  createdAt: string;
};

export type AdminImportSchemaItem = {
  schemaId: string;
  name: string;
};

export type AdminImportRecordPreview = {
  rowIndex: number;
  records: Record<string, unknown>;
  /** manifest/schema conformity status — not business state or hub lifecycle state */
  status: "valid" | "invalid";
  validationErrors: string[];
};

export type AdminImportPreviewResult = {
  ok: boolean;
  snapshotId: string;
  sourceType: string;
  manifestId: string;
  schemaId: string;
  validCount: number;
  invalidCount: number;
  records: AdminImportRecordPreview[];
};

export type AdminImportApplyResult = {
  ok: boolean;
  applyLogId: string;
  snapshotId: string;
  appliedRecordCount: number;
  status: string;
  note: string;
};

export async function listImportManifests(): Promise<AdminImportManifestItem[] | null> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "admin_csv_json_import",
    action: "list_manifests",
  });
  if (emission === null) return null;
  return (emission.data ?? null) as AdminImportManifestItem[] | null;
}

export async function listImportSchemas(): Promise<AdminImportSchemaItem[] | null> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "admin_csv_json_import",
    action: "list_schemas",
  });
  if (emission === null) return null;
  return (emission.data ?? null) as AdminImportSchemaItem[] | null;
}

export async function uploadImportPreview(
  sourceType: "csv" | "json",
  fileName: string,
  manifestId: string,
  schemaId: string,
  content: string,
): Promise<AdminImportPreviewResult> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "admin_csv_json_import",
    action: "upload_preview",
    payload: { sourceType, fileName, manifestId, schemaId, content },
  });
  if (emission === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return emission.data as AdminImportPreviewResult;
}

export async function applyImport(snapshotId: string): Promise<AdminImportApplyResult> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "admin_csv_json_import",
    action: "apply",
    payload: { snapshotId },
  });
  if (emission === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return emission.data as AdminImportApplyResult;
}
