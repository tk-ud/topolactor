import type { RuntimeComponentFactory } from "../components/runtimeContract.ts";

const registry = new Map<string, RuntimeComponentFactory>();

export function registerRuntimeComponentFactory(
  factory: RuntimeComponentFactory,
): void {
  for (const kind of factory.componentKinds) {
    registry.set(kind, factory);
  }
}

export function resolveRuntimeComponentFactory(
  componentKind: string,
): RuntimeComponentFactory | null {
  return registry.get(componentKind) ?? null;
}

export function hasRuntimeComponentFactory(componentKind: string): boolean {
  return registry.has(componentKind);
}

/**
 * Round 25: true when componentKind's registered factory declares
 * RuntimeComponentFactory.acceptsAuthoredChildren — i.e. it embeds real schema children itself
 * (e.g. Modal's Confirm/Cancel Actions as a footer) rather than the caller
 * (components/LayoutProjectionTree.tsx) rendering them as trailing DOM siblings. False (the
 * existing behavior) for every componentKind that hasn't opted in, including when the kind is
 * unregistered entirely.
 */
export function componentAcceptsAuthoredChildren(componentKind: string): boolean {
  return registry.get(componentKind)?.acceptsAuthoredChildren === true;
}

export function listRuntimeComponentKinds(): string[] {
  return [...registry.keys()];
}

import { RUNTIME_COMPONENT_FACTORIES } from "./runtimeComponentFactory.ts";
let initialized = false;

export function ensureRuntimeComponentRegistryInitialized(): void {
  if (initialized) return;
  for (const factory of RUNTIME_COMPONENT_FACTORIES) {
    registerRuntimeComponentFactory(factory);
  }
  initialized = true;
}
