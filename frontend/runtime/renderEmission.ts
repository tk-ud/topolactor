import type { Emission } from "../api/dispatch.ts";
import type { ComponentRegistry } from "../registry/componentRegistry.ts";

export type ComponentSpec = {
  componentId: string;
  componentType: string;
  def: Record<string, unknown>;
};

/**
 * Resolves an Emission's componentIds through the ComponentRegistry and
 * returns an array of fully-expanded ComponentSpec objects.
 *
 * Broken references are errors, not silent no-ops: if a componentId listed in
 * the emission is not present in the registry, an error ComponentSpec is
 * returned in its place so the caller can surface the broken reference.
 */
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
