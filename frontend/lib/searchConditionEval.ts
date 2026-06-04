import type { HavingCondition, SearchCondition } from "./manifestScreenDesign.ts";

/** Compute an aggregation measure value from a list of numbers. Returns null for unknown functions or empty input. */
export function computeMeasure(values: number[], fn: string): number | null {
  if (values.length === 0) return null;
  switch (fn) {
    case "sum": return values.reduce((a, b) => a + b, 0);
    case "avg": return values.reduce((a, b) => a + b, 0) / values.length;
    case "max": return Math.max(...values);
    case "min": return Math.min(...values);
    case "count": return values.length;
    default: return null;
  }
}

/**
 * Apply search conditions to rows using SSOT logicalConnector semantics:
 * conditions[i].logicalConnector joins conditions[i] to conditions[i+1].
 * Conditions are evaluated left-to-right; connector from previous condition
 * applies to the current result accumulation.
 */
export function applySearchConditions(
  rows: Record<string, string>[],
  conditions: SearchCondition[],
): Record<string, string>[] {
  if (conditions.length === 0) return rows;
  return rows.filter((row) => {
    let result = true;
    for (let i = 0; i < conditions.length; i++) {
      const cond = conditions[i];
      const cellVal = row[cond.column] ?? "";
      let match = false;
      switch (cond.operator) {
        case "=": match = cellVal === (cond.value ?? ""); break;
        case "!=": case "<>": match = cellVal !== (cond.value ?? ""); break;
        case "like": case "ilike": match = cellVal.toLowerCase().includes((cond.value ?? "").toLowerCase()); break;
        case "not like": match = !cellVal.toLowerCase().includes((cond.value ?? "").toLowerCase()); break;
        case ">": match = Number(cellVal) > Number(cond.value ?? 0); break;
        case ">=": match = Number(cellVal) >= Number(cond.value ?? 0); break;
        case "<": match = Number(cellVal) < Number(cond.value ?? 0); break;
        case "<=": match = Number(cellVal) <= Number(cond.value ?? 0); break;
        case "between":
          match = Number(cellVal) >= Number(cond.value ?? 0) && Number(cellVal) <= Number(cond.valueTo ?? 0);
          break;
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
      const nums = rows.map((r) => Number(r[hc.column])).filter((n) => !Number.isNaN(n));
      const measureVal = computeMeasure(nums, hc.function);
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
