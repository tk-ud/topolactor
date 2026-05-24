import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { emitComponentOperationEvent } from "../runtime/frontendScheduler.ts";

Deno.test("emitComponentOperationEvent: returns explicit error when componentId missing", () => {
  const result = emitComponentOperationEvent({
    componentId: "",
    eventType: "click",
    actorOrSource: "ui",
    payload: { label: "save" },
  });
  assertEquals(result.ok, false);
});

Deno.test("emitComponentOperationEvent: accepts normalized event", () => {
  const result = emitComponentOperationEvent({
    componentId: "cmp-1",
    packageId: "pkg-1",
    layoutId: "layout-1",
    eventType: "toggle",
    actorOrSource: "ui",
    payload: { checked: true, token: "hidden" },
  });
  assertEquals(result.ok, true);
});
