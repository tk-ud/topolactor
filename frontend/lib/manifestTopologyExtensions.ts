/** Parse manifest topology extension entries from AdminManifestDetail.topologyRawJson */

export type AggregationMeasureShape = {
  column: string;
  function: string;
};

export type RelationIntentShape = {
  localTableRef: string;
  joinTableRef: string;
  localKey: string;
  remoteKey: string;
  remoteManifestId?: string;
};

export type LogicalTableShape = {
  tableName: string;
  columns: ColumnShape[];
};

export type OperationEntityBindingShape = {
  operationKind: string;
  entityTargetColumn: string | null;
  entityTargetColumns: string[];
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
  aggregationColumns: string[];
  aggregationFunction: string | null;
  aggregationMeasures: AggregationMeasureShape[];
  logicalTables: LogicalTableShape[];
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
    const aggregationColumns = Array.isArray(entry.aggregationColumns)
      ? entry.aggregationColumns.filter((t): t is string => typeof t === "string")
      : [];
    const aggregationFunction =
      typeof entry.aggregationFunction === "string" ? entry.aggregationFunction : null;
    const aggregationMeasures = Array.isArray(entry.aggregationMeasures)
      ? entry.aggregationMeasures
          .filter((m): m is Record<string, unknown> => typeof m === "object" && m !== null)
          .map((m) => ({
            column: typeof m.column === "string" ? m.column : "",
            function: typeof m.function === "string" ? m.function : "",
          }))
          .filter((m) => m.column && m.function)
      : [];
    const logicalTables = Array.isArray(entry.logicalTables)
      ? entry.logicalTables
          .filter((t): t is Record<string, unknown> => typeof t === "object" && t !== null)
          .map((t) => ({
            tableName: typeof t.tableName === "string" ? t.tableName : "",
            columns: Array.isArray(t.columns)
              ? t.columns
                  .filter((c): c is Record<string, unknown> =>
                    typeof c === "object" && c !== null
                  )
                  .map((c) => ({
                    name: typeof c.name === "string" ? c.name : "",
                    dataType: typeof c.dataType === "string" ? c.dataType : "text",
                    nullable: typeof c.nullable === "boolean" ? c.nullable : true,
                  }))
              : [],
          }))
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
            localTableRef: typeof r.localTableRef === "string" ? r.localTableRef : "",
            joinTableRef: typeof r.joinTableRef === "string" ? r.joinTableRef : "",
            localKey: typeof r.localKey === "string" ? r.localKey : "",
            remoteKey: typeof r.remoteKey === "string" ? r.remoteKey : "",
            remoteManifestId: typeof r.remoteManifestId === "string" && r.remoteManifestId.trim()
              ? r.remoteManifestId.trim()
              : undefined,
          }))
      : [];
    const operationEntityBindings = Array.isArray(entry.operationEntityBindings)
      ? entry.operationEntityBindings
          .filter((b): b is Record<string, unknown> => typeof b === "object" && b !== null)
          .map((b) => {
            const legacyCol = typeof b.entityTargetColumn === "string"
              ? b.entityTargetColumn
              : null;
            const cols = Array.isArray(b.entityTargetColumns)
              ? b.entityTargetColumns.filter((t): t is string => typeof t === "string")
              : legacyCol
              ? [legacyCol]
              : [];
            return {
              operationKind: typeof b.operationKind === "string" ? b.operationKind : "",
              entityTargetColumn: legacyCol,
              entityTargetColumns: cols,
            };
          })
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
      aggregationColumns,
      aggregationFunction,
      aggregationMeasures,
      logicalTables,
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
    aggregationColumns: [],
    aggregationFunction: null,
    aggregationMeasures: [],
    logicalTables: [],
    screenOperationKind: null,
    screenOperationKinds: [],
    userFacingTopologyLabel: null,
    columns: [],
    relationIntents: [],
    operationEntityBindings: [],
    initialDataRows: [],
  };
}
