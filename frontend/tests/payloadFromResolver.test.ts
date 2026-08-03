/**
 * payloadFromResolver tests
 *
 * SSOT: docs/design/ui-builder-preset-ecosystem-ssot.yaml payloadFrom_resolver_contract
 * Implementation: frontend/runtime/payloadFromResolver.ts
 *
 * Validates the resolver contract:
 *   - Recognized source patterns resolve correctly.
 *   - Unresolved ref patterns produce structured errors (no silent fallback).
 *   - Missing nodeId produces PAYLOAD_FROM_NODE_NOT_FOUND (no silent fallback).
 *   - Untraversable event path produces PAYLOAD_FROM_EVENT_PATH_NOT_FOUND.
 *   - Full resolvePayloadFrom returns { ok: true, payload } or { ok: false, errors }.
 */

import {
  assertEquals,
  assertMatch,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  parsePayloadFromSource,
  type PayloadFromSource,
  resolvePayloadFrom,
  resolvePayloadFromSource,
} from "../runtime/payloadFromResolver.ts";

// ---------------------------------------------------------------------------
// parsePayloadFromSource
// ---------------------------------------------------------------------------

Deno.test("payloadFromResolver: node:<nodeId>.value parses to node_value", () => {
  const result = parsePayloadFromSource("node:hub_search_input.value");
  assertEquals(result.kind, "node_value");
  if (result.kind === "node_value") {
    assertEquals(result.nodeId, "hub_search_input");
  }
});

Deno.test("payloadFromResolver: node:<nodeId>.value with hyphens parses correctly", () => {
  const result = parsePayloadFromSource("node:crud-search-input.value");
  assertEquals(result.kind, "node_value");
  if (result.kind === "node_value") {
    assertEquals(result.nodeId, "crud-search-input");
  }
});

Deno.test("payloadFromResolver: event.item.id parses to event_path", () => {
  const result = parsePayloadFromSource("event.item.id");
  assertEquals(result.kind, "event_path");
  if (result.kind === "event_path") {
    assertEquals(result.path, ["item", "id"]);
  }
});

Deno.test("payloadFromResolver: event.row.id parses to event_path", () => {
  const result = parsePayloadFromSource("event.row.id");
  assertEquals(result.kind, "event_path");
  if (result.kind === "event_path") {
    assertEquals(result.path, ["row", "id"]);
  }
});

Deno.test("payloadFromResolver: event.record.id parses to event_path", () => {
  const result = parsePayloadFromSource("event.record.id");
  assertEquals(result.kind, "event_path");
  if (result.kind === "event_path") {
    assertEquals(result.path, ["record", "id"]);
  }
});

Deno.test("payloadFromResolver: event.value parses to event_path", () => {
  const result = parsePayloadFromSource("event.value");
  assertEquals(result.kind, "event_path");
  if (result.kind === "event_path") {
    assertEquals(result.path, ["value"]);
  }
});

Deno.test("payloadFromResolver: literal:<string> parses to literal", () => {
  const result = parsePayloadFromSource("literal:active");
  assertEquals(result.kind, "literal");
  if (result.kind === "literal") {
    assertEquals(result.value, "active");
  }
});

Deno.test("payloadFromResolver: literal: with empty string parses to literal with empty value", () => {
  const result = parsePayloadFromSource("literal:");
  assertEquals(result.kind, "literal");
  if (result.kind === "literal") {
    assertEquals(result.value, "");
  }
});

Deno.test("payloadFromResolver: unrecognized pattern parses to unresolved_ref — no silent fallback", () => {
  const unrecognized = [
    "hub:search", // old hub:search pattern — not recognized
    "query", // bare field name
    "node:foo", // missing .value
    "event", // bare event — no path
    "emission.data.rows", // prop binding path — not a payloadFrom pattern
    "", // empty string
  ];
  for (const raw of unrecognized) {
    const result = parsePayloadFromSource(raw);
    assertEquals(
      result.kind,
      "unresolved_ref",
      `Expected unresolved_ref for "${raw}" but got "${result.kind}"`,
    );
  }
});

// ---------------------------------------------------------------------------
// resolvePayloadFromSource — node_value
// ---------------------------------------------------------------------------

Deno.test("payloadFromResolver: node_value resolves to current node value", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "hub_search_input",
  };
  const nodeValues = { hub_search_input: "topology search" };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, "topology search");
});

Deno.test("payloadFromResolver: node_value with undefined value resolves ok (empty input is valid)", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "crud_search_input",
  };
  const nodeValues = { crud_search_input: undefined };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, undefined);
});

Deno.test("payloadFromResolver: node_value produces PAYLOAD_FROM_NODE_NOT_FOUND when nodeId absent — no silent fallback", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "missing_node",
  };
  const nodeValues = { other_node: "foo" };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertMatch(result.error, /PAYLOAD_FROM_NODE_NOT_FOUND/);
    assertMatch(result.error, /missing_node/);
  }
});

// ---------------------------------------------------------------------------
// resolvePayloadFromSource — event_path
// ---------------------------------------------------------------------------

Deno.test("payloadFromResolver: event.item.id resolves from eventPayload", () => {
  const source: PayloadFromSource = {
    kind: "event_path",
    path: ["item", "id"],
  };
  const eventPayload = { item: { id: "hub-uuid-123" } };
  const result = resolvePayloadFromSource(source, {}, eventPayload);
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, "hub-uuid-123");
});

Deno.test("payloadFromResolver: event.row.id resolves from eventPayload", () => {
  const source: PayloadFromSource = { kind: "event_path", path: ["row", "id"] };
  const eventPayload = { row: { id: "entity-uuid-456" } };
  const result = resolvePayloadFromSource(source, {}, eventPayload);
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, "entity-uuid-456");
});

Deno.test("payloadFromResolver: event.record.id resolves from eventPayload", () => {
  const source: PayloadFromSource = {
    kind: "event_path",
    path: ["record", "id"],
  };
  const eventPayload = { record: { id: "record-uuid-789" } };
  const result = resolvePayloadFromSource(source, {}, eventPayload);
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, "record-uuid-789");
});

Deno.test("payloadFromResolver: event path returns PAYLOAD_FROM_EVENT_PATH_NOT_FOUND when path not traversable — no silent fallback", () => {
  const source: PayloadFromSource = {
    kind: "event_path",
    path: ["item", "id"],
  };
  const eventPayload = { item: "not_an_object" }; // item is a string, not an object
  const result = resolvePayloadFromSource(source, {}, eventPayload);
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertMatch(result.error, /PAYLOAD_FROM_EVENT_PATH_NOT_FOUND/);
  }
});

Deno.test("payloadFromResolver: event path returns error when intermediate segment is undefined (not traversable)", () => {
  // event.item.id where eventPayload has no item → item is undefined → .id traversal fails
  const source: PayloadFromSource = {
    kind: "event_path",
    path: ["item", "id"],
  };
  const eventPayload = {}; // no item
  const result = resolvePayloadFromSource(source, {}, eventPayload);
  // undefined is not an object, so traversal of .id on undefined is an error (not silent fallback)
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertMatch(result.error, /PAYLOAD_FROM_EVENT_PATH_NOT_FOUND/);
  }
});

Deno.test("payloadFromResolver: event path single segment returns PAYLOAD_FROM_EVENT_PATH_NOT_FOUND when key absent", () => {
  // event.value where eventPayload has no value key → absent key at leaf → structured error, no silent undefined
  const source: PayloadFromSource = { kind: "event_path", path: ["value"] };
  const eventPayload = {}; // no value key
  const result = resolvePayloadFromSource(source, {}, eventPayload);
  assertEquals(result.ok, false); // absent key at any segment (including leaf) is an error
  if (!result.ok) {
    assertMatch(result.error, /PAYLOAD_FROM_EVENT_PATH_NOT_FOUND/);
  }
});

// ---------------------------------------------------------------------------
// resolvePayloadFromSource — literal
// ---------------------------------------------------------------------------

Deno.test("payloadFromResolver: literal value resolves to static string", () => {
  const source: PayloadFromSource = { kind: "literal", value: "active" };
  const result = resolvePayloadFromSource(source, {}, {});
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, "active");
});

// ---------------------------------------------------------------------------
// resolvePayloadFromSource — unresolved_ref
// ---------------------------------------------------------------------------

Deno.test("payloadFromResolver: unresolved_ref returns PAYLOAD_FROM_UNRESOLVED_REF error", () => {
  const source: PayloadFromSource = {
    kind: "unresolved_ref",
    raw: "hub:search",
  };
  const result = resolvePayloadFromSource(source, {}, {});
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertMatch(result.error, /PAYLOAD_FROM_UNRESOLVED_REF/);
    assertMatch(result.error, /hub:search/);
  }
});

// ---------------------------------------------------------------------------
// resolvePayloadFrom — full descriptor
// ---------------------------------------------------------------------------

Deno.test("payloadFromResolver: resolvePayloadFrom resolves hub_search keyword correctly", () => {
  const payloadFrom = { keyword: "node:hub_search_input.value" };
  const nodeValues = { hub_search_input: "test query" };
  const result = resolvePayloadFrom(payloadFrom, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.payload, { keyword: "test query" });
  }
});

Deno.test("payloadFromResolver: resolvePayloadFrom resolves event.item.id to entityId", () => {
  const payloadFrom = { entityId: "event.item.id" };
  const eventPayload = { item: { id: "entity-abc" } };
  const result = resolvePayloadFrom(payloadFrom, {}, eventPayload);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.payload, { entityId: "entity-abc" });
  }
});

Deno.test("payloadFromResolver: resolvePayloadFrom resolves multiple fields from mixed sources", () => {
  const payloadFrom = {
    keyword: "node:search_input.value",
    entityId: "event.item.id",
    state: "literal:active",
  };
  const nodeValues = { search_input: "foo" };
  const eventPayload = { item: { id: "uuid-123" } };
  const result = resolvePayloadFrom(payloadFrom, nodeValues, eventPayload);
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.payload, {
      keyword: "foo",
      entityId: "uuid-123",
      state: "active",
    });
  }
});

Deno.test("payloadFromResolver: resolvePayloadFrom returns all errors when any field fails — no partial payload", () => {
  const payloadFrom = {
    keyword: "node:missing_node.value", // node not in nodeValues
    entityId: "hub:search", // unrecognized pattern
  };
  const nodeValues = {};
  const result = resolvePayloadFrom(payloadFrom, nodeValues, {});
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(result.errors.length, 2);
    const allErrors = result.errors.join("\n");
    assertMatch(allErrors, /PAYLOAD_FROM_NODE_NOT_FOUND/);
    assertMatch(allErrors, /PAYLOAD_FROM_UNRESOLVED_REF/);
  }
});

Deno.test("payloadFromResolver: empty payloadFrom descriptor resolves to empty payload", () => {
  const result = resolvePayloadFrom({}, {}, {});
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.payload, {});
  }
});

// ---------------------------------------------------------------------------
// Hub search wiring alignment contract
// ---------------------------------------------------------------------------

Deno.test("payloadFromResolver: hub_search seed payloadFrom { keyword } resolves correctly", () => {
  const payloadFrom = { keyword: "node:hub_search_input.value" };
  const nodeValues = { hub_search_input: "my topology" };
  const result = resolvePayloadFrom(payloadFrom, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(typeof result.payload.keyword, "string");
    assertEquals(result.payload.keyword, "my topology");
  }
});

Deno.test("payloadFromResolver: hub:search is NOT a recognized payloadFrom pattern (SSOT alignment)", () => {
  const source = parsePayloadFromSource("hub:search");
  assertEquals(source.kind, "unresolved_ref");
});

// ─── own-property identity (PR #599 review): a nodeId/event-path segment
// colliding with an inherited Object.prototype key must never resolve as an
// inherited value ────────────────────────────────────────────────────────

Deno.test("resolvePayloadFrom: node:<id>.value fails close (PAYLOAD_FROM_NODE_NOT_FOUND) for an Object.prototype-shaped nodeId that was never set — even against a plain {} nodeValues map", () => {
  const nodeValues: Record<string, unknown> = {};
  const result = resolvePayloadFrom(
    { id: "node:constructor.value" },
    nodeValues,
    {},
  );
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(
      result.errors.some((e) => e.includes("PAYLOAD_FROM_NODE_NOT_FOUND")),
      true,
    );
  }
});

Deno.test("resolvePayloadFrom: event.<path> fails close for an Object.prototype-shaped path segment that was never actually present on the event payload", () => {
  const result = resolvePayloadFrom(
    { id: "event.toString" },
    {},
    { unrelated: true },
  );
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertEquals(
      result.errors.some((e) =>
        e.includes("PAYLOAD_FROM_EVENT_PATH_NOT_FOUND")
      ),
      true,
    );
  }
});

// ─── node:<nodeId>.value.<path> dotted-path drilling (round 20 — admin-enum
// selected-row carrier: a table's tracked selected-row object value needs a
// single field extracted for a later button's payloadFrom) ─────────────────

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> parses to node_value with a path", () => {
  const result = parsePayloadFromSource("node:enum_table.value.groupId");
  assertEquals(result.kind, "node_value");
  if (result.kind === "node_value") {
    assertEquals(result.nodeId, "enum_table");
    assertEquals(result.path, ["groupId"]);
  }
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<a>.<b> parses to node_value with a multi-segment path", () => {
  const result = parsePayloadFromSource("node:enum_table.value.detail.groupId");
  assertEquals(result.kind, "node_value");
  if (result.kind === "node_value") {
    assertEquals(result.nodeId, "enum_table");
    assertEquals(result.path, ["detail", "groupId"]);
  }
});

Deno.test("payloadFromResolver: node:<nodeId>.value (no suffix) still parses with an empty path — bare tracked-value form unchanged", () => {
  const result = parsePayloadFromSource("node:hub_search_input.value");
  assertEquals(result.kind, "node_value");
  if (result.kind === "node_value") {
    assertEquals(result.path ?? [], []);
  }
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> drills into an object tracked value", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "enum_table",
    path: ["groupId"],
  };
  const nodeValues = {
    enum_table: { groupId: "22222222-2222-2222-2222-222222222201", groupName: "demo_status" },
  };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.value, "22222222-2222-2222-2222-222222222201");
  }
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> fails close (PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND) when the tracked value is not an object", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "enum_table",
    path: ["groupId"],
  };
  const nodeValues = { enum_table: "not an object" };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertMatch(result.error, /PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND/);
  }
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> fails close (PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND) when the field is absent on the tracked object", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "enum_table",
    path: ["groupId"],
  };
  const nodeValues = { enum_table: { groupName: "demo_status" } };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertMatch(result.error, /PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND/);
    assertMatch(result.error, /groupId/);
  }
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> fails close when the nodeId itself has never been tracked (no row selected yet)", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "enum_table",
    path: ["groupId"],
  };
  const result = resolvePayloadFromSource(source, {}, {});
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertMatch(result.error, /PAYLOAD_FROM_NODE_NOT_FOUND/);
  }
});

Deno.test("payloadFromResolver: resolvePayloadFrom resolves a full delete_group-style payload from a table's tracked selected row", () => {
  const payloadFrom = {
    groupId: "node:enum_table.value.groupId",
    confirmed: "literal:true",
  };
  const nodeValues = {
    enum_table: { groupId: "row-uuid-1", groupName: "demo_status", indexNum: 3 },
  };
  const result = resolvePayloadFrom(payloadFrom, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.payload, { groupId: "row-uuid-1", confirmed: "true" });
  }
});

// ─── round 21 audit: exhaustive edge-case coverage for node:<id>.value.<path>
// per docs/design/ui-builder-preset-ecosystem-ssot.yaml payloadFrom_resolver_contract
// .recognized_source_patterns.node_value_path ────────────────────────────────

Deno.test("payloadFromResolver: node:<nodeId>.value.<a>.<b> drills through nested objects (multi-segment success, not just single-segment)", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "enum_table",
    path: ["detail", "groupId"],
  };
  const nodeValues = { enum_table: { detail: { groupId: "nested-uuid-1" } } };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, "nested-uuid-1");
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> fails close (PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND) when an INTERMEDIATE segment is null", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "enum_table",
    path: ["detail", "groupId"],
  };
  const nodeValues = { enum_table: { detail: null } };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND/);
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> fails close (PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND) when the tracked value itself is null", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "enum_table",
    path: ["groupId"],
  };
  const nodeValues = { enum_table: null };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND/);
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> fails close (PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND) when the tracked value is an ARRAY, not an object — no implicit array-as-object fallback", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "enum_table",
    path: ["groupId"],
  };
  const nodeValues = { enum_table: [{ groupId: "row-uuid-1" }] };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND/);
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> resolves ok when the field is PRESENT but its value is undefined — absent key vs. present-undefined-value distinction", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "enum_table",
    path: ["groupId"],
  };
  const nodeValues = { enum_table: { groupId: undefined } };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, undefined);
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> fails close for an Object.prototype-shaped field segment (constructor/toString) that was never actually set on the tracked object — own-property identity, not `in`", () => {
  const source: PayloadFromSource = {
    kind: "node_value",
    nodeId: "enum_table",
    path: ["constructor"],
  };
  const nodeValues = { enum_table: {} };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, false);
  if (!result.ok) assertMatch(result.error, /PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND/);
});

Deno.test("payloadFromResolver: node:<nodeId>.value.<field> parses correctly for a hyphenated nodeId with a dotted field suffix — nodeId grammar unchanged by this extension", () => {
  const result = parsePayloadFromSource("node:crud-search-input.value.query");
  assertEquals(result.kind, "node_value");
  if (result.kind === "node_value") {
    assertEquals(result.nodeId, "crud-search-input");
    assertEquals(result.path, ["query"]);
  }
});

// Round 21/22 grammar parity: reads the SAME shared, machine-readable corpus file
// check_react_schema_topology_seed_translator.py's 42g/42h assertions read against the
// Python NODE_VALUE_RE — round 22 fix: this used to be a hand-retyped literal list in each
// suite (a duplication the round 22 audit itself flagged as an NG-axis violation to leave
// standing); both suites now read ONE physical artifact, so neither can silently drift from
// the other. docs/design/ui-builder-preset-ecosystem-ssot.yaml payloadFrom_resolver_contract
// .recognized_source_patterns.node_value_path.cross_implementation_parity.
type GrammarCorpus = { accept: string[]; reject: string[] };

async function loadGrammarCorpus(): Promise<GrammarCorpus> {
  const raw = await Deno.readTextFile(
    new URL(
      "../../.agent/tests/fixtures/payload-from-node-value-grammar-corpus.json",
      import.meta.url,
    ),
  );
  return JSON.parse(raw) as GrammarCorpus;
}

Deno.test("payloadFromResolver: frontend NODE_VALUE_RE accepts every string the shared grammar corpus marks accept (round 21/22 parity)", async () => {
  const corpus = await loadGrammarCorpus();
  for (const raw of corpus.accept) {
    const result = parsePayloadFromSource(raw);
    assertEquals(
      result.kind,
      "node_value",
      `expected "${raw}" to parse as node_value but got "${result.kind}"`,
    );
  }
});

Deno.test("payloadFromResolver: frontend NODE_VALUE_RE rejects every string the shared grammar corpus marks reject (round 21/22 parity)", async () => {
  const corpus = await loadGrammarCorpus();
  for (const raw of corpus.reject) {
    const result = parsePayloadFromSource(raw);
    assertEquals(
      result.kind === "node_value",
      false,
      `expected "${raw}" NOT to parse as node_value but it did`,
    );
  }
});

// ─── round 22 audit: a resolver-successful undefined value must not silently
// vanish over the wire. resolvePayloadFromSource itself still resolves a
// present-but-undefined tracked value as ok (unchanged — see the "empty input
// is valid" test above), but resolvePayloadFrom's aggregate payload is what
// dispatchOperation (frontend/api/dispatch.ts) hands directly to
// JSON.stringify(req) with no intermediate boundary — and JSON.stringify
// silently DROPS any object key whose value is `undefined`. These tests prove
// the field survives serialization as an explicit `null`, not a vanished key. ──

Deno.test("payloadFromResolver: resolvePayloadFrom normalizes a resolved-but-undefined node value to null (not omitted) in the aggregate payload", () => {
  const payloadFrom = { groupId: "node:enum_table.value.groupId" };
  const nodeValues = { enum_table: { groupId: undefined } };
  const result = resolvePayloadFrom(payloadFrom, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(
      Object.prototype.hasOwnProperty.call(result.payload, "groupId"),
      true,
      "groupId must remain a present key on the resolved payload, not be dropped",
    );
    assertEquals(result.payload.groupId, null);
  }
});

Deno.test("payloadFromResolver: a resolvePayloadFrom payload containing a normalized-to-null field survives JSON.stringify with the key still present (proves the actual wire-serialization boundary, not just the in-memory object)", () => {
  const payloadFrom = {
    groupId: "node:enum_table.value.groupId",
    confirmed: "literal:true",
  };
  const nodeValues = { enum_table: { groupId: undefined, groupName: "demo" } };
  const result = resolvePayloadFrom(payloadFrom, nodeValues, {});
  assertEquals(result.ok, true);
  if (!result.ok) return;
  const wire = JSON.stringify(result.payload);
  const reparsed = JSON.parse(wire) as Record<string, unknown>;
  assertEquals(
    Object.prototype.hasOwnProperty.call(reparsed, "groupId"),
    true,
    "groupId must survive the actual JSON.stringify -> wire -> JSON.parse round trip as an explicit key, never silently dropped",
  );
  assertEquals(reparsed.groupId, null);
  assertEquals(reparsed.confirmed, "true");
});

Deno.test("payloadFromResolver: resolvePayloadFrom does NOT normalize a genuinely-present (non-undefined) falsy value — only undefined is coerced to null, not 0/''/false", () => {
  const payloadFrom = {
    count: "node:counter.value",
    label: "node:label_input.value",
    active: "node:active_toggle.value",
  };
  const nodeValues = { counter: 0, label_input: "", active_toggle: false };
  const result = resolvePayloadFrom(payloadFrom, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) {
    assertEquals(result.payload, { count: 0, label: "", active: false });
  }
});
