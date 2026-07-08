import { assertEquals, assertThrows } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildProjectionDefinitionPayload,
  emptyManifestProjectionDraft,
  extractProjectionDefinitionFromTopology,
  formatProjectionSummary,
  parseProjectionDefinitionToDraft,
} from "../runtime/manifestProjectionEditor.ts";

Deno.test("extractProjectionDefinitionFromTopology reads projection_definition entry", () => {
  const topology = JSON.stringify([
    { type: "dispatcher_mapping", role: "admin", target: "admin", layer: "x", action: "y" },
    {
      type: "projection_constructor_mapping",
      projection_definition: {
        constructorKey: "demo_form",
        outputKind: "form_inputs",
        packageIds: ["00000000-0000-0000-0000-000000000020"],
      },
    },
  ]);
  const def = extractProjectionDefinitionFromTopology(topology);
  assertEquals(def?.constructorKey, "demo_form");
  assertEquals(formatProjectionSummary(def), "demo_form → form_inputs (1 packageId(s))");
});

Deno.test("parseProjectionDefinitionToDraft round-trips structured fields", () => {
  const draft = parseProjectionDefinitionToDraft({
    constructorKey: "canvas",
    outputKind: "component_projection",
    packageIds: ["a", "b"],
    componentId: "comp-1",
    fieldDefs: [{ key: "name", label: "Name", kind: "text" }],
  });
  assertEquals(draft.enabled, true);
  assertEquals(draft.constructorKey, "canvas");
  assertEquals(draft.packageIds, "a, b");
  assertEquals(draft.componentId, "comp-1");
  assertEquals(JSON.parse(draft.fieldDefsJson).length, 1);
});

// ── inputMapping admin editor round-trip ─────────────────────────────────────
// PR577 follow-up: inputMapping must survive read -> draft -> payload without
// being dropped, and must not silently default away an explicit declaration.

Deno.test('parseProjectionDefinitionToDraft: reads explicit inputMapping "single_row"', () => {
  const draft = parseProjectionDefinitionToDraft({
    constructorKey: "seed-projection-lane",
    outputKind: "form_inputs",
    packageIds: ["a"],
    inputMapping: "single_row",
  });
  assertEquals(draft.inputMapping, "single_row");
});

Deno.test('parseProjectionDefinitionToDraft: reads explicit inputMapping "collection"', () => {
  const draft = parseProjectionDefinitionToDraft({
    constructorKey: "table-view",
    outputKind: "ui_projection",
    packageIds: ["a"],
    inputMapping: "collection",
  });
  assertEquals(draft.inputMapping, "collection");
});

Deno.test("parseProjectionDefinitionToDraft: absent inputMapping reads as undeclared (\"\")", () => {
  const draft = parseProjectionDefinitionToDraft({
    constructorKey: "table-view",
    outputKind: "ui_projection",
    packageIds: ["a"],
  });
  assertEquals(draft.inputMapping, "");
});

Deno.test("parseProjectionDefinitionToDraft: invalid inputMapping value fails close (explicit error, not silent normalize)", () => {
  assertThrows(
    () =>
      parseProjectionDefinitionToDraft({
        constructorKey: "table-view",
        outputKind: "ui_projection",
        packageIds: ["a"],
        inputMapping: "first_row",
      }),
    Error,
    "inputMapping",
  );
});

Deno.test('buildProjectionDefinitionPayload: explicit "single_row" survives the save round-trip (does not disappear)', () => {
  const draft = emptyManifestProjectionDraft();
  draft.enabled = true;
  draft.constructorKey = "seed-projection-lane";
  draft.outputKind = "form_inputs";
  draft.packageIds = "pkg-a";
  draft.inputMapping = "single_row";
  const payload = buildProjectionDefinitionPayload(draft);
  assertEquals(payload?.inputMapping, "single_row");
});

Deno.test('buildProjectionDefinitionPayload: explicit "collection" survives the save round-trip', () => {
  const draft = emptyManifestProjectionDraft();
  draft.enabled = true;
  draft.constructorKey = "table-view";
  draft.outputKind = "ui_projection";
  draft.packageIds = "pkg-a";
  draft.inputMapping = "collection";
  const payload = buildProjectionDefinitionPayload(draft);
  assertEquals(payload?.inputMapping, "collection");
});

Deno.test('buildProjectionDefinitionPayload: undeclared ("") inputMapping omits the key (no over-specification)', () => {
  const draft = emptyManifestProjectionDraft();
  draft.enabled = true;
  draft.constructorKey = "table-view";
  draft.outputKind = "ui_projection";
  draft.packageIds = "pkg-a";
  const payload = buildProjectionDefinitionPayload(draft);
  assertEquals("inputMapping" in (payload ?? {}), false);
});

Deno.test("inputMapping full round-trip: extract -> parse -> build preserves single_row through a manifest topology literal", () => {
  const topology = JSON.stringify([
    {
      type: "projection_constructor_mapping",
      projection_definition: {
        constructorKey: "seed-projection-lane",
        packageIds: ["00000000-0000-0000-0000-000000000001"],
        outputKind: "form_inputs",
        inputMapping: "single_row",
        fieldDefs: [{ key: "seedLabel", label: "Seed label", kind: "text", required: true }],
      },
    },
  ]);
  const extracted = extractProjectionDefinitionFromTopology(topology);
  const draft = parseProjectionDefinitionToDraft(extracted);
  assertEquals(draft.inputMapping, "single_row");
  const rebuilt = buildProjectionDefinitionPayload(draft);
  assertEquals(rebuilt?.inputMapping, "single_row");
});

Deno.test("buildProjectionDefinitionPayload returns null when disabled", () => {
  assertEquals(buildProjectionDefinitionPayload(emptyManifestProjectionDraft()), null);
});

Deno.test("buildProjectionDefinitionPayload requires constructorKey when enabled", () => {
  const draft = emptyManifestProjectionDraft();
  draft.enabled = true;
  assertThrows(() => buildProjectionDefinitionPayload(draft));
});

Deno.test("buildProjectionDefinitionPayload builds object for backend", () => {
  const draft = emptyManifestProjectionDraft();
  draft.enabled = true;
  draft.constructorKey = "demo_form";
  draft.outputKind = "form_inputs";
  draft.packageIds = "pkg-a, pkg-b";
  const payload = buildProjectionDefinitionPayload(draft);
  assertEquals(payload?.constructorKey, "demo_form");
  assertEquals(payload?.packageIds, ["pkg-a", "pkg-b"]);
});
