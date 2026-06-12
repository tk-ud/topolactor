/**
 * payloadFromResolver — resolves preset seed payloadFrom descriptors into runtime dispatch payloads.
 *
 * SSOT: docs/design/ui-builder-preset-ecosystem-ssot.yaml payloadFrom_resolver_contract
 * Referenced by: docs/design/ui-builder-seed-first-gap-discovery-ssot.yaml event_payload_resolver_gap
 *
 * Recognized source patterns:
 *   node:<nodeId>.value  — current value of a canvas node tracked via onNodeValueChange
 *   event.<path>         — dotted path traversal on the triggering event payload
 *                          e.g. event.item.id, event.row.id, event.record.id, event.value
 *   literal:<string>     — literal string value (static, no runtime resolution)
 *
 * All other patterns → PAYLOAD_FROM_UNRESOLVED_REF structured error; no silent fallback.
 * Missing nodeId in nodeValues map → PAYLOAD_FROM_NODE_NOT_FOUND error; no silent fallback.
 * Untraversable event path segment → PAYLOAD_FROM_EVENT_PATH_NOT_FOUND error; no silent fallback.
 */

const NODE_VALUE_RE = /^node:([A-Za-z0-9_-]+)\.value$/;
const EVENT_PATH_RE = /^event(\.[A-Za-z0-9_]+)+$/;
const LITERAL_PREFIX = "literal:";

export type PayloadFromSource =
  | { kind: "node_value"; nodeId: string }
  | { kind: "event_path"; path: string[] }
  | { kind: "literal"; value: string }
  | { kind: "unresolved_ref"; raw: string };

/**
 * Parses a raw payloadFrom source string into a structured descriptor.
 * Unknown patterns become unresolved_ref — they are NOT silently accepted.
 */
export function parsePayloadFromSource(raw: string): PayloadFromSource {
  const nodeMatch = NODE_VALUE_RE.exec(raw);
  if (nodeMatch) return { kind: "node_value", nodeId: nodeMatch[1] };

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
      if (!(source.nodeId in nodeValues)) {
        return {
          ok: false,
          error: `PAYLOAD_FROM_NODE_NOT_FOUND: node "${source.nodeId}" is not in the current canvas node value map`,
        };
      }
      return { ok: true, value: nodeValues[source.nodeId] };
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
        if (!(seg in (current as Record<string, unknown>))) {
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
        error: `PAYLOAD_FROM_UNRESOLVED_REF: "${source.raw}" does not match any recognized payloadFrom pattern (node:<nodeId>.value | event.<path> | literal:<value>)`,
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
      payload[field] = result.value;
    }
  }

  if (errors.length > 0) return { ok: false, errors };
  return { ok: true, payload };
}
