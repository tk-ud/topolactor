import type { Emission, LayoutNode as EmissionLayoutNode } from "../api/dispatch.ts";
import type { ComponentRegistry } from "../registry/componentRegistry.ts";
import { adaptComponentDataHub, type RuntimeComponentSpec } from "./runtimeComponentAdapter.ts";
import { renderRuntimeComponent } from "./runtimePrimitiveRenderer.ts";
import { constructProjection, type ComponentDataHub, type ProjectionDefinition, type UiProjection } from "./projectionConstructor.ts";
import { ensureRuntimeComponentRegistryInitialized } from "./runtimeComponentRegistry.ts";
import type { RuntimeDispatchSpec } from "./frontendScheduler.ts";

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
  /** "catalog_component" | "structural_html" — present only when rendered from layoutNodes. */
  nodeKind?: string;
  /** HTML element tag for structural_html nodes — present only when nodeKind="structural_html". */
  htmlTag?: string;
  /** Parent node for DOM nesting — present only when rendered from layoutNodes. */
  parentNodeId?: string;
  /** Slot name within the layout template — present only when rendered from layoutNodes. */
  slotKey?: string;
  /** Render order from layout node — present only when rendered from layoutNodes. */
  orderIndex?: number;
  /** Canvas x position in px — for position:absolute style projection. */
  x?: number;
  /** Canvas y position in px — for position:absolute style projection. */
  y?: number;
  /** Canvas width in px — for position:absolute style projection. */
  width?: number;
  /** Canvas height in px — for position:absolute style projection. */
  height?: number;
  /** SSOT topology-layout-class vocabulary refs for className resolution. */
  layoutClassRefs?: string[];
};

/**
 * Maps wiring_kind to the canonical layer for backend dispatch routing.
 * search → screen_list (ScreenDataShapeQueryRuntime), aggregate → screen_aggregation,
 * CRUD kinds → entity (RuntimeExecutor CRUD path).
 */
export function mapWiringKindToLayer(wiringKind: string): string {
  if (wiringKind === "search") return "screen_list";
  if (wiringKind === "aggregate") return "screen_aggregation";
  return "entity";
}

/**
 * Maps wiring_kind to the canonical action string for backend dispatch.
 * Mirrors the backend MapWiringKindToDispatchAction mapping.
 */
export function mapWiringKindToAction(wiringKind: string): string {
  switch (wiringKind) {
    case "search": return "Search";
    case "aggregate": return "Search";
    case "create": return "Create";
    case "update": return "diffUpdate";
    case "delete": return "logicalDelete";
    default: return wiringKind;
  }
}

/**
 * Builds a RuntimeDispatchSpec from a layout node's wiring metadata.
 * Returns null when no wiringKind is set (no wiring configured → log lane only).
 * target = targetSurface || "default"; layer derived from wiringKind.
 */
export function buildRuntimeDispatchSpec(node: EmissionLayoutNode): RuntimeDispatchSpec | null {
  const wiringKind = node.wiringKind;
  if (!wiringKind) return null;
  const action = mapWiringKindToAction(wiringKind);
  const layer = mapWiringKindToLayer(wiringKind);
  const target = (node.targetSurface && node.targetSurface.trim()) ? node.targetSurface.trim() : "default";
  return {
    operationType: action,
    target,
    layer,
    action,
    wiringKey: (node.wiringKey && node.wiringKey.trim()) ? node.wiringKey.trim() : undefined,
    wiringId: (node.wiringId && node.wiringId.trim()) ? node.wiringId.trim() : undefined,
    targetRef: (node.targetRef && node.targetRef.trim()) ? node.targetRef.trim() : undefined,
  };
}

/**
 * Builds minimum renderable props for a catalog_component node.
 * Uses componentKey as label for button/action components.
 * All factories handle absent/optional fields gracefully.
 */
function buildDefaultCatalogComponentProps(node: EmissionLayoutNode): Record<string, unknown> {
  const label = (node.componentKey && node.componentKey.trim())
    ? node.componentKey.trim()
    : (node.nodeId ?? "Component");
  return { data: { label } };
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
 * Builds a map from nodeId → children (sorted by orderIndex) for tree rendering.
 * Root nodes have parentNodeId === undefined; look them up with key undefined.
 * Pure function — no DOM or Preact dependency.
 */
export function buildChildrenMap(specs: ComponentSpec[]): Map<string | undefined, ComponentSpec[]> {
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

export function renderRuntimeComponents(componentDataHubs: ComponentDataHub[]): ComponentSpec[] {
  return componentDataHubs.map((hub) => {
    const adapted = adaptComponentDataHub(hub);
    if (!adapted.ok) {
      return { componentType: "error", def: { error: adapted.error, componentId: hub.componentId } };
    }
    const rendered = renderRuntimeComponent(adapted.value);
    if (!rendered.ok) {
      return { componentId: adapted.value.componentId, componentType: "error", def: { error: rendered.error }, runtime: adapted.value };
    }
    return { componentId: adapted.value.componentId, componentType: adapted.value.componentType, def: { node: rendered.node }, runtime: adapted.value };
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
): { projection: UiProjection; error?: undefined } | { projection?: undefined; error: string } {
  const jsonKeyValue: Record<string, unknown> = emission.data ?? {};
  return constructProjection(jsonKeyValue, definition);
}

export function renderEmission(
  emission: Emission,
  registry: ComponentRegistry,
): ComponentSpec[] {
  // Layout-aware path: when layoutId is set, layoutNodes must be present.
  // Absent layoutNodes with a present layoutId is an explicit broken-layout failure —
  // no silent fallback to flat componentIds rendering.
  if (emission.layoutId !== undefined) {
    if (!emission.layoutNodes || emission.layoutNodes.length === 0) {
      return [
        {
          componentType: "error",
          def: {
            error: `LAYOUT_NODES_NOT_FOUND: layoutId "${emission.layoutId}" is set but layoutNodes is absent or empty. Broken layout configuration — no fallback.`,
            layoutId: emission.layoutId,
          },
        },
      ];
    }

    // Render in slot order. Each node carries full layout projection fields.
    return [...emission.layoutNodes]
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
          layoutClassRefs: node.layoutClassRefs,
        };

        // structural_html nodes render as actual HTML elements — no registry lookup.
        if (node.nodeKind === "structural_html") {
          return {
            componentType: "structural_html",
            def: {},
            ...layoutFields,
          };
        }

        if (!node.componentId) {
          return {
            componentType: "error",
            def: {
              error: `Layout node "${node.nodeId ?? node.slotKey ?? "(unnamed)"}" (orderIndex=${node.orderIndex}) has no componentId assigned.`,
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
              error: `CATALOG_COMPONENT_KIND_REQUIRED: catalog_component node "${node.nodeId ?? node.slotKey ?? "(unnamed)"}" (componentId="${node.componentId}") has no componentKind. Ensure ui_component_registry has a component_kind for this component.`,
              code: "CATALOG_COMPONENT_KIND_REQUIRED",
              componentId: node.componentId,
              slotKey: node.slotKey,
            },
            ...layoutFields,
          };
        }

        // Build full dispatch spec from admin-configured wiring metadata.
        const dispatchSpec = buildRuntimeDispatchSpec(node);

        ensureRuntimeComponentRegistryInitialized();
        const hub: ComponentDataHub = {
          componentId: node.componentId,
          componentKind: node.componentKind,
          packageId: emission.packageId ?? null,
          layoutId: emission.layoutId ?? null,
          wiringId: (node.wiringId && node.wiringId.trim()) ? node.wiringId.trim() : null,
          props: buildDefaultCatalogComponentProps(node),
          eventBinding: buildCatalogComponentEventBinding(dispatchSpec),
          design: undefined,
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
          runtimeSpec: adapted.value,
          ...layoutFields,
        };
      });
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
