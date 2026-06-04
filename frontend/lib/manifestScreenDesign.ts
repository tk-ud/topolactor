import type { ScreenOperationKind } from "../runtime/screenAuthoringIntent.ts";
import type { ScreenDataShapeSummary } from "./manifestTopologyExtensions.ts";
import {
  normalizeAggregationMeasures,
  type AggregationMeasure,
} from "./aggregationMeasures.ts";
import {
  logicalTablesFromLegacyColumns,
  normalizeLogicalTables,
  normalizeRelationKeyColumn,
  primaryTableColumns,
  qualifyScreenDesignColumnKeys,
} from "./manifestLogicalTables.ts";

export type { AggregationMeasure };

export type ManifestScreenColumnDraft = {
  name: string;
  dataType: string;
  nullable: boolean;
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

/** Initial data row as key-value record intent. No direct DB write — stored as topology extension intent. */
export type InitialDataRowDraft = Record<string, string>;

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
  initialDataRows: InitialDataRowDraft[];
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

export function loadManifestScreenDesignLocal(
  manifestId: string,
): ManifestScreenDesignDraft | null {
  const entry = readAll()[manifestId];
  if (!entry) return null;
  const opKind = entry.operationKind ?? "list";
  const kinds = Array.isArray(entry.operationKinds) && entry.operationKinds.length > 0
    ? entry.operationKinds
    : [opKind];
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
    logicalTables: normalizeLogicalTables(
      Array.isArray(entry.logicalTables) ? entry.logicalTables : undefined,
      entry.columns,
    ),
    columns: primaryTableColumns(
      normalizeLogicalTables(
        Array.isArray(entry.logicalTables) ? entry.logicalTables : undefined,
        entry.columns,
      ),
    ),
    relationIntents: Array.isArray(entry.relationIntents)
      ? entry.relationIntents.map(normalizeRelationIntent)
      : [],
    aggregationMeasures: normalizeAggregationMeasures({
      aggregationMeasures: entry.aggregationMeasures,
      aggregationFunction: entry.aggregationFunction,
      aggregationColumns: entry.aggregationColumns,
    }),
    initialDataRows: Array.isArray(entry.initialDataRows)
      ? entry.initialDataRows
      : [],
    operationEntityBindings: Array.isArray(entry.operationEntityBindings)
      ? entry.operationEntityBindings.map(normalizeOperationEntityBinding)
      : [],
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
  return qualifyScreenDesignColumnKeys({
    screenLabel: shape.userFacingTopologyLabel ?? "",
    operationKind: kinds[0],
    operationKinds: kinds,
    tableRef: shape.tableRef ?? "",
    importSchemaName: shape.importSchemaName ?? "",
    searchTargets: shape.searchTargets.join(", "),
    searchKeyColumns: Array.isArray(shape.searchKeyColumns)
      ? shape.searchKeyColumns
      : shape.searchTargets,
    aggregationSpec: shape.aggregationSpec ?? "",
    aggregationKey: shape.aggregationKey ?? "",
    displayColumns: Array.isArray(shape.displayColumns)
      ? shape.displayColumns
      : [],
    aggregationColumns: Array.isArray(shape.aggregationColumns)
      ? shape.aggregationColumns
      : [],
    aggregationFunction: shape.aggregationFunction ?? "",
    aggregationMeasures: normalizeAggregationMeasures({
      aggregationMeasures: shape.aggregationMeasures,
      aggregationFunction: shape.aggregationFunction,
      aggregationColumns: shape.aggregationColumns,
    }),
    logicalTables: normalizeLogicalTables(shape.logicalTables, shape.columns),
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
    initialDataRows: Array.isArray(shape.initialDataRows)
      ? shape.initialDataRows
      : [],
  });
}

export function parseSearchTargets(raw: string): string[] {
  return raw
    .split(/[,;\n]/)
    .map((s) => s.trim())
    .filter(Boolean);
}
