/**
 * propBindingResolver — resolves array prop bindings from emission.data into component props.
 *
 * Resolution order in renderEmission:
 *   default props → propsJson → stateJson → propBindings (this module)
 *
 * source must start with "emission.data.".
 * transform must be in ALLOWED_PROP_BINDING_TRANSFORMS allowlist.
 * Resolved value overwrites the target prop key (propBindings wins over propsJson/stateJson for the same key).
 * When source path resolves to undefined, the prop key is left absent (no error — data may be missing).
 * When resolved value is not an array, returns explicit error.
 */

import type { PropBinding } from "../api/dispatch.ts";

/**
 * Component kinds that accept array props, and which prop names they accept as arrays.
 * Only props listed here may be the target of a propBinding.
 */
export const COMPONENT_ARRAY_PROP_CAPABILITIES: Record<string, string[]> = {
  "data_display/table": ["rows", "columns"],
  "data_display/data_grid": ["rows", "columns"],
  "data_display/list": ["rows", "items"],
  "data_display/tree": ["nodes", "items"],
  "display/card_list": ["items"],
  "disclosure/accordion": ["items"],
  "form_input/select": ["options"],
  "form_input/checkbox": ["options"],
  "form_input/radio_group": ["options"],
  "form_input/checkbox_group": ["options"],
  "table_op/faceted_filter_bar": ["filters"],
  "table_op/column_filter": ["options"],
  "table_op/column_visibility_editor": ["columns"],
  "table_op/sort_control": ["columns"],
  "table_op/group_by_control": ["columns"],
  "table_op/bulk_action_panel": ["actions"],
  "table_op/virtualized_data_table": ["rows", "columns"],
};

/** Allowlist of permitted transform identifiers. */
export const ALLOWED_PROP_BINDING_TRANSFORMS: ReadonlySet<string> = new Set([
  "activeColumnsToTableColumns",
  "rowsToOptions",
]);

const EMISSION_DATA_PREFIX = "emission.data.";

/**
 * Strips the "emission.data." prefix and resolves the remaining dotted path
 * against emission.data. Returns undefined when the path is not found.
 */
export function resolveRuntimeDataPath(
  emissionData: Record<string, unknown>,
  source: string,
): unknown {
  if (!source.startsWith(EMISSION_DATA_PREFIX)) return undefined;
  const path = source.slice(EMISSION_DATA_PREFIX.length);
  if (!path) return undefined;
  const parts = path.split(".");
  let current: unknown = emissionData;
  for (const part of parts) {
    if (typeof current !== "object" || current === null || Array.isArray(current)) return undefined;
    current = (current as Record<string, unknown>)[part];
  }
  return current;
}

/**
 * Applies a named transform from the allowlist to the resolved array.
 * Returns { ok: false, error } when transform is not in the allowlist.
 */
export function applyPropBindingTransform(
  value: unknown[],
  transform: string,
): { ok: true; value: unknown[] } | { ok: false; error: string } {
  if (!ALLOWED_PROP_BINDING_TRANSFORMS.has(transform)) {
    return { ok: false, error: `LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM: transform "${transform}" is not in the allowlist` };
  }
  switch (transform) {
    case "activeColumnsToTableColumns":
      return { ok: true, value: activeColumnsToTableColumns(value) };
    case "rowsToOptions":
      return { ok: true, value: value };
    default:
      return { ok: false, error: `LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM: transform "${transform}" is not implemented` };
  }
}

/**
 * Converts activeColumns (string[] or object[]) to table column descriptors.
 * Strings become { key, header }; objects are passed through.
 */
function activeColumnsToTableColumns(value: unknown[]): unknown[] {
  return value.map((col) => {
    if (typeof col === "string") return { key: col, header: col };
    return col;
  });
}

/**
 * Validates that a propBinding target is accepted by the component kind.
 * Returns an error string when not accepted, undefined when OK.
 */
export function validatePropBindingTarget(
  componentKind: string,
  propName: string,
): string | undefined {
  const accepted = COMPONENT_ARRAY_PROP_CAPABILITIES[componentKind];
  if (!accepted) {
    return `LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_COMPONENT: component kind "${componentKind}" does not accept array prop bindings`;
  }
  if (!accepted.includes(propName)) {
    return `LAYOUT_NODE_PROP_BINDING_UNSUPPORTED_PROP: prop "${propName}" is not in acceptsArrayProps for "${componentKind}"`;
  }
  return undefined;
}

export type PropBindingResult =
  | { ok: true; props: Record<string, unknown> }
  | { ok: false; error: string };

/**
 * Resolves all propBindings for a layout node and merges resolved values into props.
 * Processing order: for each binding entry —
 *   1. validate source prefix
 *   2. validate prop target against component capability
 *   3. resolve path against emissionData
 *   4. skip if resolved value is undefined (data absent)
 *   5. error if resolved value is not an array
 *   6. apply transform if specified
 *   7. merge into props
 *
 * Returns { ok: false, error } on first validation or transform error.
 * Returns { ok: true, props } with all resolved bindings merged.
 */
export function resolvePropBindings(
  baseProps: Record<string, unknown>,
  propBindings: Record<string, PropBinding>,
  componentKind: string,
  emissionData: Record<string, unknown>,
): PropBindingResult {
  const props = { ...baseProps };

  for (const [propName, binding] of Object.entries(propBindings)) {
    const { source, transform } = binding;

    if (!source.startsWith(EMISSION_DATA_PREFIX)) {
      return { ok: false, error: `LAYOUT_NODE_PROP_BINDING_INVALID_SOURCE: source "${source}" must start with "emission.data."` };
    }

    const capabilityError = validatePropBindingTarget(componentKind, propName);
    if (capabilityError) return { ok: false, error: capabilityError };

    const resolved = resolveRuntimeDataPath(emissionData, source);
    if (resolved === undefined) continue;

    if (!Array.isArray(resolved)) {
      return {
        ok: false,
        error: `LAYOUT_NODE_PROP_BINDING_SCALAR_SOURCE: source "${source}" resolved to a non-array value for prop "${propName}"`,
      };
    }

    let finalValue: unknown[] = resolved;
    if (transform) {
      const transformResult = applyPropBindingTransform(resolved, transform);
      if (!transformResult.ok) return { ok: false, error: transformResult.error };
      finalValue = transformResult.value;
    }

    props[propName] = finalValue;
  }

  return { ok: true, props };
}

/**
 * Validates a propBindings object structure without resolving against emission data.
 * Used in layout_patch validate path.
 * Returns an array of error strings (empty = valid).
 */
export function validatePropBindingsStructure(
  propBindings: unknown,
  componentKind: string,
): string[] {
  const errors: string[] = [];
  if (typeof propBindings !== "object" || propBindings === null || Array.isArray(propBindings)) {
    errors.push("LAYOUT_NODE_PROP_BINDING_INVALID: propBindings must be an object");
    return errors;
  }
  const bindings = propBindings as Record<string, unknown>;
  for (const [propName, binding] of Object.entries(bindings)) {
    if (typeof binding !== "object" || binding === null || Array.isArray(binding)) {
      errors.push(`LAYOUT_NODE_PROP_BINDING_INVALID_ENTRY: binding for "${propName}" must be an object`);
      continue;
    }
    const b = binding as Record<string, unknown>;

    if (typeof b.source !== "string" || !b.source.startsWith(EMISSION_DATA_PREFIX)) {
      errors.push(`LAYOUT_NODE_PROP_BINDING_INVALID_SOURCE: source for "${propName}" must be a string starting with "emission.data."`);
    }

    const capabilityError = validatePropBindingTarget(componentKind, propName);
    if (capabilityError) errors.push(capabilityError);

    for (const pathField of ["keyPath", "labelPath", "valuePath", "childrenPath"] as const) {
      if (b[pathField] !== undefined && typeof b[pathField] !== "string") {
        errors.push(`LAYOUT_NODE_PROP_BINDING_INVALID_PATH: ${pathField} for "${propName}" must be a string`);
      }
    }

    if (b.transform !== undefined) {
      if (typeof b.transform !== "string") {
        errors.push(`LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM: transform for "${propName}" must be a string`);
      } else if (!ALLOWED_PROP_BINDING_TRANSFORMS.has(b.transform)) {
        errors.push(`LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM: transform "${b.transform}" is not in the allowlist`);
      }
    }
  }
  return errors;
}
