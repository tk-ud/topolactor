import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { assertStringIncludes } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";
import { __testOnly as factoryTestOnly } from "../runtime/runtimeComponentFactory.ts";
import { draftPreviewResultToEmission } from "../runtime/draftPreviewToEmission.ts";
import type { DraftPreviewResult } from "../api/draftPreview.ts";

const EMAIL_SMTP_RESPONSE_PORT_ID = "00000000-0000-0000-0000-000000000f03";
const EMAIL_DRAFT_ACTION_PORT_ID = "00000000-0000-0000-0000-000000000f11";
const EMAIL_APPROVAL_ACTION_PORT_ID = "00000000-0000-0000-0000-000000000f12";
const DENIED = ["credential", "smtp_host", "smtp_port", "smtp_api_key", "sender_credential", "raw_provider_response", "plaintext_payload", "token"];

Deno.test("emailPortConsumer: preset seed is CRUD-derived response_port wiring and omits secret projections", async () => {
  const seed = await Deno.readTextFile(new URL("../../db/email_approval_form_preset_seed.sql", import.meta.url));
  assertStringIncludes(seed, "email_approval_form.v1");
  assertStringIncludes(seed, "physical_search_crud_aggregate.v1");
  // Three UI actions wired to response_port records (draft / approval / smtp send).
  assertStringIncludes(seed, `external-port:response_port:${EMAIL_SMTP_RESPONSE_PORT_ID}`);
  assertStringIncludes(seed, `external-port:response_port:${EMAIL_DRAFT_ACTION_PORT_ID}`);
  assertStringIncludes(seed, `external-port:response_port:${EMAIL_APPROVAL_ACTION_PORT_ID}`);
  assertStringIncludes(seed, "dispatchExternalPort");
  assertStringIncludes(seed, "topology.email_drafts");
  assertStringIncludes(seed, "topology.email_approval_records");
  assertStringIncludes(seed, "topology.email_delivery_evidence");
  // Evidence/runtime_event_log event types required by SSOT.
  for (const evt of ["approval_recorded", "dispatch_initiated", "send_success", "send_failure"]) {
    assertStringIncludes(seed, evt);
  }
  for (const key of DENIED) assertStringIncludes(seed, key);
  // No provider-specific client and no plaintext SMTP endpoint literal.
  assertEquals(seed.includes("SmtpClient"), false);
  assertEquals(seed.includes("smtp://"), false);
});

Deno.test("emailPortConsumer: send_button wiring portTargetRef flows through render pipeline", async () => {
  const seedSql = await Deno.readTextFile(new URL("../../db/email_approval_form_preset_seed.sql", import.meta.url));
  const sectionStart = seedSql.indexOf("INSERT INTO topology.mock_preset_compile_snapshot");
  const section = seedSql.slice(sectionStart);
  const blocks: unknown[] = [];
  const re = /\$\$([\s\S]*?)\$\$::jsonb/g;
  let m: RegExpExecArray | null;
  while ((m = re.exec(section)) !== null) blocks.push(JSON.parse(m[1].trim()));
  // blocks: [0]=layout_patch, [1]=package_membership, [2]=wiring_candidate, ...
  const wiringCandidates = blocks[2] as Array<Record<string, unknown>>;
  const sendCandidate = wiringCandidates.find((c) => c.nodeId === "send_button")!;
  assertExists(sendCandidate);
  const binding = sendCandidate.binding as Record<string, unknown>;
  const portTargetRef = binding.portTargetRef as string;
  const payloadFrom = binding.payloadFrom as Record<string, string>;
  assertEquals(portTargetRef, `external-port:response_port:${EMAIL_SMTP_RESPONSE_PORT_ID}`);
  assertExists(payloadFrom.email_draft_id);
  assertExists(payloadFrom.dispatch_idempotency_key);

  const previewResult: DraftPreviewResult = {
    success: true,
    layoutId: "layout-email-seed-derived",
    packageId: "00000000-0000-0000-0000-000000000001",
    layoutNodes: [{
      nodeId: "send_button",
      nodeKind: "catalog_component",
      componentId: "send_button",
      componentKey: "button.primitive",
      componentKind: "action/button",
      orderIndex: 8,
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

Deno.test("emailPortConsumer: delivery projection exposes safe fields only", () => {
  const previewResult: DraftPreviewResult = {
    success: true,
    layoutId: "layout-email-projection",
    packageId: "00000000-0000-0000-0000-000000000001",
    data: {
      emailSendResult: {
        delivery_status: "delivered",
        event_type: "send_success",
      },
    },
    layoutNodes: [{
      nodeId: "email_result_json",
      nodeKind: "catalog_component",
      componentId: "email_result_json",
      componentKey: "json_viewer.template",
      componentKind: "data_display/json",
      orderIndex: 9,
      propsJson: JSON.stringify({ title: "Email result" }),
      propBindings: { data: { source: "emission.data.emailSendResult" } },
    }],
  };
  const emission = draftPreviewResultToEmission(previewResult);
  assertExists(emission);
  const specs = renderEmission(emission, defaultComponentRegistry);
  const data = (specs[0].runtimeSpec!.props as Record<string, unknown>).data as Record<string, unknown>;
  assertEquals(data.delivery_status, "delivered");
  for (const key of DENIED) assertEquals(key in data, false);
});
