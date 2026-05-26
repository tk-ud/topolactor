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
