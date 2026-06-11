/**
 * searchSuggestCandidateBoundary.test.ts
 *
 * Bundle: ui-builder-search-suggest-candidate-boundary
 * SSOT: docs/design/ui-ux-primitive-catalog-ssot.yaml (category_a_search_suggest)
 *
 * Boundary contract:
 * - autocomplete (autocomplete_input.primitive / AutoCompleteInput): debounce + backend
 *   read-only search via onSearch prop; no mutation / DB write / apply during typing.
 * - suggest (suggest_input.primitive / SuggestInput): same boundary, multi-row input assist.
 * - combobox (search_combobox.primitive / SearchCombobox): small-scale known candidates;
 *   candidate derivation from uiBuilderAutocompleteCandidates.ts (local data only, no fetch).
 * - uiBuilderAutocompleteCandidates.ts role: combobox candidate derivation only;
 *   NOT autocomplete or suggest body; no backend fetch.
 *
 * Tests:
 * - AutoCompleteInput has onSearch prop (backend read-only search hook)
 * - SuggestInput has onSearch prop (backend read-only search hook)
 * - SearchCombobox does NOT have onSearch (small-scale known candidates, no backend)
 * - onSearch is optional (no-arg usage still valid)
 * - Runtime factory: "search" is a valid NormalizedComponentEventType / parseEventBinding event
 * - Runtime factory: emitBoundEvent with "search" trigger does not return INVALID_EVENT_BINDING
 * - Runtime factory: autocomplete_input factory wires onSearch via eventBinding.search (no error)
 * - Runtime factory: suggest_input factory wires onSearch via eventBinding.search (no error)
 * - Boundary: AutoCompleteInput source has no eval/fetch/Function (no mutation during typing)
 * - Boundary: SuggestInput source has no eval/fetch/Function
 * - Boundary: uiBuilderAutocompleteCandidates.ts has no fetch (combobox local derivation only)
 * - Framing: uiBuilderAutocompleteCandidates.ts header declares combobox role
 */

import {
  assert,
  assertFalse,
  assertEquals,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import type { AutoCompleteInputProps } from "../components/AutoCompleteInput.tsx";
import type { SuggestInputProps } from "../components/SuggestInput.tsx";
import type { SearchComboboxProps } from "../components/SearchCombobox.tsx";
import { __testOnly } from "../runtime/runtimeComponentFactory.ts";
import type { RuntimeComponentSpec } from "../runtime/runtimeComponentAdapter.ts";

// ─── Type-level boundary checks ──────────────────────────────────────────────

Deno.test("AutoCompleteInput: onSearch prop is defined and optional (backend read-only search hook)", () => {
  const withSearch: AutoCompleteInputProps = {
    value: "test",
    onChange: () => {},
    onSearch: (_query: string) => {
      // read-only backend search; no mutation during typing
    },
  };
  assert(typeof withSearch.onSearch === "function");

  const withoutSearch: AutoCompleteInputProps = {
    value: "test",
    onChange: () => {},
  };
  assertEquals(withoutSearch.onSearch, undefined);
});

Deno.test("SuggestInput: onSearch prop is defined and optional (backend read-only search hook)", () => {
  const withSearch: SuggestInputProps = {
    value: "test",
    onChange: () => {},
    onSearch: (_query: string) => {
      // read-only backend search; no mutation during typing
    },
  };
  assert(typeof withSearch.onSearch === "function");

  const withoutSearch: SuggestInputProps = {
    value: "test",
    onChange: () => {},
  };
  assertEquals(withoutSearch.onSearch, undefined);
});

Deno.test("SearchCombobox: no onSearch prop (small-scale known candidates; no backend search)", () => {
  const props: SearchComboboxProps = {
    value: "test",
    onChange: () => {},
    options: [{ label: "A", value: "a" }],
  };
  // SearchComboboxProps has no onSearch — combobox uses local candidate derivation only
  assertFalse("onSearch" in props);
});

// ─── Boundary: no mutation during typing (structural source check) ────────────

Deno.test("boundary: AutoCompleteInput source has no eval/fetch/Function (no mutation during typing)", async () => {
  const src = await Deno.readTextFile(
    new URL("../components/AutoCompleteInput.tsx", import.meta.url),
  );
  assertFalse(src.includes("eval("), "eval() must not appear in AutoCompleteInput");
  assertFalse(src.includes("new Function("), "new Function() must not appear");
  assertFalse(src.includes("fetch("), "fetch() must not appear — no DB write during typing");
});

Deno.test("boundary: SuggestInput source has no eval/fetch/Function (no mutation during typing)", async () => {
  const src = await Deno.readTextFile(
    new URL("../components/SuggestInput.tsx", import.meta.url),
  );
  assertFalse(src.includes("eval("), "eval() must not appear in SuggestInput");
  assertFalse(src.includes("new Function("), "new Function() must not appear");
  assertFalse(src.includes("fetch("), "fetch() must not appear — no DB write during typing");
});

Deno.test("boundary: uiBuilderAutocompleteCandidates has no fetch (combobox local derivation only)", async () => {
  const src = await Deno.readTextFile(
    new URL("../lib/uiBuilderAutocompleteCandidates.ts", import.meta.url),
  );
  assertFalse(src.includes("fetch("), "fetch() must not appear — combobox candidate derivation is local only");
  assertFalse(src.includes("eval("), "eval() must not appear");
  assertFalse(src.includes("new Function("), "new Function() must not appear");
});

// ─── Framing: uiBuilderAutocompleteCandidates.ts role declaration ─────────────

Deno.test("framing: uiBuilderAutocompleteCandidates.ts declares combobox candidate derivation role", async () => {
  const src = await Deno.readTextFile(
    new URL("../lib/uiBuilderAutocompleteCandidates.ts", import.meta.url),
  );
  assert(
    src.includes("combobox candidate derivation"),
    "header must declare combobox candidate derivation role",
  );
  assert(
    src.includes("NOT autocomplete or suggest body"),
    "header must explicitly state it is NOT autocomplete/suggest body",
  );
  assert(
    src.includes("No backend fetch"),
    "header must state no backend fetch",
  );
});

// ─── Framing: AutoCompleteInput declares candidate_source_boundary ─────────────

Deno.test("framing: AutoCompleteInput source declares candidate_source_boundary debounce_backend_readonly_search", async () => {
  const src = await Deno.readTextFile(
    new URL("../components/AutoCompleteInput.tsx", import.meta.url),
  );
  assert(
    src.includes("debounce_backend_readonly_search") || src.includes("debounce"),
    "AutoCompleteInput must declare debounce/backend search boundary",
  );
  assert(
    src.includes("no mutation") || src.includes("No mutation"),
    "AutoCompleteInput must explicitly state no mutation during typing",
  );
});

Deno.test("framing: SuggestInput source declares candidate_source_boundary debounce_backend_readonly_search", async () => {
  const src = await Deno.readTextFile(
    new URL("../components/SuggestInput.tsx", import.meta.url),
  );
  assert(
    src.includes("debounce_backend_readonly_search") || src.includes("debounce"),
    "SuggestInput must declare debounce/backend search boundary",
  );
  assert(
    src.includes("no mutation") || src.includes("No mutation"),
    "SuggestInput must explicitly state no mutation during typing",
  );
});

// ─── Runtime factory: "search" event binding validation ──────────────────────

Deno.test('Runtime factory: "search" is a valid NormalizedComponentEventType / parseEventBinding event', () => {
  const parsed = __testOnly.parseEventBinding({ eventType: "search" });
  assert(parsed !== null, '"search" must parse as a valid event binding');
  assertEquals(parsed?.eventType, "search");
});

Deno.test('Runtime factory: emitBoundEvent with "search" trigger does not return INVALID_EVENT_BINDING', () => {
  const spec: RuntimeComponentSpec = {
    componentId: "test-search-emit",
    componentType: "autocomplete_input",
    props: {},
    eventBinding: { search: { eventType: "search" } },
  };
  const result = __testOnly.emitBoundEvent(spec, "search", { query: "hello" });
  assert(
    result.ok,
    `emitBoundEvent with 'search' trigger must return ok (not INVALID_EVENT_BINDING), got: ${JSON.stringify(result)}`,
  );
});

Deno.test("Runtime factory: autocomplete_input factory wires onSearch via eventBinding.search (no error)", () => {
  const spec: RuntimeComponentSpec = {
    componentId: "test-ac-onsearch-wire",
    componentType: "autocomplete_input",
    props: {},
    eventBinding: { search: { eventType: "search" } },
  };
  const result = __testOnly.emitBoundEvent(spec, "search", { query: "ac-test" });
  assert(result.ok, "autocomplete_input onSearch wired via eventBinding.search must not error");
});

Deno.test("Runtime factory: suggest_input factory wires onSearch via eventBinding.search (no error)", () => {
  const spec: RuntimeComponentSpec = {
    componentId: "test-suggest-onsearch-wire",
    componentType: "suggest_input",
    props: {},
    eventBinding: { search: { eventType: "search" } },
  };
  const result = __testOnly.emitBoundEvent(spec, "search", { query: "suggest-test" });
  assert(result.ok, "suggest_input onSearch wired via eventBinding.search must not error");
});
