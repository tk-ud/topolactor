import type { ContentDataRowDraft, DataRowLineageSource } from "./manifestScreenDesign.ts";

/** Column spec for Step3 / import unified type diagnostics. */
export type ConformanceColumnSpec = {
  key: string;
  dataType: string;
  nullable: boolean;
  enumGroupId?: string;
};

export type ContentDataConformanceWarning = {
  code: string;
  /** CI Attention fragment kind — non-blocking for draft save. */
  kind: "structural_violation" | "valid_candidate";
  rowIndex: number;
  columnKey?: string;
  message: string;
  severity: "warning";
  lineageSource?: DataRowLineageSource;
};

export type ContentDataConformanceOptions = {
  /** Returns true when enum group is loaded and has items. */
  isEnumGroupResolved?: (groupId: string) => boolean;
  /** Column keys that originate from relation intents but remote target is unresolved. */
  unresolvedRelationColumnKeys?: ReadonlySet<string>;
};

const NUMERIC_TYPES = new Set([
  "number",
  "integer",
  "float",
  "decimal",
  "int",
  "bigint",
]);

const DATE_TYPES = new Set(["date"]);
const TIMESTAMP_TYPES = new Set([
  "timestamp with time zone",
  "timestamp",
  "timestamptz",
  "datetime",
]);

/** Accepts deterministic-seed style UUIDs (8-4-4-4-12 hex) without strict RFC variant checks. */
export const UUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function validateFieldType(
  key: string,
  raw: string,
  dataType: string,
): string | null {
  const type = dataType.trim().toLowerCase() || "text";
  if (!raw.trim()) return null;

  if (NUMERIC_TYPES.has(type)) {
    const n = Number(raw);
    if (Number.isNaN(n)) {
      return `field '${key}' expects type '${dataType}' but value is not numeric: '${raw}'`;
    }
    return null;
  }

  if (type === "boolean") {
    const lower = raw.trim().toLowerCase();
    if (
      !["true", "false", "1", "0"].includes(lower) &&
      !["true", "false"].includes(raw)
    ) {
      return `field '${key}' expects type 'boolean' but value is not boolean: '${raw}'`;
    }
    return null;
  }

  if (type === "uuid") {
    if (!UUID_PATTERN.test(raw.trim())) {
      return `field '${key}' expects type 'uuid' but value is not a valid UUID: '${raw}'`;
    }
    return null;
  }

  if (DATE_TYPES.has(type) || TIMESTAMP_TYPES.has(type)) {
    if (Number.isNaN(Date.parse(raw.trim()))) {
      return `field '${key}' expects type '${dataType}' but value is not a valid date: '${raw}'`;
    }
    return null;
  }

  return null;
}

function isEmptyValue(raw: string): boolean {
  return raw.trim() === "";
}

/**
 * Shared type-shape diagnostics for manual initialDataRows and import-staged rows.
 * Warnings are non-blocking — values may still be saved as topology intent.
 */
export function diagnoseContentDataRows(
  rows: ContentDataRowDraft[],
  columns: ConformanceColumnSpec[],
  options: ContentDataConformanceOptions = {},
): ContentDataConformanceWarning[] {
  const warnings: ContentDataConformanceWarning[] = [];
  const knownKeys = new Set(columns.map((c) => c.key));
  const columnByKey = new Map(columns.map((c) => [c.key, c]));

  for (let rowIndex = 0; rowIndex < rows.length; rowIndex++) {
    const row = rows[rowIndex]!;
    const lineageSource = row.lineage.source;

    for (const [key, raw] of Object.entries(row.values)) {
      if (!knownKeys.has(key)) {
        warnings.push({
          code: "UNKNOWN_COLUMN",
          kind: "structural_violation",
          rowIndex,
          columnKey: key,
          message: `unknown column '${key}' is not defined in the data shape`,
          severity: "warning",
          lineageSource,
        });
      }
    }

    for (const col of columns) {
      const raw = row.values[col.key] ?? "";
      if (!col.nullable && isEmptyValue(raw)) {
        warnings.push({
          code: "NULLABLE_VIOLATION",
          kind: "structural_violation",
          rowIndex,
          columnKey: col.key,
          message: `field '${col.key}' is required but empty`,
          severity: "warning",
          lineageSource,
        });
      }

      if (!isEmptyValue(raw)) {
        const typeError = validateFieldType(col.key, raw, col.dataType);
        if (typeError) {
          warnings.push({
            code: "TYPE_MISMATCH",
            kind: "structural_violation",
            rowIndex,
            columnKey: col.key,
            message: typeError,
            severity: "warning",
            lineageSource,
          });
        }
      }

      const groupId = col.enumGroupId?.trim();
      if (groupId && options.isEnumGroupResolved && !options.isEnumGroupResolved(groupId)) {
        warnings.push({
          code: "ENUM_GROUP_UNRESOLVED",
          kind: "valid_candidate",
          rowIndex,
          columnKey: col.key,
          message: `enum group '${groupId}' is not resolved for column '${col.key}'`,
          severity: "warning",
          lineageSource,
        });
      }

      if (options.unresolvedRelationColumnKeys?.has(col.key)) {
        warnings.push({
          code: "RELATION_FIELD_UNRESOLVED",
          kind: "valid_candidate",
          rowIndex,
          columnKey: col.key,
          message: `relation-derived column '${col.key}' has unresolved remote target`,
          severity: "warning",
          lineageSource,
        });
      }
    }

    // Duplicate unknown keys already handled; ensure empty known keys get nullable check above
    void columnByKey;
  }

  return warnings;
}

/** Map import preview/API record object to qualified column values. */
export function importRecordToRowValues(
  records: Record<string, unknown>,
  columnKeys: string[],
): Record<string, string> {
  const values: Record<string, string> = {};
  for (const key of columnKeys) {
    const v = records[key];
    if (v === null || v === undefined) {
      values[key] = "";
    } else if (typeof v === "string") {
      values[key] = v;
    } else {
      values[key] = String(v);
    }
  }
  for (const [k, v] of Object.entries(records)) {
    if (values[k] !== undefined) continue;
    values[k] = v === null || v === undefined ? "" : String(v);
  }
  return values;
}
