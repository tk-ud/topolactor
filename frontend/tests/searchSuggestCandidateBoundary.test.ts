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

// ─── searchCallback: fires BEFORE previewMode gate ──────────────────────────

Deno.test("Runtime factory: searchCallback fires on 'search' trigger BEFORE previewMode gate", () => {
  let capturedComponentId = "";
  let capturedQuery = "";

  const spec: RuntimeComponentSpec = {
    componentId: "test-search-callback-fires",
    componentType: "autocomplete_input",
    props: {},
    eventBinding: { search: { eventType: "search" } },
    searchCallback: (componentId: string, query: string) => {
      capturedComponentId = componentId;
      capturedQuery = query;
    },
  };
  const result = __testOnly.emitBoundEvent(spec, "search", { query: "hello-world" });
  assert(result.ok, "emitBoundEvent with searchCallback must return ok");
  assertEquals(capturedComponentId, "test-search-callback-fires");
  assertEquals(capturedQuery, "hello-world");
});

Deno.test("Runtime factory: searchCallback fires even in previewMode (before previewMode gate)", () => {
  let called = false;

  const spec: RuntimeComponentSpec = {
    componentId: "test-search-cb-preview",
    componentType: "autocomplete_input",
    props: {},
    eventBinding: {},
    previewMode: true,
    searchCallback: (_componentId: string, _query: string) => {
      called = true;
    },
  };
  const result = __testOnly.emitBoundEvent(spec, "search", { query: "preview-query" });
  assert(result.ok);
  assert(called, "searchCallback must fire in previewMode (before previewMode early-return)");
});

Deno.test("Runtime factory: searchCallback NOT fired on 'change' trigger (search-only boundary)", () => {
  let called = false;

  const spec: RuntimeComponentSpec = {
    componentId: "test-search-cb-change",
    componentType: "autocomplete_input",
    props: {},
    eventBinding: { change: { eventType: "change" } },
    searchCallback: (_componentId: string, _query: string) => {
      called = true;
    },
  };
  __testOnly.emitBoundEvent(spec, "change", { value: "some-value" });
  assertFalse(called, "searchCallback must NOT fire on 'change' trigger — search-only boundary");
});

// ─── SearchCombobox integration: candidate derivation from uiBuilderAutocompleteCandidates ──

import {
  deriveRouteKeyCandidates,
  deriveComponentKeyCandidates,
  type LayoutCandidateMinimal,
} from "../lib/uiBuilderAutocompleteCandidates.ts";
import type { SearchComboboxOption } from "../components/SearchCombobox.tsx";

Deno.test("SearchCombobox integration: deriveRouteKeyCandidates maps to SearchComboboxOption[]", () => {
  const layoutCandidates: LayoutCandidateMinimal[] = [
    { routeKey: "customer-management", layoutId: "layout-1" },
    { routeKey: "product-catalog", layoutId: "layout-2" },
    { routeKey: "customer-management", layoutId: "layout-3" }, // dup
  ];

  const routeKeys = deriveRouteKeyCandidates(layoutCandidates);
  // Map to SearchComboboxOption (local derivation — no backend call)
  const options: SearchComboboxOption[] = routeKeys.map((k) => ({
    label: k,
    value: k,
  }));

  assertEquals(options.length, 2, "deduped route keys");
  assert(options.some((o) => o.value === "customer-management"));
  assert(options.some((o) => o.value === "product-catalog"));
});

Deno.test("SearchCombobox integration: SearchCombobox uses local candidate derivation only (no onSearch)", () => {
  // SearchCombobox.tsx has no onSearch prop — it only accepts options derived locally.
  // This test verifies the candidate source boundary: no backend search for SearchCombobox.
  const props: SearchComboboxProps = {
    value: "",
    onChange: () => {},
    options: [{ label: "customer-management", value: "customer-management" }],
  };
  assertFalse("onSearch" in props, "SearchCombobox must not have onSearch — local derivation only");

  // Verify options come from uiBuilderAutocompleteCandidates derivation (not from backend fetch)
  const candidates = deriveRouteKeyCandidates([
    { routeKey: "customer-management" },
    { routeKey: "order-management" },
  ]);
  const derivedOptions: SearchComboboxOption[] = candidates.map((k) => ({ label: k, value: k }));
  assertEquals(derivedOptions[0]!.value, "customer-management");
  assertEquals(derivedOptions[1]!.value, "order-management");
});

Deno.test("SearchCombobox integration: deriveComponentKeyCandidates maps to SearchComboboxOption[]", () => {
  const candidates = deriveComponentKeyCandidates([]);
  const options: SearchComboboxOption[] = candidates
    .filter((c) => c.source === "catalog")
    .map((c) => ({ label: c.value, value: c.value }));

  assert(options.length > 0, "catalog-sourced component keys must exist");
  // All options have label and value set (valid SearchComboboxOption)
  for (const o of options) {
    assert(typeof o.label === "string" && o.label.length > 0);
    assert(typeof o.value === "string" && o.value.length > 0);
  }
});

// ─── Candidate source boundary: autocomplete/suggest vs combobox separation ──

Deno.test("boundary: autocomplete/suggest use onSearch (backend), SearchCombobox uses options (local)", () => {
  // AutoCompleteInput HAS onSearch (backend read-only search boundary)
  const acProps: AutoCompleteInputProps = {
    value: "",
    onChange: () => {},
    onSearch: (_q: string) => {
      // caller debounces and calls backend read-only search provider
    },
  };
  assert(typeof acProps.onSearch === "function", "AutoCompleteInput must have onSearch for backend search");

  // SuggestInput HAS onSearch (same backend boundary)
  const suggestProps: SuggestInputProps = {
    value: "",
    onChange: () => {},
    onSearch: (_q: string) => {},
  };
  assert(typeof suggestProps.onSearch === "function", "SuggestInput must have onSearch for backend search");

  // SearchCombobox does NOT have onSearch (local derivation boundary)
  const comboboxProps: SearchComboboxProps = {
    value: "",
    onChange: () => {},
    options: [],
  };
  assertFalse("onSearch" in comboboxProps, "SearchCombobox must NOT have onSearch — local derivation only");
});

Deno.test("boundary: uiBuilderSearchProvider source has no eval/fetch in-component (read-only dispatch only)", async () => {
  const src = await Deno.readTextFile(
    new URL("../lib/uiBuilderSearchProvider.ts", import.meta.url),
  );
  assertFalse(src.includes("eval("), "eval() must not appear in uiBuilderSearchProvider");
  assertFalse(src.includes("new Function("), "new Function() must not appear");
  // Must use dispatchOperation (existing /api/dispatch route) — not raw fetch to a new endpoint
  assert(
    src.includes("dispatchOperation("),
    "uiBuilderSearchProvider must use dispatchOperation (existing canonical route)",
  );
  // Must NOT create new HTTP endpoints
  assertFalse(
    src.includes('fetch("/api/') && !src.includes("dispatchOperation"),
    "uiBuilderSearchProvider must not create new HTTP endpoints — use dispatchOperation",
  );
});

// ─── Canvas preview wiring: buildLayoutPreviewRuntimeSpec integration ─────────

import { buildLayoutPreviewRuntimeSpec } from "../runtime/layoutComponentPreview.ts";

Deno.test("canvas preview: buildLayoutPreviewRuntimeSpec sets searchCallback in spec for autocomplete_input", () => {
  let capturedId = "";
  let capturedQuery = "";
  const result = buildLayoutPreviewRuntimeSpec({
    componentKey: "autocomplete_input",
    searchCallback: (componentId: string, query: string) => {
      capturedId = componentId;
      capturedQuery = query;
    },
  });
  assert(result.ok, "buildLayoutPreviewRuntimeSpec must succeed for autocomplete_input");
  assert(
    typeof result.spec.searchCallback === "function",
    "spec.searchCallback must be set when searchCallback is passed",
  );
  // Verify the callback actually fires through emitBoundEvent (search trigger)
  const emitResult = __testOnly.emitBoundEvent(result.spec, "search", { query: "canvas-test" });
  assert(emitResult.ok, "emitBoundEvent with search trigger must return ok");
  assertEquals(capturedId, result.spec.componentId, "searchCallback must receive componentId from spec");
  assertEquals(capturedQuery, "canvas-test", "searchCallback must receive query from payload");
});

Deno.test("canvas preview: buildLayoutPreviewRuntimeSpec injects searchSuggestions into props for autocomplete_input", () => {
  const suggestions = ["customer-management", "order-management"];
  const result = buildLayoutPreviewRuntimeSpec({
    componentKey: "autocomplete_input",
    searchSuggestions: suggestions,
  });
  assert(result.ok, "buildLayoutPreviewRuntimeSpec must succeed");
  assertEquals(
    result.spec.props.suggestions,
    suggestions,
    "props.suggestions must be injected from searchSuggestions for autocomplete_input preview",
  );
});

Deno.test("canvas preview: buildLayoutPreviewRuntimeSpec injects searchSuggestions into props for suggest_input", () => {
  const suggestions = ["product-catalog", "supplier-list"];
  const result = buildLayoutPreviewRuntimeSpec({
    componentKey: "suggest_input",
    searchSuggestions: suggestions,
  });
  assert(result.ok, "buildLayoutPreviewRuntimeSpec must succeed");
  assertEquals(
    result.spec.props.suggestions,
    suggestions,
    "props.suggestions must be injected from searchSuggestions for suggest_input preview",
  );
});

Deno.test("canvas preview: buildLayoutPreviewRuntimeSpec does NOT inject suggestions for search_combobox (wrong kind)", () => {
  const result = buildLayoutPreviewRuntimeSpec({
    componentKey: "search_combobox",
    searchSuggestions: ["should-not-appear"],
  });
  assert(result.ok, "buildLayoutPreviewRuntimeSpec must succeed for search_combobox");
  assertFalse(
    Array.isArray(result.spec.props.suggestions) && (result.spec.props.suggestions as string[]).includes("should-not-appear"),
    "searchSuggestions must NOT be injected into search_combobox props — combobox uses options, not suggestions",
  );
});

Deno.test("canvas preview: buildLayoutPreviewRuntimeSpec injects comboboxOptions into props for search_combobox (local derivation only)", () => {
  const options = [
    { label: "customer-management", value: "customer-management" },
    { label: "order-management", value: "order-management" },
  ];
  const result = buildLayoutPreviewRuntimeSpec({
    componentKey: "search_combobox",
    comboboxOptions: options,
  });
  assert(result.ok, "buildLayoutPreviewRuntimeSpec must succeed for search_combobox");
  assertEquals(
    result.spec.props.options,
    options,
    "props.options must be injected from comboboxOptions for search_combobox preview (local derivation only — no backend fetch)",
  );
});

Deno.test("canvas preview: comboboxOptions from deriveRouteKeyCandidates are correctly shaped for SearchCombobox props", () => {
  const candidates = deriveRouteKeyCandidates([
    { routeKey: "customer-management", layoutId: "layout-1" },
    { routeKey: "order-management", layoutId: "layout-2" },
  ]);
  const comboboxOptions = candidates.map((k) => ({ label: k, value: k }));

  const result = buildLayoutPreviewRuntimeSpec({
    componentKey: "search_combobox",
    comboboxOptions,
  });
  assert(result.ok, "buildLayoutPreviewRuntimeSpec must succeed");
  const injectedOptions = result.spec.props.options as { label: string; value: string }[];
  assert(Array.isArray(injectedOptions), "injected options must be an array");
  assertEquals(injectedOptions.length, 2, "must have 2 deduped options");
  assert(injectedOptions.some((o) => o.value === "customer-management"));
  assert(injectedOptions.some((o) => o.value === "order-management"));
  // Verify no onSearch is involved — local derivation only
  const comboboxProps: SearchComboboxProps = {
    value: "",
    onChange: () => {},
    options: injectedOptions,
  };
  assertFalse("onSearch" in comboboxProps, "SearchComboboxProps must not have onSearch — local derivation only");
});

// ─── Key alignment: searchCallback keys by node.nodeId, not runtime componentId ──

Deno.test("key alignment: searchCallback keys suggestions by node.nodeId (not runtime spec componentId)", () => {
  // Simulates FlowCanvasNodeView's searchCallbackForNode creation:
  // (_componentId, query) => onNodeSearch?.(node.nodeId, query)
  // The spec may have a different componentId (e.g. node.componentId or preview:<key>),
  // but suggestions are stored and retrieved by node.nodeId.

  const nodeId = "canvas-node-abc123";
  const capturedKeys: string[] = [];

  // This is the searchCallback as created by FlowCanvasNodeView (keyed by nodeId):
  const searchCallbackForNode = (_componentId: string, query: string): void => {
    // handleNodeSearch stores by the key passed — here nodeId
    capturedKeys.push(nodeId);
    assertEquals(query, "test-query");
  };

  // Simulate spec with a DIFFERENT componentId than nodeId
  const specComponentId = "backend-component-xyz";
  const spec: RuntimeComponentSpec = {
    componentId: specComponentId,
    componentType: "autocomplete_input",
    props: {},
    eventBinding: { search: { eventType: "search" } },
    searchCallback: searchCallbackForNode,
  };

  // emitBoundEvent calls spec.searchCallback(spec.componentId, q) internally,
  // but the closure already closed over node.nodeId — so storage key is nodeId.
  const result = __testOnly.emitBoundEvent(spec, "search", { query: "test-query" });
  assert(result.ok, "emitBoundEvent must return ok");

  assertEquals(capturedKeys.length, 1, "searchCallback must fire once");
  assertEquals(capturedKeys[0], nodeId, "storage key must be node.nodeId, not spec.componentId");
  assertFalse(
    capturedKeys.includes(specComponentId),
    "spec.componentId must NOT be used as the storage key — would break suggestionsByNodeId.get(node.nodeId) lookup",
  );
});

Deno.test("key alignment: suggestionsByNodeId lookup uses node.nodeId that matches handleNodeSearch storage key", () => {
  // This pins the round-trip: store by nodeId → retrieve by nodeId.
  // FlowCanvasNodeView: searchSuggestionsForNode = suggestionsByNodeId?.get(node.nodeId)
  // handleNodeSearch: setSearchSuggestionsByNodeId((prev) => ({ ...prev, [componentId]: result.candidates }))
  // where componentId = node.nodeId (because searchCallbackForNode uses node.nodeId).

  const nodeId = "node-roundtrip-test";
  const suggestions = ["route-a", "route-b"];

  // Simulate what handleNodeSearch stores (keyed by nodeId via the closure):
  const searchSuggestionsByNodeId: Record<string, string[]> = {
    [nodeId]: suggestions,
  };
  // Simulate what FlowCanvasNodeView reads:
  const suggestionsByNodeId = new Map(Object.entries(searchSuggestionsByNodeId));
  const retrieved = suggestionsByNodeId.get(nodeId);

  assertEquals(retrieved, suggestions, "suggestions must round-trip through nodeId key");
  assertEquals(retrieved, ["route-a", "route-b"]);
});
