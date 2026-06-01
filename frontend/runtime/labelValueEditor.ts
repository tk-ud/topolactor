/**
 * Local draft helpers for JSONB label/value metadata authored in UiBuilderAdmin.
 *
 * The frontend owns draft editing and deterministic payload projection only.
 * Persistence and topology decisions remain behind the existing admin dispatch route.
 */

export const LABEL_VALUE_DISPLAY_POLICIES = [
  "as_is",
  "format_currency",
  "format_date",
  "format_number",
  "uppercase",
  "lowercase",
] as const;

export type LabelValueDisplayPolicy =
  typeof LABEL_VALUE_DISPLAY_POLICIES[number];

export type LabelValueEditorRow = {
  key: string;
  label: string;
  value: string;
  jsonPath: string;
  x: number;
  y: number;
  displayPolicy: LabelValueDisplayPolicy;
};

export type LabelValueFieldManifestEntry = {
  key: string;
  jsonPath: string;
  label: string;
  x: number;
  y: number;
  displayPolicy: LabelValueDisplayPolicy;
};

export type LabelValueMetadataPayload = {
  sourceJsonb: Record<string, string>;
  fieldManifest: LabelValueFieldManifestEntry[];
};

export type DocumentCanvasProjectionField = {
  key: string;
  label: string;
  x: number;
  y: number;
  value: string;
};

export function createEmptyLabelValueEditorRow(): LabelValueEditorRow {
  return {
    key: "",
    label: "",
    value: "",
    jsonPath: "",
    x: 0,
    y: 0,
    displayPolicy: "as_is",
  };
}

/**
 * Normalizes editor rows into the metadata JSONB shape persisted by the existing
 * ui_component_bucket:create intent. Empty and duplicate keys fail explicitly.
 */
export function buildLabelValueMetadataPayload(
  rows: LabelValueEditorRow[],
): LabelValueMetadataPayload {
  const sourceJsonb: Record<string, string> = {};
  const fieldManifest: LabelValueFieldManifestEntry[] = [];
  const seenKeys = new Set<string>();

  rows.forEach((row, index) => {
    const key = row.key.trim();
    if (!key) {
      throw new Error(`JSONB label/value 行 ${index + 1}: key は必須です。`);
    }
    if (seenKeys.has(key)) {
      throw new Error(
        `JSONB label/value 行 ${index + 1}: key "${key}" が重複しています。`,
      );
    }
    seenKeys.add(key);

    sourceJsonb[key] = row.value;
    fieldManifest.push({
      key,
      jsonPath: row.jsonPath.trim() || `$.${key}`,
      label: row.label,
      x: row.x,
      y: row.y,
      displayPolicy: row.displayPolicy,
    });
  });

  return { sourceJsonb, fieldManifest };
}

/** Preserves the DocumentCanvasTemplateEditor fields[{key,label,x,y,value}] props shape. */
export function projectLabelValueRowsToDocumentCanvasFields(
  rows: LabelValueEditorRow[],
): DocumentCanvasProjectionField[] {
  const payload = buildLabelValueMetadataPayload(rows);
  return payload.fieldManifest.map((field) => ({
    key: field.key,
    label: field.label,
    x: field.x,
    y: field.y,
    value: payload.sourceJsonb[field.key],
  }));
}

/** Keeps legacy empty metadata for registrations that do not author JSONB fields. */
export function serializeLabelValueMetadataJson(
  rows: LabelValueEditorRow[],
): string {
  return rows.length === 0
    ? "{}"
    : JSON.stringify(buildLabelValueMetadataPayload(rows));
}
