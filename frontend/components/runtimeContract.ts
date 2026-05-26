import type { VNode } from "preact";
import type { RuntimeComponentSpec } from "../runtime/runtimeComponentAdapter.ts";

export type RuntimeRenderableComponent = VNode<any>;

export type RuntimeComponentPropsContract = Record<string, unknown>;
export type RuntimeTopologyComponentProps = RuntimeComponentPropsContract & {
  table?: unknown;
  data?: unknown;
  layoutCss?: {
    classname?: string;
    className?: string;
    style?: string;
    cssTokenRefs?: string[];
    responsiveTokenRefs?: Record<string, string[]>;
  };
  designTailwind?: {
    tailwind?: string;
    state?: "default" | "loading" | "success" | "error";
  };
  eventBinding?: Record<string, unknown>;
};

export type RuntimePrimitiveComponent = (spec: RuntimeComponentSpec) =>
  | { ok: true; node: RuntimeRenderableComponent }
  | { ok: false; error: string };

export type RuntimeComponentFactory = {
  componentKinds: readonly string[];
  render: RuntimePrimitiveComponent;
};
