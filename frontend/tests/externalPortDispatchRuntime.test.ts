import { assert, assertEquals, assertExists, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { __testOnly as factoryTestOnly } from "../runtime/runtimeComponentFactory.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";
import type { Emission } from "../api/dispatch.ts";

Deno.test("dispatchExternalPort runtimeInteractions build external port event binding", () => {
  const emission: Emission = {
    layoutId: "layout-external-port",
    packageId: "00000000-0000-0000-0000-000000000001",
    data: {},
    layoutNodes: [{
      nodeId: "send_button",
      nodeKind: "catalog_component",
      componentId: "button-1",
      componentKey: "submit_button.primitive",
      componentKind: "action/button",
      orderIndex: 0,
      runtimeInteractions: [{
        trigger: "click",
        actionType: "dispatchExternalPort",
        portTargetRef: "external-port:response_port:00000000-0000-0000-0000-00000000abcd",
        payloadFrom: { subject: "literal:hello" },
        outputProp: "externalResult",
      }],
    }],
  };

  const specs = renderEmission(emission, defaultComponentRegistry);
  assertExists(specs[0].runtimeSpec);
  const parsed = factoryTestOnly.parseEventBinding(specs[0].runtimeSpec!.eventBinding.click);
  assertExists(parsed);
  assertEquals(parsed.externalPortDispatch?.portTargetRef, "external-port:response_port:00000000-0000-0000-0000-00000000abcd");
  assertEquals(parsed.externalPortDispatch?.payloadFrom, { subject: "literal:hello" });
});

// ─── projection_authority_runtime_interaction_identity: event-triggered path ─
// PR577 follow-up implementation. SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml
// lifecycle_policy.projection_authority_runtime_interaction_identity.

function externalPortEmission(
  nodeId: string,
  runtimeInteractionId?: string,
): Emission {
  return {
    layoutId: "layout-external-port",
    packageId: "00000000-0000-0000-0000-000000000001",
    data: {},
    layoutNodes: [{
      nodeId,
      nodeKind: "catalog_component",
      componentId: "button-1",
      componentKey: "submit_button.primitive",
      componentKind: "action/button",
      orderIndex: 0,
      runtimeInteractions: [{
        trigger: "click",
        actionType: "dispatchExternalPort",
        portTargetRef: "external-port:response_port:00000000-0000-0000-0000-00000000abcd",
        payloadFrom: { subject: "literal:hello" },
        outputProp: "externalResult",
        ...(runtimeInteractionId ? { runtimeInteractionId } : {}),
      }],
    }],
  };
}

function idempotencyKeyBaseOf(emission: Emission): string | undefined {
  const specs = renderEmission(emission, defaultComponentRegistry);
  const parsed = factoryTestOnly.parseEventBinding(
    specs[0].runtimeSpec!.eventBinding.click,
  );
  return parsed?.externalPortDispatch?.idempotencyKeyBase;
}

Deno.test("renderEmission binding builder: forwards runtimeInteractionId into idempotencyKeyBase — same id survives a nodeId rename", () => {
  const keyA = idempotencyKeyBaseOf(
    externalPortEmission("send_button", "aaaaaaaa-0000-0000-0000-000000000001"),
  );
  const keyB = idempotencyKeyBaseOf(
    externalPortEmission(
      "send_button_renamed",
      "aaaaaaaa-0000-0000-0000-000000000001",
    ),
  );
  assertExists(keyA);
  assertEquals(
    keyA,
    keyB,
    "the assigned runtimeInteractionId must survive a nodeId rename in the event-triggered binding builder, same as the lifecycle path",
  );
});

Deno.test("renderEmission binding builder: idempotencyKeyBase differs between id-less and id-assigned bindings for the same node (backward-compatible, not colliding)", () => {
  const withoutId = idempotencyKeyBaseOf(externalPortEmission("send_button"));
  const withId = idempotencyKeyBaseOf(
    externalPortEmission("send_button", "aaaaaaaa-0000-0000-0000-000000000001"),
  );
  assertExists(withoutId);
  assertExists(withId);
  assert(withoutId !== withId);
});

Deno.test("dispatchExternalPort invalid payloadFrom fails explicitly before enqueue", () => {
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (() => new Promise<Response>(() => {})) as typeof fetch;
  const result = factoryTestOnly.emitBoundEvent({
    componentId: "button-1",
    componentType: "action/button",
    props: { data: { label: "Send" } },
    eventBinding: {
      click: {
        eventType: "click",
        externalPortDispatch: {
          portTargetRef: "external-port:response_port:00000000-0000-0000-0000-00000000abcd",
          payloadFrom: { subject: "node:missing.value" },
        },
      },
    },
  }, "click", {});

  assertEquals(result.ok, false);
  if (!result.ok) assertStringIncludes(result.error, "PAYLOAD_FROM_NODE_NOT_FOUND");
  assertEquals(schedulerTestOnly.getCommandQueueLength(), 0);
});

Deno.test("dispatchExternalPort resolved payload enqueues backend command through api command lane", () => {
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (() => new Promise<Response>(() => {})) as typeof fetch;
  const result = factoryTestOnly.emitBoundEvent({
    componentId: "button-1",
    componentType: "action/button",
    props: { data: { label: "Send" } },
    payloadFromNodeValues: { subject_input: "Hello" },
    eventBinding: {
      click: {
        eventType: "click",
        externalPortDispatch: {
          portTargetRef: "external-port:response_port:00000000-0000-0000-0000-00000000abcd",
          payloadFrom: { subject: "node:subject_input.value", recordId: "event.record.id" },
          outputProp: "externalResult",
        },
      },
    },
  }, "click", { record: { id: "rec-1" } });

  assertEquals(result.ok, true);
  assertEquals(schedulerTestOnly.isCommandQueueRunning(), true);
  globalThis.fetch = originalFetch;
  schedulerTestOnly.resetCommandQueue();
});

// ─── round 23 audit: the undefined -> null wire-transport fix (payloadFromResolver.ts
// resolvePayloadFrom) is a single shared function used by ALL THREE dispatch lanes
// (admin_runtime, dispatchExternalPort, dispatchInstanceOperation) — proven for
// admin_runtime in frontend/tests/payloadFromResolver.test.ts; these two prove the SAME
// fix reaches the actual /api/dispatch request body for the other two lanes too, not just
// structurally implied by shared code. ─────────────────────────────────────────

Deno.test("dispatchExternalPort: a resolved-but-undefined payloadFrom field survives the real /api/dispatch wire body as an explicit null, not a silently dropped key", async () => {
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  const capturedBodies: Record<string, unknown>[] = [];
  globalThis.fetch = ((url: string, init?: RequestInit) => {
    if (String(url) === "/api/dispatch") {
      capturedBodies.push(JSON.parse(String(init?.body ?? "{}")));
    }
    return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
  }) as typeof fetch;

  const result = factoryTestOnly.emitBoundEvent({
    componentId: "button-1",
    componentType: "action/button",
    props: { data: { label: "Send" } },
    payloadFromNodeValues: { selected_row: { recordId: undefined, label: "Alpha" } },
    eventBinding: {
      click: {
        eventType: "click",
        externalPortDispatch: {
          portTargetRef: "external-port:response_port:00000000-0000-0000-0000-00000000abcd",
          payloadFrom: {
            recordId: "node:selected_row.value.recordId",
            label: "node:selected_row.value.label",
          },
        },
      },
    },
  }, "click", {});
  assertEquals(result.ok, true);

  for (let i = 0; i < 20 && capturedBodies.length === 0; i++) {
    await new Promise((r) => setTimeout(r, 5));
  }
  globalThis.fetch = originalFetch;
  schedulerTestOnly.resetCommandQueue();

  assertEquals(capturedBodies.length, 1);
  const dispatchPayload = capturedBodies[0].payload as Record<string, unknown>;
  const externalPayload = dispatchPayload.dispatch_payload as Record<string, unknown>;
  assertEquals(
    Object.prototype.hasOwnProperty.call(externalPayload, "recordId"),
    true,
    "recordId must remain a present key on the actual /api/dispatch wire body, not be dropped",
  );
  assertEquals(externalPayload.recordId, null);
  assertEquals(externalPayload.label, "Alpha");
});

Deno.test("dispatchInstanceOperation: a resolved-but-undefined payloadFrom field survives the real /api/dispatch wire body as an explicit null, not a silently dropped key", async () => {
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  const capturedBodies: Record<string, unknown>[] = [];
  globalThis.fetch = ((url: string, init?: RequestInit) => {
    if (String(url) === "/api/dispatch") {
      capturedBodies.push(JSON.parse(String(init?.body ?? "{}")));
    }
    return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
  }) as typeof fetch;

  const result = factoryTestOnly.emitBoundEvent({
    componentId: "button-1",
    componentType: "action/button",
    props: { data: { label: "Send" } },
    payloadFromNodeValues: { selected_row: { instanceId: undefined, label: "Beta" } },
    eventBinding: {
      click: {
        eventType: "click",
        instanceOperationDispatch: {
          instanceTargetRef: "instance-port:some_instance:00000000-0000-0000-0000-00000000abcd",
          payloadFrom: {
            instanceId: "node:selected_row.value.instanceId",
            label: "node:selected_row.value.label",
          },
        },
      },
    },
  }, "click", {});
  assertEquals(result.ok, true);

  for (let i = 0; i < 20 && capturedBodies.length === 0; i++) {
    await new Promise((r) => setTimeout(r, 5));
  }
  globalThis.fetch = originalFetch;
  schedulerTestOnly.resetCommandQueue();

  assertEquals(capturedBodies.length, 1);
  const dispatchPayload = capturedBodies[0].payload as Record<string, unknown>;
  const instancePayload = dispatchPayload.dispatch_payload as Record<string, unknown>;
  assertEquals(
    Object.prototype.hasOwnProperty.call(instancePayload, "instanceId"),
    true,
    "instanceId must remain a present key on the actual /api/dispatch wire body, not be dropped",
  );
  assertEquals(instancePayload.instanceId, null);
  assertEquals(instancePayload.label, "Beta");
});

// ─── external port generic lane: no provider-specific dispatch ────────────────

Deno.test("external port generic lane: enqueueExternalPortDispatchCommand uses target=external_port (not provider-specific)", async () => {
  const src = await Deno.readTextFile(new URL("../runtime/frontendScheduler.ts", import.meta.url));
  assert(src.includes('target: "external_port"'), "enqueueExternalPortDispatchCommand must use generic target=external_port");
  assert(src.includes('layer: "external_port"'), "enqueueExternalPortDispatchCommand must use generic layer=external_port");
  assert(src.includes('action: "dispatchExternalPort"'), "enqueueExternalPortDispatchCommand must use generic action=dispatchExternalPort");
});

Deno.test("external port generic lane: portTargetRef requires external-port: prefix (no provider-specific routing)", async () => {
  const src = await Deno.readTextFile(new URL("../runtime/frontendScheduler.ts", import.meta.url));
  assert(src.includes('startsWith("external-port:")'), "portTargetRef must start with external-port: prefix — provider identity lives in portTargetRef, not in separate dispatch lanes");
  assert(!src.includes('target: "file_storage"'), "must not route to provider-specific file_storage target");
  assert(!src.includes('target: "email_port"'), "must not route to provider-specific email_port target");
  assert(!src.includes('target: "stripe_port"'), "must not route to provider-specific stripe_port target");
});

Deno.test("external port generic lane: no provider-specific external port panel islands exist", async () => {
  const islandsDir = new URL("../islands/", import.meta.url);
  for await (const entry of Deno.readDir(islandsDir)) {
    const name = entry.name.toLowerCase();
    assert(
      !(name.includes("filestorage") && name.includes("port")),
      `provider-specific file storage port panel must not exist: ${entry.name}`,
    );
    assert(
      !(name.includes("email") && name.includes("port")),
      `provider-specific email port panel must not exist: ${entry.name}`,
    );
    assert(
      !(name.includes("stripe") && name.includes("port")),
      `provider-specific stripe port panel must not exist: ${entry.name}`,
    );
  }
});

// ─── external port boundary scope: runtime handler / credential execution is backend-only ──────

Deno.test("external port boundary scope: frontend has no credential executor — runtime handler is backend-only", async () => {
  // Frontend boundary is: DB-derived external-port: targetRef → generic dispatch lane → backend.
  // Frontend does NOT execute credentials, call external services, or own the runtime handler.
  // Runtime handler / credential execution is owned by the backend ExternalPortPolicyStepExecutor.
  const src = await Deno.readTextFile(new URL("../runtime/frontendScheduler.ts", import.meta.url));
  assert(!src.includes("CredentialExecut"), "frontend scheduler must not contain credential execution logic");
  assert(!src.includes("ExternalPortPolicy"), "frontend scheduler must not reference ExternalPortPolicyStepExecutor");
  assert(src.includes('target: "external_port"'), "frontend passes to generic external_port target — backend owns execution");
});

Deno.test("external port boundary scope: renderEmission forwards DB-derived portTargetRef unchanged to generic dispatch binding", async () => {
  // Frontend receives portTargetRef from DB-derived emission.layoutNodes[].runtimeInteractions[].portTargetRef.
  // It builds an externalPortDispatch binding and passes portTargetRef unchanged to enqueueExternalPortDispatchCommand.
  // Provider identity lives in the portTargetRef string — no provider-specific dispatch routing in frontend.
  const src = await Deno.readTextFile(new URL("../runtime/renderEmission.ts", import.meta.url));
  assert(src.includes("externalPortDispatch"), "renderEmission must build externalPortDispatch binding from runtimeInteractions");
  assert(src.includes("portTargetRef"), "renderEmission must forward DB-derived portTargetRef unchanged");
  assert(!src.includes("CredentialExecut"), "renderEmission must not contain credential execution");
});
