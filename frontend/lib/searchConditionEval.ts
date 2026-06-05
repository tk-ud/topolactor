import type { HavingCondition, SearchCondition } from "./manifestScreenDesign.ts";
import { evaluateMeasureOnRows } from "./aggregationMeasureEval.ts";

const DATE_TYPES = new Set(["date"]);
const TIMESTAMP_TYPES = new Set([
  "timestamp with time zone",
  "timestamp",
  "timestamptz",
  "datetime",
]);

export function isDateLikeType(dataType: string): boolean {
  const t = dataType.trim().toLowerCase();
  return DATE_TYPES.has(t) || TIMESTAMP_TYPES.has(t);
}

function columnDataType(
  column: string,
  columnTypes?: Record<string, string>,
): string | undefined {
  return columnTypes?.[column]?.trim() || undefined;
}

/** Parse a scalar for ordered comparison (numeric or date epoch ms). */
export function parseComparableScalar(
  value: string,
  dataType?: string,
): number | null {
  const raw = value.trim();
  if (!raw) return null;
  if (dataType && isDateLikeType(dataType)) {
    const ms = Date.parse(raw);
    return Number.isNaN(ms) ? null : ms;
  }
  const n = Number(raw);
  return Number.isNaN(n) ? null : n;
}

function compareOrdered(
  cellVal: string,
  threshold: string,
  op: ">" | ">=" | "<" | "<=",
  dataType?: string,
): boolean {
  const left = parseComparableScalar(cellVal, dataType);
  const right = parseComparableScalar(threshold, dataType);
  if (left === null || right === null) return false;
  switch (op) {
    case ">": return left > right;
    case ">=": return left >= right;
    case "<": return left < right;
    case "<=": return left <= right;
  }
}

export { computeMeasure } from "./aggregationMeasureEval.ts";

/**
 * Apply search conditions to rows using SSOT logicalConnector semantics:
 * conditions[i].logicalConnector joins conditions[i] to conditions[i+1].
 * Conditions are evaluated left-to-right; connector from previous condition
 * applies to the current result accumulation.
 */
export function applySearchConditions(
  rows: Record<string, string>[],
  conditions: SearchCondition[],
  columnTypes?: Record<string, string>,
): Record<string, string>[] {
  if (conditions.length === 0) return rows;
  return rows.filter((row) => {
    let result = true;
    for (let i = 0; i < conditions.length; i++) {
      const cond = conditions[i];
      const cellVal = row[cond.column] ?? "";
      const dataType = columnDataType(cond.column, columnTypes);
      let match = false;
      switch (cond.operator) {
        case "=": match = cellVal === (cond.value ?? ""); break;
        case "!=": case "<>": match = cellVal !== (cond.value ?? ""); break;
        case "like": case "ilike": match = cellVal.toLowerCase().includes((cond.value ?? "").toLowerCase()); break;
        case "not like": match = !cellVal.toLowerCase().includes((cond.value ?? "").toLowerCase()); break;
        case ">": match = compareOrdered(cellVal, cond.value ?? "", ">", dataType); break;
        case ">=": match = compareOrdered(cellVal, cond.value ?? "", ">=", dataType); break;
        case "<": match = compareOrdered(cellVal, cond.value ?? "", "<", dataType); break;
        case "<=": match = compareOrdered(cellVal, cond.value ?? "", "<=", dataType); break;
        case "between": {
          const left = parseComparableScalar(cellVal, dataType);
          const from = parseComparableScalar(cond.value ?? "", dataType);
          const to = parseComparableScalar(cond.valueTo ?? "", dataType);
          match = left !== null && from !== null && to !== null && left >= from && left <= to;
          break;
        }
        case "in": match = (cond.values ?? []).includes(cellVal); break;
        case "not in": match = !(cond.values ?? []).includes(cellVal); break;
        case "is null": match = cellVal === "" || cellVal == null; break;
        case "is not null": match = cellVal !== "" && cellVal != null; break;
        default: match = true;
      }
      if (i === 0) {
        result = match;
      } else {
        // conditions[i-1].logicalConnector joins row i-1 to row i (SSOT: "joins to the next condition").
        const conn = conditions[i - 1].logicalConnector ?? "and";
        if (conn === "and") result = result && match;
        else if (conn === "or") result = result || match;
        else if (conn === "not") result = result && !match;
      }
    }
    return result;
  });
}

/**
 * Filter aggregation groups by HAVING conditions applied to measure results.
 * Each HAVING condition refers to a {column, function} measure; groups that fail
 * any condition are excluded.
 */
export function applyHavingConditions(
  groups: Map<string, Record<string, string>[]>,
  havingConditions: HavingCondition[],
): Map<string, Record<string, string>[]> {
  if (havingConditions.length === 0) return groups;
  const filtered = new Map<string, Record<string, string>[]>();
  for (const [key, rows] of groups) {
    let keep = true;
    for (const hc of havingConditions) {
      const measureVal = evaluateMeasureOnRows(rows, hc);
      if (measureVal === null) { keep = false; break; }
      const threshold = Number(hc.value);
      switch (hc.operator) {
        case "=": keep = measureVal === threshold; break;
        case "!=": case "<>": keep = measureVal !== threshold; break;
        case ">": keep = measureVal > threshold; break;
        case ">=": keep = measureVal >= threshold; break;
        case "<": keep = measureVal < threshold; break;
        case "<=": keep = measureVal <= threshold; break;
      }
      if (!keep) break;
    }
    if (keep) filtered.set(key, rows);
  }
  return filtered;
}
