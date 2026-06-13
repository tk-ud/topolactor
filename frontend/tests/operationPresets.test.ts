import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  OPERATION_PRESETS,
  buildDispatchContext,
  inferPresetId,
  presetById,
  presetOperationKey,
  presetsForGroups,
} from "../runtime/operationPresets.ts";
import { resolveOperationVector } from "../runtime/resolveOperationVector.ts";

Deno.test("operationPresets: preset attractor keys match resolveOperationVector", () => {
  for (const preset of OPERATION_PRESETS) {
    const fromPreset = presetOperationKey(preset);
    const fromOp = resolveOperationVector(preset.operation).attractorKey;
    assertEquals(fromPreset, fromOp);
  }
});

Deno.test("operationPresets: default group includes default_entity_search", () => {
  const groups = presetsForGroups(["default"]);
  assertEquals(groups.some((p) => p.id === "default_entity_search"), true);
});

Deno.test("operationPresets: inferPresetId returns first default preset when no operation", () => {
  const id = inferPresetId(undefined, ["default"]);
  assertExists(id);
  assertEquals(id, "default_entity_search");
});

Deno.test("buildDispatchContext: empty context when preset has no context", () => {
  const preset = presetById("default_entity_search")!;
  const ctx = buildDispatchContext(preset);
  assertEquals(Object.keys(ctx).length, 0);
});
