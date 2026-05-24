import type { ComponentDataHub } from "./projectionConstructor.ts";

export type RuntimeComponentSpec = {
  componentId: string;
  componentType: string;
  props: Record<string, unknown>;
  eventBinding: Record<string, unknown>;
};

export function adaptComponentDataHub(hub: ComponentDataHub): RuntimeComponentSpec {
  return {
    componentId: hub.componentId ?? "unknown",
    componentType: hub.componentKind,
    props: hub.props,
    eventBinding: hub.eventBinding,
  };
}
