/**
 * UIBuilder combobox candidate derivation helpers.
 *
 * Role: combobox candidate derivation — NOT autocomplete or suggest body.
 * These helpers serve search_combobox.primitive / SearchCombobox.tsx (small-scale, known
 * candidates derived from local in-memory data). They do NOT implement the
 * autocomplete_input.primitive or suggest_input.primitive backend search path.
 *
 * Candidate boundary: autocomplete and suggest allow debounce + backend read-only search
 * via onSearch prop (see AutoCompleteInput.tsx / SuggestInput.tsx).
 * These helpers produce local-derived candidates only — no backend fetch, no mutation.
 *
 * SSOT: docs/design/admin-console-workflow-ssot.yaml
 *   - layout_node_props_contract (propBindings.source, emission.data.* paths)
 *   - frontend_local_derived_calculation_binding (ruleTable, emission, node sources)
 * SSOT: docs/design/pipeline-continuity-ssot.yaml
 *   - frontend_projection_constructor_lane (componentKey/componentKind identity)
 *   - component_wiring_execution_lane (targetRef, routeKey, wiringKind)
 *
 * No backend fetch — candidates derive only from already-loaded data:
 *   - COMPONENT_CATALOG_ENTRIES (static registry)
 *   - draftNodes (current canvas draft)
 *   - emissionDataJson (already-loaded reference data in LocalCalcBindingPanel)
 *   - layoutCandidates (already-loaded from backend)
 */

import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";

// ─── Types ────────────────────────────────────────────────────────────────────

/** Minimal draft node shape needed for candidate derivation. */
export type DraftNodeMinimal = {
  nodeId: string;
  componentKey?: string;
  componentKind?: string;
  nodeKind?: string;
  htmlTag?: string;
};

/** Minimal layout candidate shape for routeKey derivation. */
export type LayoutCandidateMinimal = {
  routeKey: string;
  layoutId?: string;
};

/** Single emission path candidate. */
export type EmissionPathCandidate = {
  path: string;
  /** true when the resolved value at this path is an array. */
  isArray: boolean;
};

// ─── Emission path derivation ─────────────────────────────────────────────────

/**
 * Derive emission.data.* dotted path candidates from already-loaded emissionDataJson.
 * Uses only the currently available reference data — no backend fetch.
 *
 * SSOT: frontend_local_derived_calculation_binding.reference_data_contract
 * Returns array paths and scalar paths, distinguished by `isArray`.
 */
export function deriveEmissionPathCandidates(
  emissionDataJson: string,
): { ok: true; candidates: EmissionPathCandidate[] } | { ok: false; reason: string } {
  if (!emissionDataJson.trim()) {
    return {
      ok: false,
      reason:
        "参照データ (emission.data.*) が未入力です。" +
        "ローカル計算 > 参照データに JSON を入力してください。",
    };
  }
  let data: unknown;
  try {
    data = JSON.parse(emissionDataJson);
  } catch {
    return { ok: false, reason: "参照データの JSON parse に失敗しました。" };
  }
  if (typeof data !== "object" || data === null || Array.isArray(data)) {
    return {
      ok: false,
      reason: "参照データが object でありません。トップレベルは object JSON にしてください。",
    };
  }
  const candidates: EmissionPathCandidate[] = [];
  collectEmissionPaths(
    data as Record<string, unknown>,
    "emission.data",
    candidates,
    0,
  );
  if (candidates.length === 0) {
    return { ok: false, reason: "参照データからパス候補が見つかりませんでした。" };
  }
  return { ok: true, candidates };
}

function collectEmissionPaths(
  obj: Record<string, unknown>,
  prefix: string,
  out: EmissionPathCandidate[],
  depth: number,
): void {
  if (depth > 4) return;
  for (const [key, value] of Object.entries(obj)) {
    const path = `${prefix}.${key}`;
    const isArray = Array.isArray(value);
    out.push({ path, isArray });
    if (
      typeof value === "object" &&
      value !== null &&
      !isArray &&
      depth < 4
    ) {
      collectEmissionPaths(
        value as Record<string, unknown>,
        path,
        out,
        depth + 1,
      );
    }
  }
}

// ─── ruleTable selectedField candidate derivation ─────────────────────────────

/**
 * Derive ruleTable selectedField candidates from tablePath + emissionDataJson.
 * Resolves tablePath to array, then returns keys of the first object element.
 *
 * SSOT: frontend_local_derived_calculation_binding.calculation_binding_model.ruleTable_source
 */
export function deriveRuleTableFieldCandidates(
  tablePath: string,
  emissionDataJson: string,
): { ok: true; fields: string[] } | { ok: false; reason: string } {
  if (!tablePath.trim()) {
    return { ok: false, reason: "tablePath が未入力です。" };
  }
  if (!emissionDataJson.trim()) {
    return {
      ok: false,
      reason:
        "参照データが未入力です。ローカル計算 > 参照データに JSON を入力してください。",
    };
  }
  let data: unknown;
  try {
    data = JSON.parse(emissionDataJson);
  } catch {
    return { ok: false, reason: "参照データの JSON parse に失敗しました。" };
  }
  if (typeof data !== "object" || data === null || Array.isArray(data)) {
    return { ok: false, reason: "参照データが object でありません。" };
  }
  const resolved = resolveEmissionDataPath(
    data as Record<string, unknown>,
    tablePath,
  );
  if (!Array.isArray(resolved)) {
    return {
      ok: false,
      reason: `tablePath "${tablePath}" が配列に解決されません。配列パスを指定してください。`,
    };
  }
  if (resolved.length === 0) {
    return {
      ok: false,
      reason: `"${tablePath}" の配列が空です。行データが必要です。`,
    };
  }
  const first = resolved[0];
  if (typeof first !== "object" || first === null || Array.isArray(first)) {
    return {
      ok: false,
      reason: `"${tablePath}" の配列要素が object でありません。object 配列が必要です。`,
    };
  }
  const fields = Object.keys(first as Record<string, unknown>);
  if (fields.length === 0) {
    return { ok: false, reason: "最初の行にフィールドがありません。" };
  }
  return { ok: true, fields };
}

function resolveEmissionDataPath(
  data: Record<string, unknown>,
  path: string,
): unknown {
  const parts = path.startsWith("emission.data.")
    ? path.slice("emission.data.".length).split(".")
    : path.split(".");
  let cur: unknown = data;
  for (const part of parts) {
    if (!part) continue;
    if (typeof cur !== "object" || cur === null) return undefined;
    cur = (cur as Record<string, unknown>)[part];
  }
  return cur;
}

// ─── Node candidate derivation ────────────────────────────────────────────────

/**
 * Derive human-readable node candidates from draftNodes.
 * Structural HTML nodes display as `<tag>`, catalog components as `componentKey / nodeId…`.
 *
 * SSOT: ui_builder_canvas_workspace.node_kind_contract
 */
export function deriveNodeCandidates(
  draftNodes: DraftNodeMinimal[],
): Array<{ nodeId: string; label: string }> {
  return draftNodes.map((n) => {
    let label: string;
    if (n.nodeKind === "structural_html" && n.htmlTag) {
      label = `<${n.htmlTag}> / ${n.nodeId.slice(0, 9)}…`;
    } else {
      const slug = n.componentKey
        ? (n.componentKey.split("/").pop() ?? n.componentKey)
        : (n.componentKind ?? n.nodeId.slice(0, 9));
      label = `${slug} / ${n.nodeId.slice(0, 9)}…`;
    }
    return { nodeId: n.nodeId, label };
  });
}

// ─── componentKey / componentKind candidate derivation ────────────────────────

/**
 * Derive componentKey candidates from COMPONENT_CATALOG_ENTRIES + current draftNodes.
 * SSOT: frontend_projection_constructor_lane (componentKey identity preservation)
 */
export function deriveComponentKeyCandidates(
  draftNodes: DraftNodeMinimal[],
): Array<{ value: string; source: "catalog" | "draft" }> {
  const out: Array<{ value: string; source: "catalog" | "draft" }> = [];
  const seen = new Set<string>();
  for (const entry of COMPONENT_CATALOG_ENTRIES) {
    if (seen.has(entry.componentKey)) continue;
    seen.add(entry.componentKey);
    out.push({ value: entry.componentKey, source: "catalog" });
  }
  for (const n of draftNodes) {
    if (!n.componentKey || seen.has(n.componentKey)) continue;
    seen.add(n.componentKey);
    out.push({ value: n.componentKey, source: "draft" });
  }
  return out;
}

/**
 * Derive componentKind candidates from catalog entries + draftNodes.
 * SSOT: frontend_projection_constructor_lane (component_kind identity)
 */
export function deriveComponentKindCandidates(
  draftNodes: DraftNodeMinimal[],
): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const entry of COMPONENT_CATALOG_ENTRIES) {
    if (!entry.componentKind || seen.has(entry.componentKind)) continue;
    seen.add(entry.componentKind);
    out.push(entry.componentKind);
  }
  for (const n of draftNodes) {
    if (!n.componentKind || seen.has(n.componentKind)) continue;
    seen.add(n.componentKind);
    out.push(n.componentKind);
  }
  return out;
}

// ─── routeKey / targetRef candidate derivation ───────────────────────────────

/**
 * Derive routeKey candidates from layout candidates.
 * SSOT: component_wiring_execution_lane.required_identity.target / targetRef
 */
export function deriveRouteKeyCandidates(
  layoutCandidates: LayoutCandidateMinimal[],
): string[] {
  return [...new Set(layoutCandidates.map((c) => c.routeKey))].sort();
}
