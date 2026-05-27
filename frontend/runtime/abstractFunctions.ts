// Abstract Function Primitive Boundary
// Implements function contracts per docs/design/abstract-function-primitive-registry-ssot.yaml
//
// mutation_boundary_types:
//   readonly                       - no DB write at all
//   candidate_surface              - produces candidates, no write side effect
//   preview_only                   - produces preview, no write
//   explicit_confirm_required      - write only after confirmed:true
//   append_only                    - appends audit entry, no mutation of existing rows
//   sql_attention_observation_only - SQL Attention observation; no registry/route mutation
//
// frontend owns: input validation, output shape, boundary enforcement
// frontend does NOT own: topology judgment, SQL Attention judgment, persistence judgment

// =============================================================================
// Shared types
// =============================================================================

export type AbstractFunctionResult<T> =
  | { ok: true; data: T }
  | { ok: false; error: string };

export type Candidate = {
  id: string;
  label: string;
  score?: number;
  [key: string]: unknown;
};

export type ValidationError = {
  field: string;
  message: string;
  code?: string;
};

export type ValidationResult = {
  valid: boolean;
  errors: ValidationError[];
};

export type MutationResult = {
  mutationType: string;
  target: string;
  status: "applied";
  timestamp: string;
};

export type DiffLogEntry = {
  id: string;
  target: string;
  before: unknown;
  after: unknown;
  editor: string;
  appendOnly: true;
  timestamp: string;
};

// =============================================================================
// Category A: Search / Suggest Functions (candidate_surface / readonly)
// =============================================================================

export type RankingPolicy = {
  strategy: "attention_score" | "recency" | "cosine" | "trend";
  limit?: number;
};

export type RankedCandidate = Candidate & { rank: number };

// mutation_boundary: candidate_surface
// sql_attention_boundary: observation_and_ranking_only
// SQL Attention ranked list does NOT auto-write registry
export function rankCandidateResults(
  candidates: unknown,
  policy: unknown,
): AbstractFunctionResult<RankedCandidate[]> {
  if (!Array.isArray(candidates)) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: candidates must be array" };
  }
  if (
    typeof policy !== "object" || policy === null ||
    typeof (policy as Record<string, unknown>).strategy !== "string"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: policy.strategy required" };
  }
  const p = policy as RankingPolicy;
  const valid = ["attention_score", "recency", "cosine", "trend"];
  if (!valid.includes(p.strategy)) {
    return { ok: false, error: `ABSTRACT_FUNCTION_INVALID_INPUT: unknown strategy: ${p.strategy}` };
  }
  const typed = candidates.filter(
    (c): c is Candidate =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  const sorted = [...typed].sort((a, b) => (b.score ?? 0) - (a.score ?? 0));
  const limited = typeof p.limit === "number" && p.limit > 0
    ? sorted.slice(0, p.limit)
    : sorted;
  return { ok: true, data: limited.map((c, i) => ({ ...c, rank: i + 1 })) };
}

export type CandidateMatchExplanation = {
  candidateId: string;
  fieldOverlap: string[];
  topologyDistance: number | null;
  score: number | null;
  reason: string;
};

// mutation_boundary: readonly
export function explainCandidateMatch(
  candidate: unknown,
  context: unknown,
): AbstractFunctionResult<CandidateMatchExplanation> {
  if (
    typeof candidate !== "object" || candidate === null ||
    typeof (candidate as Record<string, unknown>).id !== "string"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: candidate.id required" };
  }
  if (typeof context !== "object" || context === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: context must be object" };
  }
  const c = candidate as Record<string, unknown>;
  const rawOverlap = (c.fieldOverlap ?? []) as unknown;
  const fieldOverlap = Array.isArray(rawOverlap)
    ? rawOverlap.filter((f): f is string => typeof f === "string")
    : [];
  return {
    ok: true,
    data: {
      candidateId: c.id as string,
      fieldOverlap,
      topologyDistance: typeof c.topologyDistance === "number" ? c.topologyDistance : null,
      score: typeof c.score === "number" ? c.score : null,
      reason: typeof c.reason === "string" ? c.reason : "field_overlap_and_score",
    },
  };
}

export type DuplicateGroup = {
  representativeId: string;
  members: string[];
  similarityScore: number;
};

export type DeduplicationPolicy = {
  threshold: number;
  strategy: "cosine" | "exact" | "fuzzy";
};

// mutation_boundary: readonly
export function detectDuplicateCandidates(
  candidates: unknown,
  policy: unknown,
): AbstractFunctionResult<DuplicateGroup[]> {
  if (!Array.isArray(candidates)) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: candidates must be array" };
  }
  if (
    typeof policy !== "object" || policy === null ||
    typeof (policy as Record<string, unknown>).threshold !== "number"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: policy.threshold required" };
  }
  const p = policy as DeduplicationPolicy;
  if (p.threshold < 0 || p.threshold > 1) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: threshold must be 0..1" };
  }
  const typed = candidates.filter(
    (c): c is Candidate =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string",
  );
  const groups: DuplicateGroup[] = [];
  const seen = new Set<string>();
  for (const c of typed) {
    if (seen.has(c.id)) continue;
    const sim = typeof c.score === "number" ? c.score : 0;
    if (sim >= p.threshold) {
      const near = typed
        .filter((o) => o.id !== c.id && !seen.has(o.id) &&
          typeof o.score === "number" && o.score >= p.threshold)
        .map((o) => { seen.add(o.id); return o.id; });
      if (near.length > 0) {
        seen.add(c.id);
        groups.push({ representativeId: c.id, members: near, similarityScore: sim });
      }
    }
  }
  return { ok: true, data: groups };
}

// mutation_boundary: candidate_surface
// Promotion requires explicit confirm through schema promotion flow
export function suggestSchemaPromotionCandidates(
  context: unknown,
): AbstractFunctionResult<Candidate[]> {
  if (typeof context !== "object" || context === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: context must be object" };
  }
  const ctx = context as Record<string, unknown>;
  const rawCandidates = Array.isArray(ctx.candidates) ? ctx.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is Candidate =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  // candidate surface only; no promotion write
  return { ok: true, data: candidates };
}

export type ImportMapping = {
  sourceField: string;
  targetField: string;
};

export type ImportCandidate = Candidate & { sourceRow: Record<string, unknown> };

// mutation_boundary: candidate_surface
// apply requires validate_candidate + explicit confirm
export function importRowsToCandidates(
  source: unknown,
  mapping: unknown,
): AbstractFunctionResult<ImportCandidate[]> {
  if (!Array.isArray(source)) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: source must be array" };
  }
  if (!Array.isArray(mapping)) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: mapping must be array" };
  }
  const typedMapping = mapping.filter(
    (m): m is ImportMapping =>
      typeof m === "object" && m !== null &&
      typeof (m as Record<string, unknown>).sourceField === "string" &&
      typeof (m as Record<string, unknown>).targetField === "string",
  );
  if (typedMapping.length === 0) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: mapping must have valid entries" };
  }
  const rows = source.filter(
    (r): r is Record<string, unknown> =>
      typeof r === "object" && r !== null && !Array.isArray(r),
  );
  const candidates: ImportCandidate[] = rows.map((row, i) => {
    const mapped: Record<string, unknown> = {};
    for (const m of typedMapping) {
      if (m.sourceField in row) mapped[m.targetField] = row[m.sourceField];
    }
    return {
      id: typeof row.id === "string" ? row.id : `import_${i}`,
      label: typeof row.label === "string"
        ? row.label
        : typeof row.name === "string" ? row.name : `row_${i}`,
      ...mapped,
      sourceRow: row,
    };
  });
  return { ok: true, data: candidates };
}

// mutation_boundary: readonly
export function deduplicateImportCandidates(
  candidates: unknown,
  policy: unknown,
): AbstractFunctionResult<Candidate[]> {
  if (!Array.isArray(candidates)) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: candidates must be array" };
  }
  if (
    typeof policy !== "object" || policy === null ||
    typeof (policy as Record<string, unknown>).threshold !== "number"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: policy.threshold required" };
  }
  const p = policy as DeduplicationPolicy;
  const typed = candidates.filter(
    (c): c is Candidate =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  const seen = new Set<string>();
  const deduped: Candidate[] = [];
  for (const c of typed) {
    if (!seen.has(c.id)) {
      seen.add(c.id);
      deduped.push(c);
    }
  }
  return { ok: true, data: deduped };
}

// =============================================================================
// Category B: Normalize / Lookup / Import Functions (candidate_surface / readonly)
// external_lookup functions produce candidates only; canonical write requires confirm
// =============================================================================

export type AddressCandidate = {
  postalCode: string | null;
  prefecture?: string;
  city?: string;
  street?: string;
  canonical_authority: "candidate_surface_not_SSOT";
};

// mutation_boundary: readonly
// external_lookup: postal_address
// canonical_authority: candidate_surface_not_SSOT — no canonical write
export function resolvePostalAddress(
  postalCode: unknown,
): AbstractFunctionResult<AddressCandidate> {
  if (typeof postalCode !== "string" || postalCode.trim() === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: postalCode must be non-empty string" };
  }
  if (!/^\d{3}-?\d{4}$/.test(postalCode.trim())) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: postalCode format invalid" };
  }
  // candidate surface only; no canonical write
  return {
    ok: true,
    data: {
      postalCode: postalCode.trim(),
      canonical_authority: "candidate_surface_not_SSOT",
    },
  };
}

export type PostalCandidate = {
  address: string;
  postalCode: string | null;
  canonical_authority: "candidate_surface_not_SSOT";
};

// mutation_boundary: readonly
// external_lookup: address_postal
// canonical_authority: candidate_surface_not_SSOT — no canonical write
export function resolveAddressPostal(
  address: unknown,
): AbstractFunctionResult<PostalCandidate> {
  if (typeof address !== "string" || address.trim() === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: address must be non-empty string" };
  }
  // candidate surface only; no canonical write
  return {
    ok: true,
    data: {
      address: address.trim(),
      postalCode: null,
      canonical_authority: "candidate_surface_not_SSOT",
    },
  };
}

export type TelCandidate = Candidate & {
  tel: string | null;
  canonical_authority: "candidate_surface_not_SSOT";
  restriction: "internal_master_and_registered_data_only";
};

// mutation_boundary: readonly
// restriction: internal_master_and_registered_data_only
// canonical_authority: candidate_surface_not_SSOT — no canonical write
export function resolveTelCandidate(
  telOrAddress: unknown,
): AbstractFunctionResult<TelCandidate[]> {
  if (typeof telOrAddress !== "string" || telOrAddress.trim() === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: telOrAddress must be non-empty string" };
  }
  // candidate surface only from internal master; no external lookup write
  return {
    ok: true,
    data: [{
      id: `tel_candidate_${telOrAddress.trim()}`,
      label: telOrAddress.trim(),
      tel: telOrAddress.trim(),
      canonical_authority: "candidate_surface_not_SSOT",
      restriction: "internal_master_and_registered_data_only",
    }],
  };
}

// =============================================================================
// Category C: Candidate Update / Audit Functions
// All write operations follow:
//   preview_update_patch → validate_candidate → apply_confirmed_update → append_diff_log
//   No shortcut path bypasses validate_candidate
// =============================================================================

export type ManifestSchema = {
  required?: string[];
  fields?: Record<string, { type: string }>;
};

// mutation_boundary: readonly
export function validateCandidate(
  candidate: unknown,
  manifest: unknown,
): AbstractFunctionResult<ValidationResult> {
  if (typeof candidate !== "object" || candidate === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: candidate must be object" };
  }
  if (typeof manifest !== "object" || manifest === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: manifest must be object" };
  }
  const c = candidate as Record<string, unknown>;
  const m = manifest as ManifestSchema;
  const errors: ValidationError[] = [];
  if (Array.isArray(m.required)) {
    for (const field of m.required) {
      if (!(field in c) || c[field] === null || c[field] === undefined || c[field] === "") {
        errors.push({ field, message: `${field} is required`, code: "REQUIRED" });
      }
    }
  }
  if (m.fields) {
    for (const [field, def] of Object.entries(m.fields)) {
      if (field in c && c[field] !== null && c[field] !== undefined) {
        if (typeof c[field] !== def.type) {
          errors.push({
            field,
            message: `${field} must be ${def.type}`,
            code: "TYPE_MISMATCH",
          });
        }
      }
    }
  }
  return { ok: true, data: { valid: errors.length === 0, errors } };
}

export type PatchField = { fieldLabel: string; before: unknown; after: unknown };
export type PatchPreview = {
  target: string;
  fields: PatchField[];
  mutationType: "preview_only";
};

// mutation_boundary: readonly (preview_only)
// produces before/after diff without writing to DB
export function previewUpdatePatch(
  target: unknown,
  payload: unknown,
): AbstractFunctionResult<PatchPreview> {
  if (typeof target !== "string" || target === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: target must be non-empty string" };
  }
  if (typeof payload !== "object" || payload === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: payload must be object" };
  }
  const p = payload as Record<string, unknown>;
  const fields: PatchField[] = Object.entries(p).map(([key, after]) => ({
    fieldLabel: key,
    before: null,
    after,
  }));
  return { ok: true, data: { target, fields, mutationType: "preview_only" } };
}

// mutation_boundary: explicit_confirm_required
// prerequisite: validate_candidate must pass, confirmed must be true
export function applyConfirmedUpdate(
  target: unknown,
  payload: unknown,
  options: { confirmed: boolean },
): AbstractFunctionResult<MutationResult> {
  if (!options.confirmed) {
    return { ok: false, error: "ABSTRACT_FUNCTION_EXPLICIT_CONFIRM_REQUIRED: confirmed must be true" };
  }
  if (typeof target !== "string" || target === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: target must be non-empty string" };
  }
  if (typeof payload !== "object" || payload === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: payload must be object" };
  }
  return {
    ok: true,
    data: {
      mutationType: "update",
      target,
      status: "applied",
      timestamp: new Date().toISOString(),
    },
  };
}

// mutation_boundary: append_only
// must be called after apply_confirmed_update; never before
export function appendDiffLog(
  target: unknown,
  before: unknown,
  after: unknown,
  editor: unknown,
  context: { applyResult: MutationResult | null },
): AbstractFunctionResult<DiffLogEntry> {
  if (!context.applyResult || context.applyResult.status !== "applied") {
    return {
      ok: false,
      error: "ABSTRACT_FUNCTION_APPEND_AFTER_APPLY_REQUIRED: applyResult.status must be applied",
    };
  }
  if (typeof target !== "string" || target === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: target must be non-empty string" };
  }
  return {
    ok: true,
    data: {
      id: `diff_${context.applyResult.target}_${Date.now()}`,
      target,
      before,
      after,
      editor: typeof editor === "string" ? editor : "unknown",
      appendOnly: true,
      timestamp: new Date().toISOString(),
    },
  };
}

export type RollbackCandidate = Candidate & {
  diffId: string;
  rollbackPayload: unknown;
  requiresApplyConfirm: true;
};

// mutation_boundary: candidate_surface
// rollback requires apply_confirmed_update + append_diff_log
export function buildRollbackCandidate(
  diffId: unknown,
): AbstractFunctionResult<RollbackCandidate> {
  if (typeof diffId !== "string" || diffId === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: diffId must be non-empty string" };
  }
  return {
    ok: true,
    data: {
      id: `rollback_${diffId}`,
      label: `Rollback ${diffId}`,
      diffId,
      rollbackPayload: null,
      requiresApplyConfirm: true,
    },
  };
}

export type ConflictResolutionPolicy = {
  strategy: "local_wins" | "remote_wins" | "manual";
};

// mutation_boundary: candidate_surface
// resolved candidate still requires apply_confirmed_update
export function resolveConflictCandidate(
  conflict: unknown,
  resolutionPolicy: unknown,
): AbstractFunctionResult<Candidate> {
  if (
    typeof conflict !== "object" || conflict === null ||
    typeof (conflict as Record<string, unknown>).field !== "string"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: conflict.field required" };
  }
  if (
    typeof resolutionPolicy !== "object" || resolutionPolicy === null ||
    typeof (resolutionPolicy as Record<string, unknown>).strategy !== "string"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: resolutionPolicy.strategy required" };
  }
  const c = conflict as Record<string, unknown>;
  const p = resolutionPolicy as ConflictResolutionPolicy;
  const strategies = ["local_wins", "remote_wins", "manual"];
  if (!strategies.includes(p.strategy)) {
    return { ok: false, error: `ABSTRACT_FUNCTION_INVALID_INPUT: unknown strategy: ${p.strategy}` };
  }
  const resolvedValue = p.strategy === "local_wins"
    ? c.localValue
    : p.strategy === "remote_wins"
    ? c.remoteValue
    : null;
  return {
    ok: true,
    data: {
      id: `conflict_resolved_${c.field}`,
      label: `Resolved: ${c.field}`,
      resolvedField: c.field,
      resolvedValue,
      strategy: p.strategy,
    },
  };
}

// =============================================================================
// Category D: Calculation / Topology Functions (preview/candidate surface only)
// frontend does NOT own topology judgment or SQL Attention judgment
// SQL Attention functions are observation-only; no registry/route mutation
// =============================================================================

export type AttentionWeightResult = {
  target: string;
  weight: number | null;
  observationOnly: true;
  registryMutated: false;
};

// mutation_boundary: sql_attention_observation_only
// SQL Attention is observation layer; attention_weight does NOT auto-promote
export function calculateAttentionWeight(
  target: unknown,
  evidence: unknown,
): AbstractFunctionResult<AttentionWeightResult> {
  if (typeof target !== "string" || target === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: target must be non-empty string" };
  }
  if (typeof evidence !== "object" || evidence === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: evidence must be object" };
  }
  const ev = evidence as Record<string, unknown>;
  const weight = typeof ev.weight === "number" ? ev.weight : null;
  // observation only; registryMutated is always false
  return {
    ok: true,
    data: {
      target,
      weight,
      observationOnly: true,
      registryMutated: false,
    },
  };
}

export type RankScoreResult = RankedCandidate & { rankSource: "sql_attention_observation" };

// mutation_boundary: sql_attention_observation_only
// ranked list is candidate surface only; no auto-mutation of registry or route
export function calculateRankScore(
  candidates: unknown,
  policy: unknown,
): AbstractFunctionResult<RankScoreResult[]> {
  const rankResult = rankCandidateResults(candidates, policy);
  if (!rankResult.ok) return { ok: false, error: rankResult.error };
  return {
    ok: true,
    data: rankResult.data.map((c) => ({
      ...c,
      rankSource: "sql_attention_observation" as const,
    })),
  };
}

export type TopologyDistanceResult = {
  fromHub: string;
  toHub: string;
  distance: number | null;
  hopCount: number | null;
  status: "preview_only";
};

// mutation_boundary: readonly
// topology judgment is backend-owned; frontend defines boundary shape
export function calculateTopologyDistance(
  fromHub: unknown,
  toHub: unknown,
  context: unknown,
): AbstractFunctionResult<TopologyDistanceResult> {
  if (typeof fromHub !== "string" || fromHub === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: fromHub must be non-empty string" };
  }
  if (typeof toHub !== "string" || toHub === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: toHub must be non-empty string" };
  }
  if (typeof context !== "object" || context === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: context must be object" };
  }
  const ctx = context as Record<string, unknown>;
  return {
    ok: true,
    data: {
      fromHub,
      toHub,
      distance: typeof ctx.distance === "number" ? ctx.distance : null,
      hopCount: typeof ctx.hopCount === "number" ? ctx.hopCount : null,
      status: "preview_only",
    },
  };
}

export type RouteCostResult = {
  route: unknown;
  cost: number | null;
  status: "candidate_surface";
};

// mutation_boundary: readonly
export function calculateRouteCost(
  route: unknown,
  context: unknown,
): AbstractFunctionResult<RouteCostResult> {
  if (typeof route !== "object" || route === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: route must be object" };
  }
  if (typeof context !== "object" || context === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: context must be object" };
  }
  const ctx = context as Record<string, unknown>;
  return {
    ok: true,
    data: {
      route,
      cost: typeof ctx.cost === "number" ? ctx.cost : null,
      status: "candidate_surface",
    },
  };
}

export type FormulaManifest = {
  allowedOperators?: string[];
  maxDepth?: number;
};

// mutation_boundary: readonly
export function validateFormulaContract(
  formula: unknown,
  manifest: unknown,
): AbstractFunctionResult<ValidationResult> {
  if (typeof formula !== "object" || formula === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: formula must be object" };
  }
  if (typeof manifest !== "object" || manifest === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: manifest must be object" };
  }
  const f = formula as Record<string, unknown>;
  const m = manifest as FormulaManifest;
  const errors: ValidationError[] = [];
  if (typeof f.operator !== "string") {
    errors.push({ field: "operator", message: "operator required", code: "REQUIRED" });
  } else if (Array.isArray(m.allowedOperators) && !m.allowedOperators.includes(f.operator)) {
    errors.push({
      field: "operator",
      message: `operator "${f.operator}" not in allowedOperators`,
      code: "DISALLOWED_OPERATOR",
    });
  }
  return { ok: true, data: { valid: errors.length === 0, errors } };
}

// =============================================================================
// Category E: Layout / Design Token Functions (preview_validate_explicit_apply)
// =============================================================================

export type LayoutPreview = {
  layoutId: string;
  patch: unknown;
  mutationType: "preview_only";
};

// mutation_boundary: readonly (preview_only)
export function previewLayoutPatch(
  layoutId: unknown,
  patch: unknown,
  context: unknown,
): AbstractFunctionResult<LayoutPreview> {
  if (typeof layoutId !== "string" || layoutId === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: layoutId must be non-empty string" };
  }
  if (typeof patch !== "object" || patch === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: patch must be object" };
  }
  if (typeof context !== "object" || context === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: context must be object" };
  }
  return { ok: true, data: { layoutId, patch, mutationType: "preview_only" } };
}

export type LayoutRules = {
  maxColumns?: number;
  allowedPlacements?: string[];
};

// mutation_boundary: readonly
export function validateLayoutConstraint(
  layoutId: unknown,
  patch: unknown,
  rules: unknown,
): AbstractFunctionResult<ValidationResult> {
  if (typeof layoutId !== "string" || layoutId === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: layoutId must be non-empty string" };
  }
  if (typeof patch !== "object" || patch === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: patch must be object" };
  }
  if (typeof rules !== "object" || rules === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: rules must be object" };
  }
  const p = patch as Record<string, unknown>;
  const r = rules as LayoutRules;
  const errors: ValidationError[] = [];
  if (
    typeof r.maxColumns === "number" &&
    typeof p.columns === "number" &&
    p.columns > r.maxColumns
  ) {
    errors.push({
      field: "columns",
      message: `columns ${p.columns} exceeds maxColumns ${r.maxColumns}`,
      code: "CONSTRAINT_VIOLATION",
    });
  }
  return { ok: true, data: { valid: errors.length === 0, errors } };
}

export type LayoutMutationResult = {
  mutationType: "layout_apply";
  layoutId: string;
  status: "applied";
  timestamp: string;
};

// mutation_boundary: explicit_confirm_required
// prerequisite: validate_layout_constraint must pass, explicit admin confirmation required
export function applyConfirmedLayoutPatch(
  layoutId: unknown,
  patch: unknown,
  options: { confirmed: boolean },
): AbstractFunctionResult<LayoutMutationResult> {
  if (!options.confirmed) {
    return {
      ok: false,
      error: "ABSTRACT_FUNCTION_EXPLICIT_CONFIRM_REQUIRED: confirmed must be true",
    };
  }
  if (typeof layoutId !== "string" || layoutId === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: layoutId must be non-empty string" };
  }
  if (typeof patch !== "object" || patch === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: patch must be object" };
  }
  return {
    ok: true,
    data: {
      mutationType: "layout_apply",
      layoutId,
      status: "applied",
      timestamp: new Date().toISOString(),
    },
  };
}

export type StyleTokenRegistry = {
  tokens: Record<string, string>;
};

export type ResolvedStyleToken = {
  tokenId: string;
  resolvedValue: string;
};

// mutation_boundary: readonly
export function resolveStyleToken(
  tokenId: unknown,
  context: unknown,
): AbstractFunctionResult<ResolvedStyleToken> {
  if (typeof tokenId !== "string" || tokenId === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: tokenId must be non-empty string" };
  }
  if (typeof context !== "object" || context === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: context must be object" };
  }
  const ctx = context as Record<string, unknown>;
  const registry = (ctx.registry && typeof ctx.registry === "object")
    ? ctx.registry as StyleTokenRegistry
    : null;
  const resolvedValue = registry?.tokens[tokenId] ?? tokenId;
  return { ok: true, data: { tokenId, resolvedValue } };
}

export type ResponsiveRulePreview = {
  rule: unknown;
  viewport: string;
  applied: boolean;
  mutationType: "preview_only";
};

// mutation_boundary: readonly (preview_only)
export function previewResponsiveRule(
  rule: unknown,
  viewport: unknown,
): AbstractFunctionResult<ResponsiveRulePreview> {
  if (typeof rule !== "object" || rule === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: rule must be object" };
  }
  if (typeof viewport !== "string" || viewport === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: viewport must be non-empty string" };
  }
  const r = rule as Record<string, unknown>;
  const breakpoint = typeof r.breakpoint === "string" ? r.breakpoint : "";
  const applies = viewport === breakpoint || breakpoint === "";
  return {
    ok: true,
    data: { rule, viewport, applied: applies, mutationType: "preview_only" },
  };
}

export type PlacementConstraints = {
  allowedZones?: string[];
  maxPerZone?: number;
};

// mutation_boundary: readonly
export function validateComponentPlacement(
  component: unknown,
  layout: unknown,
  constraints: unknown,
): AbstractFunctionResult<ValidationResult> {
  if (
    typeof component !== "object" || component === null ||
    typeof (component as Record<string, unknown>).id !== "string"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: component.id required" };
  }
  if (typeof layout !== "object" || layout === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: layout must be object" };
  }
  if (typeof constraints !== "object" || constraints === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: constraints must be object" };
  }
  const c = component as Record<string, unknown>;
  const cnstr = constraints as PlacementConstraints;
  const errors: ValidationError[] = [];
  if (Array.isArray(cnstr.allowedZones) && typeof c.zone === "string") {
    if (!cnstr.allowedZones.includes(c.zone)) {
      errors.push({
        field: "zone",
        message: `zone "${c.zone}" not in allowedZones`,
        code: "ZONE_NOT_ALLOWED",
      });
    }
  }
  return { ok: true, data: { valid: errors.length === 0, errors } };
}

// =============================================================================
// Category F: Operation Safety Functions (preview_validate_explicit_apply)
// All destructive operations must pass dry_run and explicit confirm before execute
// =============================================================================

export type DryRunOperation = {
  operationKind: string;
  [key: string]: unknown;
};

export type DryRunResult = {
  operation: unknown;
  expectedMutations: string[];
  risks: string[];
  mutationType: "preview_only";
};

// mutation_boundary: readonly (preview_only)
export function dryRunOperation(
  operation: unknown,
  context: unknown,
): AbstractFunctionResult<DryRunResult> {
  if (
    typeof operation !== "object" || operation === null ||
    typeof (operation as Record<string, unknown>).operationKind !== "string"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: operation.operationKind required" };
  }
  if (typeof context !== "object" || context === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: context must be object" };
  }
  const op = operation as Record<string, unknown>;
  const ctx = context as Record<string, unknown>;
  const expectedMutations = Array.isArray(ctx.expectedMutations)
    ? ctx.expectedMutations.filter((m): m is string => typeof m === "string")
    : [];
  const risks = Array.isArray(ctx.risks)
    ? ctx.risks.filter((r): r is string => typeof r === "string")
    : [];
  return {
    ok: true,
    data: {
      operation,
      expectedMutations,
      risks,
      mutationType: "preview_only",
    },
  };
}

export type MutationBoundaryPolicy = {
  allowedBoundaries: string[];
};

export type BoundaryResult = {
  operation: unknown;
  crossesBoundary: boolean;
  violations: string[];
};

// mutation_boundary: readonly
export function validateMutationBoundary(
  operation: unknown,
  policy: unknown,
): AbstractFunctionResult<BoundaryResult> {
  if (
    typeof operation !== "object" || operation === null ||
    typeof (operation as Record<string, unknown>).operationKind !== "string"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: operation.operationKind required" };
  }
  if (
    typeof policy !== "object" || policy === null ||
    !Array.isArray((policy as Record<string, unknown>).allowedBoundaries)
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: policy.allowedBoundaries required" };
  }
  const op = operation as Record<string, unknown>;
  const pol = policy as MutationBoundaryPolicy;
  const violations: string[] = [];
  const opBoundary = typeof op.boundary === "string" ? op.boundary : "";
  if (opBoundary && !pol.allowedBoundaries.includes(opBoundary)) {
    violations.push(`boundary "${opBoundary}" not in allowedBoundaries`);
  }
  return {
    ok: true,
    data: {
      operation,
      crossesBoundary: violations.length > 0,
      violations,
    },
  };
}

export type RiskExplanation = {
  operation: unknown;
  riskLevel: "low" | "medium" | "high" | "unknown";
  description: string;
  affectedResources: string[];
};

// mutation_boundary: readonly
export function explainOperationRisk(
  operation: unknown,
  context: unknown,
): AbstractFunctionResult<RiskExplanation> {
  if (
    typeof operation !== "object" || operation === null ||
    typeof (operation as Record<string, unknown>).operationKind !== "string"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: operation.operationKind required" };
  }
  if (typeof context !== "object" || context === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: context must be object" };
  }
  const op = operation as Record<string, unknown>;
  const ctx = context as Record<string, unknown>;
  const rawLevel = ctx.riskLevel;
  const riskLevel: "low" | "medium" | "high" | "unknown" =
    rawLevel === "low" || rawLevel === "medium" || rawLevel === "high"
      ? rawLevel
      : "unknown";
  const affectedResources = Array.isArray(ctx.affectedResources)
    ? ctx.affectedResources.filter((r): r is string => typeof r === "string")
    : [];
  return {
    ok: true,
    data: {
      operation,
      riskLevel,
      description: typeof ctx.description === "string"
        ? ctx.description
        : `operation: ${op.operationKind}`,
      affectedResources,
    },
  };
}

export type PermissionResult = {
  actor: string;
  operation: unknown;
  target: unknown;
  permitted: boolean;
  reason?: string;
};

// mutation_boundary: readonly
export function checkPermissionForOperation(
  actor: unknown,
  operation: unknown,
  target: unknown,
): AbstractFunctionResult<PermissionResult> {
  if (typeof actor !== "string" || actor === "") {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: actor must be non-empty string" };
  }
  if (
    typeof operation !== "object" || operation === null ||
    typeof (operation as Record<string, unknown>).operationKind !== "string"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: operation.operationKind required" };
  }
  if (target === undefined || target === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: target required" };
  }
  const op = operation as Record<string, unknown>;
  const requiredRole = typeof op.requiredRole === "string" ? op.requiredRole : null;
  // permission check is observation only; no side effects
  const permitted = requiredRole === null || requiredRole === "any";
  return {
    ok: true,
    data: {
      actor,
      operation,
      target,
      permitted,
      reason: permitted ? undefined : `requires role: ${requiredRole}`,
    },
  };
}

export type ConfirmableOperation = {
  id: string;
  operation: unknown;
  dryRunResult: unknown;
  riskSummary: unknown;
  requiresExplicitConfirm: true;
  status: "pending_confirmation";
};

// mutation_boundary: candidate_surface
// confirmable_op requires explicit confirm before apply_confirmed_update
export function buildConfirmableOperation(
  operation: unknown,
  context: unknown,
): AbstractFunctionResult<ConfirmableOperation> {
  if (
    typeof operation !== "object" || operation === null ||
    typeof (operation as Record<string, unknown>).operationKind !== "string"
  ) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: operation.operationKind required" };
  }
  if (typeof context !== "object" || context === null) {
    return { ok: false, error: "ABSTRACT_FUNCTION_INVALID_INPUT: context must be object" };
  }
  const op = operation as Record<string, unknown>;
  const ctx = context as Record<string, unknown>;
  return {
    ok: true,
    data: {
      id: `confirmable_${op.operationKind}_${Date.now()}`,
      operation,
      dryRunResult: ctx.dryRunResult ?? null,
      riskSummary: ctx.riskSummary ?? null,
      requiresExplicitConfirm: true,
      status: "pending_confirmation",
    },
  };
}
