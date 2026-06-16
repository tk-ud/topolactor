/** Admin package wiring editor — target surfaces allowed in ui_wiring_registry. */
export const PACKAGE_WIRING_TARGET_SURFACES = ["route", "ui", "manifest", "external_port"] as const;

export type PackageWiringTargetSurface = (typeof PACKAGE_WIRING_TARGET_SURFACES)[number];

export function mergeWiringKindSuggestions(componentKinds: string[]): string[] {
  const base = ["button", "form", "layout", "navigation", "evt"];
  const seen = new Set<string>();
  const out: string[] = [];
  for (const k of [...componentKinds, ...base]) {
    const t = k.trim();
    if (!t || seen.has(t)) continue;
    seen.add(t);
    out.push(t);
  }
  return out.sort((a, b) => a.localeCompare(b));
}

export function isPackageWiringTargetSurface(value: string): value is PackageWiringTargetSurface {
  return (PACKAGE_WIRING_TARGET_SURFACES as readonly string[]).includes(value);
}
