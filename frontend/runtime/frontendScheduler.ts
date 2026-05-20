import { dispatchOperation } from "../api/dispatch.ts";
import type { DispatchResponse } from "../api/dispatch.ts";
import { resolveOperationVector } from "./resolveOperationVector.ts";
import type { UserOperation } from "./resolveOperationVector.ts";

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
