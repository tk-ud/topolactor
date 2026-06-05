import type { ComponentDataHub } from "./projectionConstructor.ts";
import type { RuntimeTopologyComponentProps } from "../components/runtimeContract.ts";
import {
  ensureRuntimeComponentRegistryInitialized,
  hasRuntimeComponentFactory,
} from "./runtimeComponentRegistry.ts";
import { resolveTopologyLayoutClassRefs } from "./topologyLayoutClassResolver.ts";

type NormalizedDesign = {
  classname?: string;
  className?: string;
  tailwind?: string;
  style?: string;
  state?: "default" | "loading" | "success" | "error";
  topologyLayoutClassRefs?: string[];
};

export type RuntimeComponentSpec = {
  componentId: string;
  packageId?: string | null;
  layoutId?: string | null;
  wiringId?: string | null;
  componentType: string;
  props: RuntimeTopologyComponentProps;
  eventBinding: Record<string, unknown>;
  className?: string;
  design?: NormalizedDesign;
  /** UI Builder canvas preview — no runtime event dispatch; relaxed binding checks. */
  previewMode?: boolean;
};

type AdaptResult = { ok: true; value: RuntimeComponentSpec } | {
  ok: false;
  error: string;
};

function extractTopologyLayoutClassRefs(
  value: unknown,
): string[] | undefined {
  if (!Array.isArray(value)) return undefined;
  const refs = value.filter((x): x is string =>
    typeof x === "string" && x.trim().length > 0
  );
  return refs.length > 0 ? refs : undefined;
}

function normalizeDesign(
  value: unknown,
): { ok: true; value: NormalizedDesign; className?: string } | {
  ok: false;
  error: string;
} {
  if (value === undefined || value === null) return { ok: true, value: {} };
  if (typeof value !== "object" || Array.isArray(value)) {
    return { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_INVALID_DESIGN" };
  }
  const raw = value as Record<string, unknown>;
  const topologyLayoutClassRefs = extractTopologyLayoutClassRefs(
    raw.topologyLayoutClassRefs,
  );
  const classname = typeof raw.classname === "string"
    ? raw.classname.trim()
    : undefined;
  const className = typeof raw.className === "string"
    ? raw.className.trim()
    : undefined;
  const tailwind = typeof raw.tailwind === "string"
    ? raw.tailwind.trim()
    : undefined;
  const style = typeof raw.style === "string" ? raw.style : undefined;
  const stateRaw = raw.state;
  const state = stateRaw === "default" || stateRaw === "loading" ||
      stateRaw === "success" || stateRaw === "error"
    ? stateRaw
    : undefined;
  if (stateRaw !== undefined && state === undefined) {
    return {
      ok: false,
      error: "RUNTIME_COMPONENT_ADAPTER_INVALID_DESIGN_STATE",
    };
  }

  if (topologyLayoutClassRefs) {
    const resolved = resolveTopologyLayoutClassRefs(topologyLayoutClassRefs);
    if (!resolved.ok) {
      return { ok: false, error: resolved.error };
    }
    return {
      ok: true,
      value: { topologyLayoutClassRefs, style, state },
      className: resolved.className,
    };
  }

  const merged = [classname, className, tailwind].filter((v): v is string =>
    Boolean(v && v.length > 0)
  ).join(" ").trim();
  return {
    ok: true,
    value: { classname, className, tailwind, style, state },
    className: merged.length > 0 ? merged : undefined,
  };
}

function normalizeTopologyProps(
  props: Record<string, unknown>,
): RuntimeTopologyComponentProps {
  return props as RuntimeTopologyComponentProps;
}

export function adaptComponentDataHub(hub: ComponentDataHub): AdaptResult {
  if (!hub.componentId || hub.componentId.trim().length === 0) {
    return {
      ok: false,
      error: "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_ID",
    };
  }
  if (!hub.componentKind || hub.componentKind.trim().length === 0) {
    return {
      ok: false,
      error: "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_KIND",
    };
  }
  ensureRuntimeComponentRegistryInitialized();
  if (!hasRuntimeComponentFactory(hub.componentKind)) {
    return {
      ok: false,
      error:
        `RUNTIME_COMPONENT_ADAPTER_UNSUPPORTED_COMPONENT_KIND: ${hub.componentKind}`,
    };
  }
  if (
    typeof hub.props !== "object" || hub.props === null ||
    Array.isArray(hub.props)
  ) {
    return { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_INVALID_PROPS" };
  }
  if (
    typeof hub.eventBinding !== "object" || hub.eventBinding === null ||
    Array.isArray(hub.eventBinding)
  ) {
    return {
      ok: false,
      error: "RUNTIME_COMPONENT_ADAPTER_INVALID_EVENT_BINDING",
    };
  }
  const normalizedDesign = normalizeDesign(hub.design);
  if (!normalizedDesign.ok) return normalizedDesign;
  const design = normalizedDesign.value;
  let className = normalizedDesign.className;

  const props = hub.props as RuntimeTopologyComponentProps;
  const layoutCssRefs = props.layoutCss?.topologyLayoutClassRefs;
  if (layoutCssRefs?.length) {
    const resolved = resolveTopologyLayoutClassRefs(layoutCssRefs);
    if (!resolved.ok) return { ok: false, error: resolved.error };
    className = resolved.className;
  }

  return {
    ok: true,
    value: {
      componentId: hub.componentId,
      packageId: hub.packageId,
      layoutId: hub.layoutId,
      wiringId: hub.wiringId,
      componentType: hub.componentKind,
      props: normalizeTopologyProps(hub.props),
      eventBinding: hub.eventBinding,
      className,
      design,
    },
  };
}
