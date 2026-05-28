import { assertEquals, assertExists } from "https://deno.land/std@0.224.0/assert/mod.ts";
import type {
  SqlAttentionProjectionPanelProps,
  TopologyProjectionCandidate,
  TopologyProjectionStatus,
} from "../components/SqlAttentionProjectionPanel.tsx";

// ---------------------------------------------------------------------------
// SQL Attention Projection Panel — type and boundary contract tests
// (SQLA-6 child projection boundary)
//
// These tests verify:
//   1. Props types are structurally correct.
//   2. Status is always explicit (no silent fallback status value).
//   3. Candidates carry all three evidence layers (statistics/attention/phase_attention).
//   4. No mutation helpers are exported from the component module.
//   5. The component is projection-only (read-only candidate surface).
// ---------------------------------------------------------------------------

Deno.test("TopologyProjectionStatus covers all expected values", () => {
  const statuses: TopologyProjectionStatus[] = [
    "ok",
    "no_evidence",
    "missing_policy",
    "malformed_policy",
    "db_unavailable",
  ];
  assertEquals(statuses.length, 5);
  statuses.forEach((s) => assertExists(s));
});

Deno.test("TopologyProjectionCandidate carries all three evidence layers", () => {
  const candidate: TopologyProjectionCandidate = {
    hubId: null,
    attractorKey: "attractor_a",
    relationRegistryId: "00000000-0000-0000-0000-000000000001",
    attentionScore: 2.375,
    scoreBand: "strong",
    statisticsJson: '{"count": 10}',
    vectorJson: '{"hub_a": 0.8}',
    phaseVectorJson: '{"w": 1.2}',
    evidenceJson: '{"cosine_score": 0.95}',
    hitRank: 1,
  };

  // All three SSOT evidence layers must be present.
  assertExists(candidate.statisticsJson, "statistics layer missing");
  assertExists(candidate.vectorJson, "attention layer missing");
  assertExists(candidate.phaseVectorJson, "phase_attention layer missing");
  assertExists(candidate.evidenceJson, "provenance missing");

  // AttentionScore is a display/ranking helper — not the collapsed evidence.
  assertEquals(typeof candidate.attentionScore, "number");
});

Deno.test("SqlAttentionProjectionPanelProps: ok status with candidates is valid", () => {
  const candidate: TopologyProjectionCandidate = {
    hubId: "00000000-0000-0000-0000-000000000001",
    attractorKey: "attractor_b",
    relationRegistryId: null,
    attentionScore: 1.5,
    scoreBand: "normal",
    statisticsJson: '{"count": 5}',
    vectorJson: '{"hub_b": 0.7}',
    phaseVectorJson: '{"w": 0.8}',
    evidenceJson: '{"cosine_score": 0.91}',
    hitRank: 2,
  };

  const props: SqlAttentionProjectionPanelProps = {
    status: "ok",
    candidates: [candidate],
  };

  assertEquals(props.status, "ok");
  assertEquals(props.candidates.length, 1);
});

Deno.test("SqlAttentionProjectionPanelProps: no_evidence status has empty candidates", () => {
  const props: SqlAttentionProjectionPanelProps = {
    status: "no_evidence",
    statusDetail: "No recent attention evidence rows found.",
    candidates: [],
  };

  assertEquals(props.status, "no_evidence");
  assertEquals(props.candidates.length, 0);
  assertEquals(props.statusDetail, "No recent attention evidence rows found.");
});

Deno.test("SqlAttentionProjectionPanelProps: missing_policy is explicit failure with empty candidates", () => {
  const props: SqlAttentionProjectionPanelProps = {
    status: "missing_policy",
    statusDetail: "No active function_parameters row.",
    candidates: [],
  };

  assertEquals(props.status, "missing_policy");
  assertEquals(props.candidates.length, 0);
});

Deno.test("SqlAttentionProjectionPanelProps: malformed_policy is explicit failure with empty candidates", () => {
  const props: SqlAttentionProjectionPanelProps = {
    status: "malformed_policy",
    statusDetail: "Policy JSON is malformed.",
    candidates: [],
  };

  assertEquals(props.status, "malformed_policy");
  assertEquals(props.candidates.length, 0);
});

Deno.test("SqlAttentionProjectionPanelProps: db_unavailable is explicit failure with empty candidates", () => {
  const props: SqlAttentionProjectionPanelProps = {
    status: "db_unavailable",
    statusDetail: "logs.attention query failed.",
    candidates: [],
  };

  assertEquals(props.status, "db_unavailable");
  assertEquals(props.candidates.length, 0);
});

Deno.test("Projection-only boundary: candidates are read-only data, no mutation helpers", async () => {
  // Import the module and verify no mutation functions are exported.
  const mod = await import("../components/SqlAttentionProjectionPanel.tsx");

  // Only the component function and type exports are expected.
  assertExists(mod.SqlAttentionProjectionPanel, "component must be exported");

  // No mutation functions should be present.
  const mutationNames = ["apply", "promote", "mutate", "update", "upsert", "write", "append"];
  for (const name of mutationNames) {
    assertEquals(
      typeof (mod as Record<string, unknown>)[name],
      "undefined",
      `Mutation export '${name}' must not exist on a projection-only component.`,
    );
  }
});

Deno.test("TopologyProjectionCandidate: attentionScore does not collapse evidence layers", () => {
  // A candidate with zero attentionScore still must carry evidence layers.
  const candidate: TopologyProjectionCandidate = {
    hubId: null,
    attractorKey: "attractor_c",
    relationRegistryId: null,
    attentionScore: 0.0,
    scoreBand: "evidence_only",
    statisticsJson: "{}",
    vectorJson: "{}",
    phaseVectorJson: "{}",
    evidenceJson: "{}",
    hitRank: 99,
  };

  // Evidence layers present even when attentionScore is 0.
  assertExists(candidate.statisticsJson);
  assertExists(candidate.vectorJson);
  assertExists(candidate.phaseVectorJson);
  assertExists(candidate.evidenceJson);
});
