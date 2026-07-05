import {
  qualifiedColumnKey,
  parseQualifiedColumnKey,
  type QualifiedColumnRef,
} from "./manifestLogicalTables.ts";
import {
  dataRowValues,
  type ContentDataRowDraft,
  type RelationIntentDraft,
} from "./manifestScreenDesign.ts";

/** Generate a UUID v4 string for manual initial-data entry. */
export function generateUuidValue(): string {
  if (typeof globalThis.crypto?.randomUUID === "function") {
    return globalThis.crypto.randomUUID();
  }
  const bytes = new Uint8Array(16);
  globalThis.crypto.getRandomValues(bytes);
  bytes[6] = (bytes[6]! & 0x0f) | 0x40;
  bytes[8] = (bytes[8]! & 0x3f) | 0x80;
  const hex = [...bytes].map((b) => b.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export type RelationUuidCandidate = {
  value: string;
  label: string;
};

function isUuidColumnType(dataType: string): boolean {
  return dataType.trim().toLowerCase() === "uuid";
}

function relatedQualifiedKeysForColumn(
  qualifiedKey: string,
  relationIntents: RelationIntentDraft[],
): string[] {
  const { tableRef, columnName } = parseQualifiedColumnKey(qualifiedKey);
  const keys = new Set<string>([qualifiedKey]);

  for (const rel of relationIntents) {
    const localRef = rel.localTableRef.trim();
    const localKey = rel.localKey.trim();
    const joinRef = rel.joinTableRef.trim();
    const remoteKey = rel.remoteKey.trim();
    if (!joinRef || !remoteKey) continue;

    const remoteQualified = qualifiedColumnKey(joinRef, remoteKey);

    if (localRef === tableRef && localKey === columnName) {
      keys.add(remoteQualified);
    }
    if (joinRef === tableRef && remoteKey === columnName) {
      keys.add(qualifiedColumnKey(localRef, localKey));
    }
  }

  return [...keys];
}

const DISPLAY_COLUMN_HINTS = [
  "name",
  "title",
  "label",
  "username",
  "email",
  "code",
  "key",
] as const;

/** Resolve table/column for dotted keys such as `auth.user.id`. */
function columnRefForKey(
  key: string,
  qualifiedColumns: QualifiedColumnRef[],
): { tableRef: string; columnName: string } {
  const fromCatalog = qualifiedColumns.find((q) => q.key === key);
  if (fromCatalog) {
    return {
      tableRef: fromCatalog.tableRef,
      columnName: fromCatalog.columnName,
    };
  }
  const lastDot = key.lastIndexOf(".");
  if (lastDot <= 0) return { tableRef: "", columnName: key.trim() };
  return {
    tableRef: key.slice(0, lastDot),
    columnName: key.slice(lastDot + 1),
  };
}

function semanticLabelForUuidValue(
  value: string,
  searchKeys: string[],
  initialDataRows: ContentDataRowDraft[],
  qualifiedColumns: QualifiedColumnRef[],
  fallbackIndex: number,
): string {
  for (const row of initialDataRows) {
    const vals = dataRowValues(row);
    const matchedKey = searchKeys.find((k) => vals[k]?.trim() === value);
    if (!matchedKey) continue;

    const { tableRef } = columnRefForKey(matchedKey, qualifiedColumns);
    for (const hint of DISPLAY_COLUMN_HINTS) {
      for (const q of qualifiedColumns) {
        if (q.tableRef !== tableRef) continue;
        const colName = q.columnName.toLowerCase();
        if (!colName.includes(hint) || isUuidColumnType(q.column.dataType)) {
          continue;
        }
        const display = vals[q.key]?.trim();
        if (display && display !== value) {
          return display;
        }
      }
    }

    for (const q of qualifiedColumns) {
      if (q.tableRef !== tableRef) continue;
      if (isUuidColumnType(q.column.dataType)) continue;
      const colName = q.columnName.toLowerCase();
      if (colName === "id") continue;
      const display = vals[q.key]?.trim();
      if (display && display !== value) {
        return display;
      }
    }
  }

  return `候補 ${fallbackIndex + 1}`;
}

/**
 * Candidate UUID values for a column: relation-resolved remote keys plus existing row values.
 * Labels prefer related row display fields; synthetic `候補 N` when none found.
 */
export function relationUuidCandidatesForColumn(
  qualifiedKey: string,
  relationIntents: RelationIntentDraft[],
  initialDataRows: ContentDataRowDraft[],
  qualifiedColumns: QualifiedColumnRef[] = [],
): RelationUuidCandidate[] {
  const { tableRef, columnName } = parseQualifiedColumnKey(qualifiedKey);
  const candidates = new Set<string>();

  for (const row of initialDataRows) {
    const v = dataRowValues(row)[qualifiedKey]?.trim();
    if (v) candidates.add(v);
  }

  for (const rel of relationIntents) {
    const localRef = rel.localTableRef.trim();
    const localKey = rel.localKey.trim();
    const joinRef = rel.joinTableRef.trim();
    const remoteKey = rel.remoteKey.trim();
    if (!joinRef || !remoteKey) continue;

    const remoteQualified = qualifiedColumnKey(joinRef, remoteKey);

    if (localRef === tableRef && localKey === columnName) {
      for (const row of initialDataRows) {
        const v = dataRowValues(row)[remoteQualified]?.trim();
        if (v) candidates.add(v);
      }
    }

    if (joinRef === tableRef && remoteKey === columnName) {
      for (const row of initialDataRows) {
        const v = dataRowValues(row)[qualifiedKey]?.trim();
        if (v) candidates.add(v);
      }
    }
  }

  const searchKeys = relatedQualifiedKeysForColumn(qualifiedKey, relationIntents);
  return [...candidates].map((value, index) => ({
    value,
    label: semanticLabelForUuidValue(
      value,
      searchKeys,
      initialDataRows,
      qualifiedColumns,
      index,
    ),
  }));
}

export function columnOptionLabel(q: QualifiedColumnRef): string {
  if (q.remoteManifestId) {
    return `${q.key}（有効マニフェスト: ${q.remoteManifestId.slice(0, 8)}…）`;
  }
  if (q.tableRef) {
    return `${q.key}（${q.tableRef}）`;
  }
  return q.key;
}
