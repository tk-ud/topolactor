import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  DEMO_CONTEXT_SESSION_ID,
  DEMO_CONTEXT_TOKEN_ID_ACTIVE,
  OPERATION_PRESETS,
  buildDispatchContext,
  inferPresetId,
  presetById,
  presetOperationKey,
  presetsForGroups,
} from "../runtime/operationPresets.ts";
import { resolveOperationVector } from "../runtime/resolveOperationVector.ts";

Deno.test("operationPresets: demo recommendation uses seed UUIDs", () => {
  const preset = presetById("demo_hub_recommendation");
  assertExists(preset);
  assertEquals(preset.context?.ContextSessionId, DEMO_CONTEXT_SESSION_ID);
  assertEquals(preset.context?.ContextTokenIds, DEMO_CONTEXT_TOKEN_ID_ACTIVE);
});

Deno.test("operationPresets: preset attractor keys match resolveOperationVector", () => {
  for (const preset of OPERATION_PRESETS) {
    const fromPreset = presetOperationKey(preset);
    const fromOp = resolveOperationVector(preset.operation).attractorKey;
    assertEquals(fromPreset, fromOp);
  }
});

Deno.test("operationPresets: home groups include default and demo", () => {
  const groups = presetsForGroups(["default", "demo"]);
  assertEquals(groups.some((p) => p.id === "default_entity_search"), true);
  assertEquals(groups.some((p) => p.id === "demo_hub_overview"), true);
});

Deno.test("operationPresets: demo mode excludes default preset", () => {
  const demoOnly = presetsForGroups(["demo"]);
  assertEquals(demoOnly.every((p) => p.group === "demo"), true);
  assertEquals(demoOnly.some((p) => p.id === "default_entity_search"), false);
});

Deno.test("operationPresets: inferPresetId matches demo hub defaults", () => {
  assertEquals(
    inferPresetId({ target: "demo", layer: "hub", action: "overview" }, ["demo"]),
    "demo_hub_overview",
  );
});

Deno.test("buildDispatchContext: preset context without overrides", () => {
  const preset = presetById("demo_hub_recommendation")!;
  const ctx = buildDispatchContext(preset);
  assertEquals(ctx.ContextSessionId, DEMO_CONTEXT_SESSION_ID);
  assertEquals(ctx.ContextTokenIds, DEMO_CONTEXT_TOKEN_ID_ACTIVE);
});
