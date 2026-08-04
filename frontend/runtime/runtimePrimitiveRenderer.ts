import type { VNode } from "preact";
import {
  ensureRuntimeComponentRegistryInitialized,
  resolveRuntimeComponentFactory,
} from "./runtimeComponentRegistry.ts";
import type { RuntimeComponentSpec } from "./runtimeComponentAdapter.ts";

type RenderResult = { ok: true; node: VNode<any> } | {
  ok: false;
  error: string;
};

/**
 * authoredChildren (round 25): the REAL nested schema children of this node (e.g. a Modal's
 * Confirm/Cancel Action buttons), already rendered to VNodes by the caller
 * (components/LayoutProjectionTree.tsx). Merged onto spec ONLY when non-empty, so a factory
 * that never reads spec.authoredChildren (every existing factory except modalFactory) is
 * completely unaffected — this parameter is purely additive.
 */
export function renderRuntimeComponent(
  spec: RuntimeComponentSpec,
  authoredChildren?: VNode[],
): RenderResult {
  ensureRuntimeComponentRegistryInitialized();
  const factory = resolveRuntimeComponentFactory(spec.componentType);
  if (!factory) {
    return {
      ok: false,
      error:
        `RUNTIME_PRIMITIVE_RENDERER_UNSUPPORTED_COMPONENT_KIND: ${spec.componentType}`,
    };
  }
  const effectiveSpec = authoredChildren?.length
    ? { ...spec, authoredChildren }
    : spec;
  return factory.render(effectiveSpec);
}
