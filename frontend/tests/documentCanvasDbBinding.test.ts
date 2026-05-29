/**
 * documentCanvasDbBinding.test.ts
 *
 * Proves: DB/JSONB manifest runtime → DocumentCanvas fields[key,label,x,y,value] loop
 * at the frontend projection constructor boundary.
 *
 * This test closes the data_binding_to_document_canvas completion condition:
 *   - jsonb_label_value_manifest_runtime resolves fields from JSONB source data
 *   - resolved fields are validated by projectionConstructor
 *   - projectionConstructor produces canvas-ready component_projection
 *
 * The JSONB manifest runtime is modeled at the frontend boundary as a pure
 * field resolution function. The DB/JSONB source is represented as a
 * plain JSON object (the frontend receives resolved props from the backend
 * projection pipeline; it does not directly query JSONB).
 *
 * Does not prove:
 *   - Backend DB persistence (backend scope).
 *   - PDF generation (PdfExportSnapshotRuntime scope).
 *   - Live SSE backend push (integration scope).
 */

import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  constructProjection,
  type ProjectionDefinition,
} from "../runtime/projectionConstructor.ts";

// ---------------------------------------------------------------------------
// Helper: simulate resolved JSONB manifest fields (backend-resolved props)
// ---------------------------------------------------------------------------

type CanvasField = {
  key: string;
  label?: string;
  x?: number;
  y?: number;
  value?: string;
};

/**
 * Simulates the jsonb_label_value_manifest_runtime field resolution step.
 * In production, this is done by the backend JsonbManifestRuntime before
 * the frontend receives the projection props.
 */
function resolveJsonbManifestFields(
  sourceJsonb: Record<string, unknown>,
  fieldManifest: Array<{
    key: string;
    jsonPath: string;
    label?: string;
    x?: number;
    y?: number;
    displayPolicy?: "as_is" | "uppercase" | "lowercase";
  }>,
): CanvasField[] {
  return fieldManifest.map((entry) => {
    // Simple dot-notation path resolution (mirrors backend JsonbManifestRuntime)
    const pathSegments = entry.jsonPath.replace(/^\$\./, "").split(".");
    let value: unknown = sourceJsonb;
    for (const seg of pathSegments) {
      if (typeof value === "object" && value !== null && seg in value) {
        value = (value as Record<string, unknown>)[seg];
      } else {
        value = undefined;
        break;
      }
    }
    let strValue: string | undefined;
    if (value !== null && value !== undefined) {
      strValue = String(value);
      if (entry.displayPolicy === "uppercase") strValue = strValue.toUpperCase();
      if (entry.displayPolicy === "lowercase") strValue = strValue.toLowerCase();
    }
    return { key: entry.key, label: entry.label, x: entry.x, y: entry.y, value: strValue };
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

Deno.test("documentCanvasDbBinding: jsonb manifest → canvas fields → projectionConstructor succeeds", () => {
  // Simulate backend JSONB source row
  const sourceJsonb = {
    company_name: "株式会社テスト",
    invoice_number: "INV-2025-001",
    total_amount: 150000,
    due_date: "2025-08-31",
  };

  // Field manifest (user-defined binding policy)
  const fieldManifest = [
    { key: "company_name",   jsonPath: "$.company_name",   label: "会社名",   x: 50,  y: 30  },
    { key: "invoice_number", jsonPath: "$.invoice_number", label: "請求番号", x: 50,  y: 60  },
    { key: "total_amount",   jsonPath: "$.total_amount",   label: "合計金額", x: 300, y: 200 },
    { key: "due_date",       jsonPath: "$.due_date",       label: "支払期日", x: 300, y: 230 },
  ];

  // Step 1: Resolve JSONB manifest → canvas fields
  const resolvedFields = resolveJsonbManifestFields(sourceJsonb, fieldManifest);
  assertEquals(resolvedFields.length, 4);
  assertEquals(resolvedFields[0].value, "株式会社テスト");
  assertEquals(resolvedFields[1].value, "INV-2025-001");
  assertEquals(resolvedFields[2].value, "150000");
  assertEquals(resolvedFields[3].value, "2025-08-31");

  // Step 2: Feed resolved fields into projectionConstructor
  const def: ProjectionDefinition = {
    constructorKey: "invoice_canvas",
    packageIds: ["pkg-canvas"],
    outputKind: "component_projection",
    componentId: "cmp-invoice-canvas",
    componentDefinition: {
      componentId: "cmp-invoice-canvas",
      component_kind: "document_canvas/document_canvas_template_editor",
    },
  };

  const result = constructProjection(
    {
      backgroundImageUrl: "https://example.com/invoice_bg.png",
      fields: resolvedFields,
    },
    def,
  );

  // Step 3: Verify component_projection with correct canvas field shape
  assertExists(result.projection);
  assertEquals(result.error, undefined);
  if (!result.projection || result.projection.kind !== "component_projection") {
    throw new Error("expected component_projection");
  }

  const fields = result.projection.props.fields as CanvasField[];
  assertEquals(fields.length, 4);

  // Verify all fields have the required shape: key, label, x, y, value
  const fieldMap = Object.fromEntries(fields.map((f) => [f.key, f]));

  assertEquals(fieldMap["company_name"].label,   "会社名");
  assertEquals(fieldMap["company_name"].x,       50);
  assertEquals(fieldMap["company_name"].y,       30);
  assertEquals(fieldMap["company_name"].value,   "株式会社テスト");

  assertEquals(fieldMap["invoice_number"].label, "請求番号");
  assertEquals(fieldMap["invoice_number"].value, "INV-2025-001");

  assertEquals(fieldMap["total_amount"].label,   "合計金額");
  assertEquals(fieldMap["total_amount"].value,   "150000");

  assertEquals(fieldMap["due_date"].label,       "支払期日");
  assertEquals(fieldMap["due_date"].value,       "2025-08-31");
});

Deno.test("documentCanvasDbBinding: empty fields from manifest resolution produce valid empty-fields canvas", () => {
  const resolvedFields: CanvasField[] = [];

  const def: ProjectionDefinition = {
    constructorKey: "empty_canvas",
    packageIds: ["pkg"],
    outputKind: "component_projection",
    componentId: "cmp-canvas-empty",
    componentDefinition: {
      componentId: "cmp-canvas-empty",
      component_kind: "document_canvas/document_canvas_template_editor",
    },
  };

  const result = constructProjection({ fields: resolvedFields }, def);
  assertExists(result.projection);
  assertEquals(result.error, undefined);
});

Deno.test("documentCanvasDbBinding: null value from missing jsonb path resolves to undefined field value", () => {
  const sourceJsonb = { name: "test" };
  const fieldManifest = [
    { key: "missing_field", jsonPath: "$.not_present", label: "Missing", x: 0, y: 0 },
  ];

  const resolved = resolveJsonbManifestFields(sourceJsonb, fieldManifest);
  assertEquals(resolved[0].key, "missing_field");
  assertEquals(resolved[0].value, undefined);

  // Undefined value is valid in canvas fields (not required)
  const def: ProjectionDefinition = {
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "component_projection",
    componentId: "cmp",
    componentDefinition: {
      componentId: "cmp",
      component_kind: "document_canvas/document_canvas_template_editor",
    },
  };

  const result = constructProjection({ fields: resolved }, def);
  assertExists(result.projection);
  assertEquals(result.error, undefined);
});

Deno.test("documentCanvasDbBinding: display policy uppercase applied before canvas binding", () => {
  const sourceJsonb = { ref_code: "ref-abc-001" };
  const fieldManifest = [
    { key: "ref_code", jsonPath: "$.ref_code", label: "参照コード", x: 0, y: 0, displayPolicy: "uppercase" as const },
  ];

  const resolved = resolveJsonbManifestFields(sourceJsonb, fieldManifest);
  assertEquals(resolved[0].value, "REF-ABC-001");

  const def: ProjectionDefinition = {
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "component_projection",
    componentId: "cmp",
    componentDefinition: {
      componentId: "cmp",
      component_kind: "document_canvas/document_canvas_template_editor",
    },
  };

  const result = constructProjection({ fields: resolved }, def);
  assertExists(result.projection);
  if (!result.projection || result.projection.kind !== "component_projection") {
    throw new Error("expected component_projection");
  }
  const fields = result.projection.props.fields as CanvasField[];
  assertEquals(fields[0].value, "REF-ABC-001");
});

Deno.test("documentCanvasDbBinding: backgroundImageUrl is preserved through projection constructor", () => {
  const bgUrl = "https://storage.example.com/template_bg.png";
  const def: ProjectionDefinition = {
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "component_projection",
    componentId: "cmp-bg",
    componentDefinition: {
      componentId: "cmp-bg",
      component_kind: "document_canvas/document_canvas_template_editor",
    },
  };

  const result = constructProjection(
    {
      backgroundImageUrl: bgUrl,
      fields: [{ key: "f1", label: "F1", x: 0, y: 0, value: "v" }],
    },
    def,
  );

  assertExists(result.projection);
  if (!result.projection || result.projection.kind !== "component_projection") {
    throw new Error("expected component_projection");
  }
  assertEquals(result.projection.props.backgroundImageUrl, bgUrl);
});

Deno.test("documentCanvasDbBinding: invalid field shape from manifest returns explicit projectionConstructor error", () => {
  // If the manifest runtime produces a field with non-string value (type mismatch),
  // the projectionConstructor catches it explicitly.
  const def: ProjectionDefinition = {
    constructorKey: "k",
    packageIds: ["p"],
    outputKind: "component_projection",
    componentId: "cmp-err",
    componentDefinition: {
      componentId: "cmp-err",
      component_kind: "document_canvas/document_canvas_template_editor",
    },
  };

  const result = constructProjection(
    { fields: [{ key: "f1", value: 42 }] },  // value is number, not string
    def,
  );
  assertEquals(
    result.error,
    "PROJECTION_CONSTRUCTOR_INVALID_DOCUMENT_CANVAS_FIELD_VALUE: fields[0].value must be string",
  );
});
