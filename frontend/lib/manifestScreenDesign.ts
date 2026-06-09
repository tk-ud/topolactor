import type { ScreenOperationKind } from "../runtime/screenAuthoringIntent.ts";
import type {
  HavingConditionShape,
  ScreenDataShapeSummary,
  SearchConditionShape,
} from "./manifestTopologyExtensions.ts";
import {
  flattenAggregationBlocks,
  hydrateAggregationBlockConditions,
  normalizeAggregationBlocks,
  normalizeAggregationMeasures,
  type AggregationBlock,
  type AggregationMeasure,
} from "./aggregationMeasures.ts";
import { formatAggregationSpecFromBlocks, formatSearchTargetsRaw } from "./screenDataShapeRawBinding.ts";
import {
  logicalTablesFromLegacyColumns,
  normalizeLogicalTables,
  normalizeRelationKeyColumn,
  primaryLogicalTableRef,
  primaryTableColumns,
  qualifyScreenDesignColumnKeys,
} from "./manifestLogicalTables.ts";

export type { AggregationBlock, AggregationMeasure };

/** Keep legacy flat fields in sync when blocks change (authoring — preserves in-progress measure rows). */
export function patchAggregationBlocks(
  blocks: AggregationBlock[],
  _primarySourceRef = "",
): Pick<
  ManifestScreenDesignDraft,
  "aggregationBlocks" | "aggregationKey" | "aggregationMeasures"
> & Partial<Pick<ManifestScreenDesignDraft, "searchConditions" | "havingConditions">> {
  const aggregationBlocks = blocks.map((b) => ({
    sourceRef: (b.sourceRef ?? "").trim(),
    aggregationKey: (b.aggregationKey ?? "").trim(),
    measures: Array.isArray(b.measures)
      ? b.measures.map((m) => ({
        column: String(m.column ?? ""),
        function: String(m.function ?? ""),
      }))
      : [],
    searchConditions: Array.isArray(b.searchConditions) ? [...b.searchConditions] : [],
    havingConditions: Array.isArray(b.havingConditions) ? [...b.havingConditions] : [],
  }));
  const { aggregationKey, aggregationMeasures } = flattenAggregationBlocks(aggregationBlocks);
  return {
    aggregationBlocks,
    aggregationKey,
    aggregationMeasures,
    ...(aggregationBlocks.length > 0
      ? { searchConditions: [] as SearchCondition[], havingConditions: [] as HavingCondition[] }
      : {}),
  };
}

/** Search operator vocabulary (SSOT step 3 searchConditions). */
export type SearchOperator =
  | "=" | "!=" | "<>" | "like" | "ilike" | "not like"
  | ">" | ">=" | "<" | "<="
  | "between" | "in" | "not in"
  | "is null" | "is not null";

/** Logical connector joining adjacent search conditions. */
export type LogicalConnector = "and" | "or" | "not";

/** Structured search condition (SSOT step 3 searchConditions). */
export type SearchCondition = {
  column: string;
  operator: SearchOperator;
  /** Single value for =, !=, <>, like, ilike, not like, >, >=, <, <=. */
  value?: string;
  /** Upper bound for between. */
  valueTo?: string;
  /** Value list for in / not in. */
  values?: string[];
  /** Connects to next condition. Default: and. */
  logicalConnector?: LogicalConnector;
};

/** HAVING condition on an aggregation measure result. */
export type HavingCondition = {
  column: string;
  function: string;
  operator: "=" | "!=" | "<>" | ">" | ">=" | "<" | "<=";
  value: string;
};

/** Explicit display column mode (SSOT step 3 displayColumnMode_ambiguity_resolution). */
export type DisplayColumnMode = "selected" | "all" | "none";

const SEARCH_OPERATORS: readonly SearchOperator[] = [
  "=", "!=", "<>", "like", "ilike", "not like",
  ">", ">=", "<", "<=", "between", "in", "not in",
  "is null", "is not null",
];
const HAVING_OPERATORS: readonly HavingCondition["operator"][] = [
  "=", "!=", "<>", ">", ">=", "<", "<=",
];
const LOGICAL_CONNECTORS: readonly LogicalConnector[] = ["and", "or", "not"];

function normalizeSearchConditionShape(s: SearchConditionShape): SearchCondition | null {
  if (!s.column || !SEARCH_OPERATORS.includes(s.operator as SearchOperator)) return null;
  return {
    column: s.column,
    operator: s.operator as SearchOperator,
    value: s.value,
    valueTo: s.valueTo,
    values: s.values,
    logicalConnector: LOGICAL_CONNECTORS.includes(s.logicalConnector as LogicalConnector)
      ? s.logicalConnector as LogicalConnector
      : undefined,
  };
}

function normalizeHavingConditionShape(h: HavingConditionShape): HavingCondition | null {
  if (!h.column || !h.function || !HAVING_OPERATORS.includes(h.operator as HavingCondition["operator"])) return null;
  return {
    column: h.column,
    function: h.function,
    operator: h.operator as HavingCondition["operator"],
    value: h.value,
  };
}

export type ManifestScreenColumnDraft = {
  name: string;
  dataType: string;
  nullable: boolean;
  /** References enum.groups.group_id when set. */
  enumGroupId?: string;
};

/** Step 2 logical table (SSOT: multiple tables per manifest). */
export type LogicalTableDraft = {
  tableName: string;
  columns: ManifestScreenColumnDraft[];
};

/** Structured relation/join intent: local table.column → remote table.column */
export type RelationIntentDraft = {
  localTableRef: string;
  localKey: string;
  joinTableRef: string;
  remoteKey: string;
  /** When set, remote table/column resolve on this active manifest (not draft-only). */
  remoteManifestId?: string;
};

/** Per-operation entity targets at event time (SSOT step 3, multi-select). */
export type OperationEntityBindingDraft = {
  operationKind: ScreenOperationKind;
  entityTargetColumns: string[];
};

export type DataRowLineageSource =
  | "manual"
  | "import_preview"
  | "import_applied"
  | "edited";

export type DataRowLineage = {
  source: DataRowLineageSource;
  snapshotId?: string;
  rowIndex?: number;
  applyLogId?: string;
};

/** Step3 unified data row with source lineage (manual / import / edited). */
export type ContentDataRowDraft = {
  values: Record<string, string>;
  lineage: DataRowLineage;
};

/** @deprecated flat map — normalized via normalizeContentDataRow on load. */
export type InitialDataRowDraft = Record<string, string>;

const LINEAGE_META_KEY = "__lineage";

export function isStructuredContentDataRow(
  row: unknown,
): row is ContentDataRowDraft {
  return typeof row === "object" && row !== null &&
    "values" in row && typeof (row as ContentDataRowDraft).values === "object" &&
    "lineage" in row && typeof (row as ContentDataRowDraft).lineage === "object";
}

export function normalizeContentDataRow(
  row: Record<string, unknown> | ContentDataRowDraft,
  defaultSource: DataRowLineageSource = "manual",
): ContentDataRowDraft {
  if (isStructuredContentDataRow(row)) {
    return {
      values: { ...row.values },
      lineage: { ...row.lineage },
    };
  }
  const values: Record<string, string> = {};
  let lineage: DataRowLineage | undefined;
  for (const [k, v] of Object.entries(row)) {
    if (k === LINEAGE_META_KEY && typeof v === "object" && v !== null) {
      lineage = v as DataRowLineage;
      continue;
    }
    values[k] = typeof v === "string" ? v : String(v ?? "");
  }
  return {
    values,
    lineage: lineage ?? { source: defaultSource },
  };
}

export function normalizeContentDataRows(
  rows: unknown[],
  defaultSource: DataRowLineageSource = "manual",
): ContentDataRowDraft[] {
  return rows
    .filter((r): r is Record<string, unknown> | ContentDataRowDraft =>
      typeof r === "object" && r !== null
    )
    .map((r) => normalizeContentDataRow(r, defaultSource));
}

export function dataRowValues(row: ContentDataRowDraft): Record<string, string> {
  return row.values;
}

export function markDataRowEdited(row: ContentDataRowDraft): ContentDataRowDraft {
  return {
    values: { ...row.values },
    lineage: { ...row.lineage, source: "edited" },
  };
}

export function flattenInitialDataRowsForSample(
  rows: ContentDataRowDraft[],
): Record<string, string>[] {
  return rows.map(dataRowValues);
}

/** Persist lineage alongside values in screen_data_shape.initialDataRows. */
export function serializeContentDataRowsForShape(
  rows: ContentDataRowDraft[],
): Record<string, unknown>[] {
  return rows.map((r) => ({
    values: r.values,
    lineage: r.lineage,
  }));
}

/** Local draft cache only — not canonical. Backend topology extensions are SSOT after save. */
export type ManifestScreenDesignDraft = {
  screenLabel: string;
  /** @deprecated use operationKinds — kept for local cache compat */
  operationKind: ScreenOperationKind;
  /** SSOT step 3: multi-select operation kinds. */
  operationKinds: ScreenOperationKind[];
  tableRef: string;
  importSchemaName: string;
  /** @deprecated raw comma-separated — preserved for advanced/raw disclosure only */
  searchTargets: string;
  /** Structured search key columns (normal-view selection). Maps to searchTargets on save. */
  searchKeyColumns: string[];
  /** @deprecated raw aggregation spec — preserved for advanced/raw disclosure only */
  aggregationSpec: string;
  /** Structured aggregation key (normal-view). "group by" must not appear in UX vocabulary. */
  aggregationKey: string;
  /** Structured display columns (normal-view multi-select). */
  displayColumns: string[];
  /** SQL-source-scoped aggregation blocks (Step 3 normal view). */
  aggregationBlocks: AggregationBlock[];
  /** Multiple measures e.g. salary+sum, salary+max (SSOT step 3). */
  aggregationMeasures: AggregationMeasure[];
  /** @deprecated use aggregationMeasures */
  aggregationFunction: string;
  /** @deprecated use aggregationMeasures */
  aggregationColumns: string[];
  /** Step 2: one or more logical tables. */
  logicalTables: LogicalTableDraft[];
  /** @deprecated flat columns — mirrors primary logical table for compat */
  columns: ManifestScreenColumnDraft[];
  /** Structured relation/join intents for this draft's data-shape only. */
  relationIntents: RelationIntentDraft[];
  /** Per-operation entity column binding at event time. */
  operationEntityBindings: OperationEntityBindingDraft[];
  /** Initial data rows as topology intent (not direct DB write). */
  initialDataRows: ContentDataRowDraft[];
  /** Structured search conditions with operator/value/logical-connector (SSOT step 3). */
  searchConditions: SearchCondition[];
  /** HAVING conditions on aggregation measure results (SSOT step 3). */
  havingConditions: HavingCondition[];
  /** Explicit display column mode (SSOT step 3 displayColumnMode_ambiguity_resolution). */
  displayColumnMode: DisplayColumnMode;
};

const STORAGE_KEY = "topolactor_manifest_screen_design_v2";

export const MANIFEST_SCREEN_DESIGN_LOCAL_CACHE_NOTE =
  "この端末に一時保存した未反映の変更があります。";

export const emptyManifestScreenDesign = (): ManifestScreenDesignDraft => ({
  screenLabel: "",
  operationKind: "list",
  operationKinds: ["list"],
  tableRef: "",
  importSchemaName: "",
  searchTargets: "",
  searchKeyColumns: [],
  aggregationSpec: "",
  aggregationKey: "",
  displayColumns: [],
  aggregationBlocks: [],
  aggregationMeasures: [],
  aggregationColumns: [],
  aggregationFunction: "",
  logicalTables: logicalTablesFromLegacyColumns([
    { name: "", dataType: "text", nullable: true },
  ]),
  columns: [{ name: "", dataType: "text", nullable: true }],
  relationIntents: [],
  operationEntityBindings: [],
  initialDataRows: [],
  searchConditions: [],
  havingConditions: [],
  displayColumnMode: "selected",
});

function readAll(): Record<string, ManifestScreenDesignDraft> {
  if (typeof globalThis.localStorage === "undefined") return {};
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Record<string, ManifestScreenDesignDraft>;
    return typeof parsed === "object" && parsed !== null ? parsed : {};
  } catch {
    return {};
  }
}

function resolveDesignAggregationFields(
  input: {
    aggregationBlocks?: AggregationBlock[];
    aggregationKey?: string | null;
    aggregationMeasures?: AggregationMeasure[];
    aggregationFunction?: string | null;
    aggregationColumns?: string[];
    primarySourceRef?: string;
  },
  globalSearch: SearchCondition[],
  globalHaving: HavingCondition[],
): Pick<
  ManifestScreenDesignDraft,
  "aggregationBlocks" | "aggregationKey" | "aggregationMeasures" | "searchConditions" | "havingConditions"
> {
  const aggregationBlocks = hydrateAggregationBlockConditions(
    normalizeAggregationBlocks(input),
    globalSearch,
    globalHaving,
  );
  const { aggregationKey, aggregationMeasures } = flattenAggregationBlocks(aggregationBlocks);
  const blockScoped = aggregationBlocks.length > 0;
  return {
    aggregationBlocks,
    aggregationKey,
    aggregationMeasures,
    searchConditions: blockScoped ? [] : globalSearch,
    havingConditions: blockScoped ? [] : globalHaving,
  };
}

export function loadManifestScreenDesignLocal(
  manifestId: string,
): ManifestScreenDesignDraft | null {
  const entry = readAll()[manifestId];
  if (!entry) return null;
  const opKind = entry.operationKind ?? "list";
  const kinds = Array.isArray(entry.operationKinds) && entry.operationKinds.length > 0
    ? entry.operationKinds
    : [opKind];
  const logicalTables = normalizeLogicalTables(
    Array.isArray(entry.logicalTables) ? entry.logicalTables : undefined,
    entry.columns,
  );
  const aggregationFields = resolveDesignAggregationFields(
    {
      aggregationBlocks: entry.aggregationBlocks,
      aggregationKey: entry.aggregationKey,
      aggregationMeasures: entry.aggregationMeasures,
      aggregationFunction: entry.aggregationFunction,
      aggregationColumns: entry.aggregationColumns,
      primarySourceRef: primaryLogicalTableRef(logicalTables),
    },
    Array.isArray(entry.searchConditions) ? entry.searchConditions : [],
    Array.isArray(entry.havingConditions) ? entry.havingConditions : [],
  );
  const draft: ManifestScreenDesignDraft = {
    ...emptyManifestScreenDesign(),
    ...entry,
    operationKind: opKind,
    operationKinds: kinds,
    tableRef: entry.tableRef ??
      (entry as { dbTableName?: string }).dbTableName ?? "",
    searchKeyColumns: Array.isArray(entry.searchKeyColumns)
      ? entry.searchKeyColumns
      : [],
    displayColumns: Array.isArray(entry.displayColumns)
      ? entry.displayColumns
      : [],
    aggregationColumns: Array.isArray(entry.aggregationColumns)
      ? entry.aggregationColumns
      : [],
    aggregationFunction: typeof entry.aggregationFunction === "string"
      ? entry.aggregationFunction
      : "",
    logicalTables,
    columns: primaryTableColumns(logicalTables),
    relationIntents: Array.isArray(entry.relationIntents)
      ? entry.relationIntents.map(normalizeRelationIntent)
      : [],
    ...aggregationFields,
    initialDataRows: normalizeContentDataRows(
      Array.isArray(entry.initialDataRows) ? entry.initialDataRows : [],
    ),
    operationEntityBindings: Array.isArray(entry.operationEntityBindings)
      ? entry.operationEntityBindings.map(normalizeOperationEntityBinding)
      : [],
    displayColumnMode: (entry.displayColumnMode === "selected" || entry.displayColumnMode === "all" || entry.displayColumnMode === "none")
      ? entry.displayColumnMode
      : (Array.isArray(entry.displayColumns) && entry.displayColumns.length > 0 ? "selected" : "all"),
  };
  return qualifyScreenDesignColumnKeys(draft);
}

function normalizeRelationIntent(
  r: RelationIntentDraft & { localTableRef?: string },
): RelationIntentDraft {
  return {
    localTableRef: r.localTableRef ?? "",
    joinTableRef: r.joinTableRef ?? "",
    localKey: normalizeRelationKeyColumn(r.localKey ?? ""),
    remoteKey: normalizeRelationKeyColumn(r.remoteKey ?? ""),
    remoteManifestId: typeof r.remoteManifestId === "string" && r.remoteManifestId.trim()
      ? r.remoteManifestId.trim()
      : undefined,
  };
}

function normalizeOperationEntityBinding(
  b: OperationEntityBindingDraft & { entityTargetColumn?: string },
): OperationEntityBindingDraft {
  const cols = Array.isArray(b.entityTargetColumns) && b.entityTargetColumns.length > 0
    ? b.entityTargetColumns
    : typeof b.entityTargetColumn === "string" && b.entityTargetColumn.trim()
    ? [b.entityTargetColumn.trim()]
    : [];
  return {
    operationKind: b.operationKind,
    entityTargetColumns: cols,
  };
}

export function saveManifestScreenDesignLocal(
  manifestId: string,
  draft: ManifestScreenDesignDraft,
): void {
  if (typeof globalThis.localStorage === "undefined") return;
  const all = readAll();
  all[manifestId] = draft;
  localStorage.setItem(STORAGE_KEY, JSON.stringify(all));
}

export function clearManifestScreenDesignLocal(manifestId: string): void {
  if (typeof globalThis.localStorage === "undefined") return;
  const all = readAll();
  delete all[manifestId];
  localStorage.setItem(STORAGE_KEY, JSON.stringify(all));
}

export function screenDesignFromBackendShape(
  shape: ScreenDataShapeSummary,
  operationKind: ScreenOperationKind,
): ManifestScreenDesignDraft {
  const kinds = Array.isArray(shape.screenOperationKinds) &&
      shape.screenOperationKinds.length > 0
    ? shape.screenOperationKinds as ScreenOperationKind[]
    : [(shape.screenOperationKind as ScreenOperationKind) ?? operationKind];
  const logicalTables = normalizeLogicalTables(shape.logicalTables, shape.columns);
  const primaryRef = primaryLogicalTableRef(logicalTables);
  const globalSearch = Array.isArray(shape.searchConditions)
    ? shape.searchConditions
      .map(normalizeSearchConditionShape)
      .filter((c): c is SearchCondition => c !== null)
    : [];
  const globalHaving = Array.isArray(shape.havingConditions)
    ? shape.havingConditions
      .map(normalizeHavingConditionShape)
      .filter((h): h is HavingCondition => h !== null)
    : [];
  const aggregationBlocksFromShape = Array.isArray(shape.aggregationBlocks) &&
      shape.aggregationBlocks.length > 0
    ? shape.aggregationBlocks.map((b) => ({
      sourceRef: b.sourceRef,
      aggregationKey: b.aggregationKey,
      measures: b.measures,
      searchConditions: Array.isArray(b.searchConditions)
        ? b.searchConditions
          .map(normalizeSearchConditionShape)
          .filter((c): c is SearchCondition => c !== null)
        : [],
      havingConditions: Array.isArray(b.havingConditions)
        ? b.havingConditions
          .map(normalizeHavingConditionShape)
          .filter((h): h is HavingCondition => h !== null)
        : [],
    }))
    : undefined;
  const aggregationFields = resolveDesignAggregationFields(
    {
      aggregationBlocks: aggregationBlocksFromShape,
      aggregationKey: shape.aggregationKey,
      aggregationMeasures: shape.aggregationMeasures,
      aggregationFunction: shape.aggregationFunction,
      aggregationColumns: shape.aggregationColumns,
      primarySourceRef: primaryRef,
    },
    globalSearch,
    globalHaving,
  );
  return qualifyScreenDesignColumnKeys({
    screenLabel: shape.userFacingTopologyLabel ?? "",
    operationKind: kinds[0],
    operationKinds: kinds,
    tableRef: shape.tableRef ?? "",
    importSchemaName: shape.importSchemaName ?? "",
    searchKeyColumns: Array.isArray(shape.searchKeyColumns)
      ? shape.searchKeyColumns
      : shape.searchTargets,
    searchTargets: formatSearchTargetsRaw(
      Array.isArray(shape.searchKeyColumns) && shape.searchKeyColumns.length > 0
        ? shape.searchKeyColumns
        : shape.searchTargets,
    ),
    aggregationSpec: aggregationFields.aggregationBlocks.length > 0
      ? formatAggregationSpecFromBlocks(aggregationFields.aggregationBlocks)
      : (shape.aggregationSpec ?? ""),
    displayColumns: Array.isArray(shape.displayColumns)
      ? shape.displayColumns
      : [],
    aggregationColumns: Array.isArray(shape.aggregationColumns)
      ? shape.aggregationColumns
      : [],
    aggregationFunction: shape.aggregationFunction ?? "",
    ...aggregationFields,
    logicalTables,
    columns: primaryTableColumns(
      normalizeLogicalTables(shape.logicalTables, shape.columns),
    ),
    relationIntents: Array.isArray(shape.relationIntents)
      ? shape.relationIntents.map((r) => normalizeRelationIntent({
        localTableRef: r.localTableRef ?? "",
        joinTableRef: r.joinTableRef,
        localKey: r.localKey,
        remoteKey: r.remoteKey,
      }))
      : [],
    operationEntityBindings: Array.isArray(shape.operationEntityBindings)
      ? shape.operationEntityBindings.map((b) => {
        const legacy = b.entityTargetColumn?.trim();
        const cols = Array.isArray(b.entityTargetColumns) && b.entityTargetColumns.length > 0
          ? b.entityTargetColumns
          : legacy
          ? [legacy]
          : [];
        return {
          operationKind: (b.operationKind as ScreenOperationKind) ?? "list",
          entityTargetColumns: cols,
        };
      })
      : [],
    initialDataRows: normalizeContentDataRows(
      Array.isArray(shape.initialDataRows) ? shape.initialDataRows : [],
    ),
    displayColumnMode: (shape.displayColumnMode === "selected" || shape.displayColumnMode === "all" || shape.displayColumnMode === "none")
      ? shape.displayColumnMode
      : (Array.isArray(shape.displayColumns) && shape.displayColumns.length > 0 ? "selected" : "all"),
  });
}

export function parseSearchTargets(raw: string): string[] {
  return raw
    .split(/[,;\n]/)
    .map((s) => s.trim())
    .filter(Boolean);
}
