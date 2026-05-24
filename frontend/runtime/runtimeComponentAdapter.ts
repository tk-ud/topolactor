import type { ComponentDataHub } from "./projectionConstructor.ts";

export type RuntimeComponentSpec = {
  componentId: string;
  packageId?: string | null;
  layoutId?: string | null;
  componentType: string;
  props: Record<string, unknown>;
  eventBinding: Record<string, unknown>;
};

type AdaptResult = { ok: true; value: RuntimeComponentSpec } | { ok: false; error: string };

const SUPPORTED_COMPONENT_KINDS = new Set([
  "action/button",
  "form_input/input",
  "form_input/textarea",
  "form_input/search_input",
  "display/card",
  "disclosure_structure/panel",
  "disclosure_structure/section",
  "data_display/table",
  "data_display/data_grid",
  "data_display/list",
]);

export function adaptComponentDataHub(hub: ComponentDataHub): AdaptResult {
  if (!hub.componentId || hub.componentId.trim().length === 0) {
    return { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_ID" };
  }
  if (!hub.componentKind || hub.componentKind.trim().length === 0) {
    return { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_KIND" };
  }
  if (!SUPPORTED_COMPONENT_KINDS.has(hub.componentKind)) {
    return { ok: false, error: `RUNTIME_COMPONENT_ADAPTER_UNSUPPORTED_COMPONENT_KIND: ${hub.componentKind}` };
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
      packageId: hub.packageId,
      layoutId: hub.layoutId,
      componentType: hub.componentKind,
      props: hub.props,
      eventBinding: hub.eventBinding,
    },
  };
}
