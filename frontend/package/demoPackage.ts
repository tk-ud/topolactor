import type { PackageDef } from "./defaultPackage.ts";

export const demoHubOverviewPackage: PackageDef = {
  packageId: "demo-hub-overview-package",
  name: "demo_hub_overview_package",
  componentIds: ["demo-hub-overview"],
};

export const demoEntityRegistryPackage: PackageDef = {
  packageId: "demo-entity-registry-package",
  name: "demo_entity_registry_package",
  componentIds: ["demo-entity-table"],
};

export const demoRecommendationPackage: PackageDef = {
  packageId: "demo-recommendation-package",
  name: "demo_recommendation_package",
  componentIds: ["demo-recommendation-panel"],
};
