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

export function renderRuntimeComponent(
  spec: RuntimeComponentSpec,
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
  return factory.render(spec);
}
