import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { __testOnly, queueAdminClientCommand, queueClientCommand } from "../runtime/frontendScheduler.ts";
import type { DispatchRequest } from "../api/dispatch.ts";

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

    // dispatchOperation wraps network errors as { success: false, errors } rather than re-throwing,
    // so p1 resolves (not rejects) with success:false carrying the error message.
    const result1 = await p1;
    assertEquals(result1.success, false, "p1 must resolve as failed (network error wrapped by dispatchOperation)");
    assertEquals(result1.errors?.[0]?.message, "DISPATCH_NETWORK_FAIL", "error message must be preserved");

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

// ─── triggerKind=client enforcement — client_command_lane SSOT ────────────────
// Completion condition: queueClientCommand and queueAdminClientCommand must both
// inject triggerKind="client" per pipeline-continuity-ssot.yaml (api_command_lane).

Deno.test("scheduler: queueClientCommand injects triggerKind='client' in dispatch body", async () => {
  __testOnly.resetCommandQueue();
  let capturedBody: Record<string, unknown> = {};
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (_input: unknown, init?: RequestInit) => {
    capturedBody = JSON.parse(init!.body as string);
    return new Response(JSON.stringify({ success: true, errors: null }), { status: 200 });
  };
  try {
    await queueClientCommand({ operationType: "Search", target: "t", layer: "entity", action: "search" });
    assertEquals(capturedBody.triggerKind, "client", "queueClientCommand must inject triggerKind='client'");
    assertEquals("role" in capturedBody, false, "role must NOT be set by frontend");
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("scheduler: queueAdminClientCommand routes through FIFO queue with triggerKind='client'", async () => {
  __testOnly.resetCommandQueue();
  let capturedBody: Record<string, unknown> = {};
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (_input: unknown, init?: RequestInit) => {
    capturedBody = JSON.parse(init!.body as string);
    return new Response(JSON.stringify({ success: true, errors: null }), { status: 200 });
  };
  try {
    const result = await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "seed_runtime",
      action: "load",
    });
    assertEquals(result.success, true);
    assertEquals(capturedBody.triggerKind, "client", "queueAdminClientCommand must inject triggerKind='client'");
    assertEquals(capturedBody.operationType, "admin");
    assertEquals(capturedBody.layer, "seed_runtime");
    assertEquals(capturedBody.action, "load");
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("scheduler: queueAdminClientCommand must NOT include role in dispatch body", async () => {
  __testOnly.resetCommandQueue();
  let capturedBody: Record<string, unknown> = {};
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (_input: unknown, init?: RequestInit) => {
    capturedBody = JSON.parse(init!.body as string);
    return new Response(JSON.stringify({ success: true, errors: null }), { status: 200 });
  };
  try {
    await queueAdminClientCommand({ operationType: "admin", target: "admin", layer: "seed_runtime", action: "load" });
    assertEquals("role" in capturedBody, false, "role must NOT be in frontend dispatch body; JWT claim is authoritative");
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("scheduler: queueAdminClientCommand strips role from dispatch body even when passed via runtime cast", async () => {
  __testOnly.resetCommandQueue();
  let capturedBody: Record<string, unknown> = {};
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (_input: unknown, init?: RequestInit) => {
    capturedBody = JSON.parse(init!.body as string);
    return new Response(JSON.stringify({ success: true, errors: null }), { status: 200 });
  };
  try {
    // Bypass type safety to simulate a caller accidentally injecting role at runtime
    await queueAdminClientCommand(
      { operationType: "admin", target: "admin", layer: "seed_runtime", action: "load", role: "admin" } as unknown as Omit<DispatchRequest, "triggerKind" | "role">,
    );
    assertEquals("role" in capturedBody, false, "role must be stripped from dispatch body even when injected via runtime cast");
    assertEquals(capturedBody.triggerKind, "client");
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("scheduler: queueAdminClientCommand type regression — role rejected at compile time", () => {
  // @ts-expect-error — role must not be assignable to the req parameter; this line must remain a TS error
  const _bad: Parameters<typeof queueAdminClientCommand>[0] = { operationType: "admin", target: "admin", layer: "x", action: "y", role: "admin" };
  void _bad;
});

// ─── Source guard: direct fetch("/api/dispatch") bypass detection ─────────────
// Allowed: frontend/api/dispatch.ts, frontend/routes/api/dispatch.ts
// Forbidden: all other .ts/.tsx files in frontend/

Deno.test("source guard: direct fetch('/api/dispatch') forbidden outside allowed files", async () => {
  const repoRoot = new URL("../../", import.meta.url);
  const frontendDir = new URL("../../frontend/", import.meta.url);

  const allowedRelPaths = [
    "frontend/api/dispatch.ts",
    "frontend/routes/api/dispatch.ts",
  ];

  const violations: string[] = [];

  const pathFromRepoRoot = (fileUrl: URL): string => {
    const rootHref = repoRoot.href.endsWith("/") ? repoRoot.href : `${repoRoot.href}/`;
    if (!fileUrl.href.startsWith(rootHref)) {
      throw new Error(`path not under repo root: ${fileUrl.href}`);
    }
    return fileUrl.href.slice(rootHref.length);
  };

  async function scanDir(dir: URL): Promise<void> {
    for await (const entry of Deno.readDir(dir)) {
      const entryUrl = new URL(entry.name, dir);
      if (entry.isDirectory) {
        if (entry.name === "_fresh" || entry.name.startsWith(".") || entry.name === "node_modules") continue;
        await scanDir(new URL(`${entry.name}/`, dir));
      } else if (entry.isFile && (entry.name.endsWith(".ts") || entry.name.endsWith(".tsx"))) {
        if (entry.name.endsWith(".test.ts") || entry.name.endsWith(".test.tsx")) continue;
        const relPath = pathFromRepoRoot(entryUrl);
        if (allowedRelPaths.includes(relPath)) continue;
        const content = await Deno.readTextFile(entryUrl);
        if (content.includes('fetch("/api/dispatch"') || content.includes("fetch('/api/dispatch'")) {
          violations.push(relPath);
        }
      }
    }
  }

  await scanDir(frontendDir);

  assertEquals(
    violations,
    [],
    `direct fetch("/api/dispatch") found outside allowed files:\n  ${violations.join("\n  ")}\n` +
      `All client commands must go through queueClientCommand or queueAdminClientCommand → frontend/api/dispatch.ts`,
  );
});
