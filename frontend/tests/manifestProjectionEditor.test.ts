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
