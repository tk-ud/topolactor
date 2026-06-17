import { assertEquals, assertExists, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
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
