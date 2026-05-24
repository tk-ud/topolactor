import type { Emission } from "../api/dispatch.ts";
import type { ComponentRegistry } from "../registry/componentRegistry.ts";
import { adaptComponentDataHub, type RuntimeComponentSpec } from "./runtimeComponentAdapter.ts";
import { renderRuntimeComponent } from "./runtimePrimitiveRenderer.ts";
import type { ComponentDataHub } from "./projectionConstructor.ts";

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
