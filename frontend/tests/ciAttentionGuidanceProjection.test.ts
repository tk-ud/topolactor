import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { projectCiAttentionGuidance } from "../runtime/abstractFunctions.ts";

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
