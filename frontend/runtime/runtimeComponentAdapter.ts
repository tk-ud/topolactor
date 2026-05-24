import type { ComponentDataHub } from "./projectionConstructor.ts";

export type RuntimeComponentSpec = {
  componentId: string;
  componentType: string;
  props: Record<string, unknown>;
  eventBinding: Record<string, unknown>;
};

type AdaptResult = { ok: true; value: RuntimeComponentSpec } | { ok: false; error: string };

export function adaptComponentDataHub(hub: ComponentDataHub): AdaptResult {
  if (!hub.componentId || hub.componentId.trim().length === 0) {
    return { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_ID" };
  }
  if (!hub.componentKind || hub.componentKind.trim().length === 0) {
    return { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_KIND" };
  }
  if (typeof hub.props !== "object" || hub.props === null || Array.isArray(hub.props)) {
    return { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_INVALID_PROPS" };
  }
  if (typeof hub.eventBinding !== "object" || hub.eventBinding === null || Array.isArray(hub.eventBinding)) {
    return { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_INVALID_EVENT_BINDING" };
  }
  return {
    ok: true,
    value: {
      componentId: hub.componentId,
      componentType: hub.componentKind,
      props: hub.props,
      eventBinding: hub.eventBinding,
    },
  };
}
