/**
 * SSE receiver skeleton.
 *
 * Owns the sse_receiver boundary per runtime-orchestration-ssot:
 * external_event_listener feeding a hook_trigger into the frontend scheduler queue.
 * Currently provides connect/disconnect and raw event routing to a handler.
 *
 * Lane: sse_projection_lane in pipeline-continuity-ssot.yaml (frontend.sse_receiver node).
 */

export type SseEventHandler = (eventType: string, data: string) => void;

export type SseReceiverOptions = {
  /** URL to connect to. Defaults to /api/sse. */
  url?: string;
  onEvent: SseEventHandler;
  onError?: (err: Event) => void;
};

export type SseReceiver = {
  connect: () => void;
  disconnect: () => void;
  readonly connected: boolean;
};

/**
 * Creates an SSE receiver that connects to the backend SSE endpoint and
 * routes events to the provided handler. Call connect() to start.
 */
export function createSseReceiver(options: SseReceiverOptions): SseReceiver {
  const url = options.url ?? "/api/sse";
  let source: EventSource | null = null;

  function connect(): void {
    if (source !== null) return;

    source = new EventSource(url);

    source.addEventListener("ping", (e: MessageEvent) => {
      options.onEvent("ping", e.data);
    });

    source.onmessage = (e: MessageEvent) => {
      options.onEvent("message", e.data);
    };

    source.onerror = (err: Event) => {
      options.onError?.(err);
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
