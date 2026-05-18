import type { SchemaDef } from "./defaultSchema.ts";

export const demoHubSchema: SchemaDef = {
  schemaId: "demo-hub-schema",
  name: "demo_hub_schema",
  fields: [
    { key: "hub_id",        type: "text",   label: "Hub ID" },
    { key: "relation_name", type: "text",   label: "Relation" },
    { key: "state_name",    type: "text",   label: "State" },
    { key: "entity_count",  type: "number", label: "Entity Count" },
  ],
};

export const demoEntitySchema: SchemaDef = {
  schemaId: "demo-entity-schema",
  name: "demo_entity_schema",
  fields: [
    { key: "entity_id", type: "text", label: "Entity ID" },
    { key: "label",     type: "text", label: "Label" },
    { key: "state",     type: "text", label: "State" },
  ],
};

export const demoRecommendationSchema: SchemaDef = {
  schemaId: "demo-recommendation-schema",
  name: "demo_recommendation_schema",
  fields: [
    { key: "status",         type: "text", label: "Status" },
    { key: "next_operation", type: "text", label: "Next Operation" },
    { key: "score",          type: "number", label: "Score" },
  ],
};
