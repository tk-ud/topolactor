import { dispatchOperation } from "../api/dispatch.ts";
import type { DispatchResponse } from "../api/dispatch.ts";
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
  | "drop";

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
  try {
    const response = await fetch("/api/component-events/append", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ events: batch }),
    });
    if (!response.ok) throw new Error(`HTTP_${response.status}`);
    queue = queue.slice(batch.length);
    persistFallback();
  } catch (error) {
    console.error("COMPONENT_EVENT_FLUSH_FAILED", error);
    queue = batch.map((evt) => ({ ...evt, retryCount: evt.retryCount + 1 })).filter((evt) => evt.retryCount <= DEFAULT_EVENT_QUEUE_CONFIG.maxRetry)
      .concat(queue.slice(batch.length));
    persistFallback();
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

/**
 * Client-command scheduler skeleton.
 *
 * Owns client_command_order and sse_projection_order boundaries per
 * runtime-orchestration-ssot. Currently executes client commands synchronously
 * (immediate pass-through). Ordering, batching, and rollback boundaries are
 * future scope confined to this module.
 *
 * Lane: api_command_lane in pipeline-continuity-ssot.yaml (frontend.scheduler node).
 */

export type ScheduledCommandResult = DispatchResponse;

/**
 * Queues a client command and dispatches it through the api_client.
 * Skeleton: executes immediately. When scheduler semantics are implemented,
 * ordering and collision control are added here without changing callers.
 */
export async function queueClientCommand(
  op: UserOperation,
  token?: string,
  context?: Record<string, string>,
): Promise<ScheduledCommandResult> {
  const vector = resolveOperationVector(op);

  return dispatchOperation(
    {
      operationType: op.operationType,
      target: vector.target,
      layer: vector.layer,
      action: vector.action,
      payload: op.payload,
      context: context && Object.keys(context).length > 0 ? context : undefined,
    },
    token,
  );
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
};
