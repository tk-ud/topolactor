/** Parse manifest topology extension entries from AdminManifestDetail.topologyRawJson */

export type ScreenDataShapeSummary = {
  tableRef: string | null;
  importSchemaName: string | null;
  searchTargets: string[];
  aggregationSpec: string | null;
  screenOperationKind: string | null;
};

function parseTopologyEntries(raw: string): Record<string, unknown>[] {
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (Array.isArray(parsed)) {
      return parsed.filter((e): e is Record<string, unknown> =>
        typeof e === "object" && e !== null
      );
    }
    return [];
  } catch {
    return [];
  }
}

export function extractScreenDataShapeFromTopology(raw: string): ScreenDataShapeSummary {
  for (const entry of parseTopologyEntries(raw)) {
    if (entry.type !== "screen_data_shape") continue;
    const tableRef =
      (typeof entry.tableRef === "string" && entry.tableRef) ||
      (typeof entry.dbTableName === "string" && entry.dbTableName) ||
      null;
    const importSchemaName =
      typeof entry.importSchemaName === "string" ? entry.importSchemaName : null;
    const aggregationSpec =
      typeof entry.aggregationSpec === "string" ? entry.aggregationSpec : null;
    const screenOperationKind =
      typeof entry.screenOperationKind === "string" ? entry.screenOperationKind : null;
    const searchTargets = Array.isArray(entry.searchTargets)
      ? entry.searchTargets.filter((t): t is string => typeof t === "string")
      : [];
    return { tableRef, importSchemaName, searchTargets, aggregationSpec, screenOperationKind };
  }
  return {
    tableRef: null,
    importSchemaName: null,
    searchTargets: [],
    aggregationSpec: null,
    screenOperationKind: null,
  };
}
