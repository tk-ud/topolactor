import { assertEquals, assertExists, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { __testOnly as factoryTestOnly } from "../runtime/runtimeComponentFactory.ts";
import { __testOnly as schedulerTestOnly } from "../runtime/frontendScheduler.ts";
import type { Emission } from "../api/dispatch.ts";
import { draftPreviewResultToEmission } from "../runtime/draftPreviewToEmission.ts";
import type { DraftPreviewResult } from "../api/draftPreview.ts";

const FILE_STORAGE_ACCESS_PORT_ID = "00000000-0000-0000-0000-000000000f01";
const FILE_STORAGE_RESPONSE_PORT_ID = "00000000-0000-0000-0000-000000000f02";
const FILE_STORAGE_ATTACHMENT_BIND_PORT_ID = "00000000-0000-0000-0000-000000000f0a";
const FILE_STORAGE_ATTACHMENT_LIST_PORT_ID = "00000000-0000-0000-0000-000000000f0b";
const FILE_STORAGE_ATTACHMENT_UNBIND_PORT_ID = "00000000-0000-0000-0000-000000000f0c";

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

// Unit test: proves draftPreviewResultToEmission correctly maps runtimeInteractions with
// dispatchExternalPort through to renderEmission + parseEventBinding.
// DB projection proof (that DB seed rows produce this structure) is in
// backend/tests/Topolactor.Integration.Tests/FileStoragePortConsumerLiveDbTests.cs.
Deno.test("fileStoragePortConsumer: draftPreviewResultToEmission preserves dispatchExternalPort portTargetRef through render pipeline", () => {
  const previewResult: DraftPreviewResult = {
    success: true,
    layoutId: "layout-file-storage-projection",
    packageId: "00000000-0000-0000-0000-000000000001",
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
          export_job_id: "literal:job-proj-001",
          requested_by: "literal:system",
          period: "literal:2026-06",
          export_format: "literal:pdf",
        },
        outputProp: "exportResult",
      }],
    }],
  };

  const emission = draftPreviewResultToEmission(previewResult);
  assertExists(emission);
  assertExists(emission.layoutNodes);
  assertEquals(emission.layoutNodes!.length, 1);
  const node = emission.layoutNodes![0];
  assertExists(node.runtimeInteractions);
  assertEquals(node.runtimeInteractions!.length, 1);
  const interaction = node.runtimeInteractions![0];
  assertEquals(interaction.actionType, "dispatchExternalPort");
  assertEquals(interaction.portTargetRef, `external-port:access_port:${FILE_STORAGE_ACCESS_PORT_ID}`);

  const specs = renderEmission(emission, defaultComponentRegistry);
  assertExists(specs[0].runtimeSpec);
  const parsed = factoryTestOnly.parseEventBinding(specs[0].runtimeSpec!.eventBinding.click);
  assertExists(parsed);
  assertEquals(
    parsed.externalPortDispatch?.portTargetRef,
    `external-port:access_port:${FILE_STORAGE_ACCESS_PORT_ID}`,
  );
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

Deno.test("fileStoragePortConsumer: attachment CRUD preset seed uses portTargetRef wiring and omits secret projections", async () => {
  const seed = await Deno.readTextFile(new URL("../../db/file_attachment_crud_preset_seed.sql", import.meta.url));
  assertStringIncludes(seed, "file_attachment_crud.v1");
  assertStringIncludes(seed, "derivedFrom");
  assertStringIncludes(seed, "physical_search_crud_aggregate.v1");
  assertStringIncludes(seed, `external-port:response_port:${FILE_STORAGE_ATTACHMENT_BIND_PORT_ID}`);
  assertStringIncludes(seed, `external-port:response_port:${FILE_STORAGE_ATTACHMENT_LIST_PORT_ID}`);
  assertStringIncludes(seed, `external-port:response_port:${FILE_STORAGE_ATTACHMENT_UNBIND_PORT_ID}`);
  assertStringIncludes(seed, "topology.fs_bind_record_file_attachment");
  assertStringIncludes(seed, "topology.fs_list_record_file_attachments");
  assertStringIncludes(seed, "topology.fs_unbind_record_file_attachment");
  assertStringIncludes(seed, "credentialPlane");
  assertStringIncludes(seed, "external_port_substrate reference_key resolution only");
  assertStringIncludes(seed, "forbiddenProjectionFields");
});
