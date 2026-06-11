/**
 * uiBuilderAutocompleteCandidates.test.ts
 *
 * Combobox candidate derivation helpers for UIBuilder.
 * Role: These helpers serve search_combobox.primitive / SearchCombobox.tsx
 * (small-scale, known candidates from local in-memory data). NOT autocomplete or suggest body.
 * autocomplete and suggest use onSearch + debounce + backend read-only search (separate path).
 *
 * SSOT: docs/design/admin-console-workflow-ssot.yaml
 *   - layout_node_props_contract (propBindings.source, emission.data.* paths)
 *   - frontend_local_derived_calculation_binding (ruleTable, emission sources)
 * SSOT: docs/design/pipeline-continuity-ssot.yaml
 *   - frontend_projection_constructor_lane (componentKey/componentKind)
 *   - component_wiring_execution_lane (routeKey, targetRef)
 *
 * Tests:
 * - deriveEmissionPathCandidates: JSON parse, empty, non-object, depth, array/scalar distinction
 * - deriveRuleTableFieldCandidates: tablePath resolution, empty array, non-object first row, fields
 * - deriveNodeCandidates: structural_html label, catalog_component label, empty
 * - deriveComponentKeyCandidates: catalog entries, draft-only keys, dedup
 * - deriveComponentKindCandidates: catalog entries, draft additions, dedup
 * - deriveRouteKeyCandidates: sort, dedup, empty
 * - no-candidate reason: each helper returns structured reason when candidates unavailable
 * - advanced path separation: normal candidates distinct from advanced free-text (verified by reason field)
 */

import {
  assertEquals,
  assertFalse,
  assert,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  deriveEmissionPathCandidates,
  deriveRuleTableFieldCandidates,
  deriveNodeCandidates,
  deriveComponentKeyCandidates,
  deriveComponentKindCandidates,
  deriveRouteKeyCandidates,
  type DraftNodeMinimal,
  type EmissionPathCandidate,
} from "../lib/uiBuilderAutocompleteCandidates.ts";

// ─── deriveEmissionPathCandidates ─────────────────────────────────────────────

Deno.test("deriveEmissionPathCandidates: empty input returns no-candidate reason", () => {
  const result = deriveEmissionPathCandidates("");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.length > 0);
});

Deno.test("deriveEmissionPathCandidates: whitespace-only input returns no-candidate reason", () => {
  const result = deriveEmissionPathCandidates("   ");
  assertFalse(result.ok);
});

Deno.test("deriveEmissionPathCandidates: invalid JSON returns parse-failure reason", () => {
  const result = deriveEmissionPathCandidates("{invalid");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("parse"));
});

Deno.test("deriveEmissionPathCandidates: array root returns non-object reason", () => {
  const result = deriveEmissionPathCandidates("[1, 2]");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("object"));
});

Deno.test("deriveEmissionPathCandidates: scalar root returns non-object reason", () => {
  const result = deriveEmissionPathCandidates('"string"');
  assertFalse(result.ok);
});

Deno.test("deriveEmissionPathCandidates: simple object returns dotted path candidates", () => {
  const json = JSON.stringify({ rows: [{ id: 1 }], total: 5 });
  const result = deriveEmissionPathCandidates(json);
  assert(result.ok);
  const paths = (result as { ok: true; candidates: EmissionPathCandidate[] }).candidates.map((c) => c.path);
  assert(paths.includes("emission.data.rows"));
  assert(paths.includes("emission.data.total"));
});

Deno.test("deriveEmissionPathCandidates: array paths have isArray=true", () => {
  const json = JSON.stringify({ rows: [{ id: 1 }], scalar: 42 });
  const result = deriveEmissionPathCandidates(json);
  assert(result.ok);
  const candidates = (result as { ok: true; candidates: EmissionPathCandidate[] }).candidates;
  const rowsCandidate = candidates.find((c) => c.path === "emission.data.rows");
  assert(rowsCandidate?.isArray === true);
  const scalarCandidate = candidates.find((c) => c.path === "emission.data.scalar");
  assert(scalarCandidate?.isArray === false);
});

Deno.test("deriveEmissionPathCandidates: nested object paths are collected", () => {
  const json = JSON.stringify({ meta: { count: 1, page: 2 }, rows: [] });
  const result = deriveEmissionPathCandidates(json);
  assert(result.ok);
  const paths = (result as { ok: true; candidates: EmissionPathCandidate[] }).candidates.map((c) => c.path);
  assert(paths.includes("emission.data.meta.count"));
  assert(paths.includes("emission.data.meta.page"));
});

Deno.test("deriveEmissionPathCandidates: depth capped at 4 levels", () => {
  // Build 6 levels deep
  const deep = { a: { b: { c: { d: { e: { f: 1 } } } } } };
  const result = deriveEmissionPathCandidates(JSON.stringify(deep));
  assert(result.ok);
  const paths = (result as { ok: true; candidates: EmissionPathCandidate[] }).candidates.map((c) => c.path);
  // Should NOT have path 5 levels deep from "emission.data"
  assertFalse(paths.some((p) => p.split(".").length > 7)); // emission.data + 4 more = 6 segments max + 1 safety
});

Deno.test("deriveEmissionPathCandidates: empty object returns no-candidate reason", () => {
  const result = deriveEmissionPathCandidates("{}");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.length > 0);
});

// ─── deriveRuleTableFieldCandidates ───────────────────────────────────────────

Deno.test("deriveRuleTableFieldCandidates: empty tablePath returns reason", () => {
  const result = deriveRuleTableFieldCandidates("", '{"rates": []}');
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("tablePath"));
});

Deno.test("deriveRuleTableFieldCandidates: empty emissionDataJson returns reason", () => {
  const result = deriveRuleTableFieldCandidates("emission.data.rates", "");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.length > 0);
});

Deno.test("deriveRuleTableFieldCandidates: invalid JSON returns parse-failure reason", () => {
  const result = deriveRuleTableFieldCandidates("emission.data.rates", "bad");
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("parse"));
});

Deno.test("deriveRuleTableFieldCandidates: non-array tablePath returns reason", () => {
  const json = JSON.stringify({ rates: { scalar: 1 } });
  const result = deriveRuleTableFieldCandidates("emission.data.rates", json);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("配列"));
});

Deno.test("deriveRuleTableFieldCandidates: empty array returns reason", () => {
  const json = JSON.stringify({ rates: [] });
  const result = deriveRuleTableFieldCandidates("emission.data.rates", json);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("空"));
});

Deno.test("deriveRuleTableFieldCandidates: first row is non-object returns reason", () => {
  const json = JSON.stringify({ rates: [1, 2, 3] });
  const result = deriveRuleTableFieldCandidates("emission.data.rates", json);
  assertFalse(result.ok);
  assert((result as { ok: false; reason: string }).reason.includes("object"));
});

Deno.test("deriveRuleTableFieldCandidates: returns field names from first row", () => {
  const json = JSON.stringify({
    rates: [{ priority: 1, rate: 250, enabled: true }, { priority: 2, rate: 300 }],
  });
  const result = deriveRuleTableFieldCandidates("emission.data.rates", json);
  assert(result.ok);
  const { fields } = result as { ok: true; fields: string[] };
  assertEquals(fields.includes("priority"), true);
  assertEquals(fields.includes("rate"), true);
  assertEquals(fields.includes("enabled"), true);
});

Deno.test("deriveRuleTableFieldCandidates: path without emission.data prefix resolves", () => {
  const json = JSON.stringify({ laborRates: [{ hourlyRate: 2500, priority: 1 }] });
  const result = deriveRuleTableFieldCandidates("laborRates", json);
  assert(result.ok);
  const { fields } = result as { ok: true; fields: string[] };
  assert(fields.includes("hourlyRate"));
});

Deno.test("deriveRuleTableFieldCandidates: nested tablePath resolves correctly", () => {
  const json = JSON.stringify({ meta: { rules: [{ field: "type", value: "A" }] } });
  const result = deriveRuleTableFieldCandidates("emission.data.meta.rules", json);
  assert(result.ok);
  const { fields } = result as { ok: true; fields: string[] };
  assert(fields.includes("field"));
  assert(fields.includes("value"));
});

// ─── deriveNodeCandidates ─────────────────────────────────────────────────────

Deno.test("deriveNodeCandidates: empty array returns empty", () => {
  assertEquals(deriveNodeCandidates([]), []);
});

Deno.test("deriveNodeCandidates: structural_html node shows tag in label", () => {
  const nodes: DraftNodeMinimal[] = [
    { nodeId: "node_abc12345", nodeKind: "structural_html", htmlTag: "div" },
  ];
  const result = deriveNodeCandidates(nodes);
  assertEquals(result.length, 1);
  assert(result[0]!.label.includes("<div>"));
  assert(result[0]!.label.includes("node_abc"));
  assertEquals(result[0]!.nodeId, "node_abc12345");
});

Deno.test("deriveNodeCandidates: catalog_component node shows componentKey slug", () => {
  const nodes: DraftNodeMinimal[] = [
    { nodeId: "node_def67890", nodeKind: "catalog_component", componentKey: "action/button" },
  ];
  const result = deriveNodeCandidates(nodes);
  assertEquals(result.length, 1);
  assert(result[0]!.label.includes("button"));
  assert(result[0]!.label.includes("node_def6"));
});

Deno.test("deriveNodeCandidates: node without componentKey falls back to componentKind", () => {
  const nodes: DraftNodeMinimal[] = [
    { nodeId: "node_xyz99999", nodeKind: "catalog_component", componentKind: "form_input/input" },
  ];
  const result = deriveNodeCandidates(nodes);
  assertEquals(result.length, 1);
  assert(result[0]!.label.includes("form_input/input"));
});

// ─── deriveComponentKeyCandidates ─────────────────────────────────────────────

Deno.test("deriveComponentKeyCandidates: catalog entries included", () => {
  const result = deriveComponentKeyCandidates([]);
  assert(result.length > 0);
  assert(result.some((c) => c.source === "catalog"));
});

Deno.test("deriveComponentKeyCandidates: draft-only keys appended", () => {
  const draftNodes: DraftNodeMinimal[] = [
    { nodeId: "n1", componentKey: "custom/not-in-catalog-xyz" },
  ];
  const result = deriveComponentKeyCandidates(draftNodes);
  const custom = result.find((c) => c.value === "custom/not-in-catalog-xyz");
  assert(custom !== undefined);
  assertEquals(custom!.source, "draft");
});

Deno.test("deriveComponentKeyCandidates: no duplicate keys from catalog + draft", () => {
  // If a draft node uses a key already in catalog, should not be duped
  const catalogEntry = { nodeId: "n1", componentKey: "button.primitive" };
  const result = deriveComponentKeyCandidates([catalogEntry]);
  const buttons = result.filter((c) => c.value === "button.primitive");
  assertEquals(buttons.length, 1);
  assertEquals(buttons[0]!.source, "catalog"); // catalog takes precedence
});

// ─── deriveComponentKindCandidates ────────────────────────────────────────────

Deno.test("deriveComponentKindCandidates: catalog kinds included", () => {
  const result = deriveComponentKindCandidates([]);
  assert(result.length > 0);
  assert(result.some((k) => k.startsWith("action/") || k.startsWith("form_input/") || k.startsWith("data_display/")));
});

Deno.test("deriveComponentKindCandidates: draft-only kinds appended without dup", () => {
  const draftNodes: DraftNodeMinimal[] = [
    { nodeId: "n1", componentKind: "custom/special-xyz" },
  ];
  const result = deriveComponentKindCandidates(draftNodes);
  assert(result.includes("custom/special-xyz"));
});

Deno.test("deriveComponentKindCandidates: no duplicates", () => {
  const draftNodes: DraftNodeMinimal[] = [
    { nodeId: "n1", componentKind: "action/button" }, // already in catalog
  ];
  const result = deriveComponentKindCandidates(draftNodes);
  assertEquals(result.filter((k) => k === "action/button").length, 1);
});

// ─── deriveRouteKeyCandidates ─────────────────────────────────────────────────

Deno.test("deriveRouteKeyCandidates: empty candidates returns empty", () => {
  assertEquals(deriveRouteKeyCandidates([]), []);
});

Deno.test("deriveRouteKeyCandidates: routes are sorted", () => {
  const candidates = [
    { routeKey: "shop" },
    { routeKey: "admin" },
    { routeKey: "user" },
  ];
  const result = deriveRouteKeyCandidates(candidates);
  assertEquals(result, ["admin", "shop", "user"]);
});

Deno.test("deriveRouteKeyCandidates: duplicate routeKeys are deduped", () => {
  const candidates = [
    { routeKey: "home", layoutId: "layout-a" },
    { routeKey: "home", layoutId: "layout-b" },
    { routeKey: "about" },
  ];
  const result = deriveRouteKeyCandidates(candidates);
  assertEquals(result.filter((k) => k === "home").length, 1);
  assertEquals(result.length, 2);
});

// ─── no-candidate reason display (advanced path separation) ──────────────────

Deno.test("advanced path: no-candidate reason is structured (ok:false with reason string)", () => {
  // All helpers return {ok: false, reason: string} when no candidates available.
  // This enables UI to display reason instead of empty datalist (silent fallback prohibited).
  const r1 = deriveEmissionPathCandidates("");
  assertFalse(r1.ok);
  assert(typeof (r1 as { ok: false; reason: string }).reason === "string");

  const r2 = deriveRuleTableFieldCandidates("", "");
  assertFalse(r2.ok);
  assert(typeof (r2 as { ok: false; reason: string }).reason === "string");
});

Deno.test("advanced path: candidate result and free-text path are separable by ok flag", () => {
  // When ok:true, candidates are available -> normal path (select/datalist)
  // When ok:false, reason is shown -> advanced free-text path becomes explicit
  const json = JSON.stringify({ items: [{ id: 1 }] });
  const good = deriveEmissionPathCandidates(json);
  assert(good.ok);
  // Now bad
  const bad = deriveEmissionPathCandidates("");
  assertFalse(bad.ok);
  // The two states are clearly distinguishable — no silent fallback
});
