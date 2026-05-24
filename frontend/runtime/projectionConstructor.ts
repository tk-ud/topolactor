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

type Result<T> = { ok: true; value: T } | { ok: false; error: string };

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

function validateOptionalObject(name: string, value: unknown): Result<JsonObject | undefined> {
  if (value === undefined) return { ok: true, value: undefined };
  if (!isObject(value)) return { ok: false, error: `PROJECTION_CONSTRUCTOR_INVALID_${name}: ${name} must be object` };
  return { ok: true, value };
}

function normalizeComponentProps(componentKind: string, props: JsonObject): Result<JsonObject> {
  switch (componentKind) {
    case "action/button": {
      if (typeof props.label !== "string" || props.label.length === 0) return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_BUTTON_LABEL: label(string) is required" };
      if (props.disabled !== undefined && typeof props.disabled !== "boolean") return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_BUTTON_DISABLED: disabled must be boolean" };
      if (props.variant !== undefined && props.variant !== "primary" && props.variant !== "secondary" && props.variant !== "danger") return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_BUTTON_VARIANT: variant must be primary|secondary|danger" };
      if (props.type !== undefined && props.type !== "button" && props.type !== "submit" && props.type !== "reset") return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_BUTTON_TYPE: type must be button|submit|reset" };
      return { ok: true, value: { ...props, disabled: props.disabled ?? false, variant: props.variant ?? "primary", type: props.type ?? "button" } };
    }
    case "form_input/input": {
      if (props.value !== undefined && typeof props.value !== "string") return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_INPUT_VALUE: value must be string" };
      if (props.disabled !== undefined && typeof props.disabled !== "boolean") return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_INPUT_DISABLED: disabled must be boolean" };
      if (props.type !== undefined && props.type !== "text" && props.type !== "password" && props.type !== "number" && props.type !== "email") return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_INPUT_TYPE: type must be text|password|number|email" };
      return { ok: true, value: { ...props, value: props.value ?? "", disabled: props.disabled ?? false, type: props.type ?? "text" } };
    }
    case "display/card":
      if (props.variant !== undefined && props.variant !== "default" && props.variant !== "info" && props.variant !== "warning" && props.variant !== "error") return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_CARD_VARIANT: variant must be default|info|warning|error" };
      return { ok: true, value: { ...props, variant: props.variant ?? "default" } };
    case "data_display/table":
      if (!Array.isArray(props.columns)) return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_TABLE_COLUMNS: columns(array) is required" };
      if (!Array.isArray(props.rows)) return { ok: false, error: "PROJECTION_CONSTRUCTOR_INVALID_TABLE_ROWS: rows(array) is required" };
      return { ok: true, value: props };
    default:
      return { ok: false, error: `PROJECTION_CONSTRUCTOR_UNSUPPORTED_COMPONENT_KIND: ${componentKind}` };
  }
}

function validateBySchema(schema: JsonObject, mergedProps: JsonObject): string | undefined {
  const required = schema.required;
  if (required !== undefined && !Array.isArray(required)) return "PROJECTION_CONSTRUCTOR_INVALID_PARAMETER_SCHEMA_REQUIRED: required must be array";
  for (const item of (required as unknown[] | undefined) ?? []) {
    if (typeof item !== "string") return "PROJECTION_CONSTRUCTOR_INVALID_PARAMETER_SCHEMA_REQUIRED: required entries must be string";
    if (!(item in mergedProps)) return `PROJECTION_CONSTRUCTOR_SCHEMA_REQUIRED_MISSING: ${item}`;
  }
  const properties = schema.properties;
  if (properties !== undefined && !isObject(properties)) return "PROJECTION_CONSTRUCTOR_INVALID_PARAMETER_SCHEMA_PROPERTIES: properties must be object";
  if (!properties) return undefined;

  for (const [key, definition] of Object.entries(properties)) {
    if (!isObject(definition) || typeof definition.type !== "string") return `PROJECTION_CONSTRUCTOR_INVALID_PARAMETER_SCHEMA_DEFINITION: ${key}`;
    const expected = definition.type;
    if (!["string", "number", "boolean", "array", "object"].includes(expected)) return `PROJECTION_CONSTRUCTOR_INVALID_PARAMETER_SCHEMA_TYPE: ${key} unsupported type ${expected}`;
    if (!(key in mergedProps)) continue;
    const value = mergedProps[key];
    if (value === null || value === undefined) continue;
    if (expected === "string" && typeof value !== "string") return `PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: ${key} expected string`;
    if (expected === "number" && typeof value !== "number") return `PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: ${key} expected number`;
    if (expected === "boolean" && typeof value !== "boolean") return `PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: ${key} expected boolean`;
    if (expected === "array" && !Array.isArray(value)) return `PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: ${key} expected array`;
    if (expected === "object" && !isObject(value)) return `PROJECTION_CONSTRUCTOR_SCHEMA_TYPE_MISMATCH: ${key} expected object`;
  }

  return undefined;
}

export function constructProjection(jsonKeyValue: Record<string, unknown>, definition: ProjectionDefinition): { projection: UiProjection; error?: undefined } | { projection?: undefined; error: string } {
  if (!definition.constructorKey) return { error: "PROJECTION_CONSTRUCTOR_MISSING_KEY: constructorKey is required" };

  switch (definition.outputKind) {
    case "form_inputs": {
      if (!definition.fieldDefs || definition.fieldDefs.length === 0) return { error: "PROJECTION_CONSTRUCTOR_NO_FIELD_DEFS: fieldDefs required for form_inputs" };
      return { projection: { kind: "form_inputs", fields: definition.fieldDefs.map((def) => ({ key: def.key, label: def.label, kind: def.kind, value: jsonKeyValue[def.key] ?? null, options: def.options, required: def.required })) } };
    }
    case "component_projection": {
      const componentDefinition = definition.componentDefinition;
      const definitionComponentId = definition.componentId;
      const payloadComponentId = componentDefinition?.componentId;
      if (definitionComponentId && payloadComponentId && definitionComponentId !== payloadComponentId) {
        return { error: "PROJECTION_CONSTRUCTOR_COMPONENT_ID_MISMATCH: definition.componentId and componentDefinition.componentId must match" };
      }
      const componentId = definitionComponentId ?? payloadComponentId;
      if (!componentId) return { error: "PROJECTION_CONSTRUCTOR_MISSING_COMPONENT_ID: componentId required for component_projection" };
      if (!componentDefinition) return { error: "PROJECTION_CONSTRUCTOR_MISSING_COMPONENT_DEFINITION: componentDefinition required" };
      if (!componentDefinition.component_kind) return { error: "PROJECTION_CONSTRUCTOR_MISSING_COMPONENT_KIND: component_kind required" };

      const defaultsCheck = validateOptionalObject("DEFAULT_PARAMETERS", componentDefinition.default_parameters);
      if (!defaultsCheck.ok) return { error: defaultsCheck.error };
      const overridesCheck = validateOptionalObject("PROJECTION_OVERRIDES", definition.projectionOverrides);
      if (!overridesCheck.ok) return { error: overridesCheck.error };
      const schemaCheck = validateOptionalObject("PARAMETER_SCHEMA", componentDefinition.parameter_schema);
      if (!schemaCheck.ok) return { error: schemaCheck.error };
      const eventBindingCheck = validateOptionalObject("EVENT_BINDING", componentDefinition.event_binding);
      if (!eventBindingCheck.ok) return { error: eventBindingCheck.error };

      const mergedProps: JsonObject = {
        ...(defaultsCheck.value ?? {}),
        ...jsonKeyValue,
        ...(overridesCheck.value ?? {}),
      };

      if (schemaCheck.value) {
        const schemaError = validateBySchema(schemaCheck.value, mergedProps);
        if (schemaError) return { error: schemaError };
      }
      const normalized = normalizeComponentProps(componentDefinition.component_kind, mergedProps);
      if (!normalized.ok) return { error: normalized.error };

      return {
        projection: {
          kind: "component_projection",
          componentId,
          props: normalized.value,
          componentDataHub: {
            componentId,
            componentKey: componentDefinition.componentKey,
            packageId: componentDefinition.packageId,
            layoutId: componentDefinition.layoutId,
            componentKind: componentDefinition.component_kind,
            semanticRole: componentDefinition.semantic_role,
            visualRole: componentDefinition.visual_role,
            props: normalized.value,
            eventBinding: eventBindingCheck.value ?? {},
          },
        },
      };
    }
    case "ui_projection":
      return { projection: { kind: "ui_projection", raw: { ...jsonKeyValue } } };
    default: {
      const exhaustive: never = definition.outputKind;
      return { error: `PROJECTION_CONSTRUCTOR_UNKNOWN_KIND: ${exhaustive}` };
    }
  }
}
