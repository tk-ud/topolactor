import {
  UX_RELATION_ROW_ID_KEY,
  UX_RELATION_ROW_ID_LABEL,
} from "../content/adminUxTerms.ts";
import type {
  InitialDataRowDraft,
  LogicalTableDraft,
  ManifestScreenColumnDraft,
  ManifestScreenDesignDraft,
} from "./manifestScreenDesign.ts";

/** Physical-table-qualified key: `employees.name` when tableName is set; else bare `name`. */
export function qualifiedColumnKey(tableRef: string, columnName: string): string {
  const col = columnName.trim();
  if (!col) return "";
  const table = tableRef.trim();
  return table ? `${table}.${col}` : col;
}

export function parseQualifiedColumnKey(
  key: string,
): { tableRef: string; columnName: string } {
  const k = key.trim();
  const dot = k.indexOf(".");
  if (dot <= 0) return { tableRef: "", columnName: k };
  return { tableRef: k.slice(0, dot), columnName: k.slice(dot + 1) };
}

export type QualifiedColumnRef = {
  key: string;
  tableRef: string;
  columnName: string;
  column: ManifestScreenColumnDraft;
};

export function qualifiedColumnsFromLogicalTables(
  tables: LogicalTableDraft[],
): QualifiedColumnRef[] {
  const out: QualifiedColumnRef[] = [];
  for (const table of tables) {
    const tableRef = table.tableName.trim();
    for (const col of table.columns) {
      const columnName = col.name.trim();
      if (!columnName) continue;
      const key = qualifiedColumnKey(tableRef, columnName);
      out.push({ key, tableRef, columnName, column: col });
    }
  }
  return out;
}

/** Flatten column names from all logical tables (legacy; prefer qualifiedColumnsFromLogicalTables). */
export function namedColumnsFromLogicalTables(
  tables: LogicalTableDraft[],
): ManifestScreenColumnDraft[] {
  return qualifiedColumnsFromLogicalTables(tables).map((q) => q.column);
}

/** Logical table names with non-empty tableName. */
export function namedLogicalTableRefs(tables: LogicalTableDraft[]): string[] {
  return tables.map((t) => t.tableName.trim()).filter(Boolean);
}

/** Column names defined on a logical table (empty tableRef matches first unnamed table). */
export function columnNamesForTableRef(
  tables: LogicalTableDraft[],
  tableRef: string,
): string[] {
  const ref = tableRef.trim();
  const table = ref
    ? tables.find((t) => t.tableName.trim() === ref)
    : tables[0];
  if (!table) return [];
  return table.columns.map((c) => c.name.trim()).filter(Boolean);
}

export type RelationKeyColumnOption = { value: string; label: string };

/** Canonical row-id key for relation intents (SSOT: user.id → employee.user_id). */
export const RELATION_ROW_ID_KEY = UX_RELATION_ROW_ID_KEY;

/** Legacy drafts used recordId; normalize to id on load/save. */
export function normalizeRelationKeyColumn(name: string): string {
  const n = name.trim();
  return n === "recordId" ? RELATION_ROW_ID_KEY : n;
}

function userDefinesRowIdColumn(userColumns: string[]): boolean {
  return userColumns.includes(RELATION_ROW_ID_KEY) ||
    userColumns.includes("recordId");
}

/**
 * Step-2 logical columns plus row id when not already defined.
 * Row id is one option: value `id`, label `id (record id)`.
 */
export function relationKeyColumnOptionsForTableRef(
  tables: LogicalTableDraft[],
  tableRef: string,
): RelationKeyColumnOption[] {
  const user = columnNamesForTableRef(tables, tableRef);
  const out: RelationKeyColumnOption[] = [];
  if (!userDefinesRowIdColumn(user)) {
    out.push({ value: RELATION_ROW_ID_KEY, label: UX_RELATION_ROW_ID_LABEL });
  }
  for (const name of user) {
    if (name === "recordId") continue;
    if (!out.some((o) => o.value === name)) {
      out.push({ value: name, label: name });
    }
  }
  return out;
}

/** Default local table for relation intents (primary / first named). */
export function defaultLocalTableRef(
  tables: LogicalTableDraft[],
  physicalTableRef?: string,
): string {
  const named = namedLogicalTableRefs(tables);
  const pref = physicalTableRef?.trim();
  if (pref && named.includes(pref)) return pref;
  return named[0] ?? "";
}

export function emptyLogicalTable(): LogicalTableDraft {
  return {
    tableName: "",
    columns: [{ name: "", dataType: "text", nullable: true }],
  };
}

/** Build logicalTables from legacy flat columns (single implicit table). */
export function logicalTablesFromLegacyColumns(
  columns: ManifestScreenColumnDraft[],
): LogicalTableDraft[] {
  if (!columns.length) return [emptyLogicalTable()];
  return [{ tableName: "", columns: [...columns] }];
}

/** Primary table columns sent as legacy `columns` field (first table). */
export function primaryTableColumns(
  tables: LogicalTableDraft[],
): ManifestScreenColumnDraft[] {
  const first = tables[0];
  return first?.columns ?? [];
}

function upgradeColumnKeyForTables(
  tables: LogicalTableDraft[],
  key: string,
  qualified: QualifiedColumnRef[],
): string {
  const trimmed = key.trim();
  if (!trimmed) return trimmed;
  const canonical = qualifiedColumnKey(
    parseQualifiedColumnKey(trimmed).tableRef,
    parseQualifiedColumnKey(trimmed).columnName,
  );
  const known = qualified.find((q) => q.key === canonical || q.key === trimmed);
  if (known) return known.key;
  const bareMatches = qualified.filter((q) => q.columnName === trimmed);
  if (bareMatches.length >= 1) return bareMatches[0].key;
  return trimmed;
}

function qualifyKeyList(
  tables: LogicalTableDraft[],
  keys: string[],
): string[] {
  const qualified = qualifiedColumnsFromLogicalTables(tables);
  const seen = new Set<string>();
  const out: string[] = [];
  for (const k of keys) {
    const q = upgradeColumnKeyForTables(tables, k, qualified);
    if (q && !seen.has(q)) {
      seen.add(q);
      out.push(q);
    }
  }
  return out;
}

function qualifyInitialDataRow(
  tables: LogicalTableDraft[],
  row: InitialDataRowDraft,
): InitialDataRowDraft {
  const qualified = qualifiedColumnsFromLogicalTables(tables);
  const next: InitialDataRowDraft = {};
  for (const q of qualified) {
    if (row[q.key] !== undefined) {
      next[q.key] = row[q.key];
      continue;
    }
    const bareMatches = qualified.filter((x) => x.columnName === q.columnName);
    const bare = row[q.columnName];
    if (bare !== undefined && bareMatches.length === 1) {
      next[q.key] = bare;
    } else {
      next[q.key] = "";
    }
  }
  return next;
}

/** Step 3 field keys use `tableRef.columnName` when multiple logical tables are named. */
export function qualifyScreenDesignColumnKeys(
  draft: ManifestScreenDesignDraft,
): ManifestScreenDesignDraft {
  const tables = draft.logicalTables;
  const qualified = qualifiedColumnsFromLogicalTables(tables);
  if (qualified.length === 0) return draft;
  const hasQualified = qualified.some((q) => q.tableRef !== "");
  if (!hasQualified) return draft;

  return {
    ...draft,
    searchKeyColumns: qualifyKeyList(tables, draft.searchKeyColumns),
    displayColumns: qualifyKeyList(tables, draft.displayColumns),
    aggregationKey: upgradeColumnKeyForTables(tables, draft.aggregationKey, qualified),
    aggregationColumns: qualifyKeyList(tables, draft.aggregationColumns),
    aggregationMeasures: draft.aggregationMeasures.map((m) => ({
      ...m,
      column: upgradeColumnKeyForTables(tables, m.column, qualified),
    })),
    operationEntityBindings: draft.operationEntityBindings.map((b) => ({
      ...b,
      entityTargetColumns: qualifyKeyList(tables, b.entityTargetColumns),
    })),
    initialDataRows: draft.initialDataRows.map((row) =>
      qualifyInitialDataRow(tables, row)
    ),
  };
}

export function normalizeLogicalTables(
  tables: LogicalTableDraft[] | undefined,
  legacyColumns: ManifestScreenColumnDraft[] | undefined,
): LogicalTableDraft[] {
  if (Array.isArray(tables) && tables.length > 0) {
    return tables.map((t) => ({
      tableName: t.tableName ?? "",
      columns: Array.isArray(t.columns) && t.columns.length > 0
        ? t.columns
        : [{ name: "", dataType: "text", nullable: true }],
    }));
  }
  if (legacyColumns && legacyColumns.length > 0) {
    return logicalTablesFromLegacyColumns(legacyColumns);
  }
  return [emptyLogicalTable()];
}
