import type {
  DispatchResponse,
  Emission,
  LayoutNode as EmissionLayoutNode,
} from "../api/dispatch.ts";
import type { ComponentRegistry } from "../registry/componentRegistry.ts";
import {
  adaptComponentDataHub,
  type RuntimeComponentSpec,
  type RuntimeDispatchResultContext,
  type RuntimeGuardedStateStore,
} from "./runtimeComponentAdapter.ts";
import { renderRuntimeComponent } from "./runtimePrimitiveRenderer.ts";
import {
  type ComponentDataHub,
  constructProjection,
  type ProjectionDefinition,
  type UiProjection,
} from "./projectionConstructor.ts";
import { ensureRuntimeComponentRegistryInitialized } from "./runtimeComponentRegistry.ts";
import type { RuntimeDispatchSpec } from "./frontendScheduler.ts";
import { resolvePropBindings } from "./propBindingResolver.ts";
import { mergeCatalogPropsWithComponentDesign } from "./mergeComponentDesignProps.ts";
import { buildPreviewInertEventBinding } from "./previewInertEventBinding.ts";
import { buildLayoutPreviewPlaceholderProps } from "./layoutComponentPreview.ts";
import { resolveUnknownCssTokenRefs } from "./cssDictionary.ts";
import { interpolateLinkHrefReadOnly } from "./linkPlaceholderInterpolation.ts";
import {
  type CalcBinding,
  type CalcContext,
  evaluateAllCalcBindings,
} from "./frontendLocalCalculationResolver.ts";
import { projectionInputFromData } from "./projectionInput.ts";
import {
  computeDispatchIdempotencyKey,
  wiringSettingCategoryOf,
} from "../lib/uiBuilderWiringProjection.ts";
import { resolveUiStateUpdateMutation } from "./uiEventEffectRunner.ts";
import {
  ADMIN_RUNTIME_READ_ACTIONS,
  isFieldFamilyComponentKind,
} from "./adminRuntimeReadActions.ts";

export type RenderEmissionOptions = {
  /**
   * Read-only projection (UI Builder inspection / draft preview). Uses inert
   * event bindings and relaxed factory checks — same contract as UI Builder
   * canvas preview.
   */
  previewMode?: boolean;
  /** Frontend-local calculation bindings from layout patch root. Evaluated without backend dispatch. */
  calculationBindings?: CalcBinding[];
  /** Live node values for calc binding trigger resolution. */
  calcNodeValues?: Record<string, Record<string, unknown>>;
  /**
   * Guarded projection-local UI state store consumed by runtime UI interaction
   * wiring. Both event-triggered mutations (built here) and lifecycle mutations
   * (uiEventEffectRunner) write through this same guarded dispatcher instance —
   * mutation authority is not duplicated across paths.
   */
  localStateStore?: RuntimeGuardedStateStore;
  /** Snapshot for dispatchExternalPort payloadFrom node:<nodeId>.value resolution. */
  payloadFromNodeValues?: Record<string, unknown>;
  /**
   * Registers a node's own latest scalar value under its stable nodeId — the
   * write side of payloadFromNodeValues. See liveNodeValueTracker.ts. Absent
   * by default (no-op) — callers that never wire this in keep today's
   * behavior unchanged.
   */
  onNodeValueChange?: (nodeId: string, value: unknown) => void;
  /**
   * Fires with a node's own settled admin_runtime dispatch result (this node's
   * nodeId + the DispatchResponse). Absent by default (no-op) — callers that
   * never wire this in keep today's fire-and-forget behavior unchanged. See
   * ComponentDataHub.onRuntimeDispatchResult / emitBoundEvent
   * (runtimeComponentFactory.ts) for where the result comes from.
   */
  onRuntimeDispatchResult?: (
    nodeId: string,
    result: DispatchResponse,
    context: RuntimeDispatchResultContext,
  ) => void;
};

export type ComponentSpec = {
  componentId?: string;
  componentType: string;
  def: Record<string, unknown>;
  runtime?: RuntimeComponentSpec;
  /**
   * Resolved RuntimeComponentSpec for catalog_component nodes.
   * Present when componentKind is known and adaptComponentDataHub succeeded.
   * ProjectionShell uses this to render via renderRuntimeComponent instead of SpecCard.
   */
  runtimeSpec?: RuntimeComponentSpec;
  /** Node identifier from layout_patch_json — present only when rendered from layoutNodes. */
  nodeId?: string;
  /** "catalog_component" | "structural_html" | "structural_node" | "unresolved_gap" — present only when rendered from layoutNodes. */
  nodeKind?: string;
  /** HTML element tag for structural_html nodes — present only when nodeKind="structural_html". */
  htmlTag?: string;
  /** Parent node for DOM nesting — present only when rendered from layoutNodes. */
  parentNodeId?: string;
  /** Slot name within the layout template — present only when rendered from layoutNodes. */
  slotKey?: string;
  /** Render order from layout node — present only when rendered from layoutNodes. */
  orderIndex?: number;
  /** @deprecated Legacy absolute canvas geometry — not projected in flow layout mode. */
  x?: number;
  /** @deprecated Legacy absolute canvas geometry — not projected in flow layout mode. */
  y?: number;
  /** Flow width — px number, percent string, or auto. */
  width?: number | string;
  /** Flow height — px number, percent string, or auto. */
  height?: number | string;
  /** Controls inline width style projection. See SizingMode in visualLayoutUtils.ts. */
  widthMode?: "auto" | "preset" | "custom";
  /** Controls inline height style projection. See SizingMode in visualLayoutUtils.ts. */
  heightMode?: "auto" | "preset" | "custom";
  /** SSOT topology-layout-class vocabulary refs for className resolution. */
  layoutClassRefs?: string[];
  /** structural_html text content from component_style_design.inlineText */
  inlineText?: string;
  /** css_dictionary token refs applied to wrapper inline style */
  cssTokenRefs?: string[];
};

/**
 * Returns true when wiringKind designates frontend-local route navigation.
 * navigation wiring does NOT produce a backend RuntimeDispatchSpec — execution is
 * client-side only (globalThis.location.href). route:<routeKey> must never reach
 * ManifestDispatcher as target_ref.
 */
export function isNavigationWiringKind(wiringKind: string): boolean {
  return wiringKind === "navigation";
}

/**
 * Parses "<layer>:<action>" out of a wiringKind=admin_runtime node's targetRef.
 *
 * targetRef MUST still be a valid ManifestDispatcher manifest reference —
 * "manifest:<manifestUuid>:<layer>:<action>" — never a bare "<layer>:<action>"
 * string: ManifestDispatcher.TryParseManifestTargetRef (backend/runtime/
 * ManifestDispatcher.cs) requires this exact "manifest:" prefix + UUID shape
 * to resolve WHICH manifest is authorizing the dispatch at all (the SAME
 * payload.target_ref this function reads is also forwarded verbatim as the
 * dispatch request's own target_ref — see enqueueRuntimeComponentCommand in
 * frontendScheduler.ts). A bare "<layer>:<action>" targetRef would make
 * ManifestDispatcher fail TARGET_REF_INVALID before ever reaching
 * AdminRuntime.ExecuteDataAsync — this was corrected after a live-DB proof
 * caught it (Split(':', 3) on "manifest:<uuid>:<key>" leaves parts[2] free-text
 * and unvalidated by manifest resolution, which is what carries "<layer>:
 * <action>" here). Seed side specifies the concrete admin_runtime operation
 * entirely as data (target_ref content) — this function adds no per-operation
 * case of its own, so it is reusable by any admin_runtime action
 * (enum_dictionary:*, auth_users:*, team_markdown:*, scheduler_jobs:*, ...),
 * never a single surface's dedicated handler. Returns null (fail-close, no
 * partial parse) when targetRef is absent or not exactly
 * "manifest:<uuid>:<layer>:<action>" (4 colon-separated segments, uuid a
 * syntactically plausible UUID, layer/action non-empty).
 * SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
 * lane_storage_boundary.known_gaps admin_runtime_layer_action_dispatch_lane_not_yet_defined
 * (extend_wiring_kind_vocabulary direction).
 */
const ADMIN_RUNTIME_TARGET_REF_RE =
  /^manifest:([0-9a-fA-F-]{36}):([^:]+):([^:]+)$/;

function parseAdminRuntimeLayerAction(
  targetRef: string | null | undefined,
): { layer: string; action: string } | null {
  const trimmed = targetRef?.trim();
  if (!trimmed) return null;
  const match = ADMIN_RUNTIME_TARGET_REF_RE.exec(trimmed);
  if (!match) return null;
  const [, , layer, action] = match;
  if (!layer || !action) return null;
  return { layer, action };
}

/**
 * Maps wiring_kind to the canonical layer for backend dispatch routing.
 * search → screen_list (ScreenDataShapeQueryRuntime), aggregate → screen_aggregation,
 * CRUD kinds → entity (RuntimeExecutor CRUD path), admin_runtime → the layer
 * encoded in targetRef (generic admin_runtime layer:action dispatch — see
 * parseAdminRuntimeLayerAction).
 * navigation is excluded — it is frontend-local and must not enter this mapping.
 * Returns null for unknown wiringKind — callers must treat null as a misconfiguration and not fall back.
 */
export function mapWiringKindToLayer(
  wiringKind: string,
  targetRef?: string | null,
): string | null {
  if (wiringKind === "search") return "screen_list";
  if (wiringKind === "aggregate") return "screen_aggregation";
  if (
    wiringKind === "create" || wiringKind === "update" ||
    wiringKind === "delete"
  ) return "entity";
  if (wiringKind === "admin_runtime") {
    return parseAdminRuntimeLayerAction(targetRef)?.layer ?? null;
  }
  return null;
}

/**
 * Maps wiring_kind to the canonical action string for backend dispatch.
 * Mirrors the backend MapWiringKindToDispatchAction mapping (admin_runtime is
 * frontend-only: NpgsqlTopologyRepository.RuntimeDispatchAction is unused by
 * any frontend caller, so no backend mirror update is required for this case).
 * Returns null for unknown wiringKind — callers must not pass raw unknown values as actions.
 */
export function mapWiringKindToAction(
  wiringKind: string,
  targetRef?: string | null,
): string | null {
  if (wiringKind === "admin_runtime") {
    return parseAdminRuntimeLayerAction(targetRef)?.action ?? null;
  }
  switch (wiringKind) {
    case "search":
      return "Search";
    case "aggregate":
      return "Search";
    case "create":
      return "Create";
    case "update":
      return "diffUpdate";
    case "delete":
      return "logicalDelete";
    default:
      return null;
  }
}

/**
 * Builds a RuntimeDispatchSpec from a layout node's wiring metadata.
 * Returns null when no wiringKind is set (no wiring configured → log lane only).
 * Returns null for navigation wiringKind — navigation is frontend-local, not backend dispatch.
 * targetSurface must be present and non-empty when wiringKind is set — absent targetSurface is a
 * wiring misconfiguration and must not silently fall back to "default".
 * Callers that receive null render an error node.
 */
export function buildRuntimeDispatchSpec(
  node: EmissionLayoutNode,
): RuntimeDispatchSpec | null {
  const wiringKind = node.wiringKind;
  if (!wiringKind) return null;
  if (isNavigationWiringKind(wiringKind)) return null;
  const targetSurface = node.targetSurface && node.targetSurface.trim();
  if (!targetSurface) return null;
  const action = mapWiringKindToAction(wiringKind, node.targetRef);
  if (!action) return null;
  const layer = mapWiringKindToLayer(wiringKind, node.targetRef);
  if (!layer) return null;
  return {
    operationType: action,
    target: targetSurface,
    layer,
    action,
    wiringKey: (node.wiringKey && node.wiringKey.trim())
      ? node.wiringKey.trim()
      : undefined,
    wiringId: (node.wiringId && node.wiringId.trim())
      ? node.wiringId.trim()
      : undefined,
    targetRef: (node.targetRef && node.targetRef.trim())
      ? node.targetRef.trim()
      : undefined,
  };
}

/**
 * Builds honest production default props for a catalog_component leaf from its authored schema
 * label (layout_schema_json.records[].record.label — see LayoutSchemaTensorComposer.Compose)
 * — never the UI-Builder canvas-preview placeholder's inert content (a hardcoded caption, a
 * fabricated sample option list, or a forced disabled:true). A schema-composed leaf never
 * carries node-local propsJson/design to override these, so buildLayoutPreviewPlaceholderProps'
 * disabled:true for action/button would otherwise reach the real production DOM unconditionally.
 * Component kinds not modeled explicitly here fall back to the existing placeholder behavior
 * unchanged (their production content is expected to arrive via node-local propsJson/design, as
 * before — this function only closes the gap for schema-composed leaves that never get one).
 */
function buildProductionCatalogComponentProps(
  node: EmissionLayoutNode,
  componentKind: string,
  componentKey: string,
): Record<string, unknown> {
  const authoredLabel = node.label?.trim();
  switch (componentKind) {
    case "action/button":
      return {
        data: {
          label: authoredLabel || componentKey,
          variant: "primary",
          disabled: false,
        },
      };
    case "form_input/form_field":
      return { data: { label: authoredLabel || componentKey } };
    case "form_input/select":
      // The option list is business data the schema record does not carry — an honest empty
      // list, never a fabricated sample option, until real option data is wired.
      return {
        data: {
          value: "",
          options: [],
          label: authoredLabel,
          placeholder: authoredLabel || "",
        },
      };
    default:
      return buildLayoutPreviewPlaceholderProps(componentKind, componentKey);
  }
}

/**
 * Builds minimum renderable props for a catalog_component node. previewMode uses the UI-Builder
 * canvas-preview placeholder (intentionally inert — disabled actions, sample content — for
 * unset/unauthored canvas nodes); non-preview (production) rendering never injects that inert
 * state — see buildProductionCatalogComponentProps.
 */
function buildDefaultCatalogComponentProps(
  node: EmissionLayoutNode,
  previewMode: boolean,
): Record<string, unknown> {
  const componentKey = (node.componentKey && node.componentKey.trim())
    ? node.componentKey.trim()
    : (node.nodeId ?? "Component");
  const componentKind = node.componentKind?.trim();
  if (!componentKind) {
    return { data: { label: node.label?.trim() || componentKey } };
  }
  if (previewMode) {
    return buildLayoutPreviewPlaceholderProps(componentKind, componentKey);
  }
  return buildProductionCatalogComponentProps(
    node,
    componentKind,
    componentKey,
  );
}

/**
 * Merges node-local propsJson/stateJson over the default props.
 * propsJson (when present and valid) is shallow-merged over the default props top-level.
 * stateJson (when present and valid) is merged into props.data when data is an object, else top-level.
 * Invalid JSON returns explicit error — no silent fallback.
 */
export function mergeNodeLocalProps(
  baseProps: Record<string, unknown>,
  propsJson: string | null | undefined,
  stateJson: string | null | undefined,
): { ok: true; props: Record<string, unknown> } | { ok: false; error: string } {
  let props = { ...baseProps };

  if (propsJson && propsJson.trim()) {
    let parsed: unknown;
    try {
      parsed = JSON.parse(propsJson.trim());
    } catch {
      return {
        ok: false,
        error:
          "LAYOUT_NODE_PROPS_JSON_INVALID: propsJson が JSON として解析できません",
      };
    }
    if (
      typeof parsed !== "object" || parsed === null || Array.isArray(parsed)
    ) {
      return {
        ok: false,
        error:
          "LAYOUT_NODE_PROPS_JSON_INVALID: propsJson はオブジェクトである必要があります",
      };
    }
    props = { ...props, ...(parsed as Record<string, unknown>) };
  }

  if (stateJson && stateJson.trim()) {
    let parsedState: unknown;
    try {
      parsedState = JSON.parse(stateJson.trim());
    } catch {
      return {
        ok: false,
        error:
          "LAYOUT_NODE_STATE_JSON_INVALID: stateJson が JSON として解析できません",
      };
    }
    if (
      typeof parsedState !== "object" || parsedState === null ||
      Array.isArray(parsedState)
    ) {
      return {
        ok: false,
        error:
          "LAYOUT_NODE_STATE_JSON_INVALID: stateJson はオブジェクトである必要があります",
      };
    }
    const stateObj = parsedState as Record<string, unknown>;
    const existingData = props.data;
    if (
      typeof existingData === "object" && existingData !== null &&
      !Array.isArray(existingData)
    ) {
      props = {
        ...props,
        data: { ...(existingData as Record<string, unknown>), ...stateObj },
      };
    } else {
      props = { ...props, ...stateObj };
    }
  }

  return { ok: true, props };
}

/**
 * The exact set of triggers component_wiring_execution_lane's dispatch binding
 * generator (buildCatalogComponentEventBinding below) actually creates an
 * eventBinding entry for — the single authority both that function and
 * buildAdminRuntimePayloadFromByTrigger's trigger validation share (PR #599
 * review round 7: previously duplicated as a separate literal array in each,
 * which is how "input"/"focus"/"blur" — recognized by the broader
 * normalizeAuthoredEventType() but never members of this 5-trigger set — could
 * pass dispatchPayloadFromByTrigger's trigger validation yet silently have no
 * binding to attach to). SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml
 * admin_runtime_payload_binding_contract.required_fields.trigger.
 */
const COMPONENT_WIRING_EXECUTION_LANE_TRIGGERS = [
  "click",
  "change",
  "select",
  "submit",
  "toggle",
] as const;

export type AdminRuntimePayloadFromByTriggerResult =
  | { ok: true; byTrigger: Record<string, Record<string, string>> }
  | { ok: false; error: string };

/**
 * Reads a node's OWN dispatchPayloadFromByTrigger field — { trigger: { field: source } } —
 * a per-node, data-only payload binding for this SAME node's admin_runtime dispatch
 * (component_wiring_execution_lane, wiring_kind="admin_runtime"). SSOT:
 * admin-uibuilder-ui-structure-wiring-ssot.yaml lane_storage_boundary.known_gaps.
 * remaining_write_payload_capture_gap write_payload_capture_mechanism_implemented.
 *
 * This field carries NO action authority of its own and lives OUTSIDE
 * runtimeInteractions[]/actionType entirely (PR #599 review round 6: action authority
 * (actionType, closed vocabulary) and effect data (payloadFrom) are separate concepts —
 * a data-only payload binding must not be expressed as a fake runtimeInteractions[]
 * actionType, and effect fields must never promote an unrecognized actionType past
 * ACTION_OUTSIDE_VOCABULARY). Every admin_runtime action (enum_dictionary:*, auth_users:*,
 * team_markdown:*, scheduler_jobs:*, ...) reuses this SAME node-level field — no
 * per-operation case.
 *
 * dispatchPayloadFromByTrigger is a single object keyed by RAW trigger key, so two
 * entries can never share the exact same raw key — but normalizeAuthoredEventType()
 * maps multiple raw aliases onto the same canonical trigger (e.g. "click" and
 * "onClick" both -> "click"), so a conflict CAN still occur after normalization
 * (PR #599 review round 7 correction: round 6's claim that duplicate_field_conflict
 * became "structurally impossible" was true only for identical raw keys, not for
 * alias collisions — the two are different claims). Two raw keys normalizing to the
 * same canonical trigger fail the WHOLE node closed
 * (RUNTIME_INTERACTION_TRIGGER_CONFLICT_AFTER_NORMALIZATION) rather than silently
 * letting the later one win. A raw trigger key that normalizeAuthoredEventType()
 * recognizes but that is not a member of COMPONENT_WIRING_EXECUTION_LANE_TRIGGERS
 * (e.g. "input"/"focus"/"blur" — valid triggers for OTHER lanes, never for this
 * one) also fails the WHOLE node closed (RUNTIME_INTERACTION_TRIGGER_UNSUPPORTED)
 * instead of silently validating into a payloadFrom map with no binding to ever
 * attach to. A present-but-malformed value still fails the WHOLE node closed, never
 * silently skipped/filtered: an unrecognized trigger key, a non-object per-trigger map, or
 * a non-string field value, or an empty per-trigger map ({}) — the SAME error-code vocabulary
 * (RUNTIME_INTERACTION_PAYLOAD_FROM_MUST_BE_OBJECT / _VALUE_MUST_BE_STRING / _EMPTY /
 * RUNTIME_INTERACTION_TRIGGER_REQUIRED / _UNSUPPORTED / _CONFLICT_AFTER_NORMALIZATION)
 * the backend's own ValidateDispatchPayloadFromByTrigger (NpgsqlUiTopologyRepository.cs,
 * reusing its existing ValidatePayloadFromShape helper — with rejectEmpty:true only for THIS
 * caller — also used by dispatchExternalPort/dispatchInstanceOperation's own payloadFrom)
 * validates at the persistence boundary — not an admin-specific vocabulary.
 *
 * PR #599 review round 8 correction: an earlier version of this function let an empty
 * per-trigger map ({}) pass, reasoning it matched the backend's own leniency for
 * dispatchExternalPort/dispatchInstanceOperation's payloadFrom shape. That leniency claim
 * was accurate for THOSE two actionTypes' own persistence-time check, but this field's own
 * DISPATCH-time boundary (runtimeComponentFactory.ts parseEventBinding's
 * runtimeDispatch.payloadFrom branch) already rejected {} before this correction — so build
 * time and persistence time silently accepting what dispatch time would later reject was a
 * genuine three-boundary mismatch for this field specifically, not a deliberate leniency
 * match. Now build time, persistence time, and dispatch time all reject {} for
 * dispatchPayloadFromByTrigger; dispatchExternalPort/dispatchInstanceOperation's own
 * payloadFrom keeps its separate, unchanged (still lenient) contract, since ITS dispatch-time
 * boundary tolerates {} too and is out of this Bundle's scope.
 */
function buildAdminRuntimePayloadFromByTrigger(
  rawByTrigger: unknown,
): AdminRuntimePayloadFromByTriggerResult {
  if (rawByTrigger === undefined || rawByTrigger === null) {
    return { ok: true, byTrigger: {} };
  }
  if (typeof rawByTrigger !== "object" || Array.isArray(rawByTrigger)) {
    return {
      ok: false,
      error:
        `RUNTIME_INTERACTION_DISPATCH_PAYLOAD_FROM_BY_TRIGGER_MUST_BE_OBJECT: dispatchPayloadFromByTrigger must be an object`,
    };
  }
  const byTrigger: Record<string, Record<string, string>> = {};
  for (const [rawTrigger, payloadFromRaw] of Object.entries(rawByTrigger)) {
    const trigger = normalizeAuthoredEventType(rawTrigger);
    if (!trigger) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_TRIGGER_REQUIRED: dispatchPayloadFromByTrigger has an unrecognized trigger key "${rawTrigger}"`,
      };
    }
    if (
      !(COMPONENT_WIRING_EXECUTION_LANE_TRIGGERS as readonly string[]).includes(
        trigger,
      )
    ) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_TRIGGER_UNSUPPORTED: dispatchPayloadFromByTrigger trigger "${rawTrigger}" normalizes to "${trigger}", which component_wiring_execution_lane does not bind (supported: ${
            COMPONENT_WIRING_EXECUTION_LANE_TRIGGERS.join(", ")
          })`,
      };
    }
    if (Object.prototype.hasOwnProperty.call(byTrigger, trigger)) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_TRIGGER_CONFLICT_AFTER_NORMALIZATION: dispatchPayloadFromByTrigger has more than one raw trigger key normalizing to "${trigger}"`,
      };
    }
    if (
      typeof payloadFromRaw !== "object" || payloadFromRaw === null ||
      Array.isArray(payloadFromRaw)
    ) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_PAYLOAD_FROM_MUST_BE_OBJECT: dispatchPayloadFromByTrigger entry for trigger "${trigger}" is not an object`,
      };
    }
    if (Object.keys(payloadFromRaw).length === 0) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_PAYLOAD_FROM_EMPTY: dispatchPayloadFromByTrigger entry for trigger "${trigger}" declares no fields`,
      };
    }
    const payloadFrom: Record<string, string> = {};
    for (const [field, value] of Object.entries(payloadFromRaw)) {
      if (typeof value !== "string") {
        return {
          ok: false,
          error:
            `RUNTIME_INTERACTION_PAYLOAD_FROM_VALUE_MUST_BE_STRING: dispatchPayloadFromByTrigger entry for trigger "${trigger}" field "${field}" is not a string source`,
        };
      }
      payloadFrom[field] = value;
    }
    byTrigger[trigger] = payloadFrom;
  }
  return { ok: true, byTrigger };
}

export type AdminRuntimeTargetRefOverrideByTriggerResult =
  | { ok: true; byTrigger: Record<string, RuntimeDispatchSpec> }
  | { ok: false; error: string };

/**
 * Reads a node's OWN dispatchTargetRefByTrigger field — { trigger: "manifest:<uuid>:<layer>:<action>" } —
 * a per-node, per-trigger admin_runtime dispatch TARGET override, independent of the layout's own
 * uniform wiringKind="admin_runtime"/targetRef (which NpgsqlTopologyRepository.LoadLayoutNodesAsync
 * applies unconditionally to every non-structural node from the layout's single ui_wiring_registry
 * row). This is the SAME "per-trigger authored target, independent of the shared wiring row" pattern
 * dispatchExternalPort/dispatchInstanceOperation already use (wiring.portTargetRef /
 * wiring.instanceTargetRef, read directly off the node's own runtimeInteractions[] entry in
 * buildExternalPortEventBinding below) — this field applies that existing, precedented pattern to
 * admin_runtime, letting a single per-screen layout host nodes for MULTIPLE admin_runtime operations
 * (e.g. a create-modal's own submit button dispatching enum_dictionary:create_group while the rest of
 * the SAME layout keeps dispatching enum_dictionary:list_groups) without any new component kind,
 * actionType, runtime lane, or payload resolver, and without changing the layout's own default/
 * fallback target for every node that does NOT author this field.
 *
 * Value shape and validation mirror parseAdminRuntimeLayerAction's own requirement exactly — each
 * per-trigger value must be the SAME "manifest:<uuid>:<layer>:<action>" ManifestDispatcher-resolvable
 * target_ref shape node.targetRef itself uses (never a bare "<layer>:<action>"). Trigger key
 * normalization/collision/support rules mirror buildAdminRuntimePayloadFromByTrigger exactly (same
 * error-code vocabulary) so authors and the backend persistence-boundary validator
 * (NpgsqlUiTopologyRepository.ValidateDispatchTargetRefByTrigger) agree on the same accept/reject
 * decisions for the same inputs. A present-but-malformed value fails the WHOLE node closed, never
 * silently skipped.
 */
function buildAdminRuntimeTargetRefOverrideByTrigger(
  rawByTrigger: unknown,
  targetSurface: string | null | undefined,
  componentKind: string | null | undefined,
): AdminRuntimeTargetRefOverrideByTriggerResult {
  if (rawByTrigger === undefined || rawByTrigger === null) {
    return { ok: true, byTrigger: {} };
  }
  if (typeof rawByTrigger !== "object" || Array.isArray(rawByTrigger)) {
    return {
      ok: false,
      error:
        `RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_MUST_BE_OBJECT: dispatchTargetRefByTrigger must be an object`,
    };
  }
  const surface = targetSurface?.trim();
  const byTrigger: Record<string, RuntimeDispatchSpec> = {};
  for (const [rawTrigger, targetRefRaw] of Object.entries(rawByTrigger)) {
    const trigger = normalizeAuthoredEventType(rawTrigger);
    if (!trigger) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_TRIGGER_REQUIRED: dispatchTargetRefByTrigger has an unrecognized trigger key "${rawTrigger}"`,
      };
    }
    if (
      !(COMPONENT_WIRING_EXECUTION_LANE_TRIGGERS as readonly string[]).includes(
        trigger,
      )
    ) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_TRIGGER_UNSUPPORTED: dispatchTargetRefByTrigger trigger "${rawTrigger}" normalizes to "${trigger}", which component_wiring_execution_lane does not bind (supported: ${
            COMPONENT_WIRING_EXECUTION_LANE_TRIGGERS.join(", ")
          })`,
      };
    }
    if (Object.prototype.hasOwnProperty.call(byTrigger, trigger)) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_TRIGGER_CONFLICT_AFTER_NORMALIZATION: dispatchTargetRefByTrigger has more than one raw trigger key normalizing to "${trigger}"`,
      };
    }
    if (typeof targetRefRaw !== "string" || !targetRefRaw.trim()) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_VALUE_MUST_BE_STRING: dispatchTargetRefByTrigger entry for trigger "${trigger}" is not a non-empty string`,
      };
    }
    const targetRef = targetRefRaw.trim();
    const parsed = parseAdminRuntimeLayerAction(targetRef);
    if (!parsed) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_TARGET_REF_INVALID: dispatchTargetRefByTrigger entry for trigger "${trigger}" is not a valid "manifest:<uuid>:<layer>:<action>" target_ref`,
      };
    }
    // Round 37 defense-in-depth layer 3 of 3 (translator authoring-time / backend layout_patch
    // save-time / THIS live runtime-dispatch-time render boundary) -- a Field-family node
    // (componentKind starting with "form_input/") must resolve to a resource:action listed in
    // ADMIN_RUNTIME_READ_ACTIONS. Even if BOTH earlier layers were bypassed (a directly-DB-edited
    // layout_patch_json reaching this render/dispatch-wiring boundary without ever going through
    // the translator or the NpgsqlUiTopologyRepository save-time validator), this is the last point
    // before a click/keystroke handler is actually wired up to fire the request -- refusing here
    // means no request is ever sent, not merely that one was rejected server-side after the fact.
    if (
      isFieldFamilyComponentKind(componentKind) &&
      !ADMIN_RUNTIME_READ_ACTIONS.has(`${parsed.layer}:${parsed.action}`)
    ) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_FIELD_DISPATCH_TARGET_REF_NOT_READ_ACTION: dispatchTargetRefByTrigger entry for trigger "${trigger}" on Field-family componentKind "${componentKind}" resolves to "${parsed.layer}:${parsed.action}", which is not a member of ADMIN_RUNTIME_READ_ACTIONS`,
      };
    }
    if (!surface) {
      return {
        ok: false,
        error:
          `RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_TARGET_SURFACE_MISSING: dispatchTargetRefByTrigger entry for trigger "${trigger}" requires the node's own targetSurface`,
      };
    }
    // wiringKey/wiringId are intentionally OMITTED here (unlike buildRuntimeDispatchSpec's base
    // spec, which carries the layout's own uniform ui_wiring_registry row identity). Carrying the
    // layout's wiringKey/wiringId over into an override dispatch would be actively misleading —
    // they identify the layout's OWN wiring row, not the different manifest/layer/action this
    // override actually targets, and no backend consumer reads request.wiring_key/wiring_id at the
    // top request level today (confirmed via full grep of ManifestDispatcher/OperationVectorResolver/
    // AdminRuntimeDispatchAdapter — they are forwarded but never read there; the persisted audit
    // trail, AdminMasterRosterAudit's actor/target_table/target_id/operation/before/after/
    // changed_fields, is populated server-side keyed by layer:action and is entirely independent of
    // whatever wiring_key/wiring_id the frontend happens to send). No override-specific identity is
    // fabricated in their place.
    byTrigger[trigger] = {
      operationType: parsed.action,
      target: surface,
      layer: parsed.layer,
      action: parsed.action,
      targetRef,
    };
  }
  return { ok: true, byTrigger };
}

/**
 * Builds an eventBinding for a catalog_component node from its RuntimeDispatchSpec.
 * Populates standard triggers (click, change, select, submit, toggle) each carrying
 * the full runtimeDispatch spec so emitBoundEvent fires both log and dispatch lanes.
 * Returns empty object when spec is null/absent AND no trigger has its own
 * targetRefOverrideByTrigger entry (log lane only). payloadFromByTrigger (built by
 * buildAdminRuntimePayloadFromByTrigger) attaches a per-trigger payloadFrom map onto
 * that trigger's own spec copy — absent for triggers with no authored entry.
 * targetRefOverrideByTrigger (built by buildAdminRuntimeTargetRefOverrideByTrigger)
 * REPLACES that trigger's spec entirely with a different admin_runtime layer:action
 * target — absent for triggers with no authored override, which keep using spec
 * (the layout's own uniform target) unchanged.
 *
 * Round 41/42 (Owner decision, admin-uibuilder-ui-structure-wiring-ssot.yaml
 * owner_decision_2026_08_13_explicit_dispatch_participation_required, superseded/
 * generalized by owner_decision_2026_08_14_general_dispatch_participation_contract_round42):
 * for EVERY node whose wiringKind is "admin_runtime" — regardless of componentKind, Field
 * or not — a layout merely HAVING a default spec must never by itself bind a trigger to a
 * REAL business-operation dispatch. A trigger only gets a runtimeDispatch binding when the
 * node's OWN authoring/topology data explicitly participates for that trigger: either
 * targetRefOverrideByTrigger[trigger] (a target override) or payloadFromByTrigger[trigger]
 * (a payload-shape customization, falling back to spec as the target since authoring a
 * payload shape for a specific trigger is itself an explicit, per-trigger authoring
 * signal — this pre-existing generic base-spec-plus-payloadFrom-only pattern, used
 * outside admin-enum too, is preserved unchanged). A trigger with NEITHER authored gets no
 * business dispatch at all, regardless of componentKind (confirmed via the real
 * admin_enum_ae200_layout_nodes.json fixture: every write-only typed Field like
 * enum_create_group_name_input has neither, yet its "change" trigger was previously
 * bound anyway purely because the layout happens to be admin_runtime — spurious
 * dispatch on every keystroke, confounding search-debounce non-interference).
 *
 * Round 41 first shipped this scoped to Field-family nodes only
 * (isFieldFamilyComponentKind), reverting a broader draft after it broke ~40
 * pre-existing tests that relied on non-Field nodes' implicit base-spec dispatch as
 * untested-as-such behavior — most concretely enum_table's own "select" trigger (row
 * click) re-dispatching list_groups. Round 42 (Owner clarification) corrects that: the
 * contract was never Field-specific, and an existing test asserting the OLD implicit
 * inheritance is not itself authority for keeping the contract narrower than the actual
 * Owner decision — the capability those tests protected is preserved by AUTHORING
 * explicit dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger on the affected real
 * production nodes (the same authoring shape every already-explicit ae200 node already
 * uses), not by a componentKind conditional in this function. There is no
 * isFieldFamilyComponentKind check left in this function's gating as of round 42; every
 * wiringKind === "admin_runtime" node is scoped identically regardless of componentKind.
 * Every other wiringKind (search/aggregate/create/update/delete) keeps its pre-existing
 * base-spec-fallback behavior for EVERY trigger unconditionally — a separate,
 * pre-existing contract this decision does not revisit.
 *
 * Every admin_runtime trigger still gets an eventType-only binding entry even without
 * dispatch participation (`{ eventType: trigger }`, no runtimeDispatch key) rather than
 * being omitted entirely — omitting it was tried first and reverted:
 * runtimeComponentFactory.ts's requireBinding (inputFactory/buttonFactory/etc.) fails a
 * component's entire render closed when its own required trigger key is absent from
 * eventBinding at all, which would have made every real write-only Field (e.g.
 * enum_create_group_name_input) stop rendering as an `<input>` entirely — a functional
 * regression far worse than the spurious-dispatch bug this round fixes. An
 * eventType-only entry keeps requireBinding satisfied and keeps emitBoundEvent's
 * pre-dispatch lanes (onNodeValueChange node-value tracking, calcTriggerCallback, the
 * observation log) working exactly as before, while parseEventBinding's own optional
 * runtimeDispatch means no business dispatch fires — this is what actually eliminates
 * the spurious dispatch, not the entry's mere absence.
 */
export function buildCatalogComponentEventBinding(
  spec: RuntimeDispatchSpec | null,
  payloadFromByTrigger: Record<string, Record<string, string>> = {},
  targetRefOverrideByTrigger: Record<string, RuntimeDispatchSpec> = {},
  nodeWiringKind?: string,
): Record<string, unknown> {
  const triggers = COMPONENT_WIRING_EXECUTION_LANE_TRIGGERS;
  const binding: Record<string, unknown> = {};
  const requiresExplicitParticipation = nodeWiringKind === "admin_runtime";
  for (const trigger of triggers) {
    const overrideSpec = targetRefOverrideByTrigger[trigger];
    const payloadFrom = payloadFromByTrigger[trigger];
    if (requiresExplicitParticipation) {
      const hasExplicitParticipation = overrideSpec !== undefined ||
        payloadFrom !== undefined;
      const triggerSpec = hasExplicitParticipation ? (overrideSpec ?? spec) : null;
      binding[trigger] = triggerSpec
        ? {
          eventType: trigger,
          runtimeDispatch: payloadFrom ? { ...triggerSpec, payloadFrom } : triggerSpec,
        }
        : { eventType: trigger };
      continue;
    }
    const triggerSpec = overrideSpec ?? spec;
    if (!triggerSpec) continue;
    binding[trigger] = {
      eventType: trigger,
      runtimeDispatch: payloadFrom ? { ...triggerSpec, payloadFrom } : triggerSpec,
    };
  }
  return binding;
}

/**
 * Builds an eventBinding for a navigation wiringKind node.
 * Populates standard triggers each carrying routeNavigation: { targetRef } instead of
 * runtimeDispatch, so emitBoundEvent executes frontend-local route navigation (not backend dispatch).
 * Returns empty object when targetRef is absent or not a route: prefix.
 */
export function buildRouteNavigationEventBinding(
  targetRef: string | null | undefined,
): Record<string, unknown> {
  if (!targetRef) return {};
  const ref = targetRef.trim();
  if (!ref.startsWith("route:")) return {};
  const triggers = COMPONENT_WIRING_EXECUTION_LANE_TRIGGERS;
  const binding: Record<string, unknown> = {};
  for (const trigger of triggers) {
    binding[trigger] = {
      eventType: trigger,
      routeNavigation: { targetRef: ref },
    };
  }
  return binding;
}

function normalizeAuthoredEventType(value: unknown): string | null {
  if (typeof value !== "string") return null;
  const trimmed = value.trim();
  const map: Record<string, string> = {
    onClick: "click",
    click: "click",
    onChange: "change",
    change: "change",
    onInput: "input",
    input: "input",
    onSubmit: "submit",
    submit: "submit",
    onOpen: "toggle",
    onClose: "toggle",
    toggle: "toggle",
    onFocus: "focus",
    focus: "focus",
    onBlur: "blur",
    blur: "blur",
    onSelect: "select",
    select: "select",
  };
  return map[trimmed] ?? null;
}

/**
 * UI状態更新 event binding. Classification (wiringSettingCategoryOf) and target
 * resolution (resolveUiStateUpdateMutation) are shared with the lifecycle path
 * in uiEventEffectRunner.ts, so both paths agree on exactly which actionTypes
 * are UI状態更新 and how targetNodeId/statePath/value/action are derived from
 * them — the actual guarded write happens later in emitBoundEvent, through the
 * same dispatcher instance the lifecycle path uses.
 */
function buildLocalUiStateEventBinding(
  rawWirings: unknown,
): Record<string, unknown> {
  if (!Array.isArray(rawWirings)) return {};
  const binding: Record<string, unknown> = {};
  for (const raw of rawWirings) {
    if (typeof raw !== "object" || raw === null || Array.isArray(raw)) continue;
    const wiring = raw as Record<string, unknown>;
    const trigger = normalizeAuthoredEventType(
      wiring.trigger ?? wiring.eventType,
    );
    const actionType = typeof wiring.actionType === "string"
      ? wiring.actionType.trim()
      : "";
    if (!trigger || !actionType) continue;
    if (wiringSettingCategoryOf({ actionType }) !== "ui_state_update") continue;
    const resolved = resolveUiStateUpdateMutation({
      actionType,
      targetNodeId: typeof wiring.targetNodeId === "string"
        ? wiring.targetNodeId
        : undefined,
      statePath: typeof wiring.statePath === "string"
        ? wiring.statePath
        : undefined,
      value: wiring.value,
      targetRef: typeof wiring.targetRef === "string"
        ? wiring.targetRef
        : undefined,
    });
    if (!resolved) continue;
    const previous =
      typeof binding[trigger] === "object" && binding[trigger] !== null
        ? binding[trigger] as Record<string, unknown>
        : { eventType: trigger };
    binding[trigger] = {
      ...previous,
      eventType: trigger,
      localStateMutation: resolved,
    };
  }
  return binding;
}

/**
 * Backend-side dispatch bindings (外部API連携 / 外部インスタンス連携).
 * dispatchExternalPort → externalPortDispatch lane; dispatchInstanceOperation →
 * instanceOperationDispatch lane. Both execute through the api_command_lane in
 * emitBoundEvent; preview stays inert (caller skips this builder in previewMode).
 */
function buildExternalPortEventBinding(
  rawWirings: unknown,
  identity: {
    layoutId?: string | null;
    packageId?: string | null;
    nodeId: string;
  },
): Record<string, unknown> {
  if (!Array.isArray(rawWirings)) return {};
  const binding: Record<string, unknown> = {};
  for (const [interactionIndex, raw] of rawWirings.entries()) {
    if (typeof raw !== "object" || raw === null || Array.isArray(raw)) continue;
    const wiring = raw as Record<string, unknown>;
    const trigger = normalizeAuthoredEventType(
      wiring.trigger ?? wiring.eventType,
    );
    if (!trigger) continue;
    const payloadFromRaw = wiring.payloadFrom;
    const payloadFrom =
      (typeof payloadFromRaw === "object" && payloadFromRaw !== null &&
          !Array.isArray(payloadFromRaw))
        ? Object.fromEntries(
          Object.entries(payloadFromRaw).filter(([, value]) =>
            typeof value === "string"
          ),
        ) as Record<string, string>
        : {};
    const outputProp =
      typeof wiring.outputProp === "string" && wiring.outputProp.trim()
        ? wiring.outputProp.trim()
        : undefined;
    // high_frequency_policy: debounceMs travels with the binding so the runtime
    // dispatch guard in emitBoundEvent can fail close at event time — not only
    // at authoring/apply time — when a high-frequency trigger lacks it.
    const debounceMs = typeof wiring.debounceMs === "number"
      ? wiring.debounceMs
      : undefined;
    const actionType = typeof wiring.actionType === "string"
      ? wiring.actionType
      : "";
    // projection_authority_runtime_interaction_identity: read-only forward of the
    // backend-assigned id (never generated/mutated here). Absent on entries not
    // yet re-persisted since the field was introduced — computeDispatchIdempotencyKey
    // falls back to nodeId+interactionIndex in that case.
    const runtimeInteractionId =
      typeof wiring.runtimeInteractionId === "string" &&
        wiring.runtimeInteractionId.trim()
        ? wiring.runtimeInteractionId.trim()
        : undefined;
    if (wiring.actionType === "dispatchExternalPort") {
      const portTargetRef = typeof wiring.portTargetRef === "string"
        ? wiring.portTargetRef.trim()
        : "";
      // retry_safe_dispatch_idempotency: identity-only base key computed here at
      // binding-build time (stable across reload/reconnect for the SAME authored
      // interaction); emitBoundEvent extends it with the event-time RESOLVED
      // payload via appendResolvedPayloadToIdempotencyKey before dispatching.
      const idempotencyKeyBase = computeDispatchIdempotencyKey({
        layoutId: identity.layoutId,
        packageId: identity.packageId,
        nodeId: identity.nodeId,
        interactionIndex,
        runtimeInteractionId,
        trigger,
        actionType,
        targetRef: portTargetRef,
      });
      binding[trigger] = {
        eventType: trigger,
        externalPortDispatch: {
          portTargetRef,
          payloadFrom,
          outputProp,
          debounceMs,
          idempotencyKeyBase,
        },
      };
    } else if (wiring.actionType === "dispatchInstanceOperation") {
      const instanceTargetRef = typeof wiring.instanceTargetRef === "string"
        ? wiring.instanceTargetRef.trim()
        : "";
      const idempotencyKeyBase = computeDispatchIdempotencyKey({
        layoutId: identity.layoutId,
        packageId: identity.packageId,
        nodeId: identity.nodeId,
        interactionIndex,
        runtimeInteractionId,
        trigger,
        actionType,
        targetRef: instanceTargetRef,
      });
      binding[trigger] = {
        eventType: trigger,
        instanceOperationDispatch: {
          instanceTargetRef,
          payloadFrom,
          outputProp,
          debounceMs,
          idempotencyKeyBase,
        },
      };
    }
  }
  return binding;
}

function applyLocalStateOverrides(
  props: Record<string, unknown>,
  nodeId: string | undefined,
  localStateStore: RuntimeGuardedStateStore | undefined,
): Record<string, unknown> {
  if (!nodeId || !localStateStore) return props;
  const open = localStateStore.get(nodeId, "open");
  if (open === undefined) return props;
  const existingData = props.data;
  if (
    typeof existingData === "object" && existingData !== null &&
    !Array.isArray(existingData)
  ) {
    return {
      ...props,
      data: { ...(existingData as Record<string, unknown>), open },
    };
  }
  return { ...props, open };
}

/**
 * Promotes the live node value tracker (liveNodeValueTracker.ts) to the SAME
 * canonical authority for a node's DISPLAYED value that Lane 2's payloadFrom
 * resolution already uses for its DISPATCHED value — closing a real
 * display/dispatch authority divergence: without this, an SSE-refresh-driven
 * rerender resets a surviving controlled input's displayed value to its
 * emission-derived default (buildDefaultCatalogComponentProps rebuilds every
 * leaf's default props from scratch), while a later dispatch would still
 * silently resolve and send the tracker's pre-refresh typed value — sending a
 * value the user can no longer see on screen.
 *
 * Own-property identity (not `in`/bracket truthiness) matches
 * liveNodeValueTracker.ts/payloadFromResolver.ts's existing contract. Applies
 * ONLY when (a) the tracker has an entry for this exact nodeId (untouched
 * nodes are entirely unaffected — no invented value) and (b) the node's
 * already-built default props carry a `data.value` key (never invents a
 * "value" concept for a component kind that doesn't have one — e.g.
 * action/button, form_input/form_field). Applied BEFORE propBindings
 * resolution (resolvePropBindings runs later in the pipeline), so a node
 * whose value is ALSO server-data-bound via propBindings still lets that
 * fresher, data-driven binding win — a stale local edit is not preferred over
 * live server data once the projection actually carries one.
 */
function applyLiveNodeValueOverride(
  props: Record<string, unknown>,
  nodeId: string | undefined,
  payloadFromNodeValues: Record<string, unknown> | undefined,
): Record<string, unknown> {
  if (!nodeId || !payloadFromNodeValues) return props;
  if (!Object.prototype.hasOwnProperty.call(payloadFromNodeValues, nodeId)) {
    return props;
  }
  const trackedValue = payloadFromNodeValues[nodeId];
  const existingData = props.data;
  if (
    typeof existingData === "object" && existingData !== null &&
    !Array.isArray(existingData) &&
    Object.prototype.hasOwnProperty.call(existingData, "value")
  ) {
    return {
      ...props,
      data: {
        ...(existingData as Record<string, unknown>),
        value: trackedValue,
      },
    };
  }
  return props;
}

/**
 * Builds a map from nodeId → children (sorted by orderIndex) for tree rendering.
 * Root nodes have parentNodeId === undefined; look them up with key undefined.
 * Pure function — no DOM or Preact dependency.
 */
export function buildChildrenMap(
  specs: ComponentSpec[],
): Map<string | undefined, ComponentSpec[]> {
  const map = new Map<string | undefined, ComponentSpec[]>();
  for (const spec of specs) {
    const key = spec.parentNodeId ?? undefined;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(spec);
  }
  for (const children of map.values()) {
    children.sort((a, b) => (a.orderIndex ?? 0) - (b.orderIndex ?? 0));
  }
  return map;
}

/**
 * Evaluates calculationBindings (from layout patch root) against emission data and
 * node values, then injects results into matching ComponentSpecs.
 * No backend dispatch — frontend-local only.
 * Unresolved/error results are recorded on the spec as calc_error (not thrown).
 */
export function applyCalcBindingsToSpecs(
  specs: ComponentSpec[],
  calculationBindings: CalcBinding[],
  emissionData: Record<string, unknown>,
  nodeValues: Record<string, Record<string, unknown>> = {},
): ComponentSpec[] {
  if (calculationBindings.length === 0) return specs;
  const ctx: CalcContext = { nodeValues, emissionData };
  const results = evaluateAllCalcBindings(calculationBindings, ctx);
  if (results.size === 0) return specs;

  return specs.map((spec) => {
    if (!spec.nodeId) return spec;
    const overrideEntries = [...results.values()].filter(
      (e) => e.targetNodeId === spec.nodeId,
    );
    if (overrideEntries.length === 0) return spec;
    const calcErrors: string[] = [];
    let updatedSpec = spec;
    for (const entry of overrideEntries) {
      if (!entry.result.ok) {
        calcErrors.push(entry.result.error);
        continue;
      }
      if (entry.targetProp === "inlineText") {
        updatedSpec = {
          ...updatedSpec,
          inlineText: String(entry.result.value),
        };
      } else if (updatedSpec.runtimeSpec) {
        const existingProps = updatedSpec.runtimeSpec.props ?? {};
        // Inject at top level for any targetProp, AND into props.data when data is an object.
        // Factories (inputFactory, calculationPreviewPanelFactory, etc.) read from props.data.
        const existingData = existingProps.data;
        const updatedData =
          (typeof existingData === "object" && existingData !== null &&
              !Array.isArray(existingData))
            ? {
              ...(existingData as Record<string, unknown>),
              [entry.targetProp]: entry.result.value,
            }
            : existingData;
        updatedSpec = {
          ...updatedSpec,
          runtimeSpec: {
            ...updatedSpec.runtimeSpec,
            props: {
              ...existingProps,
              [entry.targetProp]: entry.result.value,
              ...(updatedData !== existingData ? { data: updatedData } : {}),
            },
          },
        };
      }
    }
    if (calcErrors.length > 0) {
      const existing = updatedSpec.def as Record<string, unknown>;
      updatedSpec = {
        ...updatedSpec,
        def: { ...existing, calc_errors: calcErrors },
      };
    }
    return updatedSpec;
  });
}

export function renderRuntimeComponents(
  componentDataHubs: ComponentDataHub[],
): ComponentSpec[] {
  return componentDataHubs.map((hub) => {
    const adapted = adaptComponentDataHub(hub);
    if (!adapted.ok) {
      return {
        componentType: "error",
        def: { error: adapted.error, componentId: hub.componentId },
      };
    }
    const rendered = renderRuntimeComponent(adapted.value);
    if (!rendered.ok) {
      return {
        componentId: adapted.value.componentId,
        componentType: "error",
        def: { error: rendered.error },
        runtime: adapted.value,
      };
    }
    return {
      componentId: adapted.value.componentId,
      componentType: adapted.value.componentType,
      def: { node: rendered.node },
      runtime: adapted.value,
    };
  });
}

/**
 * Maps a backend dispatch Emission to a UiProjection via ProjectionDefinition.
 *
 * Closes the manifest_response_constructor_mapping_end_to_end_route:
 *   backend dispatch emission → emission.data as jsonKeyValue → constructProjection → UiProjection
 *
 * Frontend does not make topology meaning judgment or SQL Attention judgment.
 * All projection logic is data-defined via the provided definition.
 *
 * Completion condition: manifest_response_constructor_mapping_end_to_end_route_is_proven
 */
export function projectionFromEmission(
  emission: Emission,
  definition: ProjectionDefinition,
): { projection: UiProjection; error?: undefined } | {
  projection?: undefined;
  error: string;
} {
  const jsonKeyValue = projectionInputFromData(
    emission.data,
    definition.inputMapping,
  );
  return constructProjection(jsonKeyValue, definition);
}

export function renderEmission(
  emission: Emission,
  registry: ComponentRegistry,
  options?: RenderEmissionOptions,
): ComponentSpec[] {
  const previewMode = options?.previewMode === true;
  // Layout-aware path: when layoutId is set, layoutNodes must be present.
  // Absent layoutNodes with a present layoutId is an explicit broken-layout failure —
  // no silent fallback to flat componentIds rendering.
  if (emission.layoutId !== undefined) {
    if (!emission.layoutNodes || emission.layoutNodes.length === 0) {
      return [
        {
          componentType: "error",
          def: {
            error:
              `LAYOUT_NODES_NOT_FOUND: layoutId "${emission.layoutId}" is set but layoutNodes is absent or empty. Broken layout configuration — no fallback.`,
            layoutId: emission.layoutId,
          },
        },
      ];
    }

    // Render in slot order. Each node carries full layout projection fields.
    const rawSpecs = [...emission.layoutNodes]
      .sort((a, b) => a.orderIndex - b.orderIndex)
      .map((node): ComponentSpec => {
        const layoutFields = {
          nodeId: node.nodeId,
          nodeKind: node.nodeKind,
          htmlTag: node.htmlTag,
          parentNodeId: node.parentNodeId,
          slotKey: node.slotKey,
          orderIndex: node.orderIndex,
          x: node.x,
          y: node.y,
          width: node.width,
          height: node.height,
          widthMode: node.widthMode,
          heightMode: node.heightMode,
          layoutClassRefs: node.layoutClassRefs,
        };

        const design = node.componentDesign;
        const unknownCssTokenRefs = design?.cssTokenRefs?.length
          ? resolveUnknownCssTokenRefs(design.cssTokenRefs)
          : [];
        if (unknownCssTokenRefs.length > 0) {
          return {
            componentId: node.componentId,
            componentType: "error",
            def: {
              error: `CSS_TOKEN_REF_UNRESOLVED: ${
                unknownCssTokenRefs.join(", ")
              }`,
              code: "CSS_TOKEN_REF_UNRESOLVED",
              cssTokenRefs: unknownCssTokenRefs,
            },
            ...layoutFields,
          };
        }
        const linkHrefResult = interpolateLinkHrefReadOnly(design?.linkHref);
        if (!linkHrefResult.ok) {
          return {
            componentId: node.componentId,
            componentType: "error",
            def: {
              error: linkHrefResult.message,
              code: linkHrefResult.code,
              linkHref: design?.linkHref,
            },
            ...layoutFields,
          };
        }

        // structural_node nodes (Category/Section/Form/Workflow/Validation sourced from
        // components_layout_design.layout_schema_json.records[] — the structural authority
        // tree) render as a generic labeled group. No registry lookup, no componentId/
        // componentKind — backend never assigns one to a structural node.
        if (node.nodeKind === "structural_node") {
          return {
            componentType: "structural_node",
            def: { recordType: node.recordType },
            inlineText: node.label,
            ...layoutFields,
          };
        }

        // unresolved_gap nodes (topology_ui_unresolved — a terminal known-gap marker the
        // translator itself flagged) always render as an explicit error carrying knownGapRefs —
        // never resolved to a component, never silently dropped or treated as a structural_node.
        // SSOT: docs/design/runtime-orchestration-ssot.yaml
        // ui_projection_render_reachability_contract.layout_schema_structural_render_contract
        // unresolved_gap_resolution.
        if (node.nodeKind === "unresolved_gap") {
          return {
            componentType: "error",
            def: {
              error: `TOPOLOGY_UI_UNRESOLVED_GAP_REF: layout node "${
                node.nodeId ?? "(unnamed)"
              }" is an unresolved authoring gap (recordType="${node.recordType}").`,
              code: "TOPOLOGY_UI_UNRESOLVED_GAP_REF",
              knownGapRefs: node.knownGapRefs ?? [],
            },
            inlineText: node.label,
            ...layoutFields,
          };
        }

        // structural_html nodes render as actual HTML elements — no registry lookup.
        if (node.nodeKind === "structural_html") {
          return {
            componentType: "structural_html",
            def: { linkHref: linkHrefResult.value || design?.linkHref },
            inlineText: design?.inlineText,
            cssTokenRefs: design?.cssTokenRefs,
            ...layoutFields,
          };
        }

        if (!node.componentId) {
          return {
            componentType: "error",
            def: {
              error: `Layout node "${
                node.nodeId ?? node.slotKey ?? "(unnamed)"
              }" (orderIndex=${node.orderIndex}) has no componentId assigned.`,
              slotKey: node.slotKey,
              orderIndex: node.orderIndex,
            },
            ...layoutFields,
          };
        }

        // catalog_component: componentKind required — absent componentKind is an explicit error.
        // SSOT: componentKind must be present on all catalog_component nodes. No registry fallback.
        if (!node.componentKind) {
          return {
            componentId: node.componentId,
            componentType: "error",
            def: {
              error:
                `CATALOG_COMPONENT_KIND_REQUIRED: catalog_component node "${
                  node.nodeId ?? node.slotKey ?? "(unnamed)"
                }" (componentId="${node.componentId}") has no componentKind. Ensure ui_component_registry has a component_kind for this component.`,
              code: "CATALOG_COMPONENT_KIND_REQUIRED",
              componentId: node.componentId,
              slotKey: node.slotKey,
            },
            ...layoutFields,
          };
        }

        // Preview surfaces: inert bindings (no wiring required). Product: wiring-backed dispatch.
        const nodeWiringKind = node.wiringKind ?? "";

        ensureRuntimeComponentRegistryInitialized();
        const defaultProps = buildDefaultCatalogComponentProps(
          node,
          previewMode,
        );
        const mergedProps = mergeNodeLocalProps(
          defaultProps,
          node.propsJson,
          node.stateJson,
        );
        if (!mergedProps.ok) {
          return {
            componentId: node.componentId,
            componentType: "error",
            def: { error: mergedProps.error, componentId: node.componentId },
            ...layoutFields,
          };
        }
        const propsWithDesign = mergeCatalogPropsWithComponentDesign(
          node.componentKind,
          node.componentKey ?? node.nodeId ?? "Component",
          applyLiveNodeValueOverride(
            applyLocalStateOverrides(
              mergedProps.props,
              node.nodeId,
              options?.localStateStore,
            ),
            node.nodeId,
            options?.payloadFromNodeValues,
          ),
          design
            ? { ...design, linkHref: linkHrefResult.value || design.linkHref }
            : undefined,
        );
        // dispatchPayloadFromByTrigger validation is fail-closed for the whole node —
        // never silently skipped/filtered (SSOT remaining_write_payload_capture_gap
        // negative-case contract). Additionally fail-closed (not silently ignored) when
        // authored on a non-admin_runtime node — same reasoning as dispatchTargetRefByTrigger
        // below (a node's own wiringKind is uniformly inherited from the layout's single
        // ui_wiring_registry row, never authored per-node, so this is the only point where
        // "this binding belongs to an admin_runtime layout" can be checked).
        const hasDispatchPayloadFromByTrigger = node.dispatchPayloadFromByTrigger !== undefined &&
          node.dispatchPayloadFromByTrigger !== null;
        const adminRuntimePayloadFrom = previewMode ||
            isNavigationWiringKind(nodeWiringKind)
          ? { ok: true as const, byTrigger: {} }
          : hasDispatchPayloadFromByTrigger && nodeWiringKind !== "admin_runtime"
          ? {
            ok: false as const,
            error:
              `RUNTIME_INTERACTION_DISPATCH_PAYLOAD_FROM_BY_TRIGGER_REQUIRES_ADMIN_RUNTIME_WIRING: dispatchPayloadFromByTrigger is only valid on a wiringKind="admin_runtime" node (this node's wiringKind is "${
                nodeWiringKind || "(absent)"
              }")`,
          }
          : buildAdminRuntimePayloadFromByTrigger(
            node.dispatchPayloadFromByTrigger,
          );
        if (!adminRuntimePayloadFrom.ok) {
          return {
            componentId: node.componentId,
            componentType: "error",
            def: {
              error: adminRuntimePayloadFrom.error,
              componentId: node.componentId,
            },
            ...layoutFields,
          };
        }
        // dispatchTargetRefByTrigger validation is fail-closed for the whole node —
        // same discipline as dispatchPayloadFromByTrigger above. Additionally fail-closed
        // (not silently ignored) when authored on a non-admin_runtime node: a node's own
        // wiringKind is uniformly inherited from the layout's single ui_wiring_registry row
        // (NpgsqlTopologyRepository.LoadLayoutNodesAsync), never authored per-node, so this is
        // the only point where "this override belongs to an admin_runtime layout" can be
        // checked — silently no-op'ing it here would let an author believe an authored
        // override is active when it never took effect.
        const hasDispatchTargetRefByTrigger = node.dispatchTargetRefByTrigger !== undefined &&
          node.dispatchTargetRefByTrigger !== null;
        const adminRuntimeTargetRefOverride = previewMode ||
            isNavigationWiringKind(nodeWiringKind)
          ? { ok: true as const, byTrigger: {} }
          : hasDispatchTargetRefByTrigger && nodeWiringKind !== "admin_runtime"
          ? {
            ok: false as const,
            error:
              `RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_REQUIRES_ADMIN_RUNTIME_WIRING: dispatchTargetRefByTrigger is only valid on a wiringKind="admin_runtime" node (this node's wiringKind is "${
                nodeWiringKind || "(absent)"
              }")`,
          }
          : buildAdminRuntimeTargetRefOverrideByTrigger(
            node.dispatchTargetRefByTrigger,
            node.targetSurface,
            node.componentKind,
          );
        if (!adminRuntimeTargetRefOverride.ok) {
          return {
            componentId: node.componentId,
            componentType: "error",
            def: {
              error: adminRuntimeTargetRefOverride.error,
              componentId: node.componentId,
            },
            ...layoutFields,
          };
        }
        const baseEventBinding = previewMode
          ? buildPreviewInertEventBinding()
          : isNavigationWiringKind(nodeWiringKind)
          ? buildRouteNavigationEventBinding(node.targetRef)
          : buildCatalogComponentEventBinding(
            buildRuntimeDispatchSpec(node),
            adminRuntimePayloadFrom.byTrigger,
            adminRuntimeTargetRefOverride.byTrigger,
            nodeWiringKind,
          );
        const rawLocalInteractions = node.runtimeInteractions ??
          propsWithDesign.eventWirings;
        const localStateEventBinding = previewMode
          ? {}
          : buildLocalUiStateEventBinding(rawLocalInteractions);
        const externalPortEventBinding = previewMode
          ? {}
          : buildExternalPortEventBinding(node.runtimeInteractions, {
            layoutId: emission.layoutId,
            packageId: emission.packageId,
            nodeId: node.nodeId ?? "",
          });
        const componentEventBinding = { ...baseEventBinding };
        for (
          const [trigger, localBinding] of Object.entries({
            ...localStateEventBinding,
            ...externalPortEventBinding,
          })
        ) {
          const existing = typeof componentEventBinding[trigger] === "object" &&
              componentEventBinding[trigger] !== null
            ? componentEventBinding[trigger] as Record<string, unknown>
            : {};
          componentEventBinding[trigger] = {
            ...existing,
            ...(localBinding as Record<string, unknown>),
          };
        }

        // An authored runtimeInteraction whose actionType is outside the recognized taxonomy
        // (wiringSettingCategoryOf / dispatchExternalPort / dispatchInstanceOperation) resolves
        // to zero event bindings here — never silently rendered as a normal, unbound component
        // that later fails cryptically deep inside the runtime factory
        // (RUNTIME_PRIMITIVE_RENDERER_MISSING_EVENT_BINDING). Fail explicit at the same layer
        // CATALOG_COMPONENT_KIND_REQUIRED already lives, so renderEmission()'s error count
        // actually reflects what the real factory/DOM will do with this leaf.
        if (
          !previewMode &&
          Array.isArray(node.runtimeInteractions) &&
          node.runtimeInteractions.length > 0 &&
          Object.keys(componentEventBinding).length === 0
        ) {
          return {
            componentId: node.componentId,
            componentType: "error",
            def: {
              error:
                `RUNTIME_INTERACTION_UNATTRIBUTABLE: catalog_component node "${
                  node.nodeId ?? node.slotKey ?? "(unnamed)"
                }" has ${node.runtimeInteractions.length} authored runtimeInteractions entr${
                  node.runtimeInteractions.length === 1 ? "y" : "ies"
                } but none resolved to a recognized event binding (actionType outside the classified taxonomy).`,
              code: "RUNTIME_INTERACTION_UNATTRIBUTABLE",
              componentId: node.componentId,
            },
            ...layoutFields,
          };
        }
        let finalProps = propsWithDesign;
        if (node.propBindings && Object.keys(node.propBindings).length > 0) {
          const emissionData = emission.data ?? {};
          const bindingResult = resolvePropBindings(
            propsWithDesign,
            node.propBindings,
            node.componentKind,
            emissionData,
            emission.navigationSequence,
          );
          if (!bindingResult.ok) {
            return {
              componentId: node.componentId,
              componentType: "error",
              def: {
                error: bindingResult.error,
                componentId: node.componentId,
              },
              ...layoutFields,
            };
          }
          finalProps = bindingResult.props;
        }
        const hubDesign = design
          ? {
            classname: design.classname,
            tailwind: design.tailwind,
          }
          : undefined;
        const hub: ComponentDataHub = {
          nodeId: node.nodeId,
          componentId: node.componentId,
          componentKind: node.componentKind,
          packageId: emission.packageId ?? null,
          layoutId: emission.layoutId ?? null,
          wiringId: (node.wiringId && node.wiringId.trim())
            ? node.wiringId.trim()
            : null,
          props: finalProps,
          eventBinding: componentEventBinding,
          localStateStore: options?.localStateStore,
          payloadFromNodeValues: options?.payloadFromNodeValues,
          onNodeValueChange: (options?.onNodeValueChange && node.nodeId)
            ? (value: unknown) =>
              options.onNodeValueChange!(node.nodeId!, value)
            : undefined,
          onRuntimeDispatchResult: (options?.onRuntimeDispatchResult && node.nodeId)
            ? (result: DispatchResponse, context: RuntimeDispatchResultContext) =>
              options.onRuntimeDispatchResult!(node.nodeId!, result, context)
            : undefined,
          debounceMs: typeof node.debounceMs === "number" ? node.debounceMs : undefined,
          design: hubDesign,
        };
        const adapted = adaptComponentDataHub(hub);
        if (!adapted.ok) {
          return {
            componentId: node.componentId,
            componentType: "error",
            def: { error: adapted.error, componentId: node.componentId },
            ...layoutFields,
          };
        }
        return {
          componentId: node.componentId,
          componentType: node.componentKind,
          def: {},
          runtimeSpec: previewMode
            ? { ...adapted.value, previewMode: true }
            : adapted.value,
          cssTokenRefs: design?.cssTokenRefs,
          ...layoutFields,
        };
      });

    const bindings = options?.calculationBindings ??
      emission.calculationBindings ?? [];
    if (bindings.length === 0) return rawSpecs;
    return applyCalcBindingsToSpecs(
      rawSpecs,
      bindings,
      emission.data ?? {},
      options?.calcNodeValues ?? {},
    );
  }

  // No layout: flat componentIds rendering.
  const componentIds = emission.componentIds ?? [];

  return componentIds.map((componentId): ComponentSpec => {
    const entry = registry[componentId];

    if (!entry) {
      return {
        componentId,
        componentType: "error",
        def: {
          error: `ComponentRegistry: unknown componentId "${componentId}"`,
          missingId: componentId,
        },
      };
    }

    return {
      componentId: entry.componentId,
      componentType: entry.componentType,
      def: entry.def,
    };
  });
}
