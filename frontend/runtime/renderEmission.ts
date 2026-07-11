import type {
  Emission,
  LayoutNode as EmissionLayoutNode,
} from "../api/dispatch.ts";
import type { ComponentRegistry } from "../registry/componentRegistry.ts";
import {
  adaptComponentDataHub,
  type RuntimeComponentSpec,
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
 * Maps wiring_kind to the canonical layer for backend dispatch routing.
 * search → screen_list (ScreenDataShapeQueryRuntime), aggregate → screen_aggregation,
 * CRUD kinds → entity (RuntimeExecutor CRUD path).
 * navigation is excluded — it is frontend-local and must not enter this mapping.
 * Returns null for unknown wiringKind — callers must treat null as a misconfiguration and not fall back.
 */
export function mapWiringKindToLayer(wiringKind: string): string | null {
  if (wiringKind === "search") return "screen_list";
  if (wiringKind === "aggregate") return "screen_aggregation";
  if (
    wiringKind === "create" || wiringKind === "update" ||
    wiringKind === "delete"
  ) return "entity";
  return null;
}

/**
 * Maps wiring_kind to the canonical action string for backend dispatch.
 * Mirrors the backend MapWiringKindToDispatchAction mapping.
 * Returns null for unknown wiringKind — callers must not pass raw unknown values as actions.
 */
export function mapWiringKindToAction(wiringKind: string): string | null {
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
  const action = mapWiringKindToAction(wiringKind);
  if (!action) return null;
  const layer = mapWiringKindToLayer(wiringKind);
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
        data: { label: authoredLabel || componentKey, variant: "primary", disabled: false },
      };
    case "form_input/form_field":
      return { data: { label: authoredLabel || componentKey } };
    case "form_input/select":
      // The option list is business data the schema record does not carry — an honest empty
      // list, never a fabricated sample option, until real option data is wired.
      return {
        data: { value: "", options: [], label: authoredLabel, placeholder: authoredLabel || "" },
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
  return buildProductionCatalogComponentProps(node, componentKind, componentKey);
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
 * Builds an eventBinding for a catalog_component node from its RuntimeDispatchSpec.
 * Populates standard triggers (click, change, select, submit, toggle) each carrying
 * the full runtimeDispatch spec so emitBoundEvent fires both log and dispatch lanes.
 * Returns empty object when spec is null/absent (log lane only).
 */
export function buildCatalogComponentEventBinding(
  spec: RuntimeDispatchSpec | null,
): Record<string, unknown> {
  if (!spec) return {};
  const triggers = ["click", "change", "select", "submit", "toggle"] as const;
  const binding: Record<string, unknown> = {};
  for (const trigger of triggers) {
    binding[trigger] = { eventType: trigger, runtimeDispatch: spec };
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
  const triggers = ["click", "change", "select", "submit", "toggle"] as const;
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
  identity: { layoutId?: string | null; packageId?: string | null; nodeId: string },
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
    const runtimeInteractionId = typeof wiring.runtimeInteractionId === "string" &&
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
              error:
                `TOPOLOGY_UI_UNRESOLVED_GAP_REF: layout node "${
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
        const defaultProps = buildDefaultCatalogComponentProps(node, previewMode);
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
          applyLocalStateOverrides(
            mergedProps.props,
            node.nodeId,
            options?.localStateStore,
          ),
          design
            ? { ...design, linkHref: linkHrefResult.value || design.linkHref }
            : undefined,
        );
        const baseEventBinding = previewMode
          ? buildPreviewInertEventBinding()
          : isNavigationWiringKind(nodeWiringKind)
          ? buildRouteNavigationEventBinding(node.targetRef)
          : buildCatalogComponentEventBinding(buildRuntimeDispatchSpec(node));
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
