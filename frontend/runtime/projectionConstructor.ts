/**
 * projection_constructor — maps json_key_value + projection_definition to ui_projection.
 *
 * Frontend projection surface only:
 * - no topology meaning judgment
 * - no SQL Attention judgment
 * - no DB/API persistence judgment
 */

export type ProjectionFieldKind = "text" | "number" | "boolean" | "select" | "textarea";

type JsonObject = Record<string, unknown>;

export type ComponentDefinitionPayload = {
  componentId?: string;
  componentKey?: string;
  packageId?: string | null;
  layoutId?: string | null;
  component_kind: string;
  semantic_role?: string | null;
  visual_role?: string | null;
  parameter_schema?: JsonObject;
  default_parameters?: JsonObject;
  event_binding?: JsonObject;
};

export type ComponentDataHub = {
  componentId?: string;
  componentKey?: string;
  packageId?: string | null;
  layoutId?: string | null;
  componentKind: string;
  semanticRole?: string | null;
  visualRole?: string | null;
  props: JsonObject;
  eventBinding: JsonObject;
};

export type ProjectionField = {
  key: string;
  label: string;
  kind: ProjectionFieldKind;
  value: unknown;
  options?: string[];
  required?: boolean;
};

export type UiProjection =
  | { kind: "form_inputs"; fields: ProjectionField[] }
  | { kind: "component_projection"; componentId: string; props: JsonObject; componentDataHub: ComponentDataHub }
  | { kind: "ui_projection"; raw: JsonObject };

export type ProjectionDefinition = {
  constructorKey: string;
  packageIds: string[];
  outputKind: "form_inputs" | "component_projection" | "ui_projection";
  fieldDefs?: Array<{
    key: string;
    label: string;
    kind: ProjectionFieldKind;
    required?: boolean;
    options?: string[];
  }>;
  componentId?: string;
  componentDefinition?: ComponentDefinitionPayload;
  projectionOverrides?: JsonObject;
};

function isObject(value: unknown): value is JsonObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function normalizeComponentProps(componentKind: string, props: JsonObject): { ok: true; props: JsonObject } | { ok: false; error: string } {
  switch (componentKind) {
    case "action/button":
      if (typeof props.label !== "string" || props.label.length === 0) return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_BUTTON_LABEL: label(string) is required" };
      return { ok: true, props: { ...props, disabled: Boolean(props.disabled), variant: (props.variant as string) ?? "primary", type: (props.type as string) ?? "button" } };
    case "form_input/input":
      if (props.value !== undefined && typeof props.value !== "string") return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_INPUT_VALUE: value must be string" };
      return { ok: true, props: { ...props, value: (props.value as string) ?? "", disabled: Boolean(props.disabled), type: (props.type as string) ?? "text" } };
    case "display/card":
      return { ok: true, props: { ...props, variant: (props.variant as string) ?? "default" } };
    case "data_display/table":
      if (!Array.isArray(props.columns)) return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_TABLE_COLUMNS: columns(array) is required" };
      if (!Array.isArray(props.rows)) return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_TABLE_ROWS: rows(array) is required" };
      return { ok: true, props };
    default:
      return { ok: false, error: `PROJECTION_CONSTRUCTOR_UNSUPPORTED_COMPONENT_KIND: ${componentKind}` };
  }
}

function validateBySchema(schema: JsonObject, mergedProps: JsonObject): string | undefined {
  const required = Array.isArray(schema.required) ? schema.required : [];
  for (const item of required) {
    if (typeof item !== "string") continue;
    if (!(item in mergedProps)) return `PROJECTION_CONSTRUCTOR_SCHEMA_REQUIRED_MISSING: ${item}`;
  }
  const properties = isObject(schema.properties) ? schema.properties : undefined;
  if (!properties) return undefined;

  for (const [key, definition] of Object.entries(properties)) {
    if (!(key in mergedProps) || !isObject(definition) || typeof definition.type !== "string") continue;
    const value = mergedProps[key];
    if (value === null || value === undefined) continue;
    const expected = definition.type;
    if (expected === "string" && typeof value !== "string") return `PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: ${key} expected string`;
    if (expected === "number" && typeof value !== "number") return `PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: ${key} expected number`;
    if (expected === "boolean" && typeof value !== "boolean") return `PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: ${key} expected boolean`;
    if (expected === "array" && !Array.isArray(value)) return `PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: ${key} expected array`;
    if (expected === "object" && !isObject(value)) return `PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: ${key} expected object`;
  }

  return undefined;
}

export function constructProjection(
  jsonKeyValue: Record<string, unknown>,
  definition: ProjectionDefinition,
): { projection: UiProjection; error?: undefined } | { projection?: undefined; error: string } {
  if (!definition.constructorKey) return { error: "PROJECTION_CONSTRUCTOR_MISSING_KEY: constructorKey is required" };

  switch (definition.outputKind) {
    case "form_inputs": {
      if (!definition.fieldDefs || definition.fieldDefs.length === 0) return { error: "PROJECTION_CONSTRUCTOR_NO_FIELD_DEFS: fieldDefs required for form_inputs" };
      return { projection: { kind: "form_inputs", fields: definition.fieldDefs.map((def) => ({ key: def.key, label: def.label, kind: def.kind, value: jsonKeyValue[def.key] ?? null, options: def.options, required: def.required })) } };
    }
    case "component_projection": {
      const componentId = definition.componentId ?? definition.componentDefinition?.componentId;
      if (!componentId) return { error: "PROJECTION_CONSTRUCTOR_MISSING_COMPONENT_ID: componentId required for component_projection" };
      const componentDefinition = definition.componentDefinition;
      if (!componentDefinition) return { error: "PROJECTION_CONSTRUCTOR_MISSING_COMPONENT_DEFINITION: componentDefinition required" };
      if (!componentDefinition.component_kind) return { error: "PROJECTION_CONSTRUCTOR_MISSING_COMPONENT_KIND: component_kind required" };

      const mergedProps: JsonObject = {
        ...(componentDefinition.default_parameters ?? {}),
        ...jsonKeyValue,
        ...(definition.projectionOverrides ?? {}),
      };
      if (componentDefinition.parameter_schema && isObject(componentDefinition.parameter_schema)) {
        const schemaError = validateBySchema(componentDefinition.parameter_schema, mergedProps);
        if (schemaError) return { error: schemaError };
      }
      const normalized = normalizeComponentProps(componentDefinition.component_kind, mergedProps);
      if (!normalized.ok) return { error: normalized.error };

      const componentDataHub: ComponentDataHub = {
        componentId: componentDefinition.componentId ?? componentId,
        componentKey: componentDefinition.componentKey,
        packageId: componentDefinition.packageId,
        layoutId: componentDefinition.layoutId,
        componentKind: componentDefinition.component_kind,
        semanticRole: componentDefinition.semantic_role,
        visualRole: componentDefinition.visual_role,
        props: normalized.props,
        eventBinding: isObject(componentDefinition.event_binding) ? componentDefinition.event_binding : {},
      };

      return { projection: { kind: "component_projection", componentId, props: normalized.props, componentDataHub } };
    }
    case "ui_projection":
      return { projection: { kind: "ui_projection", raw: { ...jsonKeyValue } } };
    default: {
      const exhaustive: never = definition.outputKind;
      return { error: `PROJECTION_CONSTRUCTOR_UNKNOWN_KIND: ${exhaustive}` };
    }
  }
}
