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

Deno.test("fileStoragePortConsumer: export_job preset seed uses portTargetRef wiring to access_port and omits secret projections", async () => {
  const seed = await Deno.readTextFile(new URL("../../db/file_storage_export_job_preset_seed.sql", import.meta.url));
  assertStringIncludes(seed, "file_storage_export_job.v1");
  assertStringIncludes(seed, "derivedFrom");
  assertStringIncludes(seed, "physical_search_crud_aggregate.v1");
  assertStringIncludes(seed, `external-port:access_port:${FILE_STORAGE_ACCESS_PORT_ID}`);
  assertStringIncludes(seed, "credentialPlane");
  assertStringIncludes(seed, "external_port_substrate reference_key resolution only");
  assertStringIncludes(seed, "forbiddenProjectionFields");
  // Projection response fields must be present (non-secret only)
  assertStringIncludes(seed, "authorization_key");
  assertStringIncludes(seed, "file_artifact_id");
  // payloadFrom must include file_name and file_type to align with af02 c009/c00a bindings (required=true)
  assertStringIncludes(seed, "file_name_input");
  assertStringIncludes(seed, "file_type_input");
  assertStringIncludes(seed, `"file_name":"node:file_name_input.value"`);
  assertStringIncludes(seed, `"file_type":"node:file_type_input.value"`);
  // file_type_input must be required:true to match af02 step 1 binding c00a (required=true from payload)
  assertStringIncludes(seed, `"File type","required":true`);
  // unresolved_json must be empty (no caller resolution gap)
  assertStringIncludes(seed, "$$[]$$::jsonb");
  // Secret fields must be absent from the seed
  assertEquals(seed.includes("signed_url_value"), false);
  assertEquals(seed.includes("storage_endpoint"), false);
  assertEquals(seed.includes("bucket_name"), false);
});

// Proves that the export_job preset seed's wiring_candidate_json (the DB SSOT for dispatchExternalPort)
// correctly flows through draftPreviewResultToEmission + renderEmission + parseEventBinding.
// This test is seed-derived: the portTargetRef and payloadFrom are extracted from the actual SQL seed
// file rather than being handwritten. This anchors the frontend pipeline proof to the DB seed SSOT.
// DB manifest proof (af02/af04 steps load correctly from seed_empty.sql) is in
// backend/tests/Topolactor.Integration.Tests/FileStoragePortConsumerLiveDbTests.cs.
Deno.test("fileStoragePortConsumer: export_job seed wiring_candidate_json portTargetRef flows through render pipeline (seed-derived)", async () => {
  const seedSql = await Deno.readTextFile(new URL("../../db/file_storage_export_job_preset_seed.sql", import.meta.url));
  // Extract wiring_candidate_json compile snapshot block — same extraction as presetSeedLineContract.test.ts
  const sectionStart = seedSql.indexOf("INSERT INTO topology.mock_preset_compile_snapshot");
  const section = seedSql.slice(sectionStart);
  const blocks: unknown[] = [];
  const re = /\$\$([\s\S]*?)\$\$::jsonb/g;
  let m: RegExpExecArray | null;
  while ((m = re.exec(section)) !== null) blocks.push(JSON.parse(m[1].trim()));
  // blocks[2] = wiring_candidate_json
  const wiringCandidates = blocks[2] as Array<Record<string, unknown>>;
  assertExists(wiringCandidates[0]);
  const binding = wiringCandidates[0].binding as Record<string, unknown>;
  const portTargetRef = binding.portTargetRef as string;
  const payloadFrom = binding.payloadFrom as Record<string, string>;
  assertEquals(portTargetRef, `external-port:access_port:${FILE_STORAGE_ACCESS_PORT_ID}`);
  assertExists(payloadFrom["file_name"]);
  assertExists(payloadFrom["file_type"]);

  // Build emission from the seed-extracted wiring and verify the render pipeline
  const previewResult: DraftPreviewResult = {
    success: true,
    layoutId: "layout-file-storage-seed-derived",
    packageId: "00000000-0000-0000-0000-000000000001",
    layoutNodes: [{
      nodeId: "export_job_submit_button",
      nodeKind: "catalog_component",
      componentId: "export_job_submit_button",
      componentKey: "button.primitive",
      componentKind: "action/button",
      orderIndex: 7,
      runtimeInteractions: [{
        trigger: "click",
        actionType: "dispatchExternalPort",
        portTargetRef,
        payloadFrom,
        outputProp: binding.outputProp as string,
      }],
    }],
  };
  const emission = draftPreviewResultToEmission(previewResult);
  assertExists(emission);
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertExists(specs[0].runtimeSpec);
  const parsed = factoryTestOnly.parseEventBinding(specs[0].runtimeSpec!.eventBinding.click);
  assertExists(parsed);
  assertEquals(parsed.externalPortDispatch?.portTargetRef, portTargetRef);
});

// Proves record_file_artifact projection response (af02 step 2 OutputProp) maps through
// draftPreviewResultToEmission / renderEmission into json viewer props.
// DB projection proof (that af02 step 2 actually fires) is in
// backend/tests/Topolactor.Integration.Tests/FileStoragePortConsumerLiveDbTests.cs.
Deno.test("fileStoragePortConsumer: record_file_artifact projection result maps through draft preview emission into json viewer props", () => {
  const previewResult: DraftPreviewResult = {
    success: true,
    layoutId: "layout-file-storage-artifact-projection",
    packageId: "00000000-0000-0000-0000-000000000001",
    data: {
      exportJobResult: {
        file_artifact_id: "artifact-job-001",
        file_name: "export-2026-06.pdf",
        file_type: "pdf",
      },
    },
    layoutNodes: [{
      nodeId: "export_job_result_json",
      nodeKind: "catalog_component",
      componentId: "export_job_result_json",
      componentKey: "json_viewer.template",
      componentKind: "data_display/json",
      orderIndex: 0,
      propsJson: JSON.stringify({ title: "Export job result" }),
      propBindings: {
        data: { source: "emission.data.exportJobResult" },
      },
    }],
  };

  const emission = draftPreviewResultToEmission(previewResult);
  assertExists(emission);
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertExists(specs[0].runtimeSpec);
  const props = specs[0].runtimeSpec!.props as Record<string, unknown>;
  const data = props.data as Record<string, unknown>;
  assertEquals(data.file_artifact_id, "artifact-job-001");
  assertEquals(data.file_name, "export-2026-06.pdf");
  assertEquals(data.file_type, "pdf");
  // Verify no secret fields appear in the projected output
  assertEquals("signed_url" in data, false);
  assertEquals("credential" in data, false);
  assertEquals("storage_ref" in data, false);
});

// Proves authorize_signed_download projection response (af04 step 2 OutputProp) maps through
// the emission pipeline. authorization_key is an opaque DB reference, not a signed URL.
Deno.test("fileStoragePortConsumer: authorize_signed_download projection returns authorization_key reference without signed_url", () => {
  const previewResult: DraftPreviewResult = {
    success: true,
    layoutId: "layout-file-storage-signed-download-projection",
    packageId: "00000000-0000-0000-0000-000000000001",
    data: {
      exportJobResult: {
        authorization_key: "opaque-auth-ref-001",
        file_artifact_id: "artifact-job-001",
      },
    },
    layoutNodes: [{
      nodeId: "export_job_result_json",
      nodeKind: "catalog_component",
      componentId: "export_job_result_json",
      componentKey: "json_viewer.template",
      componentKind: "data_display/json",
      orderIndex: 0,
      propsJson: JSON.stringify({ title: "Export job result" }),
      propBindings: {
        data: { source: "emission.data.exportJobResult" },
      },
    }],
  };

  const emission = draftPreviewResultToEmission(previewResult);
  assertExists(emission);
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertExists(specs[0].runtimeSpec);
  const props = specs[0].runtimeSpec!.props as Record<string, unknown>;
  const data = props.data as Record<string, unknown>;
  assertEquals(data.authorization_key, "opaque-auth-ref-001");
  assertEquals(data.file_artifact_id, "artifact-job-001");
  // Signed URL must not appear — authorization_key is an opaque reference only
  assertEquals("signed_url" in data, false);
  assertEquals("credential" in data, false);
  assertEquals("storage_ref" in data, false);
  assertEquals("bucket" in data, false);
});

Deno.test("fileStoragePortConsumer: attachment list result projects through draft preview emission into card list props", () => {
  const previewResult: DraftPreviewResult = {
    success: true,
    layoutId: "layout-file-attachment-list",
    packageId: "00000000-0000-0000-0000-000000000001",
    data: {
      fileAttachmentListResult: {
        attachments: [{
          attachment_binding_id: "bind-1",
          file_artifact_id: "artifact-1",
          file_name: "receipt.json",
          file_type: "json",
          checksum_value: "sha256:test",
        }],
      },
    },
    layoutNodes: [{
      nodeId: "file_attach_results",
      nodeKind: "catalog_component",
      componentId: "file_attach_results",
      componentKey: "card_list.primitive",
      componentKind: "display/card_list",
      orderIndex: 0,
      propsJson: JSON.stringify({ emptyText: "No attached files." }),
      propBindings: {
        items: { source: "emission.data.fileAttachmentListResult.attachments" },
      },
    }],
  };

  const emission = draftPreviewResultToEmission(previewResult);
  assertExists(emission);
  const specs = renderEmission(emission, defaultComponentRegistry);
  assertExists(specs[0].runtimeSpec);
  const props = specs[0].runtimeSpec!.props as Record<string, unknown>;
  const items = props.items as Array<Record<string, unknown>>;
  assertEquals(items.length, 1);
  assertEquals(items[0].file_name, "receipt.json");
  assertEquals(items[0].file_artifact_id, "artifact-1");
});
