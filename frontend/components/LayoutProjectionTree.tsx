import { h, type JSX, type VNode } from "preact";
import {
  buildChildrenMap,
  type ComponentSpec,
} from "../runtime/renderEmission.ts";
import { flowNodePresentation, flowRootClassName } from "../runtime/layoutNodeFlowProjection.ts";
import { resolveInlineStyleFromCssTokenRefs } from "../runtime/cssDictionary.ts";
import { interpolateLinkHrefReadOnly } from "../runtime/linkPlaceholderInterpolation.ts";
import { renderRuntimeComponent } from "../runtime/runtimePrimitiveRenderer.ts";
import {
  isLayoutStructureContainerClassRefs,
  resolveFlowContainerPreviewClassName,
  resolveFlowNodePreviewClassName,
  FLOW_LEAF_ROLES,
} from "../runtime/layoutClassPreviewUtils.ts";

export type LayoutProjectionTreeProps = {
  specs: ComponentSpec[];
  layoutId?: string;
  rootLayoutClassRefs?: string[];
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
}: {
  spec: ComponentSpec;
  childrenMap: Map<string | undefined, ComponentSpec[]>;
}): JSX.Element | null {
  const children = childrenMap.get(spec.nodeId) ?? [];
  const childElements = children.map((child) => (
    <ProjectionTreeNode key={child.nodeId ?? `${child.orderIndex}`} spec={child} childrenMap={childrenMap} />
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
    const rendered = renderRuntimeComponent(spec.runtimeSpec);
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
        {childElements}
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
}: LayoutProjectionTreeProps): JSX.Element {
  const childrenMap = buildChildrenMap(specs);
  const roots = childrenMap.get(undefined) ?? [];
  const rootClass = flowRootClassName(rootLayoutClassRefs);

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
            />
          ))}
      </div>
    </div>
  );
}
