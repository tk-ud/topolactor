import type { AggregationMeasure } from "./aggregationMeasures.ts";

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

/** Numeric cell values for aggregation (non-numeric cells omitted). */
export function numericValuesForMeasure(
  rows: Record<string, string>[],
  column: string,
): number[] {
  return rows
    .map((r) => Number(r[column]))
    .filter((n) => !Number.isNaN(n));
}

/**
 * Evaluate one measure across rows. Supports qualified keys from multiple logical tables.
 * `count` uses non-empty cell count; other functions use numeric coercion.
 */
export function evaluateMeasureOnRows(
  rows: Record<string, string>[],
  measure: Pick<AggregationMeasure, "column" | "function">,
): number | null {
  const col = measure.column.trim();
  const fn = measure.function.trim();
  if (!col || !fn) return null;
  if (fn === "count") {
    return rows.filter((r) => (r[col] ?? "").trim() !== "").length;
  }
  return computeMeasure(numericValuesForMeasure(rows, col), fn);
}
