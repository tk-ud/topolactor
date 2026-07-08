/**
 * Normalize backend emission/SSE data into the jsonKeyValue consumed by
 * projection_constructor.
 *
 * Collection outer shape preservation (SSOT: ScreenDataShapeQueryRuntime emission):
 * `screen_data_shape_query_result` carries `rows`, `aggregationResults`,
 * `activeColumns`, and `displayColumnMode`. That outer shape is preserved by
 * default so collection-consuming projections (propBindings, tables, aggregation
 * displays) keep every branch reachable.
 *
 * Collapsing to `rows[0]` happens ONLY when the manifest projection_definition
 * declares an explicit single-row input mapping (`inputMapping: "single_row"`).
 * There is no implicit first-row collapse.
 */

/** Data-defined input mapping declared on ProjectionDefinition. Absent = "collection". */
export type ProjectionInputMapping = "collection" | "single_row";

function isScreenDataShapeQueryResult(
  data: Record<string, unknown>,
): data is Record<string, unknown> & { rows: unknown[] } {
  return data.kind === "screen_data_shape_query_result" && Array.isArray(data.rows);
}

export function projectionInputFromData(
  data: Record<string, unknown> | null | undefined,
  inputMapping?: ProjectionInputMapping | null,
): Record<string, unknown> {
  if (!data) return {};
  if (inputMapping === "single_row" && isScreenDataShapeQueryResult(data)) {
    const first = data.rows[0];
    if (first && typeof first === "object" && !Array.isArray(first)) {
      return first as Record<string, unknown>;
    }
  }
  return data;
}
