/**
 * frontendLocalCalculationResolver.ts — pure frontend-local calculation engine.
 *
 * Computes business values (tax, labor cost, etc.) instantly in the browser
 * using reference data loaded from DB at startup.
 *
 * Constraints:
 * - No eval(), no Function(), no dynamic code execution.
 * - No fetch() calls.
 * - No Preact imports.
 * - All error returns are explicit — no silent fallback.
 * - literal source is allowed in tests and for fixed offsets.
 *   NOTE: literal source must NOT be used for business rates (税率・工費レート・単価)
 *   in production calculation definitions. Use ruleTable or emission sources instead.
 */

/** Source kinds for calculation variables */
export type CalcVariableSource =
  | { kind: "node"; nodeId: string; propKey: string }
  | { kind: "emission"; path: string } // must start with "emission.data."
  | {
    kind: "ruleTable";
    tablePath: string;
    matchConditions: RuleMatchCondition[];
    priorityOrder: "desc";
    effectiveDateHandling: "latest_effective";
    selectedField: string;
  }
  | { kind: "literal"; value: number; note?: string };

export type RuleMatchCondition = {
  field: string;
  valueFrom:
    | { kind: "node"; nodeId: string; propKey: string }
    | { kind: "literal"; value: unknown };
};

/** Allowed operations — no eval(), no Function() */
export type CalcOperation =
  | { op: "multiply"; a: string; b: string }
  | { op: "add"; a: string; b: string }
  | { op: "subtract"; a: string; b: string }
  | { op: "divide"; a: string; b: string }
  | { op: "percent"; base: string; rate: string }
  | { op: "taxIncluded"; base: string; rate: string }
  | { op: "taxAmount"; base: string; rate: string }
  | { op: "round"; a: string; decimals?: number }
  | { op: "floor"; a: string; decimals?: number }
  | { op: "ceil"; a: string; decimals?: number };

export type RoundingPolicy = "round" | "floor" | "ceil" | "none";

export type CalcBinding = {
  calculationId: string;
  variables: Record<string, CalcVariableSource>;
  operation: CalcOperation;
  targetNodeId: string;
  targetProp: string;
  roundingPolicy?: RoundingPolicy;
  roundingDecimals?: number;
};

export type CalcContext = {
  /** Live node values keyed by nodeId → propKey → value */
  nodeValues: Record<string, Record<string, unknown>>;
  /** DB-loaded emission data (read-only reference) */
  emissionData: Record<string, unknown>;
};

export type CalcResult =
  | { ok: true; value: number }
  | { ok: false; error: string };

const EMISSION_DATA_PREFIX = "emission.data.";

const ALLOWED_OPS = new Set([
  "multiply",
  "add",
  "subtract",
  "divide",
  "percent",
  "taxIncluded",
  "taxAmount",
  "round",
  "floor",
  "ceil",
]);

const ALLOWED_VARIABLE_KINDS = new Set([
  "node",
  "emission",
  "ruleTable",
  "literal",
]);

/**
 * Resolves a dotted path within an object.
 * Returns undefined when the path is not found.
 */
function resolveDottedPath(
  obj: unknown,
  path: string,
): unknown {
  if (!path) return undefined;
  const parts = path.split(".");
  let current: unknown = obj;
  for (const part of parts) {
    if (typeof current !== "object" || current === null || Array.isArray(current)) {
      return undefined;
    }
    current = (current as Record<string, unknown>)[part];
  }
  return current;
}

/**
 * Resolves a rule row from a table based on match conditions, then selects a field.
 * Filters by conditions, filters enabled !== false, sorts priority DESC then effectiveFrom DESC.
 */
export function selectRuleFromTable(
  table: unknown[],
  conditions: RuleMatchCondition[],
  selectedField: string,
  resolvedNodeValues: Record<string, Record<string, unknown>>,
): { ok: true; value: unknown } | { ok: false; error: string } {
  // Filter rows by conditions
  let filtered = table.filter((row) => {
    if (typeof row !== "object" || row === null || Array.isArray(row)) return false;
    const r = row as Record<string, unknown>;
    for (const cond of conditions) {
      let expectedValue: unknown;
      if (cond.valueFrom.kind === "literal") {
        expectedValue = cond.valueFrom.value;
      } else {
        // kind === "node"
        expectedValue = resolvedNodeValues[cond.valueFrom.nodeId]?.[cond.valueFrom.propKey];
      }
      if (r[cond.field] !== expectedValue) return false;
    }
    return true;
  });

  // Filter out disabled rows (enabled !== false passes)
  filtered = filtered.filter((row) => {
    const r = row as Record<string, unknown>;
    return r["enabled"] !== false;
  });

  if (filtered.length === 0) {
    const condStr = conditions
      .map((c) => {
        const v = c.valueFrom.kind === "literal"
          ? JSON.stringify(c.valueFrom.value)
          : `node:${c.valueFrom.nodeId}.${c.valueFrom.propKey}`;
        return `${c.field}=${v}`;
      })
      .join(", ");
    return {
      ok: false,
      error: `CALC_RULE_UNRESOLVED: no matching rule in table for conditions [${condStr}]`,
    };
  }

  // Sort: priority DESC, then effectiveFrom DESC
  filtered.sort((a, b) => {
    const ra = a as Record<string, unknown>;
    const rb = b as Record<string, unknown>;
    const pa = typeof ra["priority"] === "number" ? ra["priority"] : 0;
    const pb = typeof rb["priority"] === "number" ? rb["priority"] : 0;
    if (pb !== pa) return (pb as number) - (pa as number);
    const ea = typeof ra["effectiveFrom"] === "string" ? ra["effectiveFrom"] : "";
    const eb = typeof rb["effectiveFrom"] === "string" ? rb["effectiveFrom"] : "";
    if (eb > ea) return 1;
    if (ea > eb) return -1;
    return 0;
  });

  const best = filtered[0] as Record<string, unknown>;
  if (!(selectedField in best)) {
    return {
      ok: false,
      error: `CALC_RULE_UNRESOLVED: selected field "${selectedField}" not found in matched row`,
    };
  }
  return { ok: true, value: best[selectedField] };
}

/**
 * Resolves a single calculation variable to a number.
 */
export function resolveCalcVariable(
  name: string,
  source: CalcVariableSource,
  ctx: CalcContext,
): { ok: true; value: number } | { ok: false; error: string } {
  switch (source.kind) {
    case "node": {
      const raw = ctx.nodeValues[source.nodeId]?.[source.propKey];
      if (raw === undefined || raw === null || raw === "") {
        return {
          ok: false,
          error: `CALC_NODE_VALUE_UNRESOLVED: variable "${name}" — node "${source.nodeId}" prop "${source.propKey}" has no value`,
        };
      }
      const n = Number(raw);
      if (isNaN(n)) {
        return {
          ok: false,
          error: `CALC_NODE_VALUE_NAN: variable "${name}" — node "${source.nodeId}" prop "${source.propKey}" value "${raw}" is not a number`,
        };
      }
      return { ok: true, value: n };
    }

    case "emission": {
      if (!source.path.startsWith(EMISSION_DATA_PREFIX)) {
        return {
          ok: false,
          error: `CALC_EMISSION_PATH_INVALID: variable "${name}" — path "${source.path}" must start with "emission.data."`,
        };
      }
      const relativePath = source.path.slice(EMISSION_DATA_PREFIX.length);
      const resolved = resolveDottedPath(ctx.emissionData, relativePath);
      if (resolved === undefined) {
        return {
          ok: false,
          error: `CALC_EMISSION_PATH_UNRESOLVED: path '${source.path}' not found in emissionData`,
        };
      }
      if (Array.isArray(resolved)) {
        return {
          ok: false,
          error: `CALC_EMISSION_PATH_IS_ARRAY: path '${source.path}' resolves to an array; use ruleTable source for array paths`,
        };
      }
      const n = Number(resolved);
      if (isNaN(n)) {
        return {
          ok: false,
          error: `CALC_EMISSION_PATH_NAN: variable "${name}" — path '${source.path}' value "${resolved}" is not a number`,
        };
      }
      return { ok: true, value: n };
    }

    case "ruleTable": {
      if (!source.tablePath.startsWith(EMISSION_DATA_PREFIX)) {
        return {
          ok: false,
          error: `CALC_RULE_TABLE_PATH_INVALID: variable "${name}" — tablePath "${source.tablePath}" must start with "emission.data."`,
        };
      }
      const relativePath = source.tablePath.slice(EMISSION_DATA_PREFIX.length);
      const tableRaw = resolveDottedPath(ctx.emissionData, relativePath);
      if (tableRaw === undefined) {
        return {
          ok: false,
          error: `CALC_RULE_TABLE_UNRESOLVED: variable "${name}" — tablePath '${source.tablePath}' not found in emissionData`,
        };
      }
      if (!Array.isArray(tableRaw)) {
        return {
          ok: false,
          error: `CALC_RULE_TABLE_NOT_ARRAY: variable "${name}" — tablePath '${source.tablePath}' does not resolve to an array`,
        };
      }
      const selectResult = selectRuleFromTable(
        tableRaw,
        source.matchConditions,
        source.selectedField,
        ctx.nodeValues,
      );
      if (!selectResult.ok) return selectResult;
      const n = Number(selectResult.value);
      if (isNaN(n)) {
        return {
          ok: false,
          error: `CALC_RULE_TABLE_NAN: variable "${name}" — selected field "${source.selectedField}" value "${selectResult.value}" is not a number`,
        };
      }
      return { ok: true, value: n };
    }

    case "literal": {
      if (!isFinite(source.value)) {
        return {
          ok: false,
          error: `CALC_LITERAL_NOT_FINITE: variable "${name}" — literal value ${source.value} is not finite`,
        };
      }
      return { ok: true, value: source.value };
    }
  }
}

/**
 * Evaluates a calc operation given a map of resolved variable values.
 */
export function evaluateCalcOperation(
  op: CalcOperation,
  resolved: Record<string, number>,
): { ok: true; value: number } | { ok: false; error: string } {
  function getVar(name: string): { ok: true; value: number } | { ok: false; error: string } {
    if (!(name in resolved)) {
      return { ok: false, error: `CALC_OP_MISSING_VAR: variable "${name}" not found in resolved variables` };
    }
    return { ok: true, value: resolved[name] };
  }

  switch (op.op) {
    case "multiply": {
      const a = getVar(op.a);
      if (!a.ok) return a;
      const b = getVar(op.b);
      if (!b.ok) return b;
      return { ok: true, value: a.value * b.value };
    }
    case "add": {
      const a = getVar(op.a);
      if (!a.ok) return a;
      const b = getVar(op.b);
      if (!b.ok) return b;
      return { ok: true, value: a.value + b.value };
    }
    case "subtract": {
      const a = getVar(op.a);
      if (!a.ok) return a;
      const b = getVar(op.b);
      if (!b.ok) return b;
      return { ok: true, value: a.value - b.value };
    }
    case "divide": {
      const a = getVar(op.a);
      if (!a.ok) return a;
      const b = getVar(op.b);
      if (!b.ok) return b;
      if (b.value === 0) {
        return { ok: false, error: "CALC_DIVIDE_BY_ZERO: division by zero is not allowed" };
      }
      return { ok: true, value: a.value / b.value };
    }
    case "percent": {
      const base = getVar(op.base);
      if (!base.ok) return base;
      const rate = getVar(op.rate);
      if (!rate.ok) return rate;
      return { ok: true, value: base.value * (rate.value / 100) };
    }
    case "taxIncluded": {
      const base = getVar(op.base);
      if (!base.ok) return base;
      const rate = getVar(op.rate);
      if (!rate.ok) return rate;
      return { ok: true, value: base.value * (1 + rate.value / 100) };
    }
    case "taxAmount": {
      const base = getVar(op.base);
      if (!base.ok) return base;
      const rate = getVar(op.rate);
      if (!rate.ok) return rate;
      return { ok: true, value: base.value * (rate.value / 100) };
    }
    case "round": {
      const a = getVar(op.a);
      if (!a.ok) return a;
      const decimals = op.decimals ?? 0;
      const factor = Math.pow(10, decimals);
      return { ok: true, value: Math.round(a.value * factor) / factor };
    }
    case "floor": {
      const a = getVar(op.a);
      if (!a.ok) return a;
      const decimals = op.decimals ?? 0;
      const factor = Math.pow(10, decimals);
      return { ok: true, value: Math.floor(a.value * factor) / factor };
    }
    case "ceil": {
      const a = getVar(op.a);
      if (!a.ok) return a;
      const decimals = op.decimals ?? 0;
      const factor = Math.pow(10, decimals);
      return { ok: true, value: Math.ceil(a.value * factor) / factor };
    }
  }
}

/**
 * Applies a rounding policy to a number.
 */
export function applyRoundingPolicy(
  value: number,
  policy: RoundingPolicy | undefined,
  decimals: number | undefined,
): number {
  if (!policy || policy === "none") return value;
  const d = decimals ?? 0;
  const factor = Math.pow(10, d);
  switch (policy) {
    case "round":
      return Math.round(value * factor) / factor;
    case "floor":
      return Math.floor(value * factor) / factor;
    case "ceil":
      return Math.ceil(value * factor) / factor;
  }
}

/**
 * Evaluates a single CalcBinding against the provided context.
 * Returns CalcResult — errors do NOT throw.
 */
export function evaluateCalcBinding(
  binding: CalcBinding,
  ctx: CalcContext,
): CalcResult {
  // Resolve all variables
  const resolved: Record<string, number> = {};
  for (const [name, source] of Object.entries(binding.variables)) {
    const result = resolveCalcVariable(name, source, ctx);
    if (!result.ok) return result;
    resolved[name] = result.value;
  }

  // Evaluate the operation
  const opResult = evaluateCalcOperation(binding.operation, resolved);
  if (!opResult.ok) return opResult;

  // Apply rounding policy
  const finalValue = applyRoundingPolicy(
    opResult.value,
    binding.roundingPolicy,
    binding.roundingDecimals,
  );

  return { ok: true, value: finalValue };
}

/**
 * Evaluates all CalcBindings and returns a map from calculationId to result.
 * Errors are recorded in the result — they do NOT throw.
 */
export function evaluateAllCalcBindings(
  bindings: CalcBinding[],
  ctx: CalcContext,
): Map<string, { targetNodeId: string; targetProp: string; result: CalcResult }> {
  const results = new Map<
    string,
    { targetNodeId: string; targetProp: string; result: CalcResult }
  >();
  for (const binding of bindings) {
    const result = evaluateCalcBinding(binding, ctx);
    results.set(binding.calculationId, {
      targetNodeId: binding.targetNodeId,
      targetProp: binding.targetProp,
      result,
    });
  }
  return results;
}

/**
 * Allowed targetProp values per componentKind.
 * Used by UI Builder to restrict targetProp selects and by validateCalcBindingForKind.
 * null means the componentKind is not in the allowlist (unsupported as calc target).
 */
export const CALC_TARGET_PROP_BY_COMPONENT_KIND: Record<string, string[]> = {
  "form_input/input": ["value"],
  "form_input/textarea": ["value"],
  "form_input/search_input": ["value"],
  "form_input/select": ["value"],
  "form_input/checkbox": ["checked"],
  "calc_topology/calculation_preview_panel": ["result"],
  "calc_topology/computed_field_preview": ["preview", "value"],
  "calc_topology/cross_entity_calculation_panel": ["preview", "value"],
  "calc_topology/formula_builder": ["value"],
  // Structural HTML nodes carry componentKind="layout/structural_html" (set by makeStructuralHtmlNode
  // and parseVisualLayoutPatchJson). The bare "structural_html" key is kept as alias for
  // any path that resolves nodeKind directly. Only inlineText is valid as a calc target.
  "layout/structural_html": ["inlineText"],
  "structural_html": ["inlineText"],
};

/**
 * Returns the allowed targetProp values for a componentKind, or null if unknown.
 */
export function resolveAllowedTargetProps(componentKind: string): string[] | null {
  return CALC_TARGET_PROP_BY_COMPONENT_KIND[componentKind] ?? null;
}

/**
 * Validates that a targetProp is allowed for the given componentKind.
 * Returns error strings (empty = valid). When componentKind is not in the map,
 * returns no error (unknown kind is not blocked — only known kinds are constrained).
 */
export function validateCalcTargetProp(componentKind: string, targetProp: string): string[] {
  const allowed = resolveAllowedTargetProps(componentKind);
  if (allowed === null) return []; // unknown kind: not constrained
  if (!allowed.includes(targetProp)) {
    return [
      `CALC_TARGET_PROP_UNSUPPORTED: targetProp "${targetProp}" is not supported for componentKind "${componentKind}". Allowed: ${allowed.join(", ")}`,
    ];
  }
  return [];
}

/**
 * Validates the structure of a CalcBinding.
 * Returns an array of error strings (empty = valid).
 */
export function validateCalcBinding(binding: unknown): string[] {
  const errors: string[] = [];
  if (typeof binding !== "object" || binding === null || Array.isArray(binding)) {
    errors.push("CALC_BINDING_INVALID: binding must be an object");
    return errors;
  }
  const b = binding as Record<string, unknown>;

  // calculationId
  if (typeof b.calculationId !== "string" || b.calculationId.trim() === "") {
    errors.push("CALC_BINDING_INVALID_CALCULATION_ID: calculationId must be a non-empty string");
  }

  // variables
  if (typeof b.variables !== "object" || b.variables === null || Array.isArray(b.variables)) {
    errors.push("CALC_BINDING_INVALID_VARIABLES: variables must be an object");
  } else {
    const vars = b.variables as Record<string, unknown>;
    const varEntries = Object.entries(vars);
    if (varEntries.length === 0) {
      errors.push("CALC_BINDING_INVALID_VARIABLES: variables must have at least one entry");
    }
    for (const [varName, varSource] of varEntries) {
      if (typeof varSource !== "object" || varSource === null || Array.isArray(varSource)) {
        errors.push(`CALC_BINDING_INVALID_VARIABLE: variable "${varName}" source must be an object`);
        continue;
      }
      const vs = varSource as Record<string, unknown>;
      if (!ALLOWED_VARIABLE_KINDS.has(vs.kind as string)) {
        errors.push(
          `CALC_BINDING_INVALID_VARIABLE_KIND: variable "${varName}" kind "${vs.kind}" is not in the allowed set`,
        );
        continue;
      }
      switch (vs.kind) {
        case "emission":
          if (typeof vs.path !== "string" || !vs.path.startsWith(EMISSION_DATA_PREFIX)) {
            errors.push(
              `CALC_BINDING_INVALID_EMISSION_PATH: variable "${varName}" path must start with "emission.data."`,
            );
          }
          break;
        case "ruleTable":
          if (typeof vs.tablePath !== "string" || !vs.tablePath.startsWith(EMISSION_DATA_PREFIX)) {
            errors.push(
              `CALC_BINDING_INVALID_RULE_TABLE_PATH: variable "${varName}" tablePath must start with "emission.data."`,
            );
          }
          if (!Array.isArray(vs.matchConditions)) {
            errors.push(
              `CALC_BINDING_INVALID_RULE_TABLE_CONDITIONS: variable "${varName}" matchConditions must be an array`,
            );
          }
          if (typeof vs.selectedField !== "string" || vs.selectedField.trim() === "") {
            errors.push(
              `CALC_BINDING_INVALID_RULE_TABLE_SELECTED_FIELD: variable "${varName}" selectedField must be a non-empty string`,
            );
          }
          break;
        case "literal":
          if (typeof vs.value !== "number" || !isFinite(vs.value)) {
            errors.push(
              `CALC_BINDING_INVALID_LITERAL_VALUE: variable "${varName}" literal value must be a finite number`,
            );
          }
          break;
      }
    }
  }

  // operation
  if (typeof b.operation !== "object" || b.operation === null || Array.isArray(b.operation)) {
    errors.push("CALC_BINDING_INVALID_OPERATION: operation must be an object");
  } else {
    const op = b.operation as Record<string, unknown>;
    if (!ALLOWED_OPS.has(op.op as string)) {
      errors.push(
        `CALC_BINDING_INVALID_OPERATION_OP: operation.op "${op.op}" is not in the allowed set`,
      );
    }
  }

  // targetNodeId
  if (typeof b.targetNodeId !== "string" || b.targetNodeId.trim() === "") {
    errors.push("CALC_BINDING_INVALID_TARGET_NODE_ID: targetNodeId must be a non-empty string");
  }

  // targetProp
  if (typeof b.targetProp !== "string" || b.targetProp.trim() === "") {
    errors.push("CALC_BINDING_INVALID_TARGET_PROP: targetProp must be a non-empty string");
  }

  return errors;
}
