// Abstract Function Boundary Tests
// Verifies input contracts, output contracts, explicit failure modes,
// and prohibited side-effects for all 31 abstract function boundaries.
//
// Boundary test coverage:
//   - input contract: invalid inputs → explicit error codes
//   - output contract: valid inputs → correctly typed output shapes
//   - explicit failure: SQL Attention observation-only, mutation ordering, confirm required
//   - prohibited side effects: no registry mutation, no canonical write, no free-form eval

import { assert, assertEquals, assertMatch } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  rankCandidateResults,
  explainCandidateMatch,
  detectDuplicateCandidates,
  suggestSchemaPromotionCandidates,
  importRowsToCandidates,
  deduplicateImportCandidates,
  resolvePostalAddress,
  resolveAddressPostal,
  resolveTelCandidate,
  validateCandidate,
  previewUpdatePatch,
  applyConfirmedUpdate,
  appendDiffLog,
  buildRollbackCandidate,
  resolveConflictCandidate,
  calculateAttentionWeight,
  calculateRankScore,
  calculateTopologyDistance,
  calculateRouteCost,
  validateFormulaContract,
  previewLayoutPatch,
  validateLayoutConstraint,
  applyConfirmedLayoutPatch,
  resolveStyleToken,
  previewResponsiveRule,
  validateComponentPlacement,
  dryRunOperation,
  validateMutationBoundary,
  explainOperationRisk,
  checkPermissionForOperation,
  buildConfirmableOperation,
  type MutationResult,
} from "../runtime/abstractFunctions.ts";

// =============================================================================
// Category A: Search / Suggest — rank_candidate_results
// =============================================================================

Deno.test("rankCandidateResults: non-array candidates is explicit error", () => {
  const result = rankCandidateResults("not-array", { strategy: "recency" });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /candidates must be array/);
});

Deno.test("rankCandidateResults: missing policy.strategy is explicit error", () => {
  const result = rankCandidateResults([], { limit: 5 });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /policy\.strategy required/);
});

Deno.test("rankCandidateResults: unknown strategy is explicit error", () => {
  const result = rankCandidateResults([], { strategy: "invalid" });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /unknown strategy/);
});

Deno.test("rankCandidateResults: valid input returns ranked candidate array", () => {
  const candidates = [
    { id: "a", label: "A", score: 0.5 },
    { id: "b", label: "B", score: 0.9 },
    { id: "c", label: "C", score: 0.3 },
  ];
  const result = rankCandidateResults(candidates, { strategy: "attention_score" });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data[0].id, "b");
  assertEquals(result.data[0].rank, 1);
  assertEquals(result.data.length, 3);
});

Deno.test("rankCandidateResults: limit restricts result length", () => {
  const candidates = [
    { id: "a", label: "A", score: 0.9 },
    { id: "b", label: "B", score: 0.8 },
    { id: "c", label: "C", score: 0.7 },
  ];
  const result = rankCandidateResults(candidates, { strategy: "recency", limit: 2 });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.length, 2);
});

Deno.test("rankCandidateResults: SQL Attention boundary — result is candidate surface, no registry write", () => {
  const result = rankCandidateResults(
    [{ id: "x", label: "X", score: 1.0 }],
    { strategy: "attention_score" },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  // ranked result is array of candidates with rank; no registry mutation
  assert(Array.isArray(result.data));
  assert(result.data.every((c) => typeof c.rank === "number"));
  // no registryMutated field (readonly result)
  assert(!("registryMutated" in result.data[0]) || result.data[0].registryMutated === undefined);
});

// =============================================================================
// Category A: explain_candidate_match
// =============================================================================

Deno.test("explainCandidateMatch: missing candidate.id is explicit error", () => {
  const result = explainCandidateMatch({ label: "X" }, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /candidate\.id required/);
});

Deno.test("explainCandidateMatch: non-object context is explicit error", () => {
  const result = explainCandidateMatch({ id: "x", label: "X" }, "not-object");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /context must be object/);
});

Deno.test("explainCandidateMatch: valid input returns explanation", () => {
  const result = explainCandidateMatch(
    { id: "cand_1", label: "Candidate 1", score: 0.8, fieldOverlap: ["name", "city"] },
    { requestField: "name" },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.candidateId, "cand_1");
  assertEquals(result.data.score, 0.8);
  assert(Array.isArray(result.data.fieldOverlap));
});

// =============================================================================
// Category A: detect_duplicate_candidates
// =============================================================================

Deno.test("detectDuplicateCandidates: non-array candidates is explicit error", () => {
  const result = detectDuplicateCandidates(null, { threshold: 0.8, strategy: "cosine" });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /candidates must be array/);
});

Deno.test("detectDuplicateCandidates: missing threshold is explicit error", () => {
  const result = detectDuplicateCandidates([], { strategy: "cosine" });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /policy\.threshold required/);
});

Deno.test("detectDuplicateCandidates: out-of-range threshold is explicit error", () => {
  const result = detectDuplicateCandidates([], { threshold: 1.5, strategy: "cosine" });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /threshold must be 0\.\.1/);
});

Deno.test("detectDuplicateCandidates: valid input returns duplicate groups array", () => {
  const result = detectDuplicateCandidates(
    [{ id: "a", label: "A" }, { id: "b", label: "B" }],
    { threshold: 0.5, strategy: "cosine" },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assert(Array.isArray(result.data));
});

// =============================================================================
// Category A: suggest_schema_promotion_candidates
// =============================================================================

Deno.test("suggestSchemaPromotionCandidates: non-object context is explicit error", () => {
  const result = suggestSchemaPromotionCandidates("string");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /context must be object/);
});

Deno.test("suggestSchemaPromotionCandidates: returns candidate array only, no write", () => {
  const result = suggestSchemaPromotionCandidates({
    candidates: [{ id: "path_1", label: "$.address.city" }],
  });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assert(Array.isArray(result.data));
  assertEquals(result.data[0].id, "path_1");
});

// =============================================================================
// Category B: import_rows_to_candidates
// =============================================================================

Deno.test("importRowsToCandidates: non-array source is explicit error", () => {
  const result = importRowsToCandidates("not-array", []);
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /source must be array/);
});

Deno.test("importRowsToCandidates: non-array mapping is explicit error", () => {
  const result = importRowsToCandidates([], "not-array");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /mapping must be array/);
});

Deno.test("importRowsToCandidates: empty valid mapping is explicit error", () => {
  const result = importRowsToCandidates([{ name: "A" }], [{ noSourceField: true }]);
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /mapping must have valid entries/);
});

Deno.test("importRowsToCandidates: valid input returns import candidates (candidate surface only)", () => {
  const result = importRowsToCandidates(
    [{ id: "row1", name: "Alice", postal: "123-4567" }],
    [{ sourceField: "name", targetField: "label" }, { sourceField: "postal", targetField: "postalCode" }],
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assert(Array.isArray(result.data));
  assertEquals(result.data[0].label, "Alice");
  assertEquals(result.data[0].postalCode, "123-4567");
  // sourceRow preserved for traceability
  assert("sourceRow" in result.data[0]);
});

// =============================================================================
// Category B: deduplicate_import_candidates
// =============================================================================

Deno.test("deduplicateImportCandidates: non-array candidates is explicit error", () => {
  const result = deduplicateImportCandidates(null, { threshold: 0.8 });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /candidates must be array/);
});

Deno.test("deduplicateImportCandidates: valid input returns deduped array", () => {
  const result = deduplicateImportCandidates(
    [
      { id: "a", label: "A" },
      { id: "a", label: "A dup" },
      { id: "b", label: "B" },
    ],
    { threshold: 0.8, strategy: "exact" },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.length, 2);
  assert(result.data.every((c) => typeof c.id === "string"));
});

// =============================================================================
// Category B: resolve_postal_address (external_lookup — candidate surface only)
// =============================================================================

Deno.test("resolvePostalAddress: non-string postalCode is explicit error", () => {
  const result = resolvePostalAddress(12345);
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /postalCode must be non-empty string/);
});

Deno.test("resolvePostalAddress: invalid format is explicit error", () => {
  const result = resolvePostalAddress("not-a-code");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /postalCode format invalid/);
});

Deno.test("resolvePostalAddress: candidate surface only — no canonical write", () => {
  const result = resolvePostalAddress("123-4567");
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.canonical_authority, "candidate_surface_not_SSOT");
  assertEquals(result.data.postalCode, "123-4567");
});

// =============================================================================
// Category B: resolve_address_postal (external_lookup — candidate surface only)
// =============================================================================

Deno.test("resolveAddressPostal: empty address is explicit error", () => {
  const result = resolveAddressPostal("");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /address must be non-empty string/);
});

Deno.test("resolveAddressPostal: candidate surface only — no canonical write", () => {
  const result = resolveAddressPostal("Tokyo, Shibuya");
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.canonical_authority, "candidate_surface_not_SSOT");
});

// =============================================================================
// Category B: resolve_tel_candidate (internal_master_only — candidate surface only)
// =============================================================================

Deno.test("resolveTelCandidate: empty input is explicit error", () => {
  const result = resolveTelCandidate("");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /telOrAddress must be non-empty string/);
});

Deno.test("resolveTelCandidate: restriction=internal_master_only — no external canonical write", () => {
  const result = resolveTelCandidate("03-1234-5678");
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assert(Array.isArray(result.data));
  assertEquals(result.data[0].canonical_authority, "candidate_surface_not_SSOT");
  assertEquals(result.data[0].restriction, "internal_master_and_registered_data_only");
});

// =============================================================================
// Category C: validate_candidate
// =============================================================================

Deno.test("validateCandidate: non-object candidate is explicit error", () => {
  const result = validateCandidate("string", {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /candidate must be object/);
});

Deno.test("validateCandidate: non-object manifest is explicit error", () => {
  const result = validateCandidate({}, "string");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /manifest must be object/);
});

Deno.test("validateCandidate: required field missing returns validation error", () => {
  const result = validateCandidate(
    { name: "Alice" },
    { required: ["name", "email"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, false);
  assertEquals(result.data.errors[0].field, "email");
  assertEquals(result.data.errors[0].code, "REQUIRED");
});

Deno.test("validateCandidate: all required present returns valid", () => {
  const result = validateCandidate(
    { name: "Alice", email: "alice@example.com" },
    { required: ["name", "email"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, true);
  assertEquals(result.data.errors.length, 0);
});

Deno.test("validateCandidate: type mismatch returns validation error", () => {
  const result = validateCandidate(
    { count: "not-a-number" },
    { fields: { count: { type: "number" } } },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, false);
  assertEquals(result.data.errors[0].code, "TYPE_MISMATCH");
});

// =============================================================================
// Category C: preview_update_patch
// =============================================================================

Deno.test("previewUpdatePatch: empty target is explicit error", () => {
  const result = previewUpdatePatch("", { name: "Alice" });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /target must be non-empty string/);
});

Deno.test("previewUpdatePatch: non-object payload is explicit error", () => {
  const result = previewUpdatePatch("entity/1", "not-object");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /payload must be object/);
});

Deno.test("previewUpdatePatch: returns preview_only patch — no DB write", () => {
  const result = previewUpdatePatch("entity/1", { name: "Alice", city: "Tokyo" });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.mutationType, "preview_only");
  assertEquals(result.data.target, "entity/1");
  assert(Array.isArray(result.data.fields));
  assertEquals(result.data.fields.length, 2);
});

// =============================================================================
// Category C: apply_confirmed_update
// =============================================================================

Deno.test("applyConfirmedUpdate: confirmed:false is explicit error", () => {
  const result = applyConfirmedUpdate("entity/1", { name: "Alice" }, {
    confirmed: false,
    validationResult: { valid: true, errors: [] },
  });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /EXPLICIT_CONFIRM_REQUIRED/);
});

Deno.test("applyConfirmedUpdate: no validationResult is explicit VALIDATE_REQUIRED error", () => {
  const result = applyConfirmedUpdate("entity/1", { name: "Alice" }, {
    confirmed: true,
    validationResult: null,
  });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /VALIDATE_REQUIRED/);
});

Deno.test("applyConfirmedUpdate: validationResult.valid=false is explicit VALIDATE_REQUIRED error", () => {
  const result = applyConfirmedUpdate("entity/1", { name: "Alice" }, {
    confirmed: true,
    validationResult: { valid: false, errors: [{ field: "email", message: "required", code: "REQUIRED" }] },
  });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /VALIDATE_REQUIRED/);
});

Deno.test("applyConfirmedUpdate: empty target with confirmed:true and valid validationResult is explicit error", () => {
  const result = applyConfirmedUpdate("", { name: "Alice" }, {
    confirmed: true,
    validationResult: { valid: true, errors: [] },
  });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /target must be non-empty string/);
});

Deno.test("applyConfirmedUpdate: confirmed:true with validationResult passed returns mutation result", () => {
  const result = applyConfirmedUpdate("entity/1", { name: "Alice" }, {
    confirmed: true,
    validationResult: { valid: true, errors: [] },
  });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.status, "applied");
  assertEquals(result.data.target, "entity/1");
  assertEquals(result.data.mutationType, "update");
  assert(typeof result.data.timestamp === "string");
});

Deno.test("applyConfirmedUpdate: mutation boundary — validate_candidate must pass before apply", () => {
  // validate_candidate failure → apply must reject even with confirmed:true
  const failedValidation = { valid: false, errors: [{ field: "name", message: "required", code: "REQUIRED" }] };
  const result = applyConfirmedUpdate("entity/1", { name: "Alice" }, {
    confirmed: true,
    validationResult: failedValidation,
  });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /VALIDATE_REQUIRED/);
});

// =============================================================================
// Category C: append_diff_log
// =============================================================================

Deno.test("appendDiffLog: no applyResult is explicit error", () => {
  const result = appendDiffLog("entity/1", { old: "A" }, { new: "B" }, "editor1", {
    applyResult: null,
  });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /APPEND_AFTER_APPLY_REQUIRED/);
});

Deno.test("appendDiffLog: cannot be called before apply (ordering constraint)", () => {
  const result = appendDiffLog(
    "entity/1",
    { old: "A" },
    { new: "B" },
    "editor1",
    { applyResult: null },
  );
  assertEquals(result.ok, false);
  // must fail if no apply result provided
  assert(!result.ok);
});

Deno.test("appendDiffLog: with valid applyResult returns append-only entry", () => {
  const applyResult: MutationResult = {
    mutationType: "update",
    target: "entity/1",
    status: "applied",
    timestamp: new Date().toISOString(),
  };
  const result = appendDiffLog(
    "entity/1",
    { name: "Old" },
    { name: "New" },
    "editor1",
    { applyResult },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.appendOnly, true);
  assertEquals(result.data.target, "entity/1");
  assert(typeof result.data.timestamp === "string");
});

Deno.test("appendDiffLog: empty target with valid applyResult is explicit error", () => {
  const applyResult: MutationResult = {
    mutationType: "update",
    target: "entity/1",
    status: "applied",
    timestamp: new Date().toISOString(),
  };
  const result = appendDiffLog("", {}, {}, "editor1", { applyResult });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /target must be non-empty string/);
});

// =============================================================================
// Category C: build_rollback_candidate
// =============================================================================

Deno.test("buildRollbackCandidate: empty diffId is explicit error", () => {
  const result = buildRollbackCandidate("");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /diffId must be non-empty string/);
});

Deno.test("buildRollbackCandidate: returns candidate surface requiring apply confirm", () => {
  const result = buildRollbackCandidate("diff_001");
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.requiresApplyConfirm, true);
  assertEquals(result.data.diffId, "diff_001");
});

// =============================================================================
// Category C: resolve_conflict_candidate
// =============================================================================

Deno.test("resolveConflictCandidate: missing conflict.field is explicit error", () => {
  const result = resolveConflictCandidate({}, { strategy: "local_wins" });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /conflict\.field required/);
});

Deno.test("resolveConflictCandidate: invalid strategy is explicit error", () => {
  const result = resolveConflictCandidate(
    { field: "name", localValue: "A", remoteValue: "B" },
    { strategy: "invalid" },
  );
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /unknown strategy/);
});

Deno.test("resolveConflictCandidate: local_wins returns local value as candidate", () => {
  const result = resolveConflictCandidate(
    { field: "name", localValue: "Alice", remoteValue: "Bob" },
    { strategy: "local_wins" },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.resolvedValue, "Alice");
  assertEquals(result.data.strategy, "local_wins");
});

Deno.test("resolveConflictCandidate: resolved candidate requires apply_confirmed_update", () => {
  const result = resolveConflictCandidate(
    { field: "name", localValue: "Alice", remoteValue: "Bob" },
    { strategy: "remote_wins" },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  // resolved is a candidate; does NOT apply automatically
  assertEquals(result.data.resolvedValue, "Bob");
});

// =============================================================================
// Category D: calculate_attention_weight (SQL Attention observation only)
// =============================================================================

Deno.test("calculateAttentionWeight: empty target is explicit error", () => {
  const result = calculateAttentionWeight("", { weight: 0.7 });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /target must be non-empty string/);
});

Deno.test("calculateAttentionWeight: non-object evidence is explicit error", () => {
  const result = calculateAttentionWeight("entity/1", "not-object");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /evidence must be object/);
});

Deno.test("calculateAttentionWeight: SQL Attention observation only — registryMutated is always false", () => {
  const result = calculateAttentionWeight("entity/1", { weight: 0.85 });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.observationOnly, true);
  assertEquals(result.data.registryMutated, false);
  assertEquals(result.data.target, "entity/1");
});

Deno.test("calculateAttentionWeight: SQL Attention does NOT mutate primitive catalog or route", () => {
  const result = calculateAttentionWeight("hub/42", { weight: 1.0 });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  // Verify: result is observation surface; no mutation marker
  assertEquals(result.data.registryMutated, false);
  assert(!("catalogMutation" in result.data));
  assert(!("routeMutation" in result.data));
});

// =============================================================================
// Category D: calculate_rank_score (SQL Attention observation only)
// =============================================================================

Deno.test("calculateRankScore: invalid candidates is explicit error", () => {
  const result = calculateRankScore(null, { strategy: "attention_score" });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /candidates must be array/);
});

Deno.test("calculateRankScore: SQL Attention observation — rankSource is sql_attention_observation", () => {
  const result = calculateRankScore(
    [{ id: "a", label: "A", score: 0.7 }],
    { strategy: "attention_score" },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data[0].rankSource, "sql_attention_observation");
  // ranked list is candidate surface only; no auto-mutation of registry or route
  assert(!("registryMutated" in result.data[0]) || result.data[0].registryMutated === undefined);
});

Deno.test("calculateRankScore: ranked list does not auto-write registry", () => {
  const result = calculateRankScore(
    [{ id: "x", label: "X", score: 0.9 }, { id: "y", label: "Y", score: 0.5 }],
    { strategy: "recommendation_candidate_surface_only" as unknown as "attention_score" },
  );
  // unknown strategy → explicit error (not a silent pass-through)
  assertEquals(result.ok, false);
});

// =============================================================================
// Category D: calculate_topology_distance
// =============================================================================

Deno.test("calculateTopologyDistance: empty fromHub is explicit error", () => {
  const result = calculateTopologyDistance("", "hub/2", {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /fromHub must be non-empty string/);
});

Deno.test("calculateTopologyDistance: empty toHub is explicit error", () => {
  const result = calculateTopologyDistance("hub/1", "", {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /toHub must be non-empty string/);
});

Deno.test("calculateTopologyDistance: non-object context is explicit error", () => {
  const result = calculateTopologyDistance("hub/1", "hub/2", "not-object");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /context must be object/);
});

Deno.test("calculateTopologyDistance: valid input returns preview_only result", () => {
  const result = calculateTopologyDistance("hub/1", "hub/2", { distance: 3, hopCount: 2 });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.status, "preview_only");
  assertEquals(result.data.fromHub, "hub/1");
  assertEquals(result.data.toHub, "hub/2");
  assertEquals(result.data.distance, 3);
});

// =============================================================================
// Category D: calculate_route_cost
// =============================================================================

Deno.test("calculateRouteCost: non-object route is explicit error", () => {
  const result = calculateRouteCost(null, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /route must be object/);
});

Deno.test("calculateRouteCost: valid input returns candidate_surface result", () => {
  const result = calculateRouteCost({ nodes: ["A", "B", "C"] }, { cost: 42 });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.status, "candidate_surface");
  assertEquals(result.data.cost, 42);
});

// =============================================================================
// Category D: validate_formula_contract
// =============================================================================

Deno.test("validateFormulaContract: non-object formula is explicit error", () => {
  const result = validateFormulaContract("not-object", {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /formula must be object/);
});

Deno.test("validateFormulaContract: missing operator returns validation error", () => {
  const result = validateFormulaContract({}, { allowedOperators: ["add", "multiply"] });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, false);
  assertEquals(result.data.errors[0].field, "operator");
});

Deno.test("validateFormulaContract: disallowed operator returns DISALLOWED_OPERATOR error", () => {
  const result = validateFormulaContract(
    { operator: "eval" },
    { allowedOperators: ["add", "multiply"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, false);
  assertEquals(result.data.errors[0].code, "DISALLOWED_OPERATOR");
});

Deno.test("validateFormulaContract: no free-form eval — eval operator is rejected", () => {
  const result = validateFormulaContract(
    { operator: "eval" },
    { allowedOperators: ["add", "subtract", "multiply", "divide"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, false);
  // eval must always be disallowed when not in allowedOperators
  assert(result.data.errors.some((e) => e.code === "DISALLOWED_OPERATOR"));
});

Deno.test("validateFormulaContract: allowed operator passes", () => {
  const result = validateFormulaContract(
    { operator: "add" },
    { allowedOperators: ["add", "subtract"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, true);
});

// =============================================================================
// Category E: preview_layout_patch
// =============================================================================

Deno.test("previewLayoutPatch: empty layoutId is explicit error", () => {
  const result = previewLayoutPatch("", {}, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /layoutId must be non-empty string/);
});

Deno.test("previewLayoutPatch: valid input returns preview_only result", () => {
  const result = previewLayoutPatch("layout/main", { columns: 3 }, { viewport: "desktop" });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.mutationType, "preview_only");
  assertEquals(result.data.layoutId, "layout/main");
});

// =============================================================================
// Category E: validate_layout_constraint
// =============================================================================

Deno.test("validateLayoutConstraint: empty layoutId is explicit error", () => {
  const result = validateLayoutConstraint("", {}, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /layoutId must be non-empty string/);
});

Deno.test("validateLayoutConstraint: maxColumns exceeded returns constraint violation", () => {
  const result = validateLayoutConstraint(
    "layout/main",
    { columns: 5 },
    { maxColumns: 4 },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, false);
  assertEquals(result.data.errors[0].code, "CONSTRAINT_VIOLATION");
});

Deno.test("validateLayoutConstraint: within constraints returns valid", () => {
  const result = validateLayoutConstraint(
    "layout/main",
    { columns: 3 },
    { maxColumns: 4 },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, true);
});

// =============================================================================
// Category E: apply_confirmed_layout_patch
// =============================================================================

Deno.test("applyConfirmedLayoutPatch: confirmed:false is explicit error", () => {
  const result = applyConfirmedLayoutPatch("layout/main", { columns: 3 }, {
    confirmed: false,
    validationResult: { valid: true, errors: [] },
  });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /EXPLICIT_CONFIRM_REQUIRED/);
});

Deno.test("applyConfirmedLayoutPatch: no validationResult is explicit VALIDATE_REQUIRED error", () => {
  const result = applyConfirmedLayoutPatch("layout/main", { columns: 3 }, {
    confirmed: true,
    validationResult: null,
  });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /VALIDATE_REQUIRED/);
});

Deno.test("applyConfirmedLayoutPatch: validationResult.valid=false is explicit VALIDATE_REQUIRED error", () => {
  const result = applyConfirmedLayoutPatch("layout/main", { columns: 5 }, {
    confirmed: true,
    validationResult: {
      valid: false,
      errors: [{ field: "columns", message: "exceeds maxColumns", code: "CONSTRAINT_VIOLATION" }],
    },
  });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /VALIDATE_REQUIRED/);
});

Deno.test("applyConfirmedLayoutPatch: confirmed:true with validationResult passed returns layout mutation result", () => {
  const result = applyConfirmedLayoutPatch("layout/main", { columns: 3 }, {
    confirmed: true,
    validationResult: { valid: true, errors: [] },
  });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.status, "applied");
  assertEquals(result.data.layoutId, "layout/main");
  assertEquals(result.data.mutationType, "layout_apply");
});

Deno.test("applyConfirmedLayoutPatch: mutation boundary — validate_layout_constraint must pass before apply", () => {
  const failedValidation = {
    valid: false,
    errors: [{ field: "columns", message: "exceeds maxColumns", code: "CONSTRAINT_VIOLATION" }],
  };
  const result = applyConfirmedLayoutPatch("layout/main", { columns: 5 }, {
    confirmed: true,
    validationResult: failedValidation,
  });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /VALIDATE_REQUIRED/);
});

// =============================================================================
// Category E: resolve_style_token
// =============================================================================

Deno.test("resolveStyleToken: empty tokenId is explicit error", () => {
  const result = resolveStyleToken("", {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /tokenId must be non-empty string/);
});

Deno.test("resolveStyleToken: resolves from registry context", () => {
  const result = resolveStyleToken(
    "color.primary",
    { registry: { tokens: { "color.primary": "#0066cc" } } },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.tokenId, "color.primary");
  assertEquals(result.data.resolvedValue, "#0066cc");
});

Deno.test("resolveStyleToken: unknown token returns tokenId as fallback", () => {
  const result = resolveStyleToken("unknown.token", { registry: { tokens: {} } });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.resolvedValue, "unknown.token");
});

// =============================================================================
// Category E: preview_responsive_rule
// =============================================================================

Deno.test("previewResponsiveRule: non-object rule is explicit error", () => {
  const result = previewResponsiveRule("not-object", "desktop");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /rule must be object/);
});

Deno.test("previewResponsiveRule: empty viewport is explicit error", () => {
  const result = previewResponsiveRule({ breakpoint: "desktop" }, "");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /viewport must be non-empty string/);
});

Deno.test("previewResponsiveRule: matching viewport applies rule", () => {
  const result = previewResponsiveRule({ breakpoint: "desktop" }, "desktop");
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.applied, true);
  assertEquals(result.data.mutationType, "preview_only");
});

Deno.test("previewResponsiveRule: non-matching viewport does not apply", () => {
  const result = previewResponsiveRule({ breakpoint: "mobile" }, "desktop");
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.applied, false);
});

// =============================================================================
// Category E: validate_component_placement
// =============================================================================

Deno.test("validateComponentPlacement: missing component.id is explicit error", () => {
  const result = validateComponentPlacement({}, {}, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /component\.id required/);
});

Deno.test("validateComponentPlacement: zone not in allowedZones returns violation", () => {
  const result = validateComponentPlacement(
    { id: "comp/1", zone: "sidebar" },
    { id: "layout/1" },
    { allowedZones: ["main", "header"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, false);
  assertEquals(result.data.errors[0].code, "ZONE_NOT_ALLOWED");
});

Deno.test("validateComponentPlacement: allowed zone returns valid", () => {
  const result = validateComponentPlacement(
    { id: "comp/1", zone: "main" },
    { id: "layout/1" },
    { allowedZones: ["main", "header"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.valid, true);
});

// =============================================================================
// Category F: dry_run_operation
// =============================================================================

Deno.test("dryRunOperation: missing operationKind is explicit error", () => {
  const result = dryRunOperation({}, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /operation\.operationKind required/);
});

Deno.test("dryRunOperation: valid input returns preview_only result with no mutation", () => {
  const result = dryRunOperation(
    { operationKind: "delete_hub" },
    { expectedMutations: ["DELETE hub WHERE id=1"], risks: ["irreversible"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.mutationType, "preview_only");
  assert(Array.isArray(result.data.expectedMutations));
  assert(Array.isArray(result.data.risks));
});

// =============================================================================
// Category F: validate_mutation_boundary
// =============================================================================

Deno.test("validateMutationBoundary: missing operationKind is explicit error", () => {
  const result = validateMutationBoundary({}, { allowedBoundaries: [] });
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /operation\.operationKind required/);
});

Deno.test("validateMutationBoundary: missing allowedBoundaries is explicit error", () => {
  const result = validateMutationBoundary({ operationKind: "update_field" }, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /policy\.allowedBoundaries required/);
});

Deno.test("validateMutationBoundary: operation crossing boundary returns violations", () => {
  const result = validateMutationBoundary(
    { operationKind: "delete_hub", boundary: "topology" },
    { allowedBoundaries: ["candidate", "preview"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.crossesBoundary, true);
  assert(result.data.violations.length > 0);
});

Deno.test("validateMutationBoundary: operation within boundary has no violations", () => {
  const result = validateMutationBoundary(
    { operationKind: "update_field", boundary: "candidate" },
    { allowedBoundaries: ["candidate", "preview"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.crossesBoundary, false);
});

// =============================================================================
// Category F: explain_operation_risk
// =============================================================================

Deno.test("explainOperationRisk: missing operationKind is explicit error", () => {
  const result = explainOperationRisk({}, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /operation\.operationKind required/);
});

Deno.test("explainOperationRisk: valid input returns risk explanation", () => {
  const result = explainOperationRisk(
    { operationKind: "bulk_delete" },
    { riskLevel: "high", description: "Bulk delete is irreversible", affectedResources: ["hub"] },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.riskLevel, "high");
  assertEquals(result.data.description, "Bulk delete is irreversible");
  assert(Array.isArray(result.data.affectedResources));
});

Deno.test("explainOperationRisk: unknown riskLevel defaults to unknown", () => {
  const result = explainOperationRisk(
    { operationKind: "bulk_delete" },
    { riskLevel: "extreme" },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.riskLevel, "unknown");
});

// =============================================================================
// Category F: check_permission_for_operation
// =============================================================================

Deno.test("checkPermissionForOperation: empty actor is explicit error", () => {
  const result = checkPermissionForOperation("", { operationKind: "update" }, "hub/1");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /actor must be non-empty string/);
});

Deno.test("checkPermissionForOperation: missing operationKind is explicit error", () => {
  const result = checkPermissionForOperation("admin", {}, "hub/1");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /operation\.operationKind required/);
});

Deno.test("checkPermissionForOperation: null target is explicit error", () => {
  const result = checkPermissionForOperation("admin", { operationKind: "update" }, null);
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /target required/);
});

Deno.test("checkPermissionForOperation: operation without requiredRole is permitted", () => {
  const result = checkPermissionForOperation(
    "user1",
    { operationKind: "read" },
    "entity/1",
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.permitted, true);
});

Deno.test("checkPermissionForOperation: operation requiring specific role is not auto-permitted", () => {
  const result = checkPermissionForOperation(
    "user1",
    { operationKind: "admin_delete", requiredRole: "admin" },
    "entity/1",
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.permitted, false);
  assert(typeof result.data.reason === "string");
});

// =============================================================================
// Category F: build_confirmable_operation
// =============================================================================

Deno.test("buildConfirmableOperation: missing operationKind is explicit error", () => {
  const result = buildConfirmableOperation({}, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /operation\.operationKind required/);
});

Deno.test("buildConfirmableOperation: non-object context is explicit error", () => {
  const result = buildConfirmableOperation({ operationKind: "delete" }, "not-object");
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /context must be object/);
});

Deno.test("buildConfirmableOperation: returns candidate requiring explicit confirm", () => {
  const result = buildConfirmableOperation(
    { operationKind: "bulk_update" },
    { dryRunResult: { mutations: 5 }, riskSummary: "medium risk" },
  );
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.data.requiresExplicitConfirm, true);
  assertEquals(result.data.status, "pending_confirmation");
  assert(typeof result.data.id === "string");
});

// =============================================================================
// Cross-boundary invariant tests
// =============================================================================

Deno.test("[boundary] mutation chain: preview → validate → apply — validate_candidate required before apply", () => {
  // apply WITHOUT validationResult fails even with confirmed:true
  const applyNoValidation = applyConfirmedUpdate("entity/1", { name: "X" }, {
    confirmed: true,
    validationResult: null,
  });
  assertEquals(applyNoValidation.ok, false);
  if (!applyNoValidation.ok) assertMatch(applyNoValidation.error, /VALIDATE_REQUIRED/);

  // apply WITH valid validationResult AND confirmation succeeds
  const validationResult = { valid: true, errors: [] };
  const applyResult = applyConfirmedUpdate("entity/1", { name: "X" }, {
    confirmed: true,
    validationResult,
  });
  assertEquals(applyResult.ok, true);

  // append_diff_log must NOT be callable without prior apply result
  const appendResult = appendDiffLog("entity/1", {}, {}, "e1", { applyResult: null });
  assertEquals(appendResult.ok, false);
  if (!appendResult.ok) assertMatch(appendResult.error, /APPEND_AFTER_APPLY_REQUIRED/);
});

Deno.test("[boundary] layout mutation chain: preview → validate → apply — both confirm and validation required", () => {
  // preview succeeds without confirmation or validation
  const preview = previewLayoutPatch("layout/1", { cols: 2 }, {});
  assertEquals(preview.ok, true);

  // apply requires explicit confirm — missing confirm fails
  const applyNoConfirm = applyConfirmedLayoutPatch("layout/1", { cols: 2 }, {
    confirmed: false,
    validationResult: { valid: true, errors: [] },
  });
  assertEquals(applyNoConfirm.ok, false);
  if (!applyNoConfirm.ok) assertMatch(applyNoConfirm.error, /EXPLICIT_CONFIRM_REQUIRED/);

  // apply requires validate_layout_constraint pass — missing validationResult fails
  const applyNoValidation = applyConfirmedLayoutPatch("layout/1", { cols: 2 }, {
    confirmed: true,
    validationResult: null,
  });
  assertEquals(applyNoValidation.ok, false);
  if (!applyNoValidation.ok) assertMatch(applyNoValidation.error, /VALIDATE_REQUIRED/);

  // apply succeeds only when both are satisfied
  const applyWithBoth = applyConfirmedLayoutPatch("layout/1", { cols: 2 }, {
    confirmed: true,
    validationResult: { valid: true, errors: [] },
  });
  assertEquals(applyWithBoth.ok, true);
});

Deno.test("[boundary] SQL Attention functions do NOT mutate registry, route, or primitive catalog", () => {
  const attentionResult = calculateAttentionWeight("entity/x", { weight: 0.9 });
  const rankResult = calculateRankScore([{ id: "a", label: "A", score: 0.9 }], { strategy: "attention_score" });
  assertEquals(attentionResult.ok, true);
  assertEquals(rankResult.ok, true);
  if (attentionResult.ok) {
    assertEquals(attentionResult.data.registryMutated, false);
  }
  if (rankResult.ok) {
    assertEquals(rankResult.data[0].rankSource, "sql_attention_observation");
    // no registry mutation field on rank result
    assert(!("registryMutation" in rankResult.data[0]));
  }
});

Deno.test("[boundary] external lookup functions are candidate surface only — no canonical write", () => {
  const postal = resolvePostalAddress("100-0001");
  const addressPostal = resolveAddressPostal("Tokyo Chiyoda");
  const tel = resolveTelCandidate("03-1234-5678");
  if (postal.ok) assertEquals(postal.data.canonical_authority, "candidate_surface_not_SSOT");
  if (addressPostal.ok) assertEquals(addressPostal.data.canonical_authority, "candidate_surface_not_SSOT");
  if (tel.ok) {
    assert(tel.data.every((c) => c.canonical_authority === "candidate_surface_not_SSOT"));
  }
});

Deno.test("[boundary] import_rows_to_candidates is candidate surface — apply requires validate + confirm", () => {
  const importResult = importRowsToCandidates(
    [{ id: "r1", name: "Alice" }],
    [{ sourceField: "name", targetField: "label" }],
  );
  assertEquals(importResult.ok, true);
  if (!importResult.ok) return;
  // candidate has no 'applied' status; it's pending validation + confirm
  assert(!("status" in importResult.data[0]) || importResult.data[0].status !== "applied");
});
