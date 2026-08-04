/**
 * payloadFromResolver — resolves preset seed payloadFrom descriptors into runtime dispatch payloads.
 *
 * SSOT: docs/design/ui-builder-preset-ecosystem-ssot.yaml payloadFrom_resolver_contract
 * Referenced by: docs/design/ui-builder-seed-first-gap-discovery-ssot.yaml event_payload_resolver_gap
 *
 * Recognized source patterns:
 *   node:<nodeId>.value          — current value of a canvas node tracked via onNodeValueChange
 *   node:<nodeId>.value.<path>   — dotted path traversal INTO that tracked value when it is an
 *                                  object (e.g. a table's tracked selected-row value) — round 20,
 *                                  owning SSOT: this file's own header entry
 *                                  (payloadFrom_resolver_contract.recognized_source_patterns
 *                                  .node_value_path in ui-builder-preset-ecosystem-ssot.yaml). A
 *                                  bare `node:<nodeId>.value` (no suffix) is unchanged: the
 *                                  tracked value itself, whatever its shape.
 *   event.<path>                 — dotted path traversal on the triggering event payload
 *                                  e.g. event.item.id, event.row.id, event.record.id, event.value
 *   literal:<string>             — literal string value (static, no runtime resolution)
 *
 * All other patterns → PAYLOAD_FROM_UNRESOLVED_REF structured error; no silent fallback.
 * Missing nodeId in nodeValues map → PAYLOAD_FROM_NODE_NOT_FOUND error; no silent fallback.
 * Untraversable event path segment → PAYLOAD_FROM_EVENT_PATH_NOT_FOUND error; no silent fallback.
 * Untraversable node value path segment → PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND error; no silent fallback.
 */

const NODE_VALUE_RE = /^node:([A-Za-z0-9_-]+)\.value((?:\.[A-Za-z0-9_]+)*)$/;
const EVENT_PATH_RE = /^event(\.[A-Za-z0-9_]+)+$/;
const LITERAL_PREFIX = "literal:";

export type PayloadFromSource =
  | { kind: "node_value"; nodeId: string; path?: string[] }
  | { kind: "event_path"; path: string[] }
  | { kind: "literal"; value: string }
  | { kind: "unresolved_ref"; raw: string };

/**
 * Traverses a dotted path of own-property keys into `root`, failing closed (returning undefined
 * traversal state via the returned `ok:false`) the moment a segment is missing or the current
 * value stops being a traversable plain object — shared by event.<path> and node:<id>.value.<path>
 * so both dotted-path forms fail the same way on the same kind of malformed input.
 */
function traverseDottedPath(
  root: unknown,
  path: readonly string[],
  tracedPrefix: string,
): { ok: true; value: unknown } | { ok: false; error: string } {
  let current = root;
  let traversed = tracedPrefix;
  for (const seg of path) {
    if (typeof current !== "object" || current === null || Array.isArray(current)) {
      return {
        ok: false,
        error: `PATH_NOT_TRAVERSABLE: path segment "${seg}" is not traversable at "${traversed}" (value is not an object)`,
      };
    }
    // Own-property identity: `in` (and bracket-index truthiness) also match inherited
    // Object.prototype keys ("constructor", "toString", ...) — hasOwnProperty keeps a
    // coincidentally-named segment from resolving to an inherited function instead of
    // failing close as genuinely absent.
    if (
      !Object.prototype.hasOwnProperty.call(current as Record<string, unknown>, seg)
    ) {
      return {
        ok: false,
        error: `PATH_NOT_TRAVERSABLE: key "${seg}" is absent at "${traversed}"`,
      };
    }
    current = (current as Record<string, unknown>)[seg];
    traversed += `.${seg}`;
  }
  return { ok: true, value: current };
}

/**
 * Parses a raw payloadFrom source string into a structured descriptor.
 * Unknown patterns become unresolved_ref — they are NOT silently accepted.
 */
export function parsePayloadFromSource(raw: string): PayloadFromSource {
  const nodeMatch = NODE_VALUE_RE.exec(raw);
  if (nodeMatch) {
    const suffix = nodeMatch[2]; // "" or ".groupId" / ".groupId.nested" etc.
    const path = suffix ? suffix.slice(1).split(".") : [];
    return { kind: "node_value", nodeId: nodeMatch[1], path };
  }

  if (EVENT_PATH_RE.test(raw)) {
    const path = raw.slice("event.".length).split(".");
    return { kind: "event_path", path };
  }

  if (raw.startsWith(LITERAL_PREFIX)) {
    return { kind: "literal", value: raw.slice(LITERAL_PREFIX.length) };
  }

  return { kind: "unresolved_ref", raw };
}

export type ResolvedPayloadEntry =
  | { ok: true; value: unknown }
  | { ok: false; error: string };

/**
 * Resolves a single parsed payloadFrom source.
 *
 * nodeValues: map of nodeId → current scalar value (populated by onNodeValueChange in canvas).
 * eventPayload: the triggering event object (may contain .item, .row, .record, .value, etc.).
 *
 * Returns structured error on any resolution failure; never returns undefined silently.
 */
export function resolvePayloadFromSource(
  source: PayloadFromSource,
  nodeValues: Record<string, unknown>,
  eventPayload: Record<string, unknown>,
): ResolvedPayloadEntry {
  switch (source.kind) {
    case "node_value": {
      // Own-property identity: `in` (and bracket-index truthiness) also match
      // inherited Object.prototype keys ("constructor", "toString", ...) on a
      // plain-object nodeValues map, which would let a coincidentally-named
      // nodeId resolve to an inherited function value instead of failing
      // close as genuinely missing. hasOwnProperty is prototype-independent —
      // correct whether nodeValues is a plain {} (e.g. hand-built in a test)
      // or an Object.create(null) store (liveNodeValueTracker.ts).
      if (!Object.prototype.hasOwnProperty.call(nodeValues, source.nodeId)) {
        return {
          ok: false,
          error: `PAYLOAD_FROM_NODE_NOT_FOUND: node "${source.nodeId}" is not in the current canvas node value map`,
        };
      }
      const nodeValue = nodeValues[source.nodeId];
      const path = source.path ?? [];
      if (path.length === 0) return { ok: true, value: nodeValue };
      const suffix = path.join(".");
      const drilled = traverseDottedPath(
        nodeValue,
        path,
        `node:${source.nodeId}.value`,
      );
      if (!drilled.ok) {
        return {
          ok: false,
          error: `PAYLOAD_FROM_NODE_VALUE_PATH_NOT_FOUND: path "node:${source.nodeId}.value.${suffix}" ${
            drilled.error.slice("PATH_NOT_TRAVERSABLE: ".length)
          }`,
        };
      }
      return { ok: true, value: drilled.value };
    }

    case "event_path": {
      let current: unknown = eventPayload;
      let traversed = "event";
      for (const seg of source.path) {
        if (typeof current !== "object" || current === null || Array.isArray(current)) {
          return {
            ok: false,
            error: `PAYLOAD_FROM_EVENT_PATH_NOT_FOUND: path "event.${source.path.join(".")}" is not traversable at "${traversed}" (value is not an object)`,
          };
        }
        // Own-property identity, same rationale as node_value above: `in`
        // matches inherited Object.prototype keys on the caller's raw event
        // object, which would let a coincidentally-named path segment
        // (event.constructor, event.toString) resolve to an inherited
        // function instead of failing close as absent.
        if (
          !Object.prototype.hasOwnProperty.call(
            current as Record<string, unknown>,
            seg,
          )
        ) {
          return {
            ok: false,
            error: `PAYLOAD_FROM_EVENT_PATH_NOT_FOUND: path "event.${source.path.join(".")}" key "${seg}" is absent at "${traversed}"`,
          };
        }
        current = (current as Record<string, unknown>)[seg];
        traversed += `.${seg}`;
      }
      return { ok: true, value: current };
    }

    case "literal": {
      return { ok: true, value: source.value };
    }

    case "unresolved_ref": {
      return {
        ok: false,
        error: `PAYLOAD_FROM_UNRESOLVED_REF: "${source.raw}" does not match any recognized payloadFrom pattern (node:<nodeId>.value | node:<nodeId>.value.<path> | event.<path> | literal:<value>)`,
      };
    }
  }
}

export type ResolvePayloadFromResult =
  | { ok: true; payload: Record<string, unknown> }
  | { ok: false; errors: string[] };

/**
 * Resolves a full payloadFrom descriptor into a runtime dispatch payload.
 *
 * payloadFrom: { fieldName: sourceString } — from preset seed wiring candidate binding_json.
 * nodeValues:  { nodeId: currentValue }    — snapshot of canvas node values at event time.
 * eventPayload: the triggering interaction event (item click, row click, form submit, etc.).
 *
 * Returns { ok: true, payload } when all fields resolve.
 * Returns { ok: false, errors } (with all error codes) when any field fails.
 * Never silently drops unresolved fields or returns partial payloads on error.
 *
 * undefined -> null normalization (round 22/23): a present-but-undefined resolved value is
 * normalized to explicit JSON `null` here (resolvePayloadFromSource itself is UNCHANGED —
 * still returns `{ok: true, value: undefined}`). Full rationale, the three candidate
 * designs considered, and why this is the one normalization uniquely implied by existing
 * contracts (not a fresh design fork) is owned by
 * docs/design/ui-builder-preset-ecosystem-ssot.yaml
 * payloadFrom_resolver_contract.wire_transport_contract — read that before changing this.
 */
export function resolvePayloadFrom(
  payloadFrom: Record<string, string>,
  nodeValues: Record<string, unknown>,
  eventPayload: Record<string, unknown>,
): ResolvePayloadFromResult {
  const payload: Record<string, unknown> = {};
  const errors: string[] = [];

  for (const [field, rawSource] of Object.entries(payloadFrom)) {
    const source = parsePayloadFromSource(rawSource);
    const result = resolvePayloadFromSource(source, nodeValues, eventPayload);
    if (!result.ok) {
      errors.push(result.error);
    } else {
      payload[field] = result.value === undefined ? null : result.value;
    }
  }

  if (errors.length > 0) return { ok: false, errors };
  return { ok: true, payload };
}
