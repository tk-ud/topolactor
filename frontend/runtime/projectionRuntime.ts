/**
 * projection_runtime — handles SSE projection events from the sse_projection_lane.
 *
 * Per SSOT frontend_contract.lanes.projection_event_lane:
 *   sse_receiver -> frontend_scheduler -> sse_dispatcher -> projection_runtime -> ui_projection
 *
 * This module owns the projection_runtime boundary:
 * - receives hook_triggers from the frontend scheduler (fed by sse_dispatcher)
 * - applies projection_constructor to produce ui_projection updates
 * - notifies registered UI update handlers
 *
 * Frontend must not perform topology meaning judgment or sql_attention_judgment.
 * All projection logic is data-defined via manifest projection_constructor_mapping.
 */

import { constructProjection } from "./projectionConstructor.ts";
import type { ProjectionDefinition, UiProjection } from "./projectionConstructor.ts";

export type ProjectionEventPayload = {
  table_id?: string;
  table_registry_id?: string;
  manifest_id?: string;
  data?: Record<string, unknown>;
};

export type ProjectionUpdateHandler = (projection: UiProjection, payload: ProjectionEventPayload) => void;

export type ProjectionRuntime = {
  /** Register a handler for projection updates. Returns an unregister function. */
  onProjectionUpdate: (handler: ProjectionUpdateHandler) => () => void;
  /** Process an incoming projection event payload from the sse_dispatcher. */
  handleProjectionEvent: (rawData: string) => void;
  /** Set the projection definition (loaded from manifest via dispatch). */
  setProjectionDefinition: (definition: ProjectionDefinition | null) => void;
};

/**
 * Creates a projection_runtime instance.
 * Receives hook_triggers from sse_dispatcher and produces ui_projection updates.
 */
export function createProjectionRuntime(): ProjectionRuntime {
  const handlers = new Set<ProjectionUpdateHandler>();
  let currentDefinition: ProjectionDefinition | null = null;

  function onProjectionUpdate(handler: ProjectionUpdateHandler): () => void {
    handlers.add(handler);
    return () => handlers.delete(handler);
  }

  function setProjectionDefinition(definition: ProjectionDefinition | null): void {
    currentDefinition = definition;
  }

  function handleProjectionEvent(rawData: string): void {
    let payload: ProjectionEventPayload;
    try {
      payload = JSON.parse(rawData) as ProjectionEventPayload;
    } catch {
      console.error("[projectionRuntime] failed to parse projection event data:", rawData);
      return;
    }

    if (!currentDefinition) {
      console.warn("[projectionRuntime] received projection event but no definition is set; skipping.");
      return;
    }

    const jsonKeyValue: Record<string, unknown> = payload.data ?? {};
    const result = constructProjection(jsonKeyValue, currentDefinition);

    if (!result.projection) {
      console.error("[projectionRuntime] projection construction failed:", result.error);
      return;
    }

    const projection = result.projection;
    for (const handler of handlers) {
      try {
        handler(projection, payload);
      } catch (err) {
        console.error("[projectionRuntime] handler threw:", err);
      }
    }
  }

  return {
    onProjectionUpdate,
    handleProjectionEvent,
    setProjectionDefinition,
  };
}
