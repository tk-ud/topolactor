import {
  assertEquals,
  assertThrows,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  constructProjection,
  type ProjectionDefinition,
} from "../runtime/projectionConstructor.ts";
import {
  buildLabelValueMetadataPayload,
  createEmptyLabelValueEditorRow,
  type LabelValueEditorRow,
  projectLabelValueRowsToDocumentCanvasFields,
  serializeLabelValueMetadataJson,
} from "../runtime/labelValueEditor.ts";

const rows: LabelValueEditorRow[] = [
  {
    key: " company_name ",
    label: "会社名",
    value: "株式会社テスト",
    jsonPath: "",
    x: 50,
    y: 30,
    displayPolicy: "as_is",
  },
  {
    key: "invoice_number",
    label: "請求番号",
    value: "INV-2026-001",
    jsonPath: "$.invoice.number",
    x: 50,
    y: 60,
    displayPolicy: "uppercase",
  },
];

Deno.test("labelValueEditor: rows normalize into reproducible sourceJsonb and fieldManifest metadata", () => {
  assertEquals(buildLabelValueMetadataPayload(rows), {
    sourceJsonb: {
      company_name: "株式会社テスト",
      invoice_number: "INV-2026-001",
    },
    fieldManifest: [
      {
        key: "company_name",
        jsonPath: "$.company_name",
        label: "会社名",
        x: 50,
        y: 30,
        displayPolicy: "as_is",
      },
      {
        key: "invoice_number",
        jsonPath: "$.invoice.number",
        label: "請求番号",
        x: 50,
        y: 60,
        displayPolicy: "uppercase",
      },
    ],
  });
});

Deno.test("labelValueEditor: no rows preserve legacy empty metadata JSON", () => {
  assertEquals(serializeLabelValueMetadataJson([]), "{}");
});

Deno.test("labelValueEditor: empty and duplicate keys fail explicitly", () => {
  assertThrows(
    () =>
      buildLabelValueMetadataPayload([{
        ...createEmptyLabelValueEditorRow(),
        label: "空",
      }]),
    Error,
    "key は必須です",
  );
  assertThrows(
    () =>
      buildLabelValueMetadataPayload([
        { ...createEmptyLabelValueEditorRow(), key: "invoice_number" },
        { ...createEmptyLabelValueEditorRow(), key: " invoice_number " },
      ]),
    Error,
    'key "invoice_number" が重複しています',
  );
});

Deno.test("labelValueEditor: authored rows preserve DocumentCanvas fields[key,label,x,y,value] projection props", () => {
  const fields = projectLabelValueRowsToDocumentCanvasFields(rows);
  assertEquals(fields, [
    {
      key: "company_name",
      label: "会社名",
      x: 50,
      y: 30,
      value: "株式会社テスト",
    },
    {
      key: "invoice_number",
      label: "請求番号",
      x: 50,
      y: 60,
      value: "INV-2026-001",
    },
  ]);

  const definition: ProjectionDefinition = {
    constructorKey: "invoice_canvas",
    packageIds: ["pkg-canvas"],
    outputKind: "component_projection",
    componentId: "cmp-invoice-canvas",
    componentDefinition: {
      componentId: "cmp-invoice-canvas",
      component_kind: "document_canvas/document_canvas_template_editor",
    },
  };
  assertEquals(constructProjection({ fields }, definition).error, undefined);
});

Deno.test("labelValueEditor: separate editor projections do not leak sourceJsonb entries", () => {
  const first = buildLabelValueMetadataPayload([
    { ...createEmptyLabelValueEditorRow(), key: "first_key", value: "first" },
  ]);
  const second = buildLabelValueMetadataPayload([
    { ...createEmptyLabelValueEditorRow(), key: "second_key", value: "second" },
  ]);

  assertEquals(first.sourceJsonb, { first_key: "first" });
  assertEquals(second.sourceJsonb, { second_key: "second" });
});
