import type { ScreenOperationKind } from "../runtime/screenAuthoringIntent.ts";
import type { ScreenDataShapeSummary } from "./manifestTopologyExtensions.ts";

export type ManifestScreenColumnDraft = {
  name: string;
  dataType: string;
  nullable: boolean;
};

/** Structured relation/join intent for draft data-shape only. admin/manifests owns created-manifest relations. */
export type RelationIntentDraft = {
  joinTableRef: string;
  localKey: string;
  remoteKey: string;
};

/** Per-operation entity target at event time (SSOT step 3). */
export type OperationEntityBindingDraft = {
  operationKind: ScreenOperationKind;
  entityTargetColumn: string;
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
  return {
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
    relationIntents: Array.isArray(entry.relationIntents)
      ? entry.relationIntents
      : [],
    initialDataRows: Array.isArray(entry.initialDataRows)
      ? entry.initialDataRows
      : [],
    operationEntityBindings: Array.isArray(entry.operationEntityBindings)
      ? entry.operationEntityBindings
      : [],
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
  return {
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
    columns: shape.columns && shape.columns.length > 0
      ? shape.columns
      : [{ name: "", dataType: "text", nullable: true }],
    relationIntents: Array.isArray(shape.relationIntents)
      ? shape.relationIntents
      : [],
    operationEntityBindings: Array.isArray(shape.operationEntityBindings)
      ? shape.operationEntityBindings.map((b) => ({
        operationKind: (b.operationKind as ScreenOperationKind) ?? "list",
        entityTargetColumn: b.entityTargetColumn ?? "",
      }))
      : [],
    initialDataRows: Array.isArray(shape.initialDataRows)
      ? shape.initialDataRows
      : [],
  };
}

export function parseSearchTargets(raw: string): string[] {
  return raw
    .split(/[,;\n]/)
    .map((s) => s.trim())
    .filter(Boolean);
}
