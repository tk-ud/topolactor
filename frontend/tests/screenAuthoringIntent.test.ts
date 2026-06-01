import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildDraftInputFromScreenIntent,
  dispatcherAxesToScreenOperationKind,
  screenOperationLabel,
  screenOperationToDispatcherAxes,
} from "../runtime/screenAuthoringIntent.ts";

Deno.test("screenOperationToDispatcherAxes: search matches seed entity Search route", () => {
  const axes = screenOperationToDispatcherAxes("search");
  assertEquals(axes.role, "admin");
  assertEquals(axes.target, "default");
  assertEquals(axes.layer, "entity");
  assertEquals(axes.action, "Search");
  assertEquals(axes.runtimeDestination, "topology_transform_runtime");
});

Deno.test("screenOperationToDispatcherAxes: aggregation_view uses aggregation layer", () => {
  const axes = screenOperationToDispatcherAxes("aggregation_view");
  assertEquals(axes.layer, "aggregation");
  assertEquals(axes.action, "Read");
});

Deno.test("dispatcherAxesToScreenOperationKind round-trips search", () => {
  const axes = screenOperationToDispatcherAxes("search");
  assertEquals(dispatcherAxesToScreenOperationKind(axes), "search");
});

Deno.test("buildDraftInputFromScreenIntent: no projection in default path", () => {
  const input = buildDraftInputFromScreenIntent({ operationKind: "list" });
  assertEquals(input.projectionDefinition, null);
  assertEquals(input.action, "Read");
});

Deno.test("screenOperationLabel returns Japanese labels", () => {
  assertEquals(screenOperationLabel("list"), "一覧");
  assertEquals(screenOperationLabel("aggregation_view"), "集計ビュー");
});
