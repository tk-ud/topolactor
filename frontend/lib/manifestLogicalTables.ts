import {
  UX_RELATION_ROW_ID_KEY,
  UX_RELATION_ROW_ID_LABEL,
} from "../content/adminUxTerms.ts";
import type {
  ContentDataRowDraft,
  LogicalTableDraft,
  ManifestScreenColumnDraft,
  ManifestScreenDesignDraft,
  RelationIntentDraft,
} from "./manifestScreenDesign.ts";
import { dataRowValues } from "./manifestScreenDesign.ts";

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
  /** Set when column resolves from Step 2.5 active-manifest remote side. */
  remoteManifestId?: string;
};

/** Active manifest logical tables for Step 3 relation resolution (from list_relationship_remote_targets). */
export type Step3RemoteTargetManifest = {
  manifestId: string;
  logicalTables: LogicalTableDraft[];
};

export type Step3FieldSourceResult = {
  qualifiedColumns: QualifiedColumnRef[];
  unresolvedErrors: string[];
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

function logicalTableByRef(
  tables: LogicalTableDraft[],
  tableRef: string,
): LogicalTableDraft | undefined {
  const ref = tableRef.trim();
  if (!ref) return undefined;
  return tables.find((t) => t.tableName.trim() === ref);
}

function disambiguatedActiveRemoteTableRef(
  manifestId: string,
  joinTableRef: string,
  existingKeys: Set<string>,
  columnName: string,
): string {
  const base = joinTableRef.trim();
  const candidates = [
    base,
    `${base}@${manifestId.slice(0, 8)}`,
    `${base}@${manifestId}`,
  ];
  for (const tableRef of candidates) {
    const key = qualifiedColumnKey(tableRef, columnName);
    if (!existingKeys.has(key)) return tableRef;
  }
  return `${base}@${manifestId}`;
}

function pushQualifiedColumn(
  out: QualifiedColumnRef[],
  keys: Set<string>,
  ref: QualifiedColumnRef,
): void {
  if (!ref.key || keys.has(ref.key)) return;
  keys.add(ref.key);
  out.push(ref);
}

function activeRemoteManifestMatches(
  joinTableRef: string,
  remoteKey: string,
  remoteTargets: Step3RemoteTargetManifest[],
): Step3RemoteTargetManifest[] {
  const tableRef = joinTableRef.trim();
  if (!tableRef) return [];
  const key = remoteKey.trim();
  return remoteTargets.filter((manifest) => {
    const table = logicalTableByRef(manifest.logicalTables, tableRef);
    if (!table) return false;
    if (!key) return true;
    return table.columns.some((c) => c.name.trim() === key);
  });
}

/**
 * When Step 2.5 saved join_table_ref on an active-remote table without remote_manifest_id,
 * infer the manifest when exactly one active target defines that table (and remote key when set).
 */
export function enrichRelationIntentsWithRemoteTargets(
  relationIntents: RelationIntentDraft[],
  logicalTables: LogicalTableDraft[],
  remoteTargets: Step3RemoteTargetManifest[],
): RelationIntentDraft[] {
  return relationIntents.map((rel) => {
    if (rel.remoteManifestId?.trim()) return rel;
    const joinTableRef = rel.joinTableRef.trim();
    if (!joinTableRef || logicalTableByRef(logicalTables, joinTableRef)) return rel;

    const matches = activeRemoteManifestMatches(
      joinTableRef,
      rel.remoteKey,
      remoteTargets,
    );
    if (matches.length !== 1) return rel;

    return { ...rel, remoteManifestId: matches[0].manifestId };
  });
}

/**
 * Step 3 field candidates: local logical tables plus columns from Step 2.5
 * relationIntents (draft remote tables and active-manifest remote tables).
 * Unresolved relation targets are reported explicitly (no silent draft-only fallback).
 */
export function step3FieldSourceFromDesign(
  logicalTables: LogicalTableDraft[],
  relationIntents: RelationIntentDraft[],
  remoteTargets: Step3RemoteTargetManifest[],
): Step3FieldSourceResult {
  const qualifiedColumns = qualifiedColumnsFromLogicalTables(logicalTables);
  const keys = new Set(qualifiedColumns.map((q) => q.key));
  const unresolvedErrors: string[] = [];
  const resolvedIntents = enrichRelationIntentsWithRemoteTargets(
    relationIntents,
    logicalTables,
    remoteTargets,
  );

  for (let i = 0; i < resolvedIntents.length; i++) {
    const rel = resolvedIntents[i];
    const joinTableRef = rel.joinTableRef.trim();
    if (!joinTableRef) {
      unresolvedErrors.push(
        `関連 ${i + 1}: 参照先テーブルが未設定です。Step 2.5 で参照先を選んでください。`,
      );
      continue;
    }

    const remoteManifestId = rel.remoteManifestId?.trim();
    if (remoteManifestId) {
      const manifest = remoteTargets.find((t) => t.manifestId === remoteManifestId);
      if (!manifest) {
        unresolvedErrors.push(
          `関連 ${i + 1}: 有効マニフェスト「${remoteManifestId}」を解決できません。参照先一覧を読み込んでください。`,
        );
        continue;
      }
      const remoteTable = logicalTableByRef(manifest.logicalTables, joinTableRef);
      if (!remoteTable) {
        unresolvedErrors.push(
          `関連 ${i + 1}: 有効マニフェスト上にテーブル「${joinTableRef}」がありません。`,
        );
        continue;
      }
      for (const col of remoteTable.columns) {
        const columnName = col.name.trim();
        if (!columnName) continue;
        const tableRef = disambiguatedActiveRemoteTableRef(
          remoteManifestId,
          joinTableRef,
          keys,
          columnName,
        );
        const key = qualifiedColumnKey(tableRef, columnName);
        pushQualifiedColumn(qualifiedColumns, keys, {
          key,
          tableRef,
          columnName,
          column: col,
          remoteManifestId,
        });
      }
      continue;
    }

    const draftTable = logicalTableByRef(logicalTables, joinTableRef);
    if (!draftTable) {
      const activeMatches = activeRemoteManifestMatches(
        joinTableRef,
        rel.remoteKey,
        remoteTargets,
      );
      if (activeMatches.length > 1) {
        unresolvedErrors.push(
          `関連 ${i + 1}: テーブル「${joinTableRef}」は複数の有効マニフェストに存在します。Step 2.5 で「有効マニフェストのテーブル」から参照先ページを選んでください。`,
        );
      } else if (activeMatches.length === 1 && remoteTargets.length === 0) {
        unresolvedErrors.push(
          `関連 ${i + 1}: 参照先「${joinTableRef}」は有効マニフェスト側のテーブルです。参照先一覧を読み込んでから再度開いてください。`,
        );
      } else {
        unresolvedErrors.push(
          `関連 ${i + 1}: 参照先テーブル「${joinTableRef}」を解決できません。Step 2.5 で有効マニフェスト参照を選ぶか、Step 2 で草稿内に同じテーブル名を定義してください。`,
        );
      }
    }
  }

  return { qualifiedColumns, unresolvedErrors };
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

/** Primary logical table ref for physical binding (first named table in Step 2). */
export function primaryLogicalTableRef(tables: LogicalTableDraft[]): string {
  return namedLogicalTableRefs(tables)[0] ?? "";
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
  row: ContentDataRowDraft,
): ContentDataRowDraft {
  const qualified = qualifiedColumnsFromLogicalTables(tables);
  const flat = dataRowValues(row);
  const nextValues: Record<string, string> = {};
  for (const q of qualified) {
    if (flat[q.key] !== undefined) {
      nextValues[q.key] = flat[q.key];
      continue;
    }
    const bareMatches = qualified.filter((x) => x.columnName === q.columnName);
    const bare = flat[q.columnName];
    if (bare !== undefined && bareMatches.length === 1) {
      nextValues[q.key] = bare;
    } else {
      nextValues[q.key] = "";
    }
  }
  return { values: nextValues, lineage: { ...row.lineage } };
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
