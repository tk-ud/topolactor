import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  extractSqlAttentionDraftCandidatePayload,
  type SqlAttentionDraftCandidatePayload,
} from "../runtime/sseReceiver.ts";
import {
  createSseDispatcher,
  registerSqlAttentionDraftCandidateRenderHandler,
} from "../runtime/sseDispatcher.ts";

// ─── manifest_topology_key_expansion draft lane SSE signal (render-only) ─────
//
// The frontend renders only "a draft candidate was created". It holds no SQL
// Attention / recommendation / promotion judgment, decides no topology placement,
// and never infers missing fields — authority is the backend draft candidate JSONB.

const validPayload = JSON.stringify({
  event_type: "sql_attention_draft_candidate_created",
  source: "sql_attention",
  candidate_id: "00000000-0000-0000-0000-0000000000aa",
  candidate_lane: "manifest_topology_key_expansion_draft_lane",
  markdown_projection_id: "00000000-0000-0000-0000-0000000000bb",
  source_set_id: "ss1",
});

Deno.test("extractSqlAttentionDraftCandidatePayload: parses a valid structured signal", () => {
  const payload = extractSqlAttentionDraftCandidatePayload(validPayload);
  assertEquals(payload, {
    eventType: "sql_attention_draft_candidate_created",
    source: "sql_attention",
    candidateId: "00000000-0000-0000-0000-0000000000aa",
    candidateLane: "manifest_topology_key_expansion_draft_lane",
    markdownProjectionId: "00000000-0000-0000-0000-0000000000bb",
    sourceSetId: "ss1",
  } satisfies SqlAttentionDraftCandidatePayload);
});

Deno.test("extractSqlAttentionDraftCandidatePayload: drops malformed / contract-mismatched signals (no inference)", () => {
  // not JSON
  assertEquals(extractSqlAttentionDraftCandidatePayload("not-json{{"), null);
  // wrong event_type
  assertEquals(
    extractSqlAttentionDraftCandidatePayload(
      JSON.stringify({ event_type: "other", source: "sql_attention", candidate_id: "a", candidate_lane: "l", markdown_projection_id: "b" }),
    ),
    null,
  );
  // wrong source
  assertEquals(
    extractSqlAttentionDraftCandidatePayload(
      JSON.stringify({ event_type: "sql_attention_draft_candidate_created", source: "x", candidate_id: "a", candidate_lane: "l", markdown_projection_id: "b" }),
    ),
    null,
  );
  // missing markdown_projection_id
  assertEquals(
    extractSqlAttentionDraftCandidatePayload(
      JSON.stringify({ event_type: "sql_attention_draft_candidate_created", source: "sql_attention", candidate_id: "a", candidate_lane: "l" }),
    ),
    null,
  );
});

Deno.test("sseDispatcher: render-only handler forwards a validated signal and never mutates it", () => {
  const dispatcher = createSseDispatcher({ unhandledEventPolicy: "ignore" });
  const received: SqlAttentionDraftCandidatePayload[] = [];
  registerSqlAttentionDraftCandidateRenderHandler(dispatcher, (payload) => {
    received.push(payload);
  });

  dispatcher.route("sql_attention_draft_candidate", validPayload);
  assertEquals(received.length, 1);
  assertEquals(received[0].candidateLane, "manifest_topology_key_expansion_draft_lane");
  assertEquals(received[0].candidateId, "00000000-0000-0000-0000-0000000000aa");
});

Deno.test("sseDispatcher: render-only handler drops a malformed signal (callback not invoked)", () => {
  const dispatcher = createSseDispatcher({ unhandledEventPolicy: "ignore" });
  let invoked = 0;
  registerSqlAttentionDraftCandidateRenderHandler(dispatcher, () => {
    invoked++;
  });

  dispatcher.route("sql_attention_draft_candidate", "not-json{{");
  assertEquals(invoked, 0);
});
