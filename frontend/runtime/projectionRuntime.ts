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

/**
 * Policy for projection events received before a ProjectionDefinition is set.
 *
 *   "error"  — emit PROJECTION_RUNTIME_DEFINITION_MISSING to console.error (default).
 *              Silent fallback is prohibited; missing definition is an explicit configuration gap.
 *   "ignore" — explicit acknowledged no-op. Use only when the caller owns the lifecycle
 *              and knows events may arrive before definition is available.
 */
export type DefinitionMissingPolicy = "error" | "ignore";

export type ProjectionRuntimeOptions = {
  /** Policy when projection events arrive before a definition is set. Default: "error". */
  definitionMissingPolicy?: DefinitionMissingPolicy;
};

export type ProjectionRuntime = {
  /** Register a handler for projection updates. Returns an unregister function. */
  onProjectionUpdate: (handler: ProjectionUpdateHandler) => () => void;
  /** Process an incoming projection event payload from the sse_dispatcher. */
  handleProjectionEvent: (rawData: string) => void;
  /** Set the projection definition (loaded from manifest via dispatch response). */
  setProjectionDefinition: (definition: ProjectionDefinition | null) => void;
};

/**
 * Creates a projection_runtime instance.
 * Receives hook_triggers from sse_dispatcher and produces ui_projection updates.
 *
 * Call setProjectionDefinition() with the projectionDefinition from the dispatch response
 * Emission before SSE projection events arrive. When no definition is set and an event
 * arrives, the definitionMissingPolicy determines the behavior (default: "error").
 */
export function createProjectionRuntime(options?: ProjectionRuntimeOptions): ProjectionRuntime {
  const handlers = new Set<ProjectionUpdateHandler>();
  let currentDefinition: ProjectionDefinition | null = null;
  const definitionMissingPolicy: DefinitionMissingPolicy = options?.definitionMissingPolicy ?? "error";

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
      if (definitionMissingPolicy === "ignore") {
        // Explicit acknowledged no-op: caller declared events may arrive before definition is set.
        return;
      }
      // Default "error" policy: explicit diagnostic error. Silent fallback is prohibited.
      // Call setProjectionDefinition() with emission.projectionDefinition before events arrive.
      console.error(
        "[projectionRuntime] PROJECTION_RUNTIME_DEFINITION_MISSING: received projection event but no ProjectionDefinition is set. " +
        "Load the projectionDefinition from the dispatch response Emission and call setProjectionDefinition() before SSE events arrive.",
      );
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
