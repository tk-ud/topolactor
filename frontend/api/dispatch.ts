import type { ProjectionDefinition } from "../runtime/projectionConstructor.ts";
import type { CalcBinding } from "../runtime/frontendLocalCalculationResolver.ts";

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

export type RecommendProjectionStatus =
  | "ok"
  | "insufficient_history"
  | "explicit_unavailable"
  | "explicit_error";

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
  candidateKind:
    | "next_operation"
    | "next_component"
    | "next_route_action"
    | "next_context_token"
    | "next_enum_item"
    | "likely_status"
    | "state_shift_candidate";
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
 * nodeKind: "catalog_component" | "structural_html" | "structural_node" | "unresolved_gap"
 * structural_html nodes render as actual HTML elements (htmlTag); catalog_component nodes
 * render via the component registry (componentId). structural_node nodes (Category/Section/
 * Form/Workflow/Validation sourced from components_layout_design.layout_schema_json.records[] —
 * the structural authority tree) render as a generic labeled group and never carry a
 * componentId/componentKind. unresolved_gap nodes (topology_ui_unresolved from the same
 * structural authority tree) always render as an explicit error carrying knownGapRefs — never a
 * resolvable component.
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
  /**
   * Array prop bindings authored in UI Builder.
   * Maps component prop name → runtime data path binding descriptor.
   * Resolved after propsJson/stateJson in renderEmission.
   * source must start with "emission.data."; transform must be in allowlist.
   */
  propBindings?: Record<string, PropBinding> | null;
  /** Canonical runtime UI interactions. Legacy propsJson.eventWirings is fallback only. */
  runtimeInteractions?:
    | Array<{
      trigger: string;
      actionType: string;
      targetNodeId?: string;
      statePath?: string;
      value?: unknown;
      portTargetRef?: string;
      instanceTargetRef?: string;
      payloadFrom?: Record<string, string>;
      outputProp?: string;
      debounceMs?: number;
      lifecycleDispatchConfirmed?: boolean;
      idempotencyPolicy?: string;
      sideEffectNone?: boolean;
      /**
       * SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml
       * lifecycle_policy.projection_authority_runtime_interaction_identity.
       * Backend-assigned stable id (never generated on the frontend). Absent on
       * entries not yet re-persisted since the field was introduced.
       */
      runtimeInteractionId?: string;
    }>
    | null;
  /**
   * Node-local admin_runtime dispatch payload binding — { trigger: { field: source } }. Supplies
   * payloadFrom fields for this SAME node's admin_runtime dispatch (wiringKind="admin_runtime" +
   * targetRef). Data-only: carries no action authority and is independent of
   * runtimeInteractions/actionType — see renderEmission.ts's admin_runtime payload binding
   * contract. Absent when not authored.
   */
  dispatchPayloadFromByTrigger?: Record<string, Record<string, string>> | null;
  /**
   * Node-local admin_runtime dispatch TARGET override — { trigger: "manifest:<uuid>:<layer>:<action>" }.
   * Lets this SAME node's own trigger dispatch to a DIFFERENT admin_runtime layer:action than the
   * layout's own uniform wiringKind="admin_runtime"/targetRef (which every non-structural node in
   * the layout inherits unconditionally from its single ui_wiring_registry row — see
   * NpgsqlTopologyRepository.LoadLayoutNodesAsync). Mirrors the existing per-trigger authored
   * target override precedent already used by dispatchExternalPort/dispatchInstanceOperation
   * (wiring.portTargetRef / wiring.instanceTargetRef in runtimeInteractions[]) — this field applies
   * the same "per-trigger authored target, independent of the shared layout wiring row" pattern to
   * admin_runtime. Absent when not authored — the node then keeps dispatching to the layout's own
   * uniform target, unchanged. See renderEmission.ts's admin_runtime target-ref override contract.
   */
  dispatchTargetRefByTrigger?: Record<string, string> | null;
  /**
   * round 37 (search_filter_input_contract debounce_policy): this Field's own declared
   * debounceMs (Field grammar, react-schema-topology-seed-translator-ssot.yaml). Debounces
   * this node's own "input"-triggered admin_runtime Lane 2 dispatch (see
   * runtimeComponentFactory.ts scheduleOrDispatchRuntimeCommand). Absent when not authored
   * — the node then dispatches immediately on every "input" trigger, unchanged.
   */
  debounceMs?: number | null;
  widthMode?: "auto" | "preset" | "custom";
  heightMode?: "auto" | "preset" | "custom";
  /**
   * Structural-node semantic type (topology_ui_category/topology_ui_section/topology_ui_form/
   * topology_ui_workflow/topology_ui_validation) when nodeKind is "structural_node", or
   * topology_ui_unresolved when nodeKind is "unresolved_gap". Absent for "catalog_component"/
   * "structural_html" nodes.
   */
  recordType?: string;
  /**
   * Authored display label — present for every schema-composed node (structural_node,
   * catalog_component, and unresolved_gap alike). Absent for tensor-only nodes composed outside
   * the layout-schema structural authority path.
   */
  label?: string;
  /** Authored known-gap references for an unresolved_gap node. Absent for every other nodeKind. */
  knownGapRefs?: string[];
  /** Persisted component_style_design snapshot for draft-preview / pre-publish projection. */
  componentDesign?: {
    inlineText?: string;
    linkHref?: string;
    linkTarget?: string;
    cssTokenRefs?: string[];
    classname?: string;
    tailwind?: string;
  };
};

/**
 * Binding descriptor for a single array prop on a layout node.
 * source: dotted path starting with "emission.data." (e.g. "emission.data.rows").
 * keyPath/labelPath/valuePath/childrenPath: field names within each array element.
 * transform: allowlist transform identifier (e.g. "activeColumnsToTableColumns").
 */
export type PropBinding = {
  source: string;
  keyPath?: string;
  labelPath?: string;
  valuePath?: string;
  childrenPath?: string;
  transform?: string;
};

/**
 * One entry in the manifest-scoped hub_relations navigation sequence ("current hub relation"
 * candidates) — hubs.hub_relations owns manifest-scoped hub sequence / UI transition order
 * (docs/design/db-schema.yaml manifest_hub_chain), not a global hub-to-hub relation graph.
 * targetManifestId is resolved backend-side only when exactly one active topology_manifest is
 * registered under relatedHubId (no implicit oldest/first-match fallback) — absent/null means
 * "not directly callable", never a guessed target.
 * hubRelationId (the hubs.hub_relations row's own id) and topologyManifestId (the source manifest
 * this sequence was resolved for) satisfy
 * docs/design/admin-normal-surface-projection-seed-ssot.yaml
 * surface_axes.admin.surfaces.dashboard.hub_relation_navigation_binding.selected_link_payload_required.
 */
export type HubNavigationSequenceItem = {
  hubRelationId: string;
  topologyManifestId: string;
  relatedHubId: string;
  relatedHubLabel: string;
  sequencePosition: number;
  targetManifestId?: string | null;
};

export type Emission = {
  structureMapId?: string;
  packageId?: string;
  schemaId?: string;
  componentIds?: string[];
  data?: Record<string, unknown>;
  errors?: ValidationError[];
  contextRouteRecommendation?: ContextRouteRecommendation;
  /**
   * Manifold-scoped hub_relations navigation candidates for the currently resolved manifest
   * ("current hub relation" candidates the projection can move to). Absent when the manifest has
   * no active hub_relations or when NavigationSequence resolution was not applicable.
   */
  navigationSequence?: HubNavigationSequenceItem[];
  /**
   * Identity of the resolved topology_manifest for this dispatch — "current topology phase".
   * Set by ManifestDispatcher for any manifest-resolved dispatch. Absent when no manifest was
   * resolved for this request (e.g. dev bypass path).
   */
  manifestId?: string;
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
  /**
   * Frontend-local calculation bindings from layout_patch_json.calculationBindings[].
   * Backend extracts and forwards these verbatim — no server-side evaluation.
   * renderEmission applies them client-side to derive values from emission.data.* and node inputs.
   * Absent when no calculationBindings are authored in the layout patch.
   */
  calculationBindings?: CalcBinding[];
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
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
    };
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
      errors: [{
        message: "dispatch: unexpected response shape from /api/dispatch",
      }],
    };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return { success: false, errors: [{ message }] };
  }
}
