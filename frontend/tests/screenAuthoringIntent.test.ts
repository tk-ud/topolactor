import { assertEquals, assertNotEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildDraftInputFromScreenIntent,
  dispatcherAxesToScreenOperationKind,
  screenOperationLabel,
  screenOperationToDispatcherAxes,
} from "../runtime/screenAuthoringIntent.ts";

const MANIFEST_A = "11111111-1111-1111-1111-111111111101";

Deno.test("screenOperationToDispatcherAxes: search uses manifest-scoped target and layer", () => {
  const axes = screenOperationToDispatcherAxes("search", { manifestKey: "orders" });
  assertEquals(axes.role, "admin");
  assertEquals(axes.target, "orders");
  assertEquals(axes.layer, "screen_entity");
  assertEquals(axes.action, "Search");
  assertEquals(axes.runtimeDestination, "topology_transform_runtime");
});

Deno.test("screenOperationToDispatcherAxes: list and detail differ by layer for same manifestKey", () => {
  const list = screenOperationToDispatcherAxes("list", { manifestKey: "orders" });
  const detail = screenOperationToDispatcherAxes("detail", { manifestKey: "orders" });
  assertEquals(list.target, detail.target);
  assertNotEquals(list.layer, detail.layer);
});

Deno.test("screenOperationToDispatcherAxes: two list screens use distinct targets", () => {
  const a = screenOperationToDispatcherAxes("list", { manifestKey: "screen_a" });
  const b = screenOperationToDispatcherAxes("list", { manifestKey: "screen_b" });
  assertNotEquals(a.target, b.target);
  assertEquals(a.layer, "screen_list");
  assertEquals(b.layer, "screen_list");
});

Deno.test("dispatcherAxesToScreenOperationKind round-trips screen_list search", () => {
  const axes = screenOperationToDispatcherAxes("search", { manifestKey: "x" });
  assertEquals(dispatcherAxesToScreenOperationKind(axes), "search");
});

Deno.test("buildDraftInputFromScreenIntent: includes screenOperationKind", () => {
  const input = buildDraftInputFromScreenIntent({
    operationKind: "list",
    manifestId: MANIFEST_A,
  });
  assertEquals(input.projectionDefinition, null);
  assertEquals(input.screenOperationKind, "list");
  assertEquals(input.layer, "screen_list");
});

Deno.test("screenOperationLabel returns Japanese labels", () => {
  assertEquals(screenOperationLabel("list"), "一覧");
  assertEquals(screenOperationLabel("aggregation_view"), "集計ビュー");
});

Deno.test("screenOperationToDispatcherAxes: logicalDelete maps to screen_entity / logicalDelete action", () => {
  const axes = screenOperationToDispatcherAxes("logicalDelete", { manifestKey: "orders" });
  assertEquals(axes.layer, "screen_entity");
  assertEquals(axes.action, "logicalDelete");
  assertEquals(axes.role, "admin");
  assertEquals(axes.target, "orders");
  assertEquals(axes.runtimeDestination, "topology_transform_runtime");
});

Deno.test("screenOperationToDispatcherAxes: delete maps to screen_entity / logicalDelete action", () => {
  const axes = screenOperationToDispatcherAxes("delete", { manifestKey: "orders" });
  assertEquals(axes.layer, "screen_entity");
  assertEquals(axes.action, "logicalDelete");
});

Deno.test("dispatcherAxesToScreenOperationKind: logicalDelete action round-trips", () => {
  const axes = screenOperationToDispatcherAxes("logicalDelete", { manifestKey: "x" });
  assertEquals(dispatcherAxesToScreenOperationKind(axes), "logicalDelete");
});

Deno.test("screenOperationLabel: logicalDelete and delete have Japanese labels", () => {
  assertEquals(screenOperationLabel("logicalDelete"), "論理削除");
  assertEquals(screenOperationLabel("delete"), "削除");
});
