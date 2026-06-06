import type { Emission } from "../api/dispatch.ts";
import type { ComponentRegistry } from "../registry/componentRegistry.ts";
import { adaptComponentDataHub, type RuntimeComponentSpec } from "./runtimeComponentAdapter.ts";
import { renderRuntimeComponent } from "./runtimePrimitiveRenderer.ts";
import { constructProjection, type ComponentDataHub, type ProjectionDefinition, type UiProjection } from "./projectionConstructor.ts";

export type ComponentSpec = {
  componentId?: string;
  componentType: string;
  def: Record<string, unknown>;
  runtime?: RuntimeComponentSpec;
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

        const entry = registry[node.componentId];
        if (!entry) {
          return {
            componentId: node.componentId,
            componentType: "error",
            def: {
              error: `ComponentRegistry: unknown componentId "${node.componentId}" in node "${node.nodeId ?? node.slotKey ?? "(unnamed)"}"`,
              missingId: node.componentId,
              slotKey: node.slotKey,
            },
            ...layoutFields,
          };
        }

        return {
          componentId: entry.componentId,
          componentType: entry.componentType,
          def: entry.def,
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
