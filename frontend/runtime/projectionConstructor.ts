/**
 * projection_constructor — maps json_key_value + projection_definition to ui_projection.
 *
 * Per SSOT frontend_contract.projection_constructor:
 *   input:  json_key_value + projection_definition
 *   output: form_inputs | component_projection | ui_projection
 *
 * Manifest supplies projection_constructor_mapping (constructorKey -> packageIds).
 * This module resolves the mapping and builds the ui_projection surface.
 *
 * Prohibited (SSOT function_binding_and_constructor_contract):
 *   - direct_record_vector_authoring
 *   - backend_constructor_ownership
 *   - one_by_one_manual_input_construction_as_runtime_policy
 *
 * Frontend must not perform topology meaning judgment or sql_attention_judgment.
 */

export type ProjectionFieldKind = "text" | "number" | "boolean" | "select" | "textarea";

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
  | { kind: "component_projection"; componentId: string; props: Record<string, unknown> }
  | { kind: "ui_projection"; raw: Record<string, unknown> };

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
};

/**
 * Constructs a ui_projection from json_key_value data and a projection_definition.
 * Returns an explicit error string instead of throwing for recoverable failures.
 *
 * This is a hardcoded abstract function shape per SSOT:
 * runtime policy values (field defs, component IDs) are data-defined via projection_definition.
 */
export function constructProjection(
  jsonKeyValue: Record<string, unknown>,
  definition: ProjectionDefinition,
): { projection: UiProjection; error?: undefined } | { projection?: undefined; error: string } {
  if (!definition.constructorKey) {
    return { error: "PROJECTION_CONSTRUCTOR_MISSING_KEY: constructorKey is required" };
  }

  switch (definition.outputKind) {
    case "form_inputs": {
      if (!definition.fieldDefs || definition.fieldDefs.length === 0) {
        return { error: "PROJECTION_CONSTRUCTOR_NO_FIELD_DEFS: fieldDefs required for form_inputs" };
      }
      const fields: ProjectionField[] = definition.fieldDefs.map((def) => ({
        key: def.key,
        label: def.label,
        kind: def.kind,
        value: jsonKeyValue[def.key] ?? null,
        options: def.options,
        required: def.required,
      }));
      return { projection: { kind: "form_inputs", fields } };
    }

    case "component_projection": {
      if (!definition.componentId) {
        return { error: "PROJECTION_CONSTRUCTOR_MISSING_COMPONENT_ID: componentId required for component_projection" };
      }
      return {
        projection: {
          kind: "component_projection",
          componentId: definition.componentId,
          props: { ...jsonKeyValue },
        },
      };
    }

    case "ui_projection": {
      return {
        projection: {
          kind: "ui_projection",
          raw: { ...jsonKeyValue },
        },
      };
    }

    default: {
      const exhaustive: never = definition.outputKind;
      return { error: `PROJECTION_CONSTRUCTOR_UNKNOWN_KIND: ${exhaustive}` };
    }
  }
}
