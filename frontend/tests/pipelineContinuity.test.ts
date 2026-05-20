import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { createSseDispatcher } from "../runtime/sseDispatcher.ts";
import { queueClientCommand } from "../runtime/frontendScheduler.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import type { Emission } from "../api/dispatch.ts";

// ─── SSE projection lane — dispatcher skeleton ────────────────────────────────

Deno.test("sseDispatcher: registered handler is called on matching eventType", () => {
  const dispatcher = createSseDispatcher();
  let received: string | null = null;

  dispatcher.register("projection", (data) => {
    received = data;
  });
  dispatcher.route("projection", "test-payload");

  assertEquals(received, "test-payload");
});

Deno.test("sseDispatcher: unregistered eventType is ignored without error", () => {
  const dispatcher = createSseDispatcher();

  // Must not throw for unregistered type
  dispatcher.route("unknown-event", "data");
});

Deno.test("sseDispatcher: multiple event types can be registered independently", () => {
  const dispatcher = createSseDispatcher();
  const received: Record<string, string> = {};

  dispatcher.register("projection", (data) => { received["projection"] = data; });
  dispatcher.register("ping", (data) => { received["ping"] = data; });

  dispatcher.route("projection", "proj-data");
  dispatcher.route("ping", "ping-data");

  assertEquals(received["projection"], "proj-data");
  assertEquals(received["ping"], "ping-data");
});

// ─── Frontend scheduler skeleton ─────────────────────────────────────────────

Deno.test("frontendScheduler: queueClientCommand shape matches DispatchRequest contract", async () => {
  // Verifies that queueClientCommand constructs a valid DispatchRequest.
  // No real backend — fetch will fail; we verify the error is explicit (not silent fallback).
  // See docs/design/pipeline-continuity-ssot.yaml api_command_lane.frontend.scheduler.
  //
  // Note: dispatchOperation fetches "/api/dispatch" (relative URL). In Deno,
  // new Request(relativeUrl, init) throws — so we capture input/init directly
  // and parse init.body instead of constructing a Request object.
  let capturedInput: string | URL | Request | null = null;
  let capturedInit: RequestInit | undefined;

  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (input: string | URL | Request, init?: RequestInit) => {
    capturedInput = input;
    capturedInit = init;
    throw new Error("TEST_NO_NETWORK");
  };

  try {
    const result = await queueClientCommand({
      operationType: "Search",
      target: "default",
      layer: "entity",
      action: "Search",
    });

    assertEquals(result.success, false);
    assertExists(result.errors);

    assertExists(capturedInput);
    assertExists(capturedInit);
    const body = JSON.parse(capturedInit!.body as string);
    assertEquals(body.target, "default");
    assertEquals(body.layer, "entity");
    assertEquals(body.action, "Search");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

// ─── SSE projection lane — pipeline identity ─────────────────────────────────

Deno.test("pipeline identity (SSE lane): component_ids from emission project to non-error specs", () => {
  // Verifies the projection terminal node of the SSE lane:
  // emission.componentIds → renderEmission → ComponentSpec[] with non-error componentType.
  // See docs/design/pipeline-continuity-ssot.yaml sse_projection_lane.required_identity.
  const emission: Emission = {
    structureMapId: "00000000-0000-0000-0000-000000000004",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    componentIds: ["00000000-0000-0000-0000-000000000003"],
  };

  const specs = renderEmission(emission, defaultComponentRegistry);

  assertEquals(specs.length, 1);
  assertEquals(specs[0].componentType !== "error", true);
});
