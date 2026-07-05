import type { AdminImportPreviewResult, AdminImportRecordPreview } from "../api/adminApi.ts";
import {
  type ConformanceColumnSpec,
  diagnoseContentDataRows,
  importRecordToRowValues,
} from "./contentDataConformance.ts";
import type { ContentDataRowDraft } from "./manifestScreenDesign.ts";

/** Frontend-only Step 3 preview id — must not be passed to admin_csv_json_import apply/reload. */
export const LOCAL_IMPORT_PREVIEW_SNAPSHOT_PREFIX = "local-preview-";

export function isLocalImportPreviewSnapshotId(snapshotId: string): boolean {
  return snapshotId.startsWith(LOCAL_IMPORT_PREVIEW_SNAPSHOT_PREFIX);
}

export type LocalInitialDataImportPreviewError = { ok: false; error: string };

function splitCsvLine(line: string): string[] {
  const out: string[] = [];
  let cur = "";
  let inQuotes = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i]!;
    if (ch === '"') {
      if (inQuotes && line[i + 1] === '"') {
        cur += '"';
        i++;
      } else {
        inQuotes = !inQuotes;
      }
      continue;
    }
    if (ch === "," && !inQuotes) {
      out.push(cur);
      cur = "";
      continue;
    }
    cur += ch;
  }
  out.push(cur);
  return out.map((s) => s.trim());
}

export function parseCsvContent(content: string): {
  ok: true;
  rows: Record<string, unknown>[];
} | { ok: false; error: string } {
  const normalized = content.replace(/\r\n/g, "\n").replace(/\r/g, "\n");
  const lines = normalized.split("\n");
  if (lines.length > 0 && lines[lines.length - 1] === "" && normalized.endsWith("\n")) {
    lines.pop();
  }
  if (lines.length === 0 || lines.every((l) => l.trim().length === 0)) {
    return { ok: false, error: "CSV が空です。" };
  }
  const headers = splitCsvLine(lines[0]!);
  if (headers.length === 0) {
    return { ok: false, error: "CSV のヘッダー行がありません。" };
  }
  if (lines.length === 1) {
    return { ok: false, error: "CSV にデータ行がありません。" };
  }
  const rows: Record<string, unknown>[] = [];
  for (let i = 1; i < lines.length; i++) {
    const values = splitCsvLine(lines[i]!);
    if (values.length !== headers.length) {
      return {
        ok: false,
        error: `${i + 1} 行目: 列数がヘッダー（${headers.length}列）と一致しません。`,
      };
    }
    const row: Record<string, unknown> = {};
    for (let j = 0; j < headers.length; j++) {
      row[headers[j]!] = values[j] ?? "";
    }
    rows.push(row);
  }
  return { ok: true, rows };
}

export function parseJsonContent(content: string): {
  ok: true;
  rows: Record<string, unknown>[];
} | { ok: false; error: string } {
  let parsed: unknown;
  try {
    parsed = JSON.parse(content);
  } catch {
    return { ok: false, error: "JSON の形式が正しくありません。" };
  }
  if (Array.isArray(parsed)) {
    if (parsed.length === 0) {
      return { ok: false, error: "JSON 配列が空です。" };
    }
    const rows = parsed.filter((r): r is Record<string, unknown> =>
      typeof r === "object" && r !== null && !Array.isArray(r)
    );
    if (rows.length !== parsed.length) {
      return { ok: false, error: "JSON 配列の各要素はオブジェクトである必要があります。" };
    }
    return { ok: true, rows };
  }
  if (typeof parsed === "object" && parsed !== null && !Array.isArray(parsed)) {
    return { ok: true, rows: [parsed as Record<string, unknown>] };
  }
  return { ok: false, error: "JSON はオブジェクトの配列、または1件のオブジェクトにしてください。" };
}

function rowValidationErrors(
  values: Record<string, string>,
  columns: ConformanceColumnSpec[],
): string[] {
  const draft: ContentDataRowDraft = {
    values,
    lineage: { source: "import_preview" },
  };
  return diagnoseContentDataRows([draft], columns)
    .filter((w) => w.kind === "structural_violation")
    .map((w) => w.message);
}

/**
 * Step 3 embedded import: validate file rows against Step 2 logical column specs
 * (not topology.schema_registry entries shown to authors).
 */
export function previewInitialDataImportLocal(input: {
  sourceType: "csv" | "json";
  fileName: string;
  manifestId: string;
  content: string;
  columns: ConformanceColumnSpec[];
}): AdminImportPreviewResult | LocalInitialDataImportPreviewError {
  if (input.columns.length === 0) {
    return { ok: false, error: "Step 2 で項目を定義してから取り込んでください。" };
  }
  const parsed = input.sourceType === "csv"
    ? parseCsvContent(input.content)
    : parseJsonContent(input.content);
  if (!parsed.ok) return parsed;

  const columnKeys = input.columns.map((c) => c.key);
  const records: AdminImportRecordPreview[] = parsed.rows.map((rec, rowIndex) => {
    const values = importRecordToRowValues(rec, columnKeys);
    const validationErrors = rowValidationErrors(values, input.columns);
    return {
      rowIndex,
      records: { ...rec, ...values },
      status: validationErrors.length === 0 ? "valid" : "invalid",
      validationErrors,
    };
  });

  const validCount = records.filter((r) => r.status === "valid").length;
  return {
    ok: true,
    snapshotId: `${LOCAL_IMPORT_PREVIEW_SNAPSHOT_PREFIX}${crypto.randomUUID()}`,
    sourceType: input.sourceType,
    manifestId: input.manifestId,
    schemaId: "step2_logical_columns",
    validCount,
    invalidCount: records.length - validCount,
    records,
  };
}
