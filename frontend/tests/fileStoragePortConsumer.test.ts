import { assertEquals, assertExists, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { __testOnly as factoryTestOnly } from "../runtime/runtimeComponentFactory.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";
import type { Emission } from "../api/dispatch.ts";

const FILE_STORAGE_ACCESS_PORT_ID = "00000000-0000-0000-0000-000000000f01";
const FILE_STORAGE_RESPONSE_PORT_ID = "00000000-0000-0000-0000-000000000f02";

Deno.test("fileStoragePortConsumer: dispatchExternalPort builds access_port event binding", () => {
  const emission: Emission = {
    layoutId: "layout-file-storage",
    packageId: "00000000-0000-0000-0000-000000000001",
    data: {},
    layoutNodes: [{
      nodeId: "export_button",
      nodeKind: "catalog_component",
      componentId: "button-1",
      componentKey: "submit_button.primitive",
      componentKind: "action/button",
      orderIndex: 0,
      runtimeInteractions: [{
        trigger: "click",
        actionType: "dispatchExternalPort",
        portTargetRef: `external-port:access_port:${FILE_STORAGE_ACCESS_PORT_ID}`,
        payloadFrom: {
          export_job_id: "literal:job-001",
          requested_by: "literal:user1",
          period: "literal:2026-06",
          export_format: "literal:pdf",
        },
        outputProp: "exportResult",
      }],
    }],
  };

  const specs = renderEmission(emission, defaultComponentRegistry);
  assertExists(specs[0].runtimeSpec);
  const parsed = factoryTestOnly.parseEventBinding(specs[0].runtimeSpec!.eventBinding.click);
  assertExists(parsed);
  assertEquals(
    parsed.externalPortDispatch?.portTargetRef,
    `external-port:access_port:${FILE_STORAGE_ACCESS_PORT_ID}`,
  );
});

Deno.test("fileStoragePortConsumer: dispatchExternalPort builds response_port event binding", () => {
  const emission: Emission = {
    layoutId: "layout-file-storage-response",
    packageId: "00000000-0000-0000-0000-000000000001",
    data: {},
    layoutNodes: [{
      nodeId: "download_button",
      nodeKind: "catalog_component",
      componentId: "button-2",
      componentKey: "submit_button.primitive",
      componentKind: "action/button",
      orderIndex: 0,
      runtimeInteractions: [{
        trigger: "click",
        actionType: "dispatchExternalPort",
        portTargetRef: `external-port:response_port:${FILE_STORAGE_RESPONSE_PORT_ID}`,
        payloadFrom: { export_job_id: "literal:job-002" },
        outputProp: "downloadResult",
      }],
    }],
  };

  const specs = renderEmission(emission, defaultComponentRegistry);
  assertExists(specs[0].runtimeSpec);
  const parsed = factoryTestOnly.parseEventBinding(specs[0].runtimeSpec!.eventBinding.click);
  assertExists(parsed);
  assertEquals(
    parsed.externalPortDispatch?.portTargetRef,
    `external-port:response_port:${FILE_STORAGE_RESPONSE_PORT_ID}`,
  );
});

Deno.test("fileStoragePortConsumer: resolved payload enqueues backend command through api command lane", () => {
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (() => new Promise<Response>(() => {})) as typeof fetch;

  const result = factoryTestOnly.emitBoundEvent({
    componentId: "button-1",
    componentType: "action/button",
    props: { data: { label: "Export" } },
    payloadFromNodeValues: { period_input: "2026-06" },
    eventBinding: {
      click: {
        eventType: "click",
        externalPortDispatch: {
          portTargetRef: `external-port:access_port:${FILE_STORAGE_ACCESS_PORT_ID}`,
          payloadFrom: {
            export_job_id: "literal:job-003",
            period: "node:period_input.value",
            export_format: "literal:pdf",
          },
          outputProp: "exportResult",
        },
      },
    },
  }, "click", {});

  assertEquals(result.ok, true);
  assertEquals(schedulerTestOnly.isCommandQueueRunning(), true);
  globalThis.fetch = originalFetch;
  schedulerTestOnly.resetCommandQueue();
});

Deno.test("fileStoragePortConsumer: missing payloadFrom node fails explicitly without enqueue", () => {
  schedulerTestOnly.resetCommandQueue();
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (() => new Promise<Response>(() => {})) as typeof fetch;

  const result = factoryTestOnly.emitBoundEvent({
    componentId: "button-1",
    componentType: "action/button",
    props: { data: { label: "Export" } },
    eventBinding: {
      click: {
        eventType: "click",
        externalPortDispatch: {
          portTargetRef: `external-port:access_port:${FILE_STORAGE_ACCESS_PORT_ID}`,
          payloadFrom: { period: "node:missing_input.value" },
          outputProp: "exportResult",
        },
      },
    },
  }, "click", {});

  assertEquals(result.ok, false);
  if (!result.ok) assertStringIncludes(result.error, "PAYLOAD_FROM_NODE_NOT_FOUND");
  assertEquals(schedulerTestOnly.getCommandQueueLength(), 0);
  globalThis.fetch = originalFetch;
  schedulerTestOnly.resetCommandQueue();
});
