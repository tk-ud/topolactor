import { h, type JSX, type VNode } from "preact";
import {
  buildChildrenMap,
  type ComponentSpec,
} from "../runtime/renderEmission.ts";
import { flowNodePresentation, flowRootClassName } from "../runtime/layoutNodeFlowProjection.ts";
import { resolveInlineStyleFromCssTokenRefs } from "../runtime/cssDictionary.ts";
import { interpolateLinkHrefReadOnly } from "../runtime/linkPlaceholderInterpolation.ts";
import { renderRuntimeComponent } from "../runtime/runtimePrimitiveRenderer.ts";
import { componentAcceptsAuthoredChildren } from "../runtime/runtimeComponentRegistry.ts";
import {
  isLayoutStructureContainerClassRefs,
  resolveFlowContainerPreviewClassName,
  resolveFlowNodePreviewClassName,
  FLOW_LEAF_ROLES,
} from "../runtime/layoutClassPreviewUtils.ts";
import type { RuntimeGuardedStateStore } from "../runtime/runtimeComponentAdapter.ts";
import {
  buildVisibilityGraph,
  resolveNodeVisibility,
  type VisibilityGraphNode,
} from "../runtime/structuralVisibility.ts";

export type LayoutProjectionTreeProps = {
  specs: ComponentSpec[];
  layoutId?: string;
  rootLayoutClassRefs?: string[];
  /**
   * Projection-local state store — same instance renderEmission()'s
   * localStateStore option and the uiEventEffectRunner use. Absent (draft
   * preview / callers that never wire one in) means every visibilityBinding on
   * `specs` fails close as STRUCTURAL_VISIBILITY_BINDING_SOURCE_UNDECLARED
   * rather than defaulting to visible or hidden. See structuralVisibility.ts.
   */
  localStateStore?: RuntimeGuardedStateStore;
};

function isContainerSpec(
  spec: ComponentSpec,
  childCount: number,
): boolean {
  if (childCount > 0) return true;
  return isLayoutStructureContainerClassRefs(spec.layoutClassRefs ?? []);
}

function mergePresentationStyle(
  spec: ComponentSpec,
  childCount: number,
): { style: Record<string, string>; className: string | undefined } {
  const isContainer = isContainerSpec(spec, childCount);
  const { style, className: flowClassName } = flowNodePresentation(spec);
  const className = isContainer
    ? resolveFlowContainerPreviewClassName(spec.layoutClassRefs ?? []) || flowClassName
    : resolveFlowNodePreviewClassName(spec.layoutClassRefs ?? [], {
      allowedFor: FLOW_LEAF_ROLES,
    }) || flowClassName;
  const tokenResult = spec.cssTokenRefs?.length
    ? resolveInlineStyleFromCssTokenRefs(spec.cssTokenRefs)
    : { ok: true as const, style: {} };
  return {
    style: { ...style, ...(tokenResult.ok ? tokenResult.style : {}) },
    className: className || undefined,
  };
}

function ProjectionTreeNode({
  spec,
  childrenMap,
  visibilityGraph,
  localStateStore,
}: {
  spec: ComponentSpec;
  childrenMap: Map<string | undefined, ComponentSpec[]>;
  visibilityGraph: ReadonlyMap<string, VisibilityGraphNode>;
  localStateStore?: RuntimeGuardedStateStore;
}): JSX.Element | null {
  // structural_subtree_conditional_visibility_contract: evaluated BEFORE any
  // other branch below — an invisible node unmounts its entire subtree (never
  // recurses into childElements at all), and a resolver error surfaces as an
  // explicit error box rather than silently rendering or silently vanishing.
  // Generic over every nodeKind/componentKind; no credential-management or
  // manifest-specific literal here.
  if (spec.nodeId) {
    const visibility = resolveNodeVisibility(
      spec.nodeId,
      visibilityGraph,
      localStateStore,
    );
    if (!visibility.ok) {
      return (
        <div
          class="rounded border border-red-200 bg-red-50 p-2 text-sm text-red-700"
          data-node-id={spec.nodeId}
        >
          {visibility.error}
        </div>
      );
    }
    if (!visibility.visible) return null;
  }
  const children = childrenMap.get(spec.nodeId) ?? [];
  const childElements = children.map((child) => (
    <ProjectionTreeNode
      key={child.nodeId ?? `${child.orderIndex}`}
      spec={child}
      childrenMap={childrenMap}
      visibilityGraph={visibilityGraph}
      localStateStore={localStateStore}
    />
  ));
  const { style, className } = mergePresentationStyle(spec, children.length);
  const commonProps = {
    style: Object.keys(style).length > 0 ? style : undefined,
    class: className || undefined,
    "data-node-id": spec.nodeId,
  };

  if (spec.componentType === "error") {
    return (
      <div
        class="rounded border border-red-200 bg-red-50 p-2 text-sm text-red-700"
        data-node-id={spec.nodeId}
      >
        {String(spec.def?.error ?? "投影エラー")}
        {spec.def?.code && (
          <span class="ml-1 font-mono text-xs">({String(spec.def.code)})</span>
        )}
      </div>
    );
  }

  if (spec.componentType === "structural_node") {
    const recordType = typeof spec.def?.recordType === "string" ? spec.def.recordType : undefined;
    return (
      <div
        {...commonProps}
        data-structural-node-record-type={recordType}
      >
        {spec.inlineText && (
          <p class="structural-node-label font-medium">{spec.inlineText}</p>
        )}
        {childElements}
      </div>
    );
  }

  if (spec.nodeKind === "structural_html" && spec.htmlTag) {
    const text = spec.inlineText?.trim();
    const authoredHref = typeof spec.def?.linkHref === "string" ? spec.def.linkHref : undefined;
    const linkPreview = spec.htmlTag === "a" ? interpolateLinkHrefReadOnly(authoredHref) : null;
    if (linkPreview && !linkPreview.ok) {
      return (
        <div class="rounded border border-red-200 bg-red-50 p-2 text-sm text-red-700" data-node-id={spec.nodeId}>
          {linkPreview.message}
        </div>
      );
    }
    return h(
      spec.htmlTag,
      {
        ...commonProps,
        ...(spec.htmlTag === "a" && linkPreview?.ok && linkPreview.value ? { href: linkPreview.value } : {}),
      },
      text || (linkPreview?.ok ? linkPreview.value : null),
      ...childElements,
    ) as JSX.Element;
  }

  if (spec.runtimeSpec) {
    // Round 25 (Modal DOM containment): a componentKind that declares
    // acceptsAuthoredChildren (currently disclosure/modal) embeds its REAL schema children
    // itself (see modalFactory's footer usage) instead of them being rendered as trailing DOM
    // siblings below. Any other componentKind (unset — the default) keeps today's exact
    // behavior: children still passed through as siblings, authoredChildren left undefined.
    const embedsChildrenItself = componentAcceptsAuthoredChildren(spec.componentType);
    const rendered = renderRuntimeComponent(
      spec.runtimeSpec,
      embedsChildrenItself ? childElements : undefined,
    );
    if (!rendered.ok) {
      return (
        <div
          {...commonProps}
          class={["rounded border border-red-200 bg-red-50 p-2 text-sm text-red-700", commonProps.class].filter(Boolean).join(" ")}
        >
          {rendered.error}
        </div>
      );
    }
    return (
      <div {...commonProps} data-component-id={spec.componentId}>
        {rendered.node}
        {embedsChildrenItself ? null : childElements}
      </div>
    );
  }

  const legacyNode = spec.def?.node as VNode | undefined;
  return (
    <div {...commonProps} data-component-id={spec.componentId}>
      {legacyNode}
      {childElements}
    </div>
  );
}

/**
 * Shared layout projection tree — product (/ SSE dispatch) and demo (draft preview)
 * both render through renderEmission → this component.
 */
export function LayoutProjectionTree({
  specs,
  layoutId,
  rootLayoutClassRefs = [],
  localStateStore,
}: LayoutProjectionTreeProps): JSX.Element {
  const childrenMap = buildChildrenMap(specs);
  const roots = childrenMap.get(undefined) ?? [];
  const rootClass = flowRootClassName(rootLayoutClassRefs);
  const visibilityGraph = buildVisibilityGraph(
    specs
      .filter((s): s is ComponentSpec & { nodeId: string } =>
        typeof s.nodeId === "string" && s.nodeId.length > 0
      )
      .map((s) => ({
        nodeId: s.nodeId,
        parentNodeId: s.parentNodeId,
        visibilityBinding: s.visibilityBinding,
      })),
  );

  return (
    <div data-layout-id={layoutId} class="layout-projection-tree">
      <div class={rootClass || undefined}>
        {roots.length === 0
          ? (
            <p class="text-sm text-gray-500">
              投影ノードがありません。
            </p>
          )
          : roots.map((root) => (
            <ProjectionTreeNode
              key={root.nodeId ?? `root-${root.orderIndex}`}
              spec={root}
              childrenMap={childrenMap}
              visibilityGraph={visibilityGraph}
              localStateStore={localStateStore}
            />
          ))}
      </div>
    </div>
  );
}
