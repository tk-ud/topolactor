import { assertEquals, assertExists, assertStrictEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { projectCiAttentionGuidance } from "../runtime/abstractFunctions.ts";
import { extractCiAttentionFragmentPayload, createSseReceiver } from "../runtime/sseReceiver.ts";

Deno.test("projectCiAttentionGuidance maps missing_input/valid_candidate/structural_violation/break_boundary", () => {
  const result = projectCiAttentionGuidance({
    findings: [
      { checkName: "HUB_ID_EMPTY", status: "Blocking", detail: "hub missing", classification: "MissingRequired" },
      { checkName: "EVIDENCE_EMPTY_WITH_OPERATION", status: "Gap", detail: "candidate hint", classification: "NotCovered" },
      { checkName: "EVIDENCE_JSON_NOT_PARSEABLE", status: "Gap", detail: "shape invalid", classification: "InvalidShape" },
      { checkName: "ATTENTION_SCORE_NOT_FINITE", status: "Blocking", detail: "finite boundary", classification: "RuntimeFailure" },
    ],
  });
  if (!result.ok) throw new Error(result.error);
  assertEquals(result.data.map((d) => d.kind), [
    "missing_input",
    "valid_candidate",
    "structural_violation",
    "break_boundary",
  ]);
});

Deno.test("projectCiAttentionGuidance break_boundary actionable explains draft/promotion/apply split", () => {
  const result = projectCiAttentionGuidance({
    findings: [
      { checkName: "ATTENTION_SCORE_NOT_FINITE", status: "Blocking", detail: "finite boundary", classification: "RuntimeFailure" },
    ],
  });
  if (!result.ok) throw new Error(result.error);
  assertEquals(
    result.data[0]?.actionable,
    "Draft editing remains available; canonical promotion may require resolving active blocking fragments; apply-time validation is backend responsibility.",
  );
});

// ─── SSE live projection: CI Attention fragment detection ─────────────────────

Deno.test("extractCiAttentionFragmentPayload returns payload for valid CI Attention fragment event", () => {
  const fragmentId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
  const raw = JSON.stringify({
    FragmentId: fragmentId,
    Kind: "missing_input",
    Status: "active",
    Severity: "error",
    TargetKind: "component",
    TargetKey: "my_component",
    UpdatedAt: "2024-01-01T00:00:00Z",
  });

  const result = extractCiAttentionFragmentPayload(raw);

  assertExists(result);
  assertEquals(result!.FragmentId, fragmentId);
  assertEquals(result!.Kind, "missing_input");
  assertEquals(result!.Status, "active");
  assertEquals(result!.TargetKind, "component");
  assertEquals(result!.TargetKey, "my_component");
});

Deno.test("extractCiAttentionFragmentPayload returns null for non-fragment projection event (manifest_id payload)", () => {
  const raw = JSON.stringify({
    manifest_id: "m-abc",
    table_id: "t-def",
    table_registry_id: "tr-ghi",
  });

  const result = extractCiAttentionFragmentPayload(raw);

  assertStrictEquals(result, null);
});

Deno.test("extractCiAttentionFragmentPayload returns null for malformed JSON", () => {
  const result = extractCiAttentionFragmentPayload("not-json");

  assertStrictEquals(result, null);
});

Deno.test("extractCiAttentionFragmentPayload returns null for empty object", () => {
  const result = extractCiAttentionFragmentPayload("{}");

  assertStrictEquals(result, null);
});

Deno.test("frontend SSE receiver can be created with projection hook trigger callback for CI Attention fragment handling", () => {
  // Verifies that createSseReceiver accepts onProjectionHookTrigger for live fragment dispatch.
  // Frontend holds display state only; no promotion judgment authority.
  const received: string[] = [];
  const receiver = createSseReceiver({
    onProjectionHookTrigger: (trigger) => {
      const fragment = extractCiAttentionFragmentPayload(trigger.data);
      if (fragment !== null) received.push(fragment.FragmentId);
    },
  });

  assertExists(receiver);
  assertExists(receiver.connect);
  assertExists(receiver.disconnect);
  // Receiver is constructed but not connected (no EventSource in test env).
  // Test confirms: (a) receiver creation succeeds, (b) handler pattern is valid.
  assertEquals(received.length, 0);
});

Deno.test("fragment Kind is read-only guidance — frontend does not derive promotion judgment from it", () => {
  // Frontend reads Kind as display label only.
  // Promotion eligibility is ALWAYS determined by backend runtime boundary guard.
  const raw = JSON.stringify({
    FragmentId: "abc-123",
    Kind: "break_boundary",
    Status: "active",
    Severity: "error",
    TargetKind: "authoring_surface",
    TargetKey: "admin_ui_builder",
    UpdatedAt: "2024-01-01T00:00:00Z",
  });

  const fragment = extractCiAttentionFragmentPayload(raw);

  assertExists(fragment);
  // The fragment has Kind="break_boundary" but frontend must not use this to block promotion.
  // Frontend shows guidance text only. No promotion authority is derived here.
  assertEquals(fragment!.Kind, "break_boundary");
  // Verify there is NO "isBlocking" or "blockPromotion" field surfaced to frontend.
  // (These fields are backend-only: blocks_promotion column in DB)
  assertEquals((fragment as Record<string, unknown>)["BlocksPromotion"], undefined);
  assertEquals((fragment as Record<string, unknown>)["blocks_promotion"], undefined);
});
