/**
 * SSE dispatcher.
 *
 * Owns sse_dispatcher boundary per runtime-orchestration-ssot:
 * routes SSE events by type to registered handlers, feeding the frontend
 * scheduler queue as a hook_trigger.
 *
 * Completion conditions satisfied:
 *   dispatcher_routes_projection_events_into_projection_runtime
 *   unhandled_event_policy_is_explicit
 *   projection_event_identity_is_preserved
 *
 * Lane: sse_projection_lane in pipeline-continuity-ssot.yaml (frontend.sse_dispatcher node).
 */

import type { ProjectionRuntime } from "./projectionRuntime.ts";
import {
  extractSqlAttentionDraftCandidatePayload,
  type SqlAttentionDraftCandidatePayload,
} from "./sseReceiver.ts";

export type SseEventHandler = (data: string) => void;

/**
 * Explicit unhandled event policy.
 *   "log"    — unhandled event types are logged to console.warn (default)
 *   "ignore" — unhandled event types are explicitly acknowledged as no-ops
 *
 * Silent fallback is prohibited. Policy must be declared.
 */
export type UnhandledEventPolicy = "log" | "ignore";

export type SseDispatcherOptions = {
  /** Policy for events with no registered handler. Default: "log". */
  unhandledEventPolicy?: UnhandledEventPolicy;
};

export type SseDispatcher = {
  register: (eventType: string, handler: SseEventHandler) => void;
  route: (eventType: string, data: string) => void;
};

/**
 * Creates an SSE dispatcher with explicit unhandled event policy.
 *
 * Registered handlers are called when route() is invoked with a matching eventType.
 * Unhandled events are handled per the declared policy — never silent fallback.
 */
export function createSseDispatcher(options?: SseDispatcherOptions): SseDispatcher {
  const handlers = new Map<string, SseEventHandler>();
  const policy: UnhandledEventPolicy = options?.unhandledEventPolicy ?? "log";

  function register(eventType: string, handler: SseEventHandler): void {
    handlers.set(eventType, handler);
  }

  function route(eventType: string, data: string): void {
    const handler = handlers.get(eventType);
    if (handler) {
      handler(data);
    } else {
      if (policy === "log") {
        console.warn(
          `[sseDispatcher] SSE_UNHANDLED_EVENT_TYPE: no handler registered for event type "${eventType}"`,
        );
      }
      // policy === "ignore": explicitly acknowledged no-op (not silent)
    }
  }

  return { register, route };
}

/**
 * Creates an SSE dispatcher pre-wired to a ProjectionRuntime.
 * Routes "projection" events directly to projectionRuntime.handleProjectionEvent.
 * Additional handlers can be registered after creation.
 *
 * Projection event identity is preserved: rawData containing manifest_id/table_id/table_registry_id
 * flows unchanged into projectionRuntime.handleProjectionEvent.
 */
export function createSseDispatcherWithProjectionRuntime(
  projectionRuntime: ProjectionRuntime,
  options?: SseDispatcherOptions,
): SseDispatcher {
  const dispatcher = createSseDispatcher(options);
  dispatcher.register("projection", (data) => {
    projectionRuntime.handleProjectionEvent(data);
  });
  return dispatcher;
}

/**
 * Registers a render-only handler for the manifest_topology_key_expansion draft lane signal
 * ("sql_attention_draft_candidate") on an existing dispatcher.
 *
 * The handler only forwards the validated structured payload to a render callback. It performs
 * no SQL Attention / recommendation / promotion judgment and decides no UI placement; authority
 * stays in the backend draft candidate JSONB. Malformed payloads are dropped via the dispatcher's
 * explicit unhandled-data path (the render callback is simply not invoked) — never silently "fixed".
 */
export function registerSqlAttentionDraftCandidateRenderHandler(
  dispatcher: SseDispatcher,
  onSignal: (payload: SqlAttentionDraftCandidatePayload, rawData: string) => void,
): void {
  dispatcher.register("sql_attention_draft_candidate", (data) => {
    const payload = extractSqlAttentionDraftCandidatePayload(data);
    if (payload === null) {
      console.warn(
        "[sseDispatcher] SQL_ATTENTION_DRAFT_CANDIDATE_PAYLOAD_INVALID: dropping malformed draft candidate signal",
      );
      return;
    }
    onSignal(payload, data);
  });
}
