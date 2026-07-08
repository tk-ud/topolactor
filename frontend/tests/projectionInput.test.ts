/**
 * projectionInput.test.ts — collection outer shape preservation proof.
 *
 * SSOT: backend/runtime/ScreenDataShapeQueryRuntime.cs emits
 * screen_data_shape_query_result with { rows, aggregationResults,
 * activeColumns, displayColumnMode }. projectionInputFromData must preserve
 * that outer shape unless an EXPLICIT single-row mapping is declared on the
 * manifest projection_definition (inputMapping: "single_row"). Implicit
 * rows[0] collapse is prohibited.
 */
import {
  assertEquals,
  assertExists,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { projectionInputFromData } from "../runtime/projectionInput.ts";
import { projectionFromEmission } from "../runtime/renderEmission.ts";
import { createProjectionRuntime } from "../runtime/projectionRuntime.ts";
import type { Emission } from "../api/dispatch.ts";
import type {
  ProjectionDefinition,
  UiProjection,
} from "../runtime/projectionConstructor.ts";

const queryResult = {
  kind: "screen_data_shape_query_result",
  manifestId: "00000000-0000-0000-0000-0000000000aa",
  rows: [
    { name: "Alice", amount: 10 },
    { name: "Bob", amount: 20 },
  ],
  aggregationResults: [{ key: "total", value: 30 }],
  activeColumns: ["name", "amount"],
  displayColumnMode: "selected",
};

Deno.test("projectionInputFromData: default preserves collection outer shape (rows / aggregationResults / activeColumns / displayColumnMode)", () => {
  const result = projectionInputFromData(queryResult);
  assertEquals(result.rows, queryResult.rows);
  assertEquals(result.aggregationResults, queryResult.aggregationResults);
  assertEquals(result.activeColumns, ["name", "amount"]);
  assertEquals(result.displayColumnMode, "selected");
  assertEquals(result.kind, "screen_data_shape_query_result");
});

Deno.test("projectionInputFromData: no implicit rows[0] collapse without explicit mapping", () => {
  const result = projectionInputFromData(queryResult);
  // The first row's fields must NOT be hoisted to the top level implicitly.
  assertEquals(result.name, undefined);
  assertEquals(result.amount, undefined);
});

Deno.test('projectionInputFromData: explicit inputMapping "single_row" collapses to rows[0]', () => {
  const result = projectionInputFromData(queryResult, "single_row");
  assertEquals(result, { name: "Alice", amount: 10 });
});

Deno.test('projectionInputFromData: explicit "collection" behaves like default (preserve)', () => {
  const result = projectionInputFromData(queryResult, "collection");
  assertEquals(result.rows, queryResult.rows);
  assertEquals(result.displayColumnMode, "selected");
});

Deno.test("projectionInputFromData: single_row with empty rows returns data unchanged (explicit, not fabricated)", () => {
  const empty = { ...queryResult, rows: [] };
  const result = projectionInputFromData(empty, "single_row");
  assertEquals(result, empty);
});

Deno.test("projectionInputFromData: non-query-result data passes through under both mappings", () => {
  const flat = { name: "Alice" };
  assertEquals(projectionInputFromData(flat), flat);
  assertEquals(projectionInputFromData(flat, "single_row"), flat);
  assertEquals(projectionInputFromData(null), {});
  assertEquals(projectionInputFromData(undefined), {});
});

// ── projection path survival: emission → projectionFromEmission ─────────────

Deno.test("projectionFromEmission: ui_projection keeps rows / activeColumns / displayColumnMode reachable (collection default)", () => {
  const emission: Emission = {
    componentIds: [],
    data: queryResult,
  };
  const definition: ProjectionDefinition = {
    constructorKey: "collection-shape",
    packageIds: [],
    outputKind: "ui_projection",
  };
  const result = projectionFromEmission(emission, definition);
  assertExists(result.projection);
  assertEquals(result.projection!.kind, "ui_projection");
  if (result.projection!.kind === "ui_projection") {
    const raw = result.projection!.raw;
    assertEquals(raw.rows, queryResult.rows);
    assertEquals(raw.activeColumns, ["name", "amount"]);
    assertEquals(raw.displayColumnMode, "selected");
    assertEquals(raw.aggregationResults, queryResult.aggregationResults);
  }
});

Deno.test("projectionFromEmission: explicit inputMapping single_row on the definition collapses for form_inputs", () => {
  const emission: Emission = {
    componentIds: [],
    data: queryResult,
  };
  const definition: ProjectionDefinition = {
    constructorKey: "single-row-form",
    packageIds: [],
    outputKind: "form_inputs",
    inputMapping: "single_row",
    fieldDefs: [{ key: "name", label: "Name", kind: "text" }],
  };
  const result = projectionFromEmission(emission, definition);
  assertExists(result.projection);
  if (result.projection!.kind === "form_inputs") {
    assertEquals(result.projection!.fields[0].value, "Alice");
  }
});

Deno.test("projectionFromEmission: form_inputs WITHOUT explicit single_row does not see first-row fields", () => {
  const emission: Emission = {
    componentIds: [],
    data: queryResult,
  };
  const definition: ProjectionDefinition = {
    constructorKey: "no-implicit-collapse",
    packageIds: [],
    outputKind: "form_inputs",
    fieldDefs: [{ key: "name", label: "Name", kind: "text" }],
  };
  const result = projectionFromEmission(emission, definition);
  assertExists(result.projection);
  if (result.projection!.kind === "form_inputs") {
    // Collection outer shape has no top-level "name" — implicit collapse must not fill it.
    assertEquals(result.projection!.fields[0].value, null);
  }
});

// ── projection path survival: SSE lane (projectionRuntime) ───────────────────

Deno.test("projectionRuntime: SSE projection event preserves collection outer shape under default mapping", () => {
  const runtime = createProjectionRuntime();
  runtime.setProjectionDefinition({
    constructorKey: "sse-collection",
    packageIds: [],
    outputKind: "ui_projection",
  });
  let projected: UiProjection | null = null;
  runtime.onProjectionUpdate((projection) => {
    projected = projection;
  });
  runtime.handleProjectionEvent(JSON.stringify({
    manifest_id: queryResult.manifestId,
    data: queryResult,
  }));
  assertExists(projected);
  const p = projected as unknown as UiProjection;
  assertEquals(p.kind, "ui_projection");
  if (p.kind === "ui_projection") {
    assertEquals(p.raw.rows, queryResult.rows);
    assertEquals(p.raw.activeColumns, ["name", "amount"]);
    assertEquals(p.raw.displayColumnMode, "selected");
  }
});

Deno.test("projectionRuntime: SSE projection event honors explicit single_row definition mapping", () => {
  const runtime = createProjectionRuntime();
  runtime.setProjectionDefinition({
    constructorKey: "sse-single-row",
    packageIds: [],
    outputKind: "form_inputs",
    inputMapping: "single_row",
    fieldDefs: [{ key: "name", label: "Name", kind: "text" }],
  });
  let projected: UiProjection | null = null;
  runtime.onProjectionUpdate((projection) => {
    projected = projection;
  });
  runtime.handleProjectionEvent(JSON.stringify({
    manifest_id: queryResult.manifestId,
    data: queryResult,
  }));
  assertExists(projected);
  const p = projected as unknown as UiProjection;
  if (p.kind === "form_inputs") {
    assertEquals(p.fields[0].value, "Alice");
  }
});
