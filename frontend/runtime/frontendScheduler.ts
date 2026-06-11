import { dispatchOperation } from "../api/dispatch.ts";
import type { DispatchRequest, DispatchResponse } from "../api/dispatch.ts";
import { resolveOperationVector } from "./resolveOperationVector.ts";
import type { UserOperation } from "./resolveOperationVector.ts";

export type NormalizedComponentEventType =
  | "click"
  | "change"
  | "select"
  | "toggle"
  | "expand"
  | "collapse"
  | "submit"
  | "focus"
  | "blur"
  | "drag"
  | "drop"
  | "search";

type ComponentOperationEvent = {
  componentId: string;
  packageId?: string | null;
  layoutId?: string | null;
  wiringId?: string | null;
  eventType: NormalizedComponentEventType;
  payload: Record<string, unknown>;
  actorOrSource: string;
  occurredAt: string;
  idempotencyKey: string;
};

type ComponentEventQueueConfig = {
  flushIntervalMs: number;
  maxBatchSize: number;
  maxRetry: number;
  localFallback: { schemaVersion: number; maxEvents: number; maxBytes: number; ttlMs: number };
};

const DEFAULT_EVENT_QUEUE_CONFIG: ComponentEventQueueConfig = {
  flushIntervalMs: 10_000,
  maxBatchSize: 100,
  maxRetry: 3,
  localFallback: { schemaVersion: 1, maxEvents: 300, maxBytes: 220_000, ttlMs: 24 * 60 * 60 * 1000 },
};

const LOCAL_STORAGE_KEY = "topolactor:component-event-fallback";
let queue: Array<ComponentOperationEvent & { retryCount: number }> = [];
let schedulerTimer: ReturnType<typeof setInterval> | null = null;
let lifecycleHookRegistered = false;

// ─── Retry / monitoring / failure path hooks ─────────────────────────────────
// Explicit failure observability. Silent failure is prohibited.
// Callers may register hooks to monitor flush failures and retry exhaustion.

type FlushFailureEvent = {
  kind: "flush_failed";
  error: string;
  remainingInQueue: number;
  retryingCount: number;
};

type RetryExhaustedEvent = {
  kind: "retry_exhausted";
  droppedCount: number;
};

type FlushMonitorEvent = FlushFailureEvent | RetryExhaustedEvent;

type FlushMonitorHook = (event: FlushMonitorEvent) => void;

const flushMonitorHooks: Set<FlushMonitorHook> = new Set();

/**
 * Register a monitoring hook for flush failures and retry exhaustion.
 * Returns an unregister function. Use for observability dashboards and alerting.
 * Silent failure is prohibited — hooks receive structured events for all failure paths.
 */
export function onFlushMonitor(hook: FlushMonitorHook): () => void {
  flushMonitorHooks.add(hook);
  return () => flushMonitorHooks.delete(hook);
}

function emitFlushMonitor(event: FlushMonitorEvent): void {
  for (const hook of flushMonitorHooks) {
    try { hook(event); } catch { /* hook errors must not propagate to flush path */ }
  }
}

function getStorage(): Storage | null {
  try {
    return globalThis.localStorage ?? null;
  } catch {
    return null;
  }
}

function shouldSkipPayload(value: unknown): boolean {
  return typeof value === "string" && value.length > 1_000;
}

function safePayload(payload: Record<string, unknown>): Record<string, unknown> {
  const filtered = Object.fromEntries(Object.entries(payload).filter(([k, v]) => !/token|secret|password|authorization/i.test(k) && !shouldSkipPayload(v)));
  return filtered;
}

function nowIso(): string {
  return new Date().toISOString();
}

function buildIdempotencyKey(event: Omit<ComponentOperationEvent, "idempotencyKey">): string {
  return `${event.componentId}:${event.eventType}:${event.occurredAt}:${JSON.stringify(event.payload)}`;
}

function persistFallback() {
  const storage = getStorage();
  if (!storage) return;
  const persistedAt = Date.now();
  const body = JSON.stringify({ schemaVersion: DEFAULT_EVENT_QUEUE_CONFIG.localFallback.schemaVersion, persistedAt, events: queue.slice(0, DEFAULT_EVENT_QUEUE_CONFIG.localFallback.maxEvents) });
  if (body.length > DEFAULT_EVENT_QUEUE_CONFIG.localFallback.maxBytes) {
    console.error("COMPONENT_EVENT_FALLBACK_TOO_LARGE");
    return;
  }
  storage.setItem(LOCAL_STORAGE_KEY, body);
}

function restoreFallback() {
  const storage = getStorage();
  if (!storage) return;
  const raw = storage.getItem(LOCAL_STORAGE_KEY);
  if (!raw) return;
  try {
    const parsed = JSON.parse(raw) as { schemaVersion: number; persistedAt: number; events: Array<ComponentOperationEvent & { retryCount: number }> };
    if (parsed.schemaVersion !== DEFAULT_EVENT_QUEUE_CONFIG.localFallback.schemaVersion) return;
    if (Date.now() - parsed.persistedAt > DEFAULT_EVENT_QUEUE_CONFIG.localFallback.ttlMs) return;
    queue = [...parsed.events, ...queue].slice(-DEFAULT_EVENT_QUEUE_CONFIG.localFallback.maxEvents);
  } catch {
    console.error("COMPONENT_EVENT_FALLBACK_PARSE_ERROR");
  }
}

export async function flushComponentEvents(): Promise<void> {
  if (queue.length === 0) return;
  if (typeof navigator !== "undefined" && navigator.onLine === false) {
    console.error("COMPONENT_EVENT_FLUSH_OFFLINE");
    persistFallback();
    return;
  }
  const batch = queue.slice(0, DEFAULT_EVENT_QUEUE_CONFIG.maxBatchSize);
  const token = globalThis.sessionStorage?.getItem("demo_jwt_token") ?? null;
  if (!token) {
    console.error("COMPONENT_EVENT_AUTH_TOKEN_MISSING");
    persistFallback();
    emitFlushMonitor({ kind: "flush_failed", error: "AUTH_TOKEN_MISSING", remainingInQueue: queue.length, retryingCount: 0 });
    return;
  }
  try {
    const response = await fetch("/api/component-events/append", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify({ events: batch }),
    });
    if (!response.ok) throw new Error(`HTTP_${response.status}`);
    queue = queue.slice(batch.length);
    persistFallback();
  } catch (error) {
    const errorMsg = error instanceof Error ? error.message : String(error);
    console.error("COMPONENT_EVENT_FLUSH_FAILED", error);
    const incremented = batch.map((evt) => ({ ...evt, retryCount: evt.retryCount + 1 }));
    const surviving = incremented.filter((evt) => evt.retryCount <= DEFAULT_EVENT_QUEUE_CONFIG.maxRetry);
    const droppedCount = incremented.length - surviving.length;
    queue = surviving.concat(queue.slice(batch.length));
    persistFallback();
    emitFlushMonitor({ kind: "flush_failed", error: errorMsg, remainingInQueue: queue.length, retryingCount: surviving.length });
    if (droppedCount > 0) {
      console.error(`COMPONENT_EVENT_RETRY_EXHAUSTED: ${droppedCount} event(s) dropped after maxRetry=${DEFAULT_EVENT_QUEUE_CONFIG.maxRetry}`);
      emitFlushMonitor({ kind: "retry_exhausted", droppedCount });
    }
  }
}

export function emitComponentOperationEvent(input: Omit<ComponentOperationEvent, "occurredAt" | "idempotencyKey" | "payload"> & { payload?: Record<string, unknown> }): { ok: true } | { ok: false; error: string } {
  const payload = safePayload(input.payload ?? {});
  if (!input.componentId) return { ok: false, error: "COMPONENT_EVENT_MISSING_COMPONENT_ID" };
  const base = {
    componentId: input.componentId,
    packageId: input.packageId,
    layoutId: input.layoutId,
    wiringId: input.wiringId,
    eventType: input.eventType,
    payload,
    actorOrSource: input.actorOrSource,
    occurredAt: nowIso(),
  };
  const event = { ...base, idempotencyKey: buildIdempotencyKey(base), retryCount: 0 };
  queue.push(event);
  if (queue.length > DEFAULT_EVENT_QUEUE_CONFIG.localFallback.maxEvents) queue = queue.slice(-DEFAULT_EVENT_QUEUE_CONFIG.localFallback.maxEvents);
  persistFallback();
  return { ok: true };
}

export function startComponentEventRuntime(): void {
  restoreFallback();
  if (schedulerTimer === null) {
    schedulerTimer = setInterval(() => {
      void flushComponentEvents();
    }, DEFAULT_EVENT_QUEUE_CONFIG.flushIntervalMs);
  }
  if (!lifecycleHookRegistered) {
    globalThis.addEventListener?.("beforeunload", () => {
      persistFallback();
    });
    lifecycleHookRegistered = true;
  }
}

export function stopComponentEventRuntime(): void {
  if (schedulerTimer !== null) clearInterval(schedulerTimer);
  schedulerTimer = null;
}

// ─── Client-command scheduler — api_command_lane ─────────────────────────────
// Owns client_command_order and async execution per runtime-orchestration-ssot
// (scheduler_contract.frontend_scope). sse_projection_order, rollback_boundary,
// optimistic_update_boundary, and collision_control are separate boundary contracts.
//
// Commands are queued in FIFO order and executed serially: a command does not
// start until the previous command has resolved or rejected. The caller awaits
// their own result; a failure in one command propagates only to that caller and
// does not block subsequent commands.
//
// Lane: api_command_lane in pipeline-continuity-ssot.yaml (frontend.scheduler node).

export type ScheduledCommandResult = DispatchResponse;

type UserOpEntry = {
  kind: "user_op";
  op: UserOperation;
  token?: string;
  context?: Record<string, string>;
  resolve: (result: ScheduledCommandResult) => void;
  reject: (error: unknown) => void;
};

type AdminDispatchEntry = {
  kind: "admin_dispatch";
  req: Omit<DispatchRequest, "triggerKind" | "role">;
  token?: string;
  resolve: (result: ScheduledCommandResult) => void;
  reject: (error: unknown) => void;
};

type PendingCommandEntry = UserOpEntry | AdminDispatchEntry;

const clientCommandQueue: PendingCommandEntry[] = [];
let clientCommandQueueRunning = false;

async function drainClientCommandQueue(): Promise<void> {
  if (clientCommandQueueRunning) return;
  clientCommandQueueRunning = true;
  try {
    while (clientCommandQueue.length > 0) {
      const entry = clientCommandQueue.shift()!;
      try {
        let result: ScheduledCommandResult;
        if (entry.kind === "admin_dispatch") {
          // deno-lint-ignore no-unused-vars
          const { role: _role, triggerKind: _tk, ...safeReq } = entry.req as DispatchRequest;
          result = await dispatchOperation({ ...safeReq, triggerKind: "client" }, entry.token);
        } else {
          const vector = resolveOperationVector(entry.op);
          result = await dispatchOperation(
            {
              operationType: entry.op.operationType,
              target: vector.target,
              layer: vector.layer,
              action: vector.action,
              payload: entry.op.payload,
              triggerKind: "client",
              context: entry.context && Object.keys(entry.context).length > 0 ? entry.context : undefined,
            },
            entry.token,
          );
        }
        entry.resolve(result);
      } catch (err) {
        entry.reject(err);
      }
    }
  } finally {
    clientCommandQueueRunning = false;
  }
}

/**
 * Queues a client command and dispatches it through the api_client.
 * Commands execute serially in FIFO order. Errors propagate explicitly to the
 * awaiter of the failed command; subsequent commands are unaffected.
 */
export async function queueClientCommand(
  op: UserOperation,
  token?: string,
  context?: Record<string, string>,
): Promise<ScheduledCommandResult> {
  return new Promise<ScheduledCommandResult>((resolve, reject) => {
    clientCommandQueue.push({ kind: "user_op", op, token, context, resolve, reject });
    if (!clientCommandQueueRunning) {
      void drainClientCommandQueue();
    }
  });
}

/**
 * Queues an admin dispatch request through the same FIFO client_command_lane.
 * Injects triggerKind="client" unconditionally — callers must not set role.
 * Use for admin UI operations that cannot be expressed as UserOperation.
 */
export async function queueAdminClientCommand(
  req: Omit<DispatchRequest, "triggerKind" | "role">,
  token?: string,
): Promise<ScheduledCommandResult> {
  return new Promise<ScheduledCommandResult>((resolve, reject) => {
    clientCommandQueue.push({ kind: "admin_dispatch", req: { ...req }, token, resolve, reject });
    if (!clientCommandQueueRunning) {
      void drainClientCommandQueue();
    }
  });
}


/**
 * Full dispatch spec derived from ui_wiring_registry via backend emission.
 * Carries the complete routing tuple for component_wiring_execution_lane dispatch.
 * target and layer are derived from targetSurface and wiringKind by renderEmission.
 * targetRef carries the admin-configured manifest reference (e.g. "manifest:<uuid>:<wiringKey>")
 * and is forwarded as target_ref in the dispatch payload so the backend can route to the
 * specific manifest chosen in PackageWiringEditor instead of resolving by axes alone.
 */
export type RuntimeDispatchSpec = {
  operationType: string;
  target: string;
  layer: string;
  action: string;
  wiringKey?: string;
  wiringId?: string;
  /** Admin-configured target reference: "manifest:<uuid>:<wiringKey>" or similar. */
  targetRef?: string;
};

/**
 * Enqueues a runtime component command through the api_command_lane.
 * Called by emitBoundEvent when a binding carries runtimeDispatch.
 * This is the component_wiring_execution_lane — explicitly separate from the
 * frontend_component_event_log_lane (emitComponentOperationEvent).
 *
 * Receives the full dispatch spec from backend emission (not just an action string).
 * target/layer come from the admin-configured wiring (targetSurface, wiringKind).
 * wiringKey, wiringId, and targetRef are forwarded in the payload so the backend can
 * route to the exact admin-chosen manifest when target_ref is present.
 * Reads token from sessionStorage (same source as flushComponentEvents).
 * Uses queueAdminClientCommand so triggerKind="client" is always injected.
 */
export async function enqueueRuntimeComponentCommand(
  spec: RuntimeDispatchSpec,
): Promise<ScheduledCommandResult> {
  const token = globalThis.sessionStorage?.getItem("demo_jwt_token") ?? undefined;
  const payload: Record<string, unknown> = {};
  if (spec.wiringKey) payload.wiring_key = spec.wiringKey;
  if (spec.wiringId) payload.wiring_id = spec.wiringId;
  if (spec.targetRef) payload.target_ref = spec.targetRef;
  return queueAdminClientCommand(
    {
      operationType: spec.operationType,
      target: spec.target,
      layer: spec.layer,
      action: spec.action,
      ...(Object.keys(payload).length > 0 ? { payload } : {}),
    },
    token,
  );
}

/**
 * Enqueues a projection hook trigger from the SSE receiver into the SSE dispatcher.
 *
 * Bridge in the sse_projection_lane:
 *   sseReceiver → enqueueProjectionHookTrigger (scheduler hook) → sseDispatcher → projectionRuntime
 *
 * Identity is preserved in trigger.data (contains manifest_id/table_id/table_registry_id).
 * The dispatcher routes by trigger.eventType to the registered projection runtime handler.
 *
 * Completion condition: sse_receiver_feeds_frontend_scheduler_as_hook_trigger
 */
export function enqueueProjectionHookTrigger(
  trigger: { eventType: string; data: string },
  dispatcher: { route: (eventType: string, data: string) => void },
): void {
  dispatcher.route(trigger.eventType, trigger.data);
}

export const __testOnly = {
  resetQueue(): void {
    queue = [];
    const storage = getStorage();
    storage?.removeItem(LOCAL_STORAGE_KEY);
  },
  getQueueLength(): number {
    return queue.length;
  },
  getQueueSnapshot(): Array<ComponentOperationEvent & { retryCount: number }> {
    return queue.map((e) => ({ ...e }));
  },
  clearMonitorHooks(): void {
    flushMonitorHooks.clear();
  },
  getCommandQueueLength(): number {
    return clientCommandQueue.length;
  },
  isCommandQueueRunning(): boolean {
    return clientCommandQueueRunning;
  },
  resetCommandQueue(): void {
    clientCommandQueue.length = 0;
    clientCommandQueueRunning = false;
  },
};
