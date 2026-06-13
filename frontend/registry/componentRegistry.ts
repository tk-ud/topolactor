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
 * Static component registry fixture used for local type/tests.
 * Runtime component selection is derived from backend runtime responses.
 */
export const defaultComponentRegistry: ComponentRegistry = {
  "default-view": {
    componentId: "default-view",
    componentType: "view",
    def: {},
  },
  // Matches the deterministic component ID in seed_empty.sql and TopologyRepository.
  "00000000-0000-0000-0000-000000000003": {
    componentId: "00000000-0000-0000-0000-000000000003",
    componentType: "renderer",
    def: { renders: "emission_data" },
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
