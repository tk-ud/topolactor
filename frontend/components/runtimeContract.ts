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
    topologyLayoutClassRefs?: string[];
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
  /**
   * Round 25 (admin-enum subBundle, Modal DOM containment): when true, this factory's render()
   * reads spec.authoredChildren (the REAL nested schema children — e.g. a Modal's Confirm/Cancel
   * Action children) and embeds them inside its own rendered VNode subtree (e.g. as a footer),
   * rather than components/LayoutProjectionTree.tsx rendering those children as trailing DOM
   * siblings after this component's own VNode. Generic capability flag, not a componentKind
   * special case in LayoutProjectionTree.tsx itself — any factory can opt in by setting this and
   * reading spec.authoredChildren; unset (the default for every existing factory) preserves
   * today's sibling-rendering behavior exactly, so this is purely additive.
   */
  acceptsAuthoredChildren?: boolean;
};
