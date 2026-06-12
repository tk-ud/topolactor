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
  resolvePayloadFromSource,
  resolvePayloadFrom,
  type PayloadFromSource,
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
    "hub:search",           // old hub:search pattern — not recognized
    "query",                // bare field name
    "node:foo",             // missing .value
    "event",                // bare event — no path
    "emission.data.rows",   // prop binding path — not a payloadFrom pattern
    "",                     // empty string
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
  const source: PayloadFromSource = { kind: "node_value", nodeId: "hub_search_input" };
  const nodeValues = { hub_search_input: "topology search" };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, "topology search");
});

Deno.test("payloadFromResolver: node_value with undefined value resolves ok (empty input is valid)", () => {
  const source: PayloadFromSource = { kind: "node_value", nodeId: "crud_search_input" };
  const nodeValues = { crud_search_input: undefined };
  const result = resolvePayloadFromSource(source, nodeValues, {});
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, undefined);
});

Deno.test("payloadFromResolver: node_value produces PAYLOAD_FROM_NODE_NOT_FOUND when nodeId absent — no silent fallback", () => {
  const source: PayloadFromSource = { kind: "node_value", nodeId: "missing_node" };
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
  const source: PayloadFromSource = { kind: "event_path", path: ["item", "id"] };
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
  const source: PayloadFromSource = { kind: "event_path", path: ["record", "id"] };
  const eventPayload = { record: { id: "record-uuid-789" } };
  const result = resolvePayloadFromSource(source, {}, eventPayload);
  assertEquals(result.ok, true);
  if (result.ok) assertEquals(result.value, "record-uuid-789");
});

Deno.test("payloadFromResolver: event path returns PAYLOAD_FROM_EVENT_PATH_NOT_FOUND when path not traversable — no silent fallback", () => {
  const source: PayloadFromSource = { kind: "event_path", path: ["item", "id"] };
  const eventPayload = { item: "not_an_object" }; // item is a string, not an object
  const result = resolvePayloadFromSource(source, {}, eventPayload);
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertMatch(result.error, /PAYLOAD_FROM_EVENT_PATH_NOT_FOUND/);
  }
});

Deno.test("payloadFromResolver: event path returns error when intermediate segment is undefined (not traversable)", () => {
  // event.item.id where eventPayload has no item → item is undefined → .id traversal fails
  const source: PayloadFromSource = { kind: "event_path", path: ["item", "id"] };
  const eventPayload = {}; // no item
  const result = resolvePayloadFromSource(source, {}, eventPayload);
  // undefined is not an object, so traversal of .id on undefined is an error (not silent fallback)
  assertEquals(result.ok, false);
  if (!result.ok) {
    assertMatch(result.error, /PAYLOAD_FROM_EVENT_PATH_NOT_FOUND/);
  }
});

Deno.test("payloadFromResolver: event path single segment returns undefined when key absent (undefined is ok for leaf)", () => {
  // event.value where eventPayload has no value → single segment → returns undefined (value may not be present)
  const source: PayloadFromSource = { kind: "event_path", path: ["value"] };
  const eventPayload = {}; // no value key
  const result = resolvePayloadFromSource(source, {}, eventPayload);
  assertEquals(result.ok, true); // single segment → undefined is returned without error
  if (result.ok) assertEquals(result.value, undefined);
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
  const source: PayloadFromSource = { kind: "unresolved_ref", raw: "hub:search" };
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
    assertEquals(result.payload, { keyword: "foo", entityId: "uuid-123", state: "active" });
  }
});

Deno.test("payloadFromResolver: resolvePayloadFrom returns all errors when any field fails — no partial payload", () => {
  const payloadFrom = {
    keyword: "node:missing_node.value",  // node not in nodeValues
    entityId: "hub:search",              // unrecognized pattern
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
