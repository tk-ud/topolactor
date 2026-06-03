import type { DispatchRequest, Emission, ValidationError } from "./dispatch.ts";
import { validationErrorText } from "./dispatch.ts";

import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";
import { queueAdminClientCommand } from "../runtime/frontendScheduler.ts";

function getToken(): string | undefined {
  if (typeof globalThis.sessionStorage === "undefined") return undefined;
  return sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;
}

async function callAdminDispatch(request: Omit<DispatchRequest, "triggerKind">): Promise<Emission | null> {
  const result = await queueAdminClientCommand(request, getToken());
  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
    const msg = result.errors?.[0]?.message ?? result.errors?.[0]?.Message ?? "dispatch failed";
    throw new Error(msg);
  }
  if (!result.emission) throw new Error("dispatch: no emission in response");
  return result.emission;
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
  manifestKey?: string | null;
  hubId?: string | null;
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

// ---------------------------------------------------------------------------
// Manifest management — admin manifest editor surface
// ---------------------------------------------------------------------------

export type AdminManifestListItem = {
  manifestId: string;
  status: string;
  relationRegistryId: string | null;
  role: string | null;
  target: string | null;
  layer: string | null;
  action: string | null;
  runtimeDestination: string | null;
  createdAt: string;
  updatedAt: string;
};

export type AdminManifestTopologySummary = {
  dispatcherMapping: {
    role: string;
    target: string;
    layer: string;
    action: string;
  } | null;
  runtimeMapping: { runtimeDestination: string } | null;
  projectionConstructorMapping: { hasProjectionDefinition: boolean } | null;
  entryTypes: string[];
};

export type AdminManifestDetail = {
  manifestId: string;
  status: string;
  relationRegistryId: string | null;
  summary: AdminManifestTopologySummary;
  topologyRawJson: string;
  createdAt: string;
  updatedAt: string;
};

export type AdminManifestValidationIssue = {
  code: string;
  message: string;
  isBlocking: boolean;
};

export type AdminManifestValidateResult = {
  valid: boolean;
  isBlocking: boolean;
  issues: AdminManifestValidationIssue[];
  summary: AdminManifestTopologySummary | null;
};

export type AdminManifestLifecycleResult = {
  ok: boolean;
  manifestId: string;
  status: string;
  message: string;
  errorCode?: string;
};

export type AdminManifestDraftInput = {
  relationRegistryId?: string;
  role: string;
  target: string;
  layer: string;
  action: string;
  runtimeDestination: string;
  projectionDefinition?: Record<string, unknown> | null;
  screenOperationKind?: string;
};

export type AdminManifestScreenColumnInput = {
  name: string;
  dataType: string;
  nullable: boolean;
};

export type RelationIntentInput = {
  joinTableRef: string;
  localKey: string;
  remoteKey: string;
};

export type OperationEntityBindingInput = {
  operationKind: string;
  entityTargetColumn: string;
};

export type AdminManifestScreenDataShapeInput = {
  manifestId: string;
  tableRef?: string;
  /** @deprecated use tableRef — sent for backward compatibility with older topology entries */
  dbTableName?: string;
  importSchemaName?: string;
  searchTargets?: string[];
  /** Structured search key columns. Normal-view selection; maps to searchTargets on backend. */
  searchKeyColumns?: string[];
  aggregationSpec?: string;
  /** Structured aggregation key. "group by" must not appear in UX vocabulary. */
  aggregationKey?: string;
  /** Structured display columns. */
  displayColumns?: string[];
  columns?: AdminManifestScreenColumnInput[];
  /** Primary kind for dispatcher refresh (first of screenOperationKinds when set). */
  screenOperationKind?: string;
  /** Multi-select operation kinds (SSOT step 3). */
  screenOperationKinds?: string[];
  /** User-facing topology label (SSOT step 1 minimum). */
  userFacingTopologyLabel?: string;
  /** Structured relation/join intents for draft data-shape only (not created-manifest relations). */
  relationIntents?: RelationIntentInput[];
  /** Per-operation entity target at event time (SSOT step 3). */
  operationEntityBindings?: OperationEntityBindingInput[];
  /** Initial-data candidates as screen-data-shape topology intent. Actual row insertion belongs to content_bundle. */
  initialDataRows?: Record<string, string>[];
};

const RUNTIME_DESTINATION_OPTIONS = [
  "topology_transform_runtime",
  "admin_runtime",
  "sse_projection_runtime",
] as const;

export { RUNTIME_DESTINATION_OPTIONS };

async function callAdminManifestOp(
  action: string,
  payload?: unknown,
): Promise<{ success: boolean; emission?: { data?: unknown } | null; errors?: { code?: string; message?: string }[] } | null> {
  const result = await queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer: "manifest",
    action,
    payload: payload != null ? payload as Record<string, unknown> : undefined,
  }, getToken());
  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
    const msg = result.errors?.[0] ? validationErrorText(result.errors[0]) : `manifest ${action} failed`;
    throw new Error(msg);
  }
  return { success: result.success ?? true, emission: result.emission, errors: result.errors };
}

export async function listAdminManifests(status?: string): Promise<AdminManifestListItem[] | null> {
  const body = await callAdminManifestOp("list", status ? { status } : undefined);
  if (body === null) return null;
  return (body.emission?.data ?? []) as AdminManifestListItem[];
}

export async function getAdminManifest(manifestId: string): Promise<AdminManifestDetail | null> {
  const body = await callAdminManifestOp("get", { manifestId });
  if (body === null) return null;
  return body.emission?.data as AdminManifestDetail;
}

export async function validateAdminManifest(manifestId: string): Promise<AdminManifestValidateResult | null> {
  const body = await callAdminManifestOp("validate", { manifestId });
  if (body === null) return null;
  return body.emission?.data as AdminManifestValidateResult;
}

export async function createAdminManifestDraft(
  input: AdminManifestDraftInput,
): Promise<AdminManifestDetail> {
  const body = await callAdminManifestOp("create_draft", input);
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return body.emission?.data as AdminManifestDetail;
}

export async function updateAdminManifestDraft(
  manifestId: string,
  input: AdminManifestDraftInput,
): Promise<AdminManifestDetail> {
  const body = await callAdminManifestOp("update_draft", { manifestId, ...input });
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return body.emission?.data as AdminManifestDetail;
}

export async function promoteAdminManifest(manifestId: string): Promise<AdminManifestLifecycleResult | null> {
  const body = await callAdminManifestOp("promote", { manifestId });
  if (body === null) return null;
  if (!body.success && !body.emission?.data) {
    const msg = body.errors?.[0]?.message ?? "promote failed";
    throw new Error(msg);
  }
  return body.emission?.data as AdminManifestLifecycleResult;
}

export async function assignAdminManifestHubGrouping(
  manifestId: string,
  hubId: string,
  manifestKey: string,
): Promise<AdminManifestDetail> {
  const body = await callAdminManifestOp("assign_hub_grouping", { manifestId, hubId, manifestKey });
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return body.emission?.data as AdminManifestDetail;
}

export async function assignAdminManifestScreenDataShape(
  input: AdminManifestScreenDataShapeInput,
): Promise<AdminManifestDetail> {
  const body = await callAdminManifestOp("assign_screen_data_shape", input);
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return body.emission?.data as AdminManifestDetail;
}

export async function deprecateAdminManifest(manifestId: string): Promise<AdminManifestLifecycleResult | null> {
  const body = await callAdminManifestOp("deprecate", { manifestId });
  if (body === null) return null;
  if (!body.success && !body.emission?.data) {
    const msg = body.errors?.[0]?.message ?? "deprecate failed";
    throw new Error(msg);
  }
  return body.emission?.data as AdminManifestLifecycleResult;
}

// ---------------------------------------------------------------------------
// Promotion manifest metadata — disclosure / campaign intent editor surface
// ---------------------------------------------------------------------------

export type AdminPromotionManifestListItem = {
  manifestId: string;
  status: string;
  manifestKey: string;
  versionLabel: string;
  hasDisclosure: boolean;
  createdAt: string;
  updatedAt: string;
};

export type AdminPromotionManifestMetadata = {
  manifestKey: string;
  versionLabel: string;
  disclosureText: string;
  disclosureCategoryLabel: string | null;
  placementKey: string;
  projectionSurfaceType: string;
  activationPolicyType: string;
  activationConditionExpression: string | null;
  targetTopologyRefs: {
    packageId: string;
    schemaId: string;
    componentId: string;
  }[];
};

export type AdminPromotionManifestDetail = {
  manifestId: string;
  status: string;
  metadata: AdminPromotionManifestMetadata | null;
  createdAt: string;
  updatedAt: string;
};

export type AdminPromotionManifestValidateResult = {
  valid: boolean;
  isBlocking: boolean;
  issues: AdminManifestValidationIssue[];
  metadata: AdminPromotionManifestMetadata | null;
};

export type AdminPromotionManifestUpdateInput = {
  manifestId: string;
  manifestKey: string;
  versionLabel: string;
  disclosureText: string;
  disclosureCategoryLabel?: string | null;
  placementKey: string;
  projectionSurfaceType: string;
  activationPolicyType: string;
  activationConditionExpression?: string | null;
  targetTopologyRefs: {
    packageId: string;
    schemaId: string;
    componentId: string;
  }[];
};

async function callAdminPromotionManifestOp(
  action: string,
  payload?: unknown,
): Promise<{ success: boolean; emission?: { data?: unknown } | null; errors?: { code?: string; message?: string }[] } | null> {
  const result = await queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer: "promotion_manifest",
    action,
    payload: payload != null ? payload as Record<string, unknown> : undefined,
  }, getToken());
  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
  }
  return result as { success: boolean; emission?: { data?: unknown } | null; errors?: { code?: string; message?: string }[] };
}

export async function listAdminPromotionManifests(
  status?: string,
): Promise<AdminPromotionManifestListItem[] | null> {
  const body = await callAdminPromotionManifestOp("list", status ? { status } : undefined);
  if (body === null) return null;
  if (!body.success) {
    const msg = body.errors?.[0]?.message ?? "promotion manifest list failed";
    throw new Error(msg);
  }
  return (body.emission?.data ?? []) as AdminPromotionManifestListItem[];
}

export async function getAdminPromotionManifest(
  manifestId: string,
): Promise<AdminPromotionManifestDetail | null> {
  const body = await callAdminPromotionManifestOp("get", { manifestId });
  if (body === null) return null;
  if (!body.success) {
    const msg = body.errors?.[0]?.message ?? "promotion manifest get failed";
    throw new Error(msg);
  }
  return body.emission?.data as AdminPromotionManifestDetail;
}

export async function validateAdminPromotionManifest(
  manifestId: string,
): Promise<AdminPromotionManifestValidateResult | null> {
  const body = await callAdminPromotionManifestOp("validate", { manifestId });
  if (body === null) return null;
  if (!body.success) {
    const msg = body.errors?.[0]?.message ?? "promotion manifest validate failed";
    throw new Error(msg);
  }
  return body.emission?.data as AdminPromotionManifestValidateResult;
}

export async function updateAdminPromotionManifestDraft(
  input: AdminPromotionManifestUpdateInput,
): Promise<AdminPromotionManifestDetail> {
  const body = await callAdminPromotionManifestOp("update_draft", input);
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  if (!body.success) {
    const msg = body.errors?.[0]?.message ?? "promotion manifest update failed";
    throw new Error(msg);
  }
  return body.emission?.data as AdminPromotionManifestDetail;
}

// ---------------------------------------------------------------------------
// Content bundle — admin topology content management surface
// ---------------------------------------------------------------------------

export type ContentBundleListItem = {
  id: string;
  kind: "hub" | "entity" | "relation" | "hub_relation";
  label: string;
  state: string;
  hubId?: string | null;
  relationIds?: string[] | null;
  summary: string;
};

export type ContentBundleHubDetail = {
  hubId: string;
  stateName: string;
  stateId?: string | null;
  relationRegistryId?: string | null;
  relationLabel?: string | null;
  entityCount: number;
  hubRelationCount: number;
  entityIds: string[];
  summary: string;
};

export type ContentBundleRelationDetail = {
  relationRegistryId: string;
  name: string;
  active: boolean;
  entityCount: number;
  hubRelationCount: number;
  summary: string;
};

export type ContentBundleEntityDetail = {
  entityId: string;
  label: string;
  stateName: string;
  stateId?: string | null;
  hubId: string;
  hubLabel: string;
  relationIds: string[];
  relationLabels: string[];
  entityJsonb: string;
  summary: string;
};

export type ContentBundleStateItem = {
  stateId: string;
  name: string;
  owner?: string | null;
};

export type ContentBundleDraftDetail = {
  draftId: string;
  status: string;
  hubId: string;
  entityJsonb: string;
  relationIds: string[];
  stateName?: string | null;
  stateId?: string | null;
  promotedEntityId?: string | null;
  createdAt: string;
  updatedAt: string;
};

export type ContentBundleValidationIssue = {
  code: string;
  message: string;
  isBlocking: boolean;
};

export type ContentBundleValidateResult = {
  valid: boolean;
  isBlocking: boolean;
  issues: ContentBundleValidationIssue[];
};

export type ContentBundlePreviewResult = {
  draftId: string;
  label: string;
  hubId: string;
  hubLabel?: string | null;
  relationIds: string[];
  relationLabels: string[];
  stateName?: string | null;
  entityJsonb: string;
  validation: ContentBundleValidateResult;
  canPromote: boolean;
};

export type ContentBundleLifecycleResult = {
  ok: boolean;
  draftId: string;
  entityId?: string | null;
  status: string;
  message: string;
  readback?: ContentBundleEntityDetail | null;
  errorCode?: string | null;
};

export type ContentBundleDraftInput = {
  hubId: string;
  entityJsonb: Record<string, unknown>;
  relationIds: string[];
  stateName: string;
};

export type ContentBundleUpdateDraftInput = ContentBundleDraftInput & {
  draftId: string;
};

async function callAdminContentBundleOp(
  action: string,
  payload?: unknown,
): Promise<{ success: boolean; emission?: { data?: unknown } | null; errors?: { code?: string; message?: string }[] } | null> {
  const result = await queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer: "content_bundle",
    action,
    payload: payload != null ? payload as Record<string, unknown> : undefined,
  }, getToken());
  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
  }
  return result as { success: boolean; emission?: { data?: unknown } | null; errors?: { code?: string; message?: string }[] };
}

export async function listContentHubs(): Promise<ContentBundleListItem[] | null> {
  const body = await callAdminContentBundleOp("list_hubs");
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "list hubs failed");
  return (body.emission?.data ?? []) as ContentBundleListItem[];
}

export async function listContentEntities(): Promise<ContentBundleListItem[] | null> {
  const body = await callAdminContentBundleOp("list_entities");
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "list entities failed");
  return (body.emission?.data ?? []) as ContentBundleListItem[];
}

export async function listContentRelations(): Promise<ContentBundleListItem[] | null> {
  const body = await callAdminContentBundleOp("list_relations");
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "list relations failed");
  return (body.emission?.data ?? []) as ContentBundleListItem[];
}

export async function listContentHubRelations(): Promise<ContentBundleListItem[] | null> {
  const body = await callAdminContentBundleOp("list_hub_relations");
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "list hub relations failed");
  return (body.emission?.data ?? []) as ContentBundleListItem[];
}

export async function listContentStates(): Promise<ContentBundleStateItem[] | null> {
  const body = await callAdminContentBundleOp("list_states");
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "list states failed");
  return (body.emission?.data ?? []) as ContentBundleStateItem[];
}

export async function getContentEntity(entityId: string): Promise<ContentBundleEntityDetail | null> {
  const body = await callAdminContentBundleOp("get_entity", { entityId });
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "get entity failed");
  return body.emission?.data as ContentBundleEntityDetail;
}

export async function getContentHub(hubId: string): Promise<ContentBundleHubDetail | null> {
  const body = await callAdminContentBundleOp("get_hub", { hubId });
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "get hub failed");
  return body.emission?.data as ContentBundleHubDetail;
}

export async function getContentRelation(relationRegistryId: string): Promise<ContentBundleRelationDetail | null> {
  const body = await callAdminContentBundleOp("get_relation", { relationRegistryId });
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "get relation failed");
  return body.emission?.data as ContentBundleRelationDetail;
}

export async function searchContentBundle(
  keyword?: string,
  kind?: string,
  state?: string,
): Promise<ContentBundleListItem[] | null> {
  const body = await callAdminContentBundleOp("search", { keyword, kind, state });
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "search failed");
  return (body.emission?.data ?? []) as ContentBundleListItem[];
}

export async function createContentEntityDraft(
  input: ContentBundleDraftInput,
): Promise<ContentBundleDraftDetail> {
  const body = await callAdminContentBundleOp("create_entity_draft", input);
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "create draft failed");
  return body.emission?.data as ContentBundleDraftDetail;
}

export async function updateContentEntityDraft(
  input: ContentBundleUpdateDraftInput,
): Promise<ContentBundleDraftDetail> {
  const body = await callAdminContentBundleOp("update_entity_draft", input);
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "update draft failed");
  return body.emission?.data as ContentBundleDraftDetail;
}

export async function validateContentDraft(draftId: string): Promise<ContentBundleValidateResult | null> {
  const body = await callAdminContentBundleOp("validate_draft", { draftId });
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "validate draft failed");
  return body.emission?.data as ContentBundleValidateResult;
}

export async function previewContentDraft(draftId: string): Promise<ContentBundlePreviewResult | null> {
  const body = await callAdminContentBundleOp("preview_draft", { draftId });
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "preview draft failed");
  return body.emission?.data as ContentBundlePreviewResult;
}

export async function promoteContentDraft(draftId: string): Promise<ContentBundleLifecycleResult | null> {
  const body = await callAdminContentBundleOp("promote_draft", { draftId });
  if (body === null) return null;
  if (!body.success && !body.emission?.data) {
    throw new Error(body.errors?.[0]?.message ?? "promote draft failed");
  }
  return body.emission?.data as ContentBundleLifecycleResult;
}

// ---------------------------------------------------------------------------
// Hub Navigation
// ---------------------------------------------------------------------------

export type HubNavigationManifestItem = {
  topologyManifestId: string;
  manifestKey: string;
  hubId: string;
  hasHubRelations: boolean;
  hubRelationCount: number;
};

export type HubNavigationHubRelationItem = {
  hubRelationId: string;
  topologyManifestId: string;
  relatedHubId: string;
  relatedHubLabel: string;
  sequencePosition: number;
  relationConfig: string | null;
  status: string;
};

export type HubNavigationLifecycleResult = {
  ok: boolean;
  hubRelationId: string | null;
  status: string;
  message: string;
  errorCode?: string;
};

async function callHubNavigation(
  action: string,
  payload?: unknown,
): Promise<{ success?: boolean; emission?: { data?: unknown } | null; errors?: { message?: string; Message?: string }[] } | null> {
  const result = await queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer: "hub_navigation",
    action,
    ...(payload !== undefined ? { payload: payload as Record<string, unknown> } : {}),
  }, getToken());
  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
  }
  return result as { success?: boolean; emission?: { data?: unknown } | null; errors?: { message?: string; Message?: string }[] };
}

export async function listHubNavigationManifests(): Promise<HubNavigationManifestItem[] | null> {
  const body = await callHubNavigation("list_manifests");
  if (body === null) return null;
  return (body.emission?.data ?? null) as HubNavigationManifestItem[] | null;
}

export async function getHubRelationsByManifest(
  topologyManifestId: string,
): Promise<HubNavigationHubRelationItem[] | null> {
  const body = await callHubNavigation("get_hub_relations", { topologyManifestId });
  if (body === null) return null;
  return (body.emission?.data ?? null) as HubNavigationHubRelationItem[] | null;
}

export async function createHubRelation(
  topologyManifestId: string,
  relatedHubId: string,
  sequencePosition: number,
): Promise<HubNavigationLifecycleResult> {
  const body = await callHubNavigation("create", { topologyManifestId, relatedHubId, sequencePosition });
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "create hub_relation failed");
  return body.emission?.data as HubNavigationLifecycleResult;
}

export async function updateHubRelation(
  hubRelationId: string,
  relatedHubId: string,
): Promise<HubNavigationLifecycleResult> {
  const body = await callHubNavigation("update", { hubRelationId, relatedHubId });
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "update hub_relation failed");
  return body.emission?.data as HubNavigationLifecycleResult;
}

export async function deprecateHubRelation(
  hubRelationId: string,
): Promise<HubNavigationLifecycleResult> {
  const body = await callHubNavigation("deprecate", { hubRelationId });
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "deprecate hub_relation failed");
  return body.emission?.data as HubNavigationLifecycleResult;
}

export async function reorderHubRelations(
  topologyManifestId: string,
  items: Array<{ hubRelationId: string; newSequencePosition: number }>,
): Promise<HubNavigationLifecycleResult> {
  const body = await callHubNavigation("reorder", { topologyManifestId, items });
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "reorder hub_relations failed");
  return body.emission?.data as HubNavigationLifecycleResult;
}
