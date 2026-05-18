export type FieldDef = {
  key: string;
  type: string;
  label: string;
  required?: boolean;
};

export type SchemaDef = {
  schemaId: string;
  name: string;
  fields: FieldDef[];
  layout?: Record<string, unknown>;
};

/**
 * Static test fixture schema.
 * Canonical runtime schema resolution is backend/DB-backed.
 *
 * Real schemas will be defined per-hub and per-entity-type and may carry full
 * layout descriptors consumed by the component_expand step.
 */
export const defaultSchema: SchemaDef = {
  schemaId: "default-schema",
  name: "default_schema",
  fields: [
    {
      key: "label",
      type: "text",
      label: "Label",
    },
  ],
};
