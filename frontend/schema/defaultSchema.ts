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
 * The default schema is the fallback node in the schema_resolve step of the
 * canonical pipeline.  It exposes a single "label" text field and no layout
 * override.
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
