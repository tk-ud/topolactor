/**
 * SSE dispatcher skeleton.
 *
 * Owns sse_dispatcher boundary per runtime-orchestration-ssot:
 * routes SSE events by type to registered handlers, feeding the frontend
 * scheduler queue as a hook_trigger. Currently a lightweight handler map.
 *
 * Lane: sse_projection_lane in pipeline-continuity-ssot.yaml (frontend.sse_dispatcher node).
 */

export type SseEventHandler = (data: string) => void;

export type SseDispatcher = {
  register: (eventType: string, handler: SseEventHandler) => void;
  route: (eventType: string, data: string) => void;
};

/**
 * Creates an SSE dispatcher. Registered handlers are called when route() is
 * invoked with a matching eventType. Unregistered types are silently ignored
 * (not a silent fallback — unregistered types are not error conditions for the skeleton).
 */
export function createSseDispatcher(): SseDispatcher {
  const handlers = new Map<string, SseEventHandler>();

  function register(eventType: string, handler: SseEventHandler): void {
    handlers.set(eventType, handler);
  }

  function route(eventType: string, data: string): void {
    const handler = handlers.get(eventType);
    if (handler) {
      handler(data);
    }
  }

  return { register, route };
}
