import { assertEquals, assertExists, assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { __testOnly as factoryTestOnly } from "../runtime/runtimeComponentFactory.ts";
import { draftPreviewResultToEmission } from "../runtime/draftPreviewToEmission.ts";
import type { DraftPreviewResult } from "../api/draftPreview.ts";

const EXPORT_SFTP_RESPONSE_PORT_ID = "00000000-0000-0000-0000-000000000f08";
const DENIED = ["credential", "host", "user", "username", "private_key", "endpoint", "remote_path", "signed_url", "raw_provider_response"];

Deno.test("exportSftpPortConsumer: preset seed is CRUD-derived response_port wiring and omits secret projections", async () => {
  const seed = await Deno.readTextFile(new URL("../../db/export_sftp_transfer_preset_seed.sql", import.meta.url));
  assertStringIncludes(seed, "export_sftp_transfer.v1");
  assertStringIncludes(seed, "physical_search_crud_aggregate.v1");
  assertStringIncludes(seed, `external-port:response_port:${EXPORT_SFTP_RESPONSE_PORT_ID}`);
  assertStringIncludes(seed, "dispatchExternalPort");
  assertStringIncludes(seed, "topology.sftp_transfer_log");
  assertStringIncludes(seed, "topology.export_jobs");
  assertStringIncludes(seed, "topology.export_manifests");
  assertStringIncludes(seed, "topology.file_checksum_records");
  assertStringIncludes(seed, "transfer_status");
  assertStringIncludes(seed, "checksum_mismatch");
  for (const key of DENIED) assertStringIncludes(seed, key);
  assertEquals(seed.includes("sftp://"), false);
  assertEquals(seed.includes("BEGIN OPENSSH"), false);
});

Deno.test("exportSftpPortConsumer: seed wiring_candidate_json portTargetRef flows through render pipeline", async () => {
  const seedSql = await Deno.readTextFile(new URL("../../db/export_sftp_transfer_preset_seed.sql", import.meta.url));
  const sectionStart = seedSql.indexOf("INSERT INTO topology.mock_preset_compile_snapshot");
  const section = seedSql.slice(sectionStart);
  const blocks: unknown[] = [];
  const re = /\$\$([\s\S]*?)\$\$::jsonb/g;
  let m: RegExpExecArray | null;
  while ((m = re.exec(section)) !== null) blocks.push(JSON.parse(m[1].trim()));
  const wiringCandidates = blocks[2] as Array<Record<string, unknown>>;
  const binding = wiringCandidates[0].binding as Record<string, unknown>;
  const portTargetRef = binding.portTargetRef as string;
  const payloadFrom = binding.payloadFrom as Record<string, string>;
  assertEquals(portTargetRef, `external-port:response_port:${EXPORT_SFTP_RESPONSE_PORT_ID}`);
  assertExists(payloadFrom.export_job_id);

  const previewResult: DraftPreviewResult = {
    success: true,
    layoutId: "layout-export-sftp-seed-derived",
    packageId: "00000000-0000-0000-0000-000000000001",
    layoutNodes: [{
      nodeId: "sftp_transfer_submit_button",
      nodeKind: "catalog_component",
      componentId: "sftp_transfer_submit_button",
      componentKey: "button.primitive",
      componentKind: "action/button",
      orderIndex: 4,
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
  assertEquals(parsed?.externalPortDispatch?.portTargetRef, portTargetRef);
});

Deno.test("exportSftpPortConsumer: transfer projection exposes safe fields only", () => {
  const previewResult: DraftPreviewResult = {
    success: true,
    layoutId: "layout-export-sftp-projection",
    packageId: "00000000-0000-0000-0000-000000000001",
    data: {
      sftpTransferResult: {
        transfer_status: "transfer_completed",
        checksum_before: "sha256:before",
        checksum_after: "sha256:before",
        manifest_ref: "manifest:opaque-ref",
        retry_evidence_json: {},
      },
    },
    layoutNodes: [{
      nodeId: "sftp_transfer_result_json",
      nodeKind: "catalog_component",
      componentId: "sftp_transfer_result_json",
      componentKey: "json_viewer.template",
      componentKind: "data_display/json",
      orderIndex: 5,
      propsJson: JSON.stringify({ title: "Transfer status" }),
      propBindings: { data: { source: "emission.data.sftpTransferResult" } },
    }],
  };
  const emission = draftPreviewResultToEmission(previewResult);
  assertExists(emission);
  const specs = renderEmission(emission, defaultComponentRegistry);
  const data = (specs[0].runtimeSpec!.props as Record<string, unknown>).data as Record<string, unknown>;
  assertEquals(data.transfer_status, "transfer_completed");
  for (const key of DENIED) assertEquals(key in data, false);
});
