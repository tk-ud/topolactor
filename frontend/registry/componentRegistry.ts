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
  "demo-hub-overview": {
    componentId: "demo-hub-overview",
    componentType: "demo-renderer",
    def: { renders: "hub_overview", package: "demo_hub_overview_package" },
  },
  "demo-entity-table": {
    componentId: "demo-entity-table",
    componentType: "demo-renderer",
    def: { renders: "entity_table", package: "demo_entity_registry_package" },
  },
  "demo-recommendation-panel": {
    componentId: "demo-recommendation-panel",
    componentType: "demo-renderer",
    def: { renders: "recommendation", package: "demo_recommendation_package" },
  },
  "demo-token-badges": {
    componentId: "demo-token-badges",
    componentType: "demo-renderer",
    def: { renders: "context_token_badges" },
  },
  // Deterministic demo component IDs matching db/demo_seed.sql
  "00000000-0000-0000-0000-000000000014": {
    componentId: "00000000-0000-0000-0000-000000000014",
    componentType: "demo-renderer",
    def: { renders: "hub_overview", package: "demo_hub_overview_package" },
  },
  "00000000-0000-0000-0000-000000000015": {
    componentId: "00000000-0000-0000-0000-000000000015",
    componentType: "demo-renderer",
    def: { renders: "entity_table", package: "demo_entity_registry_package" },
  },
  "00000000-0000-0000-0000-000000000016": {
    componentId: "00000000-0000-0000-0000-000000000016",
    componentType: "demo-renderer",
    def: { renders: "recommendation", package: "demo_recommendation_package" },
  },
  "00000000-0000-0000-0000-000000000017": {
    componentId: "00000000-0000-0000-0000-000000000017",
    componentType: "demo-renderer",
    def: { renders: "context_token_badges" },
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
