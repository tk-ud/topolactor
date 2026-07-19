/**
 * propBindingResolver — resolves prop bindings from emission.data (or the canonical
 * emission.navigationSequence hub_relations lane) into component props.
 *
 * Resolution order in renderEmission:
 *   default props → propsJson → stateJson → propBindings (this module)
 *
 * source must be "emission.data", start with "emission.data.", or be exactly
 * "emission.navigationSequence" (no dotted sub-paths — the whole resolved link list is
 * always the value; see EMISSION_NAVIGATION_SEQUENCE_SOURCE below).
 * transform must be in ALLOWED_PROP_BINDING_TRANSFORMS allowlist.
 * Resolved value overwrites the target prop key (propBindings wins over propsJson/stateJson for the same key).
 * When source path resolves to undefined, the prop key is left absent (no error — data may be missing).
 * When resolved value is not an array for array-target props, returns explicit error.
 *
 * Path fields (labelPath / valuePath / keyPath / childrenPath):
 *   - rowsToOptions:              uses labelPath → output.label, valuePath → output.value, keyPath → output.key
 *   - activeColumnsToTableColumns: uses labelPath → output.header for object columns
 *   - keyPath (activeColumnsToTableColumns): not applied — string columns already produce key; objects pass through
 *   - childrenPath:               component-informational; no automatic element mutation (tree component handles recursion)
 *   - navigationLinksToCardItems: maps ResolvedHubNavigationLink[] (label/href/resolvable/sequencePosition)
 *                                 to CardListItem[] (id/title/footer/variant) for display/card_list
 *
 * emission.navigationSequence boundary (docs/framework-core.yaml runtime_route_attention_boundary):
 * this source reaches ONLY the canonical_route_tab lane (hubs.hub_relations, resolved via
 * ManifestDispatcher.EnrichWithHubNavigationAsync) — the same data ProjectionShell's own
 * automatic nav bar already renders (resolveHubNavigationLinks). It must never read
 * emission.recommendNavigationProjection or any other SQL Attention / attention_recommendation_tab
 * field; those stay on their own separate emission field and are never exposed through
 * propBindings, so attention/recommendation scoring can never silently blend into a projected
 * "fixed route" list.
 */

import type { HubNavigationSequenceItem, PropBinding } from "../api/dispatch.ts";
import { resolveHubNavigationLinks } from "./projectionEntry.ts";

/**
 * Component kinds that accept prop bindings, and which prop names they accept.
 * Most entries are array props; data_display/json.data and aggregation display .data
 * targets accept the full resolved emission.data object for read-only projection.
 * Only props listed here may be the target of a propBinding.
 * SSOT: admin-console-workflow-ssot.yaml layout_node_props_contract.component_array_prop_capabilities
 */
export const COMPONENT_ARRAY_PROP_CAPABILITIES: Record<string, string[]> = {
  "data_display/table": ["rows", "columns"],
  "data_display/data_grid": ["rows", "columns"],
  "data_display/list": ["rows", "items"],
  "data_display/tree": ["nodes", "items"],
  "data_display/json": ["data"],
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
  "inline_edit/audit_diff_drawer": ["entries"],
  "calc_topology/aggregation_preview_table": ["data"],
  "calc_topology/hub_statistics_panel": ["data"],
};

/** Allowlist of permitted transform identifiers. */
export const ALLOWED_PROP_BINDING_TRANSFORMS: ReadonlySet<string> = new Set([
  "activeColumnsToTableColumns",
  "rowsToOptions",
  "navigationLinksToCardItems",
]);

const EMISSION_DATA_ROOT = "emission.data";
const EMISSION_DATA_PREFIX = `${EMISSION_DATA_ROOT}.`;

/**
 * Canonical hub_relations navigation sequence lane (emission.navigationSequence). Exact-match
 * only — no dotted sub-paths, since the whole resolved link list is always the bound value.
 * See the module boundary note above: never confuse with emission.recommendNavigationProjection.
 */
export const EMISSION_NAVIGATION_SEQUENCE_SOURCE = "emission.navigationSequence";

function isEmissionDataSource(source: string): boolean {
  return source === EMISSION_DATA_ROOT || source.startsWith(EMISSION_DATA_PREFIX);
}

function isNavigationSequenceSource(source: string): boolean {
  return source === EMISSION_NAVIGATION_SEQUENCE_SOURCE;
}

function isRecognizedPropBindingSource(source: string): boolean {
  return isEmissionDataSource(source) || isNavigationSequenceSource(source);
}

function acceptsNonArrayResolvedValue(componentKind: string, propName: string): boolean {
  if (propName !== "data") return false;
  return componentKind === "data_display/json" ||
    componentKind === "calc_topology/aggregation_preview_table" ||
    componentKind === "calc_topology/hub_statistics_panel";
}

/**
 * Strips the "emission.data." prefix and resolves the remaining dotted path
 * against emission.data. Returns undefined when the path is not found.
 */
export function resolveRuntimeDataPath(
  emissionData: Record<string, unknown>,
  source: string,
): unknown {
  if (source === EMISSION_DATA_ROOT) return emissionData;
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
 * Resolves emission.navigationSequence into the same ResolvedHubNavigationLink[] shape
 * ProjectionShell's own automatic nav bar uses (resolveHubNavigationLinks) — one shared
 * resolution, not a second reimplementation. Always an array (never undefined): an absent/empty
 * navigationSequence resolves to an empty link list, a legitimate "no links yet" state, not a
 * missing-data skip.
 */
export function resolveRuntimeNavigationSequence(
  navigationSequence: readonly HubNavigationSequenceItem[] | undefined,
): unknown[] {
  return resolveHubNavigationLinks(navigationSequence);
}

/**
 * Applies a named transform from the allowlist to the resolved array.
 * Receives the full binding descriptor so transforms can use path fields.
 * Returns { ok: false, error } when transform is not in the allowlist.
 */
export function applyPropBindingTransform(
  value: unknown[],
  transform: string,
  binding: PropBinding,
): { ok: true; value: unknown[] } | { ok: false; error: string } {
  if (!ALLOWED_PROP_BINDING_TRANSFORMS.has(transform)) {
    return { ok: false, error: `LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM: transform "${transform}" is not in the allowlist` };
  }
  switch (transform) {
    case "activeColumnsToTableColumns":
      return { ok: true, value: activeColumnsToTableColumns(value, binding) };
    case "rowsToOptions":
      return { ok: true, value: rowsToOptions(value, binding) };
    case "navigationLinksToCardItems":
      return { ok: true, value: navigationLinksToCardItems(value) };
    default:
      return { ok: false, error: `LAYOUT_NODE_PROP_BINDING_INVALID_TRANSFORM: transform "${transform}" is not implemented` };
  }
}

/**
 * Maps ResolvedHubNavigationLink[] (from resolveRuntimeNavigationSequence below) to
 * display/card_list's CardListItem shape (frontend/components/CardList.tsx):
 * sequencePosition → id, label → title, resolvable href → footer, resolvability → variant.
 * Unresolvable links (no href) get variant "warning" and an omitted footer rather than a
 * fabricated placeholder link.
 */
function navigationLinksToCardItems(value: unknown[]): unknown[] {
  return value.map((entry) => {
    if (typeof entry !== "object" || entry === null || Array.isArray(entry)) return entry;
    const link = entry as {
      label?: unknown;
      href?: unknown;
      resolvable?: unknown;
      sequencePosition?: unknown;
      hubRelationId?: unknown;
      topologyManifestId?: unknown;
      relatedHubId?: unknown;
    };
    return {
      id: link.sequencePosition,
      title: link.label,
      footer: link.resolvable === true && typeof link.href === "string" ? link.href : undefined,
      variant: link.resolvable === true ? "default" : "warning",
      // Carried through even though CardListItem's own rendering (CardList.tsx) does not
      // display them — satisfies selected_link_payload_required
      // (admin-normal-surface-projection-seed-ssot.yaml) so any future onSelect wiring on this
      // node has the full identity available without a second resolution path.
      hubRelationId: link.hubRelationId,
      topologyManifestId: link.topologyManifestId,
      relatedHubId: link.relatedHubId,
    };
  });
}

/**
 * Converts activeColumns (string[] or object[]) to table column descriptors.
 * Strings become { key, header }.
 * Objects: when labelPath is specified, uses element[labelPath] as header; otherwise passes through.
 */
function activeColumnsToTableColumns(
  value: unknown[],
  binding: PropBinding,
): unknown[] {
  return value.map((col) => {
    if (typeof col === "string") return { key: col, header: col };
    if (typeof col === "object" && col !== null && !Array.isArray(col)) {
      const c = col as Record<string, unknown>;
      if (binding.labelPath) {
        return { ...c, header: c[binding.labelPath] ?? c.header ?? c.key };
      }
      return c;
    }
    return col;
  });
}

/**
 * Maps rows to option objects using labelPath and valuePath.
 * When labelPath or valuePath is specified, each element is mapped to
 * { ...element, value: element[valuePath], label: element[labelPath] }.
 * When neither is specified, rows are passed through as-is (identity).
 */
function rowsToOptions(
  value: unknown[],
  binding: PropBinding,
): unknown[] {
  const { labelPath, valuePath, keyPath } = binding;
  if (!labelPath && !valuePath && !keyPath) return value;
  return value.map((row) => {
    if (typeof row !== "object" || row === null || Array.isArray(row)) return row;
    const r = row as Record<string, unknown>;
    const result: Record<string, unknown> = { ...r };
    if (valuePath) result.value = r[valuePath];
    if (labelPath) result.label = r[labelPath];
    if (keyPath) result.key = r[keyPath];
    return result;
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
 *   3. resolve path against emissionData, or against navigationSequence for the
 *      emission.navigationSequence source
 *   4. skip if resolved value is undefined (data absent)
 *   5. error if resolved value is not an array
 *   6. apply transform (with full binding descriptor for path fields) if specified
 *   7. merge into props
 *
 * navigationSequence is optional and defaults to an empty link list — existing callers that
 * only bind to emission.data are unaffected.
 *
 * Returns { ok: false, error } on first validation or transform error.
 * Returns { ok: true, props } with all resolved bindings merged.
 */
export function resolvePropBindings(
  baseProps: Record<string, unknown>,
  propBindings: Record<string, PropBinding>,
  componentKind: string,
  emissionData: Record<string, unknown>,
  navigationSequence?: readonly HubNavigationSequenceItem[],
): PropBindingResult {
  const props = { ...baseProps };

  for (const [propName, binding] of Object.entries(propBindings)) {
    const { source, transform } = binding;

    if (!isRecognizedPropBindingSource(source)) {
      return {
        ok: false,
        error:
          `LAYOUT_NODE_PROP_BINDING_INVALID_SOURCE: source "${source}" must be "emission.data", start with "emission.data.", or be "${EMISSION_NAVIGATION_SEQUENCE_SOURCE}"`,
      };
    }

    const capabilityError = validatePropBindingTarget(componentKind, propName);
    if (capabilityError) return { ok: false, error: capabilityError };

    const resolved = isNavigationSequenceSource(source)
      ? resolveRuntimeNavigationSequence(navigationSequence)
      : resolveRuntimeDataPath(emissionData, source);
    if (resolved === undefined) continue;

    if (!Array.isArray(resolved)) {
      if (acceptsNonArrayResolvedValue(componentKind, propName)) {
        props[propName] = resolved;
        continue;
      }
      return {
        ok: false,
        error: `LAYOUT_NODE_PROP_BINDING_SCALAR_SOURCE: source "${source}" resolved to a non-array value for prop "${propName}"`,
      };
    }

    let finalValue: unknown[] = resolved;
    if (transform) {
      const transformResult = applyPropBindingTransform(resolved, transform, binding);
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

    if (typeof b.source !== "string" || !isRecognizedPropBindingSource(b.source)) {
      errors.push(
        `LAYOUT_NODE_PROP_BINDING_INVALID_SOURCE: source for "${propName}" must be a string equal to "emission.data", starting with "emission.data.", or equal to "${EMISSION_NAVIGATION_SEQUENCE_SOURCE}"`,
      );
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
