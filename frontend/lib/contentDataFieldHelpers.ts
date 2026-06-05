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

/**
 * Candidate UUID values for a column: relation-resolved remote keys plus existing row values.
 */
export function relationUuidCandidatesForColumn(
  qualifiedKey: string,
  relationIntents: RelationIntentDraft[],
  initialDataRows: ContentDataRowDraft[],
): string[] {
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

  return [...candidates];
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
