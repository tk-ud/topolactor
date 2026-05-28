import { assertEquals, assertRejects } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { __testOnly, queueClientCommand } from "../runtime/frontendScheduler.ts";

// ─── frontend.runtime_scheduler — Gap-13 closure tests ───────────────────────
// Verifies completion condition: frontend_scheduler_owns_queueing_ordering_and_async_execution_policy
// SSOT: docs/design/runtime-orchestration-ssot.yaml (frontend_scope.client_command_order)
//       docs/design/pipeline-continuity-ssot.yaml (api_command_lane.frontend.scheduler)

Deno.test("scheduler: commands execute in FIFO order (ordering)", async () => {
  __testOnly.resetCommandQueue();

  const executionOrder: number[] = [];
  const originalFetch = globalThis.fetch;

  globalThis.fetch = async (_input: unknown, init?: RequestInit) => {
    const body = JSON.parse(init!.body as string) as { action?: string };
    executionOrder.push(parseInt(body.action ?? "0", 10));
    return new Response(JSON.stringify({ success: true, errors: null }), { status: 200 });
  };

  try {
    const p1 = queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "1" });
    const p2 = queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "2" });
    const p3 = queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "3" });

    await Promise.all([p1, p2, p3]);

    assertEquals(executionOrder, [1, 2, 3], "commands must execute in enqueue order");
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("scheduler: serial execution — second command does not start until first resolves", async () => {
  __testOnly.resetCommandQueue();

  let concurrentCount = 0;
  let maxConcurrent = 0;
  const originalFetch = globalThis.fetch;

  globalThis.fetch = async () => {
    concurrentCount++;
    maxConcurrent = Math.max(maxConcurrent, concurrentCount);
    await new Promise((r) => setTimeout(r, 5));
    concurrentCount--;
    return new Response(JSON.stringify({ success: true, errors: null }), { status: 200 });
  };

  try {
    await Promise.all([
      queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "Search" }),
      queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "Search" }),
      queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "Search" }),
    ]);
    assertEquals(maxConcurrent, 1, "at most one command must execute at a time");
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("scheduler: explicit failure propagates to the failed command's awaiter only", async () => {
  __testOnly.resetCommandQueue();

  let callCount = 0;
  const originalFetch = globalThis.fetch;

  globalThis.fetch = async () => {
    callCount++;
    if (callCount === 1) throw new Error("DISPATCH_NETWORK_FAIL");
    return new Response(JSON.stringify({ success: true, errors: null }), { status: 200 });
  };

  try {
    const p1 = queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "Search" });
    const p2 = queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "Search" });

    await assertRejects(() => p1, Error, "DISPATCH_NETWORK_FAIL");

    const result2 = await p2;
    assertEquals(result2.success, true, "second command must succeed independently after first fails");
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("scheduler: queue drains to empty after all commands complete", async () => {
  __testOnly.resetCommandQueue();

  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () =>
    new Response(JSON.stringify({ success: true, errors: null }), { status: 200 });

  try {
    await Promise.all([
      queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "Search" }),
      queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "Search" }),
    ]);
    assertEquals(__testOnly.getCommandQueueLength(), 0, "command queue must be empty after all commands complete");
    assertEquals(__testOnly.isCommandQueueRunning(), false, "drain must not be running after queue empties");
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("scheduler: getCommandQueueLength reflects pending queue depth before drain starts", () => {
  __testOnly.resetCommandQueue();
  assertEquals(__testOnly.getCommandQueueLength(), 0);
  assertEquals(__testOnly.isCommandQueueRunning(), false);
});
