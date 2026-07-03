/**
 * Normalize backend emission/SSE data into the flat jsonKeyValue consumed by
 * projection_constructor. screen_data_shape query results carry rows[], while
 * projection constructors operate on one row's key/value shape.
 */
export function projectionInputFromData(data: Record<string, unknown> | null | undefined): Record<string, unknown> {
  if (!data) return {};
  if (data.kind === "screen_data_shape_query_result" && Array.isArray(data.rows)) {
    const first = data.rows[0];
    if (first && typeof first === "object" && !Array.isArray(first)) {
      return first as Record<string, unknown>;
    }
  }
  return data;
}
