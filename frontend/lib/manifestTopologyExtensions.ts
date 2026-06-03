/** Parse manifest topology extension entries from AdminManifestDetail.topologyRawJson */

export type RelationIntentShape = {
  joinTableRef: string;
  localKey: string;
  remoteKey: string;
};

export type OperationEntityBindingShape = {
  operationKind: string;
  entityTargetColumn: string;
};

export type ColumnShape = {
  name: string;
  dataType: string;
  nullable: boolean;
};

export type ScreenDataShapeSummary = {
  tableRef: string | null;
  importSchemaName: string | null;
  searchTargets: string[];
  searchKeyColumns: string[];
  aggregationSpec: string | null;
  aggregationKey: string | null;
  displayColumns: string[];
  screenOperationKind: string | null;
  screenOperationKinds: string[];
  userFacingTopologyLabel: string | null;
  columns: ColumnShape[];
  relationIntents: RelationIntentShape[];
  operationEntityBindings: OperationEntityBindingShape[];
  initialDataRows: Record<string, string>[];
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
    const aggregationKey =
      typeof entry.aggregationKey === "string" ? entry.aggregationKey : null;
    const screenOperationKind =
      typeof entry.screenOperationKind === "string" ? entry.screenOperationKind : null;
    const screenOperationKinds = Array.isArray(entry.screenOperationKinds)
      ? entry.screenOperationKinds.filter((t): t is string => typeof t === "string")
      : screenOperationKind ? [screenOperationKind] : [];
    const userFacingTopologyLabel =
      typeof entry.userFacingTopologyLabel === "string"
        ? entry.userFacingTopologyLabel
        : null;
    const searchTargets = Array.isArray(entry.searchTargets)
      ? entry.searchTargets.filter((t): t is string => typeof t === "string")
      : [];
    const searchKeyColumns = Array.isArray(entry.searchKeyColumns)
      ? entry.searchKeyColumns.filter((t): t is string => typeof t === "string")
      : [];
    const displayColumns = Array.isArray(entry.displayColumns)
      ? entry.displayColumns.filter((t): t is string => typeof t === "string")
      : [];
    const columns = Array.isArray(entry.columns)
      ? entry.columns
          .filter((c): c is Record<string, unknown> => typeof c === "object" && c !== null)
          .map((c) => ({
            name: typeof c.name === "string" ? c.name : "",
            dataType: typeof c.dataType === "string" ? c.dataType : "text",
            nullable: typeof c.nullable === "boolean" ? c.nullable : true,
          }))
      : [];
    const relationIntents = Array.isArray(entry.relationIntents)
      ? entry.relationIntents
          .filter((r): r is Record<string, unknown> => typeof r === "object" && r !== null)
          .map((r) => ({
            joinTableRef: typeof r.joinTableRef === "string" ? r.joinTableRef : "",
            localKey: typeof r.localKey === "string" ? r.localKey : "",
            remoteKey: typeof r.remoteKey === "string" ? r.remoteKey : "",
          }))
      : [];
    const operationEntityBindings = Array.isArray(entry.operationEntityBindings)
      ? entry.operationEntityBindings
          .filter((b): b is Record<string, unknown> => typeof b === "object" && b !== null)
          .map((b) => ({
            operationKind: typeof b.operationKind === "string" ? b.operationKind : "",
            entityTargetColumn: typeof b.entityTargetColumn === "string"
              ? b.entityTargetColumn
              : "",
          }))
      : [];
    const initialDataRows = Array.isArray(entry.initialDataRows)
      ? entry.initialDataRows
          .filter((row): row is Record<string, unknown> => typeof row === "object" && row !== null)
          .map((row) => {
            const record: Record<string, string> = {};
            for (const [k, v] of Object.entries(row)) {
              record[k] = typeof v === "string" ? v : String(v);
            }
            return record;
          })
      : [];
    return {
      tableRef,
      importSchemaName,
      searchTargets,
      searchKeyColumns,
      aggregationSpec,
      aggregationKey,
      displayColumns,
      screenOperationKind,
      screenOperationKinds,
      userFacingTopologyLabel,
      columns,
      relationIntents,
      operationEntityBindings,
      initialDataRows,
    };
  }
  return {
    tableRef: null,
    importSchemaName: null,
    searchTargets: [],
    searchKeyColumns: [],
    aggregationSpec: null,
    aggregationKey: null,
    displayColumns: [],
    screenOperationKind: null,
    screenOperationKinds: [],
    userFacingTopologyLabel: null,
    columns: [],
    relationIntents: [],
    operationEntityBindings: [],
    initialDataRows: [],
  };
}
