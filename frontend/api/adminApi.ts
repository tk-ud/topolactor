import type { DispatchRequest, Emission, ValidationError } from "./dispatch.ts";
import { validationErrorText } from "./dispatch.ts";

import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";
import {
  queueAdminClientCommand,
  type ScheduledCommandResult,
} from "../runtime/frontendScheduler.ts";

/** Thin admin dispatch helper for UI Builder authoring hooks. */
export function dispatchAdminOp(
  layer: string,
  action: string,
  payload?: unknown,
): Promise<ScheduledCommandResult> {
  return queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer,
    action,
    payload: payload != null ? payload as Record<string, unknown> : undefined,
  }, getToken());
}

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

export async function listImportSnapshotRecords(
  snapshotId: string,
): Promise<AdminImportPreviewResult> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "admin_csv_json_import",
    action: "list_snapshot_records",
    payload: { snapshotId },
  });
  if (emission === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return emission.data as AdminImportPreviewResult;
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
  contentsType?: string;
  topologySystemName?: string;
  userFacingTopologyLabel?: string;
  tableRef?: string;
  physicalBound?: boolean;
  authoringProgressStep?: string;
  draftOrigin?: string;
  cloneMode?: string;
  sourceActiveManifestId?: string | null;
};

export type AdminManifestListFilter = {
  status?: string;
  contentsType?: string | string[];
  physical?: boolean;
  logicalTablesMin?: number;
  /** Client-side post-filter: exclude Step-1 shells missing topologySystemName on list DTO. */
  requiresTopologySystemName?: boolean;
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

// Step 1 clone entry modes — SSOT: admin-console-workflow-ssot.yaml
// admin_contents_step1_entry_modes. The frontend only carries the author intent and the
// read-only source selection; merge target / conflict / active mutation authority is backend.
export type ContentsStep1EntryMode =
  | "create_new_topology"
  | "clone_active_as_replacement_draft"
  | "clone_active_as_new_topology_draft"
  | "resume_existing_draft";

export type CloneSourceEvidence = {
  sourceActiveManifestId: string;
  topologySystemName: string | null;
  routeKey: string | null;
  dispatcherAxes:
    | { role: string; target: string; layer: string; action: string }
    | null;
  sourceUpdatedAt: string;
  sourceTopologyHash: string;
  status: string;
};

export type CloneReplacementValidateResult = {
  ok: boolean;
  draftManifestId: string;
  draftOrigin: string;
  cloneMode: string;
  sourceEvidence: CloneSourceEvidence | null;
  sourceStale: boolean;
  activeIdentityConflictCount: number;
  changeCount: number;
  diffJson: string | null;
  validationBlocking: boolean;
  mergeReady: boolean;
  mergeBlockers: AdminManifestValidationIssue[];
};

export type CloneReplacementMergeResult = {
  ok: boolean;
  activeManifestId: string | null;
  draftManifestId: string;
  changeCount: number;
  message: string;
  errorCode?: string;
};

export type AdminManifestScreenColumnInput = {
  name: string;
  dataType: string;
  nullable: boolean;
  enumGroupId?: string;
};

export type EnumDictionaryGroup = {
  groupId: string;
  indexNum: number;
  groupName: string;
};

export type EnumDictionaryItem = {
  indexNum: number;
  name: string;
};

export type EnumDictionaryGroupDetail = {
  groupId: string;
  indexNum: number;
  groupName: string;
  items: EnumDictionaryItem[];
  itemsIndexNums: number[];
};

export type AggregationMeasureInput = {
  column: string;
  function: string;
};

export type AggregationBlockInput = {
  sourceRef: string;
  aggregationKey?: string;
  measures: AggregationMeasureInput[];
  searchConditions?: SearchConditionInput[];
  havingConditions?: HavingConditionInput[];
};

export type RelationIntentInput = {
  localTableRef?: string;
  joinTableRef: string;
  localKey: string;
  remoteKey: string;
  remoteManifestId?: string;
};

export type RelationshipRemoteTargetTable = {
  tableName: string;
  columns: AdminManifestScreenColumnInput[];
};

export type RelationshipRemoteTarget = {
  manifestId: string;
  status: string;
  manifestKey: string | null;
  logicalTables: RelationshipRemoteTargetTable[];
};

/** Registered topology.abstract_function_manifests candidate for a runtime_lane selector. */
export type AggregateTriggerProcessingFunctionCandidate = {
  functionKey: string;
  authorityScope: string;
  active: boolean;
  activeStepCount: number;
};

export type LogicalTableInput = {
  tableName: string;
  columns: AdminManifestScreenColumnInput[];
};

export type OperationEntityBindingInput = {
  operationKind: string;
  /** @deprecated use entityTargetColumns */
  entityTargetColumn?: string;
  entityTargetColumns?: string[];
};

export type ConditionValueSourceInput = {
  kind: string;
  key?: string;
  sampleValue?: string;
};

export type SearchConditionInput = {
  column: string;
  operator: string;
  value?: string;
  valueTo?: string;
  values?: string[];
  logicalConnector?: string;
  valueSource?: ConditionValueSourceInput;
  valueToSource?: ConditionValueSourceInput;
};

export type HavingConditionInput = {
  column: string;
  function: string;
  operator: string;
  value: string;
  valueSource?: ConditionValueSourceInput;
};

export type AggregateTriggerTargetBindingInput = {
  target_source: "step2_logical_entity_definition" | "step2_5_relation_definition";
  target_id: string;
};

export type AggregateTriggerDefinitionInput = {
  trigger_definition_id: string;
  trigger_source: {
    canonical_trigger_kind: "cron" | "hook" | "client";
    trigger_source_detail_kind: "client_operation_event" | "hook_event" | "scheduled_cron_event" | "runtime_function_event";
  };
  processing_function_scope: {
    function_id: string;
    operation_definition_id: string;
    accepted_event_schema_ref: string;
    allowed_source_kinds: string[];
    materialization_policy_ref: string;
  };
  execution_scope: string;
  transaction_boundary: string;
  aggregate_target_binding: AggregateTriggerTargetBindingInput;
  conflict_key_fields: string[];
  delta_map: Record<string, number>;
  threshold_policy: {
    minimum_trial_count: number;
    ratio_numerator_field: string;
    ratio_denominator_field: string;
    comparison_operator: ">" | ">=" | "<" | "<=" | "=" | "!=";
    target_ratio: number;
  };
  materialization_target_binding: AggregateTriggerTargetBindingInput;
  materialization_payload_map: Array<{
    target_field: string;
    source: "function_input_event" | "aggregate_current_row" | "selected_step2_entity_fields" | "selected_step2_5_relation_fields" | "constant" | "generated_value" | "runtime_actor_source_metadata";
    source_field?: string;
    constant_value?: unknown;
  }>;
  approval_policy: "auto_materialize_when_threshold_passes" | "require_backend_approval_before_materialization" | "require_human_approval_before_materialization";
  evidence_policy?: string;
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
  /** Aggregation function (sum, avg, max, min, count, …). @deprecated use aggregationMeasures */
  aggregationFunction?: string;
  /** @deprecated use aggregationMeasures */
  aggregationColumns?: string[];
  /** Multiple aggregation measures (column + function). */
  aggregationMeasures?: AggregationMeasureInput[];
  /** SQL-source-scoped aggregation blocks (Step 3). */
  aggregationBlocks?: AggregationBlockInput[];
  /** Structured display columns. */
  displayColumns?: string[];
  /** Step 2 logical tables (multiple per manifest). */
  logicalTables?: LogicalTableInput[];
  columns?: AdminManifestScreenColumnInput[];
  /** Primary kind for dispatcher refresh (first of screenOperationKinds when set). */
  screenOperationKind?: string;
  /** Multi-select operation kinds (SSOT step 3). */
  screenOperationKinds?: string[];
  /** Topology system name: kebab-case identifier, SSOT for route/table/UI Builder key derivation. Required at step 1. */
  topologySystemName?: string;
  /** User-facing topology label (optional display name; not used for route/table/key generation). */
  userFacingTopologyLabel?: string;
  /** Structured relation/join intents for draft data-shape only (not created-manifest relations). */
  relationIntents?: RelationIntentInput[];
  /** Per-operation entity target at event time (SSOT step 3). */
  operationEntityBindings?: OperationEntityBindingInput[];
  /** Initial-data candidates as screen-data-shape topology intent. Actual row insertion belongs to content_bundle. */
  /** Values + optional lineage per row (Step3 unified editor). */
  initialDataRows?: Record<string, unknown>[];
  /** Structured search conditions with operator/value/logical-connector. */
  searchConditions?: SearchConditionInput[];
  /** HAVING conditions on aggregation measure results. */
  havingConditions?: HavingConditionInput[];
  /** Explicit display column mode: selected | all | none. */
  displayColumnMode?: string;
  /** Step3 structured aggregate trigger authoring payload; backend validator remains runtime authority. */
  aggregateTriggerDefinitions?: AggregateTriggerDefinitionInput[];
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

export async function listAdminManifests(
  filter?: string | AdminManifestListFilter,
): Promise<AdminManifestListItem[] | null> {
  const requiresTopologySystemName = typeof filter === "object" &&
    filter?.requiresTopologySystemName === true;
  const payload = typeof filter === "string"
    ? (filter ? { status: filter } : undefined)
    : filter
    ? (({ requiresTopologySystemName: _omit, ...rest }) => rest)(filter)
    : undefined;
  const body = await callAdminManifestOp("list", payload);
  if (body === null) return null;
  let items = (body.emission?.data ?? []) as AdminManifestListItem[];
  if (requiresTopologySystemName) {
    items = items.filter((item) => Boolean(item.topologySystemName?.trim()));
  }
  return items;
}

export async function getAdminManifest(manifestId: string): Promise<AdminManifestDetail | null> {
  const body = await callAdminManifestOp("get", { manifestId });
  if (body === null) return null;
  return body.emission?.data as AdminManifestDetail;
}

function manifestKeyFromTopologyRaw(raw: string): string | null {
  try {
    const entries = JSON.parse(raw) as unknown;
    if (!Array.isArray(entries)) return null;
    for (const entry of entries) {
      if (
        typeof entry === "object" && entry !== null &&
        (entry as { type?: string }).type === "hub_grouping" &&
        typeof (entry as { manifestKey?: string }).manifestKey === "string"
      ) {
        const key = (entry as { manifestKey: string }).manifestKey.trim();
        if (key) return key;
      }
    }
  } catch {
    return null;
  }
  return null;
}

/** Build remote targets from manifest:list + manifest:get (fallback when dedicated op missing). */
async function listRelationshipRemoteTargetsFallback(
  excludeManifestId?: string,
): Promise<RelationshipRemoteTarget[] | null> {
  const { extractScreenDataShapeFromTopology } = await import(
    "../lib/manifestTopologyExtensions.ts"
  );
  const items = await listAdminManifests("active");
  if (items === null) return null;

  const targets: RelationshipRemoteTarget[] = [];
  await Promise.all(items.map(async (item) => {
    if (excludeManifestId && item.manifestId === excludeManifestId) return;
    const detail = await getAdminManifest(item.manifestId);
    if (!detail) return;
    const shape = extractScreenDataShapeFromTopology(detail.topologyRawJson);
    if (shape.logicalTables.length === 0) return;
    const manifestKey = manifestKeyFromTopologyRaw(detail.topologyRawJson) ??
      (item.role && item.target && item.layer && item.action
        ? `${item.role}.${item.target}.${item.layer}.${item.action}`
        : null);
    targets.push({
      manifestId: item.manifestId,
      status: item.status,
      manifestKey,
      logicalTables: shape.logicalTables.map((t) => ({
        tableName: t.tableName,
        columns: t.columns.map((c) => ({
          name: c.name,
          dataType: c.dataType,
          nullable: c.nullable,
        })),
      })),
    });
  }));
  return targets;
}

/** Active manifests with logical tables for Step 2.5 remote relationship targets. */
export async function listRelationshipRemoteTargets(
  excludeManifestId?: string,
): Promise<RelationshipRemoteTarget[] | null> {
  const result = await queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer: "manifest",
    action: "list_relationship_remote_targets",
    payload: excludeManifestId ? { excludeManifestId } as Record<string, unknown> : undefined,
  }, getToken());

  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
    if (code === "ADMIN_OPERATION_NOT_FOUND" || code === "MANIFEST_NOT_FOUND") {
      return await listRelationshipRemoteTargetsFallback(excludeManifestId);
    }
    const msg = result.errors?.[0]
      ? validationErrorText(result.errors[0])
      : "manifest list_relationship_remote_targets failed";
    throw new Error(msg);
  }

  return (result.emission?.data ?? []) as RelationshipRemoteTarget[];
}

/**
 * Registered processing_function_scope.function_id candidates for admin/contents Step3
 * (topology.abstract_function_manifests rows with runtime_lane=aggregate_trigger_runtime, active
 * only). Selection from this list is not itself authority — AggregateTriggerDefinitionValidator
 * remains the backend authority at assign_screen_data_shape time.
 */
export async function listAggregateTriggerProcessingFunctions(): Promise<
  AggregateTriggerProcessingFunctionCandidate[] | null
> {
  const result = await queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer: "manifest",
    action: "list_aggregate_trigger_processing_functions",
    payload: undefined,
  }, getToken());

  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
    const msg = result.errors?.[0]
      ? validationErrorText(result.errors[0])
      : "manifest list_aggregate_trigger_processing_functions failed";
    throw new Error(msg);
  }

  return (result.emission?.data ?? []) as AggregateTriggerProcessingFunctionCandidate[];
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

/** Step 1 create_new_topology entry mode — stamps clone_off metadata (manual_new / none). */
export async function createNewTopologyDraft(
  input: AdminManifestDraftInput,
): Promise<AdminManifestDetail> {
  const body = await callAdminManifestOp("create_new_topology_draft", input);
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return body.emission?.data as AdminManifestDetail;
}

/** Step 1 clone_active_as_replacement_draft — backend copies the active source + stamps replacement metadata. */
export async function createCloneReplacementDraftFromActive(
  sourceActiveManifestId: string,
): Promise<AdminManifestDetail> {
  const body = await callAdminManifestOp(
    "create_clone_replacement_draft_from_active",
    { sourceActiveManifestId },
  );
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return body.emission?.data as AdminManifestDetail;
}

/** Step 1 clone_active_as_new_topology_draft — lineage-only clone with a new topologySystemName. */
export async function createCloneNewTopologyDraftFromActive(
  sourceActiveManifestId: string,
  newTopologySystemName: string,
  userFacingTopologyLabel?: string,
): Promise<AdminManifestDetail> {
  const body = await callAdminManifestOp(
    "create_clone_new_topology_draft_from_active",
    { sourceActiveManifestId, newTopologySystemName, userFacingTopologyLabel },
  );
  if (body === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return body.emission?.data as AdminManifestDetail;
}

/** Read-only source active evidence for clone authoring display. Never replacement authority. */
export async function loadCloneSourceEvidence(
  sourceActiveManifestId: string,
): Promise<CloneSourceEvidence | null> {
  const body = await callAdminManifestOp("load_clone_source_evidence", {
    sourceActiveManifestId,
  });
  if (body === null) return null;
  return body.emission?.data as CloneSourceEvidence;
}

/**
 * Backend-computed replacement merge readiness (read-only). The frontend renders blockers;
 * it never decides the merge target or conflict outcome.
 */
export async function validateCloneReplacementDraft(
  draftManifestId: string,
): Promise<CloneReplacementValidateResult | null> {
  const body = await callAdminManifestOp("validate_clone_replacement_draft", {
    manifestId: draftManifestId,
  });
  if (body === null) return null;
  return body.emission?.data as CloneReplacementValidateResult;
}

/**
 * Submits a replacement merge INTENT to backend authority. The backend (AdminRuntime /
 * ManifestRepository transaction) is the only authority that performs the merge.
 */
export async function mergeCloneReplacementDraftToActive(
  draftManifestId: string,
): Promise<CloneReplacementMergeResult | null> {
  const body = await callAdminManifestOp(
    "merge_clone_replacement_draft_to_active",
    { manifestId: draftManifestId },
  );
  if (body === null) return null;
  return body.emission?.data as CloneReplacementMergeResult;
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

export async function listEnumDictionaryGroups(): Promise<EnumDictionaryGroup[] | null> {
  const result = await queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer: "enum_dictionary",
    action: "list_groups",
  }, getToken());
  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
    const msg = result.errors?.[0]
      ? validationErrorText(result.errors[0])
      : "enum_dictionary list_groups failed";
    throw new Error(msg);
  }
  return (result.emission?.data ?? []) as EnumDictionaryGroup[];
}

export async function getEnumDictionaryGroup(
  groupId: string,
): Promise<EnumDictionaryGroupDetail | null> {
  const result = await queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer: "enum_dictionary",
    action: "get_group",
    payload: { groupId },
  }, getToken());
  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
    const msg = result.errors?.[0]
      ? validationErrorText(result.errors[0])
      : "enum_dictionary get_group failed";
    throw new Error(msg);
  }
  return result.emission?.data as EnumDictionaryGroupDetail;
}

export const USER_STATUS_ENUM_GROUP_ID = "33333333-3333-3333-3333-333333333301";

async function callAdminMasterOp<T>(
  layer: string,
  action: string,
  payload?: Record<string, unknown>,
): Promise<T | null> {
  const result = await queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer,
    action,
    payload,
  }, getToken());
  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code;
    if (code === "DISPATCH_BACKEND_NOT_CONFIGURED") return null;
    const msg = result.errors?.[0]
      ? validationErrorText(result.errors[0])
      : `${layer}:${action} failed`;
    throw new Error(msg);
  }
  return result.emission?.data as T;
}

export async function createEnumDictionaryGroup(
  groupName: string,
  indexNum?: number,
): Promise<EnumDictionaryGroup | null> {
  return callAdminMasterOp("enum_dictionary", "create_group", { groupName, indexNum });
}

export async function updateEnumDictionaryGroup(
  groupId: string,
  groupName?: string,
  indexNum?: number,
): Promise<EnumDictionaryGroup | null> {
  return callAdminMasterOp("enum_dictionary", "update_group", { groupId, groupName, indexNum });
}

export async function deleteEnumDictionaryGroup(groupId: string): Promise<{ ok: boolean } | null> {
  return callAdminMasterOp("enum_dictionary", "delete_group", { groupId });
}

export async function createEnumDictionaryItem(
  name: string,
  indexNum?: number,
): Promise<EnumDictionaryItem | null> {
  return callAdminMasterOp("enum_dictionary", "create_item", { name, indexNum });
}

export async function updateEnumDictionaryItem(
  indexNum: number,
  name?: string,
  newIndexNum?: number,
): Promise<EnumDictionaryItem | null> {
  return callAdminMasterOp("enum_dictionary", "update_item", { indexNum, name, newIndexNum });
}

export async function deleteEnumDictionaryItem(indexNum: number): Promise<{ ok: boolean } | null> {
  return callAdminMasterOp("enum_dictionary", "delete_item", { indexNum });
}

export async function setEnumDictionaryGroupItems(
  groupId: string,
  enumIndexNums: number[],
): Promise<EnumDictionaryGroupDetail | null> {
  return callAdminMasterOp("enum_dictionary", "set_group_items", { groupId, enumIndexNums });
}

export type AuthUserRoster = {
  userId: string;
  username: string;
  active: boolean;
  approve: boolean;
  status: string | null;
  suspendedFrom: string | null;
  suspendedUntil: string | null;
  stateNote: string | null;
  createdAt: string;
  updatedAt: string;
  lastLoginAt: string | null;
  /** "admin" or "user" — read projection of auth.grants. Role changes go through updateAuthUser(roleName). */
  role: string;
};

export async function listAuthUsers(query?: string): Promise<AuthUserRoster[] | null> {
  return callAdminMasterOp("auth_users", "list", query ? { query } : undefined);
}

export async function getAuthUser(userId: string): Promise<AuthUserRoster | null> {
  return callAdminMasterOp("auth_users", "get", { userId });
}

export async function createAuthUser(input: {
  username: string;
  password: string;
  approve?: boolean;
  status?: string;
  roleName?: string;
  realm?: string;
  suspendedFrom?: string | null;
  suspendedUntil?: string | null;
  stateNote?: string | null;
}): Promise<AuthUserRoster | null> {
  return callAdminMasterOp("auth_users", "create", input);
}

export async function updateAuthUser(input: {
  userId: string;
  username?: string;
  active?: boolean;
  approve?: boolean;
  status?: string;
  suspendedFrom?: string | null;
  suspendedUntil?: string | null;
  clearSuspendedFrom?: boolean;
  clearSuspendedUntil?: boolean;
  stateNote?: string | null;
  /** "admin" or "user" — never a password field. Omit to leave role unchanged. */
  roleName?: "admin" | "user";
}): Promise<AuthUserRoster | null> {
  return callAdminMasterOp("auth_users", "update", input);
}

export async function deleteAuthUser(userId: string): Promise<{ ok: boolean } | null> {
  return callAdminMasterOp("auth_users", "delete", { userId });
}

// ─── Admin-driven session / credential revoke (thin HTTP boundary, bypasses /dispatch) ─────────
// Admin can act on any userId but never reads/sets a password value — these only revoke.

export type AdminSessionSummary = {
  sessionId: string;
  realm: string;
  audience: string;
  expiresAt: string;
  createdAt: string;
  isCurrent: boolean;
};

async function adminAuthFetch<T>(path: string, init: RequestInit): Promise<T> {
  const token = getToken();
  try {
    const response = await fetch(path, {
      ...init,
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(init.headers ?? {}),
      },
    });
    const json: unknown = await response.json();
    if (typeof json === "object" && json !== null && !Array.isArray(json) && "success" in json) {
      return json as T;
    }
    return { success: false, errors: [{ message: `unexpected response shape from ${path}` }] } as T;
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] } as T;
  }
}

export async function adminListUserSessions(
  userId: string,
): Promise<{ success: boolean; sessions?: AdminSessionSummary[]; errors?: ValidationError[] }> {
  return adminAuthFetch(`/api/admin/auth/users/${userId}/sessions`, { method: "GET" });
}

/** Omit sessionId to revoke every active session for the account. */
export async function adminRevokeUserSessions(
  userId: string,
  sessionId?: string,
): Promise<{ success: boolean; sessionsRevoked?: number; errors?: ValidationError[] }> {
  return adminAuthFetch(`/api/admin/auth/users/${userId}/sessions/revoke`, {
    method: "POST",
    body: JSON.stringify(sessionId ? { sessionId } : {}),
  });
}

/** Kill-switch: deletes the account's credential and revokes all sessions. Never sets a new password. */
export async function adminRevokeUserCredential(
  userId: string,
): Promise<{ success: boolean; errors?: ValidationError[] }> {
  return adminAuthFetch(`/api/admin/auth/users/${userId}/credential/revoke`, { method: "POST" });
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

function expectEmissionList<T>(data: unknown, operation: string): T[] {
  if (!Array.isArray(data)) {
    throw new Error(`${operation}: emission.data must be an array`);
  }
  return data as T[];
}

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
  return expectEmissionList<ContentBundleListItem>(
    body.emission?.data,
    "content_bundle:list_hubs",
  );
}

export async function listContentEntities(): Promise<ContentBundleListItem[] | null> {
  const body = await callAdminContentBundleOp("list_entities");
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "list entities failed");
  return expectEmissionList<ContentBundleListItem>(
    body.emission?.data,
    "content_bundle:list_entities",
  );
}

export async function listContentRelations(): Promise<ContentBundleListItem[] | null> {
  const body = await callAdminContentBundleOp("list_relations");
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "list relations failed");
  return expectEmissionList<ContentBundleListItem>(
    body.emission?.data,
    "content_bundle:list_relations",
  );
}

export async function listContentHubRelations(): Promise<ContentBundleListItem[] | null> {
  const body = await callAdminContentBundleOp("list_hub_relations");
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "list hub relations failed");
  return expectEmissionList<ContentBundleListItem>(
    body.emission?.data,
    "content_bundle:list_hub_relations",
  );
}

export async function listContentStates(): Promise<ContentBundleStateItem[] | null> {
  const body = await callAdminContentBundleOp("list_states");
  if (body === null) return null;
  if (!body.success) throw new Error(body.errors?.[0]?.message ?? "list states failed");
  return expectEmissionList<ContentBundleStateItem>(
    body.emission?.data,
    "content_bundle:list_states",
  );
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
  return expectEmissionList<ContentBundleListItem>(
    body.emission?.data,
    "content_bundle:search",
  );
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
  if (!body.success) {
    throw new Error(body.errors?.[0]?.message ?? "list hub navigation manifests failed");
  }
  return expectEmissionList<HubNavigationManifestItem>(
    body.emission?.data,
    "hub_navigation:list_manifests",
  );
}

export async function getHubRelationsByManifest(
  topologyManifestId: string,
): Promise<HubNavigationHubRelationItem[] | null> {
  const body = await callHubNavigation("get_hub_relations", { topologyManifestId });
  if (body === null) return null;
  if (!body.success) {
    throw new Error(body.errors?.[0]?.message ?? "get hub relations failed");
  }
  return expectEmissionList<HubNavigationHubRelationItem>(
    body.emission?.data,
    "hub_navigation:get_hub_relations",
  );
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

// ---------------------------------------------------------------------------
// Scheduler Job Settings Projection (read-only)
// ---------------------------------------------------------------------------

export type SchedulerJobManifestItem = {
  schedulerJobId: string;
  jobKey: string;
  triggerKind: string;
  schedulePolicyKind: string;
  cronExpression: string | null;
  scheduleIntervalSeconds: number | null;
  manualRunAllowed: boolean;
  active: boolean;
  maxBatchSize: number;
  leaseSeconds: number;
  authorityScope: string;
  /** reference key only — no credential plaintext */
  credentialRequirementRef: string | null;
  /** reference key only — no external port config */
  externalPortRef: string | null;
};

export async function fetchSchedulerJobManifests(): Promise<SchedulerJobManifestItem[] | null> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "scheduler_jobs",
    action: "list_settings",
  });
  if (emission === null) return null;
  const data = emission.data as { ok: boolean; schedulerJobs: SchedulerJobManifestItem[] } | null;
  return data?.schedulerJobs ?? null;
}

// ---------------------------------------------------------------------------
// Scheduler Job authoring (admin.contents). The frontend submits a manifest
// draft only — runtime judgment / SQL / credential authority stays in the
// backend AdminRuntime. Reference keys only; no secret material is sent.
// ---------------------------------------------------------------------------

/** One ordered step in an authored scheduler job manifest. */
export type SchedulerJobStepInput = {
  stepOrder?: number;
  abstractFunctionKey: string;
  onError?: string;
  resultContextKey?: string | null;
  /** manifest-defined binding map (result_context_key -> { source, path }) */
  inputBinding?: Record<string, unknown>;
  /** manifest-defined output binding (kind/result_context_key/conflict_columns/column_map) */
  resultBinding?: Record<string, unknown>;
  authorityScope?: string | null;
  active?: boolean;
};

export type SchedulerJobDraftInput = {
  jobKey: string;
  triggerKind: string;
  schedulePolicyKind: string;
  cronExpression?: string | null;
  scheduleIntervalSeconds?: number | null;
  manualRunAllowed?: boolean;
  active?: boolean;
  authorityScope: string;
  maxBatchSize?: number;
  leaseSeconds?: number;
  /** reference key only — no credential plaintext */
  credentialRequirementRef?: string | null;
  /** reference key only — no external port config */
  externalPortRef?: string | null;
  // Input source / lifecycle (manifest authority; backend validates as identifiers).
  inputTableRef?: string | null;
  inputIdColumn?: string | null;
  inputStatusColumn?: string | null;
  inputDueColumn?: string | null;
  inputStatusPendingValue?: string | null;
  inputStatusProcessingValue?: string | null;
  inputStatusCompletedValue?: string | null;
  inputStatusFailedValue?: string | null;
  inputStatusSkippedValue?: string | null;
  inputStatusRetryWaitValue?: string | null;
  // Output binding target.
  outputTableRef?: string | null;
  timezone?: string | null;
  // Policy bodies (JSON objects).
  retryPolicy?: Record<string, unknown>;
  projectionPolicy?: Record<string, unknown>;
  // Ordered step chain.
  steps?: SchedulerJobStepInput[];
};

export type SchedulerJobAuthoringResult = {
  ok: boolean;
  schedulerJobId?: string;
  jobKey?: string;
  active?: boolean;
};

export async function createSchedulerJob(
  input: SchedulerJobDraftInput,
): Promise<SchedulerJobAuthoringResult> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "scheduler_jobs",
    action: "create",
    payload: input as unknown as Record<string, unknown>,
  });
  if (emission === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return emission.data as SchedulerJobAuthoringResult;
}

export async function editSchedulerJob(
  schedulerJobId: string,
  input: SchedulerJobDraftInput,
): Promise<SchedulerJobAuthoringResult> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "scheduler_jobs",
    action: "edit",
    payload: { schedulerJobId, ...input } as unknown as Record<string, unknown>,
  });
  if (emission === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return emission.data as SchedulerJobAuthoringResult;
}

export async function disableSchedulerJob(
  schedulerJobId: string,
): Promise<SchedulerJobAuthoringResult> {
  const emission = await callAdminDispatch({
    operationType: "admin",
    target: "admin",
    layer: "scheduler_jobs",
    action: "disable",
    payload: { schedulerJobId },
  });
  if (emission === null) throw new Error("DISPATCH_BACKEND_NOT_CONFIGURED");
  return emission.data as SchedulerJobAuthoringResult;
}
