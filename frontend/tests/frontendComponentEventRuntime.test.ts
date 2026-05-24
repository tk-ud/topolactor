import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { __testOnly, emitComponentOperationEvent, flushComponentEvents, startComponentEventRuntime, stopComponentEventRuntime } from "../runtime/frontendScheduler.ts";

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
    wiringId: "wiring-1",
    eventType: "toggle",
    actorOrSource: "ui",
    payload: { checked: true, token: "hidden" },
  });
  assertEquals(result.ok, true);
});

Deno.test("component event runtime: start hook is callable and flush success drains queue", async () => {
  __testOnly.resetQueue();
  startComponentEventRuntime();

  emitComponentOperationEvent({
    componentId: "cmp-flush-1",
    eventType: "click",
    actorOrSource: "test",
    payload: { value: "ok" },
  });

  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => new Response(JSON.stringify({ success: true, accepted: 1 }), { status: 202 });
  try {
    await flushComponentEvents();
    assertEquals(__testOnly.getQueueLength(), 0);
  } finally {
    globalThis.fetch = originalFetch;
    stopComponentEventRuntime();
  }
});

Deno.test("component event runtime: flush failure retries and keeps queue", async () => {
  __testOnly.resetQueue();
  emitComponentOperationEvent({
    componentId: "cmp-retry-1",
    eventType: "submit",
    actorOrSource: "test",
    payload: { value: "retry" },
  });

  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => {
    throw new Error("NETWORK_DOWN");
  };
  try {
    await flushComponentEvents();
    assertEquals(__testOnly.getQueueLength(), 1);
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetQueue();
  }
});

Deno.test("component event runtime: bounded queue caps event count", () => {
  __testOnly.resetQueue();
  for (let i = 0; i < 400; i++) {
    emitComponentOperationEvent({ componentId: `cmp-${i}`, eventType: "click", actorOrSource: "test", payload: { i } });
  }
  assertEquals(__testOnly.getQueueLength(), 300);
  __testOnly.resetQueue();
});


Deno.test("emitComponentOperationEvent: wiringId is preserved in queue payload", () => {
  __testOnly.resetQueue();
  emitComponentOperationEvent({
    componentId: "cmp-wire",
    packageId: "pkg-wire",
    layoutId: "layout-wire",
    wiringId: "wiring-wire",
    eventType: "click",
    actorOrSource: "ui",
  });
  const [queued] = __testOnly.getQueueSnapshot();
  assertEquals(queued.wiringId, "wiring-wire");
});
