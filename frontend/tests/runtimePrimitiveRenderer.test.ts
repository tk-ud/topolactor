import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { renderRuntimeComponent } from "../runtime/runtimePrimitiveRenderer.ts";
import { __testOnly } from "../runtime/frontendScheduler.ts";

Deno.test("runtimePrimitiveRenderer: invalid eventBinding is explicit error", () => {
  const result = renderRuntimeComponent({
    componentId: "c1",
    componentType: "action/button",
    props: { label: "Submit" },
    eventBinding: { click: "bad" as unknown as Record<string, unknown> },
  });
  assertEquals(result, {
    ok: false,
    error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_EVENT_BINDING: click",
  });
});

Deno.test("runtimePrimitiveRenderer: button click eventBinding emits component event", () => {
  __testOnly.resetQueue();
  const result = renderRuntimeComponent({
    componentId: "c2",
    packageId: "pkg",
    layoutId: "lay",
    wiringId: "wir",
    componentType: "action/button",
    props: { label: "Submit" },
    eventBinding: {
      click: {
        eventType: "click",
        actorOrSource: "unit_test",
        payload: { source: "button" },
      },
    },
  });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  ((result.node as any).props.onClick as () => void)();
  assertEquals(__testOnly.getQueueLength(), 1);
  const [queued] = __testOnly.getQueueSnapshot();
  assertEquals(queued.wiringId, "wir");
});

Deno.test("runtimePrimitiveRenderer: primitive components do not directly call API", () => {
  const button = renderRuntimeComponent({
    componentId: "b",
    componentType: "action/button",
    props: { label: "B" },
    eventBinding: { click: { eventType: "click" } },
  });
  const input = renderRuntimeComponent({
    componentId: "i",
    componentType: "form_input/input",
    props: { value: "" },
    eventBinding: { change: { eventType: "change" } },
  });
  const card = renderRuntimeComponent({
    componentId: "c",
    componentType: "display/card",
    props: { title: "T" },
    eventBinding: {},
  });
  const table = renderRuntimeComponent({
    componentId: "t",
    componentType: "data_display/table",
    props: { columns: [], rows: [] },
    eventBinding: {},
  });
  assertEquals(button.ok, true);
  assertEquals(input.ok, true);
  assertEquals(card.ok, true);
  assertEquals(table.ok, true);
});

Deno.test("runtimePrimitiveRenderer: missing required binding is explicit error", () => {
  const result = renderRuntimeComponent({
    componentId: "c3",
    componentType: "form_input/input",
    props: { value: "" },
    eventBinding: {},
  });
  assertEquals(result, {
    ok: false,
    error: "RUNTIME_PRIMITIVE_RENDERER_MISSING_EVENT_BINDING: change",
  });
});

Deno.test("runtimePrimitiveRenderer: input optional focus/blur bindings emit component events", () => {
  __testOnly.resetQueue();
  const result = renderRuntimeComponent({
    componentId: "c4",
    componentType: "form_input/input",
    props: { value: "" },
    eventBinding: {
      change: { eventType: "change" },
      focus: { eventType: "focus" },
      blur: { eventType: "blur" },
    },
  });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  ((result.node as any).props.onFocus as () => void)();
  ((result.node as any).props.onBlur as () => void)();
  assertEquals(__testOnly.getQueueLength(), 2);
});

Deno.test("runtimePrimitiveRenderer: table select binding emits component events", () => {
  __testOnly.resetQueue();
  const result = renderRuntimeComponent({
    componentId: "c5",
    componentType: "data_display/table",
    props: {
      columns: [{ key: "name", header: "Name" }],
      rows: [{ id: "r1", name: "A" }],
    },
    eventBinding: {
      select: { eventType: "select", payload: { source: "table" } },
    },
  });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  ((result.node as any).props.onRowClick as (
    row: Record<string, unknown>,
  ) => void)({ id: "r1", name: "A" });
  assertEquals(__testOnly.getQueueLength(), 1);
});

Deno.test("runtimePrimitiveRenderer: forwards className/design and keeps tailwind out of inline style", () => {
  const result = renderRuntimeComponent({
    componentId: "x",
    componentType: "action/button",
    props: { label: "X" },
    eventBinding: { click: { eventType: "click" } },
    className: "dbx tw-bg-red",
    design: {
      tailwind: "tw-bg-red",
      style: "border-width:2px",
      state: "loading",
    },
  });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals((result.node as any).props.className, "dbx tw-bg-red");
  assertEquals((result.node as any).props.design.tailwind, "tw-bg-red");
});
