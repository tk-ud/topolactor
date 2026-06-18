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

/**
 * CI Attention fragment projection payload emitted by backend ci_attention:refresh_fragments.
 * Frontend uses this for read-only live guidance display updates.
 * Frontend does NOT hold promotion authority from this payload.
 */
export type CiAttentionFragmentProjectionPayload = {
  FragmentId: string;
  Kind: string;
  Status: string;
  Severity: string;
  TargetKind: string;
  TargetKey: string;
  AuthoringSurface: string;
  UpdatedAt: string;
};

/**
 * Attempts to extract a CiAttentionFragmentProjectionPayload from a raw SSE projection event data string.
 * Returns the payload if it is a CI Attention fragment event; null otherwise.
 * Never throws; parse errors return null.
 */
export function extractCiAttentionFragmentPayload(
  rawData: string,
): CiAttentionFragmentProjectionPayload | null {
  try {
    const parsed = JSON.parse(rawData) as Record<string, unknown>;
    if (typeof parsed.FragmentId !== "string") return null;
    return {
      FragmentId: parsed.FragmentId,
      Kind: typeof parsed.Kind === "string" ? parsed.Kind : "",
      Status: typeof parsed.Status === "string" ? parsed.Status : "",
      Severity: typeof parsed.Severity === "string" ? parsed.Severity : "",
      TargetKind: typeof parsed.TargetKind === "string" ? parsed.TargetKind : "",
      TargetKey: typeof parsed.TargetKey === "string" ? parsed.TargetKey : "",
      AuthoringSurface: typeof parsed.AuthoringSurface === "string" ? parsed.AuthoringSurface : "",
      UpdatedAt: typeof parsed.UpdatedAt === "string" ? parsed.UpdatedAt : "",
    };
  } catch {
    return null;
  }
}

export type SseReceiverOptions = {
  /** URL to connect to. Defaults to /api/sse. */
  url?: string;
  /**
   * Access token for the SSE endpoint (used in the access_token query parameter).
   * When absent, falls back to sessionStorage demo_jwt_token.
   * Prefer passing a ref-backed token here to avoid sessionStorage reads after mount.
   */
  token?: string;
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
  const baseUrl = options.url ?? "/api/sse";
  let source: EventSource | null = null;

  function connect(): void {
    if (source !== null) return;

    const token = options.token ?? globalThis.sessionStorage?.getItem("demo_jwt_token") ?? null;
    if (!token) {
      options.onError?.({ kind: "connection_error", event: new Event("AUTH_TOKEN_MISSING") });
      return;
    }
    const qs = new URLSearchParams({ access_token: token });
    source = new EventSource(`${baseUrl}?${qs.toString()}`);

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
