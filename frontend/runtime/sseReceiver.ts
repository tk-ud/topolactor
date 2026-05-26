/**
 * SSE receiver.
 *
 * Owns the sse_receiver boundary per runtime-orchestration-ssot:
 * external_event_listener feeding a hook_trigger into the frontend scheduler queue.
 *
 * Completion conditions satisfied:
 *   sse_receiver_feeds_frontend_scheduler_as_hook_trigger
 *   receiver_preserves_projection_event_identity
 *   backend_sse_error_states_are_explicit
 *
 * Lane: sse_projection_lane in pipeline-continuity-ssot.yaml (frontend.sse_receiver node).
 */

export type ProjectionEventIdentity = {
  manifestId?: string;
  tableId?: string;
  tableRegistryId?: string;
};

export type ProjectionHookTrigger = {
  eventType: "projection";
  data: string;
  identity: ProjectionEventIdentity;
};

/**
 * Explicit SSE error states. Never silent fallback.
 *   connection_error: EventSource error event while connection is open/connecting
 *   parse_error: projection event data could not be parsed (identity extraction failed)
 *   connection_closed: EventSource closed (CLOSED readyState on error)
 */
export type SseErrorState =
  | { kind: "connection_error"; event: Event }
  | { kind: "parse_error"; rawData: string; error: string }
  | { kind: "connection_closed" };

export type SseEventHandler = (eventType: string, data: string) => void;

export type SseReceiverOptions = {
  /** URL to connect to. Defaults to /api/sse. */
  url?: string;
  /**
   * Called when a projection event is received.
   * The trigger carries structured identity extracted from the event payload.
   * This is the hook_trigger path feeding the frontend scheduler.
   */
  onProjectionHookTrigger?: (trigger: ProjectionHookTrigger) => void;
  /** Called for non-projection events (ping, message, etc.). */
  onEvent?: SseEventHandler;
  /** Called when an explicit SSE error state is encountered. */
  onError?: (state: SseErrorState) => void;
};

export type SseReceiver = {
  connect: () => void;
  disconnect: () => void;
  readonly connected: boolean;
};

function extractProjectionIdentity(rawData: string): { ok: true; identity: ProjectionEventIdentity } | { ok: false; error: string } {
  try {
    const parsed = JSON.parse(rawData) as Record<string, unknown>;
    return {
      ok: true,
      identity: {
        manifestId: typeof parsed.manifest_id === "string" ? parsed.manifest_id : undefined,
        tableId: typeof parsed.table_id === "string" ? parsed.table_id : undefined,
        tableRegistryId: typeof parsed.table_registry_id === "string" ? parsed.table_registry_id : undefined,
      },
    };
  } catch (e) {
    return { ok: false, error: String(e) };
  }
}

/**
 * Creates an SSE receiver that connects to the backend SSE endpoint.
 * Projection events are fed as hook triggers with structured identity.
 * Error states are always explicit — never silent fallback.
 */
export function createSseReceiver(options: SseReceiverOptions): SseReceiver {
  const url = options.url ?? "/api/sse";
  let source: EventSource | null = null;

  function connect(): void {
    if (source !== null) return;

    source = new EventSource(url);

    source.addEventListener("ping", (e: MessageEvent) => {
      options.onEvent?.("ping", e.data);
    });

    source.addEventListener("projection", (e: MessageEvent) => {
      if (options.onProjectionHookTrigger) {
        const extracted = extractProjectionIdentity(e.data);
        if (!extracted.ok) {
          options.onError?.({ kind: "parse_error", rawData: e.data, error: extracted.error });
          return;
        }
        options.onProjectionHookTrigger({
          eventType: "projection",
          data: e.data,
          identity: extracted.identity,
        });
      } else {
        options.onEvent?.("projection", e.data);
      }
    });

    source.onmessage = (e: MessageEvent) => {
      options.onEvent?.("message", e.data);
    };

    source.onerror = (err: Event) => {
      if (source?.readyState === EventSource.CLOSED) {
        options.onError?.({ kind: "connection_closed" });
      } else {
        options.onError?.({ kind: "connection_error", event: err });
      }
    };
  }

  function disconnect(): void {
    source?.close();
    source = null;
  }

  return {
    connect,
    disconnect,
    get connected(): boolean {
      return source !== null && source.readyState !== EventSource.CLOSED;
    },
  };
}
