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
