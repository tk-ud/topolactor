import type { ProjectionDefinition } from "../runtime/projectionConstructor.ts";

/**
 * Structured validation error matching backend ValidationError { Code, Message }.
 * Both PascalCase (from C# serialization) and camelCase are accepted.
 */
export type ValidationError = {
  code?: string;
  Code?: string;
  message?: string;
  Message?: string;
};

/** Extract a display string from a ValidationError regardless of casing. */
export function validationErrorText(e: ValidationError): string {
  const msg = e.message ?? e.Message;
  const code = e.code ?? e.Code;
  if (msg && code) return `[${code}] ${msg}`;
  return msg ?? code ?? "unknown error";
}

export type RecommendationCandidate = {
  value: string;
  score: number;
  probability?: number;
  evidence: string[];
  lane: "ui_pressure" | "state_pressure";
};

export type ContextRouteRecommendation = {
  nextOperations: RecommendationCandidate[];
  nextTokens: RecommendationCandidate[];
  nextEnumItems: RecommendationCandidate[];
  nearestPrefixSessionIds: string[];
  contributingTokens: string[];
  status: "ok" | "insufficient_history" | "explicit_error";
  statusDetail?: string;
};


export type RecommendRuntimeDispatchSpec = {
  operationType: string;
  target: string;
  layer: string;
  action: string;
  wiringKey?: string | null;
  wiringId?: string | null;
  targetRef?: string | null;
};

export type RecommendProjectionStatus = "ok" | "insufficient_history" | "explicit_unavailable" | "explicit_error";

export type RecommendProjectionCandidate = {
  value: string;
  score: number;
  probability?: number | null;
  evidence: string[];
  lane: "ui_pressure" | "state_pressure";
  runtimeDispatchSpec?: RecommendRuntimeDispatchSpec | null;
};

export type RecommendProjectionSection = {
  lane: "ui_pressure" | "state_pressure";
  candidateKind: "next_operation" | "next_component" | "next_route_action" | "next_context_token" | "next_enum_item" | "likely_status" | "state_shift_candidate";
  title: string;
  status: RecommendProjectionStatus;
  statusDetail?: string | null;
  candidates: RecommendProjectionCandidate[];
};

export type SqlAttentionProjectionChildSpec = {
  lane: "sql_attention_projection";
  candidateKind: "next_hub_projection_candidate";
  status: RecommendProjectionStatus;
  statusDetail?: string | null;
  sourceSetId?: string | null;
};

export type RecommendNavigationProjectionSpec = {
  mainProjectionIslandId: "main_projection_island" | string;
  childIslandId: "recommend_navigation_child_island" | string;
  hubLocalSections: RecommendProjectionSection[];
  sqlAttentionProjection: SqlAttentionProjectionChildSpec;
};

/**
 * A single layout node in the Emission, derived from layout_patch_json.nodes[].
 * Carries the full structural and positional fields authored in the UI builder canvas.
 * nodeKind: "catalog_component" | "structural_html"
 * structural_html nodes render as actual HTML elements (htmlTag); catalog_component nodes
 * render via the component registry (componentId).
 * parentNodeId establishes the DOM nesting tree. orderIndex drives sibling render order.
 * width/height are flow box dimensions (px, %, auto). x/y are legacy and not projected in flow mode.
 * layoutClassRefs are SSOT topology-layout-class vocabulary refs for className resolution.
 */
export type LayoutNode = {
  nodeId?: string;
  nodeKind?: string;
  htmlTag?: string;
  componentKey?: string;
  componentId?: string;
  parentNodeId?: string;
  slotKey?: string;
  orderIndex: number;
  x?: number;
  y?: number;
  width?: number | string;
  height?: number | string;
  layoutClassRefs?: string[];
  /** Component kind from ui_component_registry — present on catalog_component nodes when wiring is configured. */
  componentKind?: string;
  /** Runtime dispatch action derived from ui_wiring_registry.wiring_kind. Null when no wiring is configured. */
  runtimeDispatchAction?: string | null;
  /** Full wiring spec from ui_wiring_registry — used by frontend to build executable dispatch spec. */
  wiringId?: string | null;
  wiringKey?: string | null;
  wiringKind?: string | null;
  targetSurface?: string | null;
  targetRef?: string | null;
  /** Node-local props override authored in UI Builder. When present, renderEmission merges over default props; invalid JSON → explicit error spec. */
  propsJson?: string | null;
  /** Node-local state JSON (e.g. open:bool for modal/drawer). When present, renderEmission merges into props.data; invalid JSON → explicit error spec. */
  stateJson?: string | null;
};

export type Emission = {
  structureMapId?: string;
  packageId?: string;
  schemaId?: string;
  componentIds?: string[];
  data?: Record<string, unknown>;
  errors?: ValidationError[];
  contextRouteRecommendation?: ContextRouteRecommendation;
  /** Backend-resolved render-only recommendation child island spec. */
  recommendNavigationProjection?: RecommendNavigationProjectionSpec;
  /**
   * ProjectionDefinition extracted from the manifest topology's projection_constructor_mapping entry.
   * Supplied by the backend ManifestDispatcher when a manifest with a projection_constructor_mapping
   * entry is resolved. Frontend must call projectionRuntime.setProjectionDefinition() with this value
   * before SSE projection events arrive. Null/absent when no manifest is configured or no
   * projection_constructor_mapping entry exists.
   */
  projectionDefinition?: ProjectionDefinition;
  /**
   * Admin-authored layout identity from structure_maps.layout_id.
   * Absent when no layout is bound to the resolved structure map entry.
   * When present, identifies which topology.components_layout_design row governs the projection.
   */
  layoutId?: string;
  /**
   * Tensor-derived layout nodes ordered by orderIndex.
   * Present when layoutId is set and topology.ui_topology_tensor rows exist for that layout_id.
   * Absent when no layout is bound to the resolved structure map entry.
   * renderEmission MUST NOT silently fall back to flat componentIds rendering when
   * layoutId is present but layoutNodes is absent — that is an explicit failure state.
   */
  layoutNodes?: LayoutNode[];
};

export type DispatchRequest = {
  operationType: string;
  target: string;
  layer: string;
  action: string;
  idOrHubId?: string;
  payload?: Record<string, unknown>;
  context?: Record<string, string>;
  /** trigger_kind per SSOT minimal_event_shape: cron | hook | client */
  triggerKind?: "cron" | "hook" | "client";
  /** role from JWT token claim for manifest axis resolution */
  role?: string;
};

export type DispatchResponse = {
  success: boolean;
  emission?: Emission;
  errors?: ValidationError[];
};

/**
 * Sends a DispatchRequest to the backend /api/dispatch endpoint and returns
 * the parsed DispatchResponse.
 *
 * If token is provided, adds Authorization: Bearer <token> to the request.
 * On any fetch-level error (network failure, JSON parse failure) the function
 * returns a failed DispatchResponse carrying the error as a ValidationError
 * rather than throwing, so the caller can treat all outcomes uniformly.
 */
export async function dispatchOperation(
  req: DispatchRequest,
  token?: string,
): Promise<DispatchResponse> {
  try {
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const response = await fetch("/api/dispatch", {
      method: "POST",
      headers,
      body: JSON.stringify(req),
    });

    const json: unknown = await response.json();

    if (
      typeof json === "object" &&
      json !== null &&
      !Array.isArray(json) &&
      "success" in json
    ) {
      return json as DispatchResponse;
    }

    return {
      success: false,
      errors: [{ message: "dispatch: unexpected response shape from /api/dispatch" }],
    };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}
