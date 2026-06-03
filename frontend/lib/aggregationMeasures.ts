/** One aggregation measure: column + function (sum, avg, max, min, count). */
export type AggregationMeasure = {
  column: string;
  function: string;
};

export function emptyAggregationMeasure(): AggregationMeasure {
  return { column: "", function: "" };
}

/** Migrate legacy single function + aggregationColumns into measures list. */
export function normalizeAggregationMeasures(input: {
  aggregationMeasures?: AggregationMeasure[];
  aggregationFunction?: string | null;
  aggregationColumns?: string[];
}): AggregationMeasure[] {
  if (Array.isArray(input.aggregationMeasures) && input.aggregationMeasures.length > 0) {
    return input.aggregationMeasures.filter((m) => m.column.trim() && m.function.trim());
  }
  const fn = (input.aggregationFunction ?? "").trim();
  const cols = input.aggregationColumns ?? [];
  if (!fn) return [];
  return cols.filter((c) => c.trim()).map((column) => ({ column: column.trim(), function: fn }));
}

export function formatMeasureLabel(m: AggregationMeasure): string {
  return `${m.column} ${m.function}`;
}
