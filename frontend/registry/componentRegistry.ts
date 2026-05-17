export type ComponentDef = {
  componentId: string;
  componentType: string;
  def: Record<string, unknown>;
};

/**
 * ComponentRegistry maps componentId strings to their full ComponentDef.
 * This is the authoritative lookup table consulted by renderEmission when
 * expanding an Emission's componentIds list into renderable ComponentSpecs.
 */
export type ComponentRegistry = Record<string, ComponentDef>;

/**
 * The default registry shipped with the skeleton.  Real registries will be
 * built up by registering hub-specific and schema-specific component defs.
 */
export const defaultComponentRegistry: ComponentRegistry = {
  "default-view": {
    componentId: "default-view",
    componentType: "view",
    def: {},
  },
};

/**
 * Look up a single component definition from the registry.
 * Returns null when the componentId is not registered.
 */
export function lookupComponent(
  registry: ComponentRegistry,
  componentId: string,
): ComponentDef | null {
  return registry[componentId] ?? null;
}
