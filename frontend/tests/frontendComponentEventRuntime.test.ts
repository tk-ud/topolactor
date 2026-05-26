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

// ─── surface-expansion: emit-only wiring for additional component surfaces ───
// Proves the emit-only boundary extends to components beyond OperationPanel.
// Components emit normalized events only; no topology/API dispatch branching.

Deno.test("surface-expansion: ProjectionView surface emits projection-related event (change)", () => {
  __testOnly.resetQueue();
  const result = emitComponentOperationEvent({
    componentId: "projection-view-001",
    packageId: "pkg-projection",
    layoutId: "layout-proj",
    eventType: "change",
    actorOrSource: "ProjectionView",
    payload: { field: "username", value: "alice" },
  });
  assertEquals(result.ok, true);
  assertEquals(__testOnly.getQueueLength(), 1);
  const [queued] = __testOnly.getQueueSnapshot();
  assertEquals(queued.componentId, "projection-view-001");
  assertEquals(queued.eventType, "change");
  assertEquals(queued.actorOrSource, "ProjectionView");
  __testOnly.resetQueue();
});

Deno.test("surface-expansion: EntityTableProjection surface emits select event", () => {
  __testOnly.resetQueue();
  const result = emitComponentOperationEvent({
    componentId: "entity-table-001",
    packageId: "pkg-entity",
    layoutId: "layout-entity",
    eventType: "select",
    actorOrSource: "EntityTableProjection",
    payload: { rowId: "row-42", action: "select" },
  });
  assertEquals(result.ok, true);
  assertEquals(__testOnly.getQueueLength(), 1);
  const [queued] = __testOnly.getQueueSnapshot();
  assertEquals(queued.componentId, "entity-table-001");
  assertEquals(queued.eventType, "select");
  __testOnly.resetQueue();
});

Deno.test("surface-expansion: RecommendationPanel surface emits toggle event (emit-only, no mutation)", () => {
  __testOnly.resetQueue();
  const result = emitComponentOperationEvent({
    componentId: "rec-panel-001",
    packageId: "pkg-rec",
    eventType: "toggle",
    actorOrSource: "RecommendationPanel",
    payload: { candidateId: "cand-001", toggled: true },
  });
  assertEquals(result.ok, true);
  const [queued] = __testOnly.getQueueSnapshot();
  assertEquals(queued.eventType, "toggle");
  assertEquals(queued.actorOrSource, "RecommendationPanel");
  __testOnly.resetQueue();
});

Deno.test("surface-expansion: multiple component surfaces emit independently without interference", () => {
  __testOnly.resetQueue();
  emitComponentOperationEvent({ componentId: "surface-a", eventType: "click", actorOrSource: "ComponentA", payload: {} });
  emitComponentOperationEvent({ componentId: "surface-b", eventType: "focus", actorOrSource: "ComponentB", payload: {} });
  emitComponentOperationEvent({ componentId: "surface-c", eventType: "blur", actorOrSource: "ComponentC", payload: {} });
  assertEquals(__testOnly.getQueueLength(), 3);
  const snapshot = __testOnly.getQueueSnapshot();
  assertEquals(snapshot[0].componentId, "surface-a");
  assertEquals(snapshot[1].componentId, "surface-b");
  assertEquals(snapshot[2].componentId, "surface-c");
  __testOnly.resetQueue();
});

// ─── hardening: retry boundary, monitoring hook, failure path ────────────────

Deno.test("hardening: flush failure increments retryCount and keeps event in queue", async () => {
  __testOnly.resetQueue();
  emitComponentOperationEvent({ componentId: "hard-001", eventType: "submit", actorOrSource: "test", payload: {} });

  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => { throw new Error("NETWORK_FAIL"); };
  try {
    await flushComponentEvents();
    const snapshot = __testOnly.getQueueSnapshot();
    assertEquals(snapshot.length, 1, "event must remain in queue after flush failure");
    assertEquals(snapshot[0].retryCount, 1, "retryCount must be incremented on flush failure");
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetQueue();
  }
});

Deno.test("hardening: event is dropped from queue after maxRetry exhausted", async () => {
  __testOnly.resetQueue();
  emitComponentOperationEvent({ componentId: "hard-retry", eventType: "click", actorOrSource: "test", payload: {} });

  // Exhaust all retries (maxRetry=3 means 3 failures, then dropped on the 4th attempt).
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => { throw new Error("NETWORK_FAIL"); };
  try {
    await flushComponentEvents(); // retryCount -> 1
    await flushComponentEvents(); // retryCount -> 2
    await flushComponentEvents(); // retryCount -> 3
    assertEquals(__testOnly.getQueueLength(), 1, "event must remain at retryCount=3");
    await flushComponentEvents(); // retryCount 3 -> filtered out (> maxRetry=3)
    assertEquals(__testOnly.getQueueLength(), 0, "event must be dropped after maxRetry is exhausted");
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetQueue();
  }
});
