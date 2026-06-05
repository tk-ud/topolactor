import {
  lookupTopologyLayoutClassKey,
  resolveTopologyLayoutClassRefs,
} from "./topologyLayoutClassResolver.ts";

const CANVAS_ROOT_ALLOWED = new Set([
  "layout_root",
  "layout_section",
  "layout_row",
]);

/** Filter selected class keys by topology layout class allowed_for role. */
export function filterLayoutClassRefsByAllowedFor(
  classKeys: readonly string[],
  allowedFor: string | readonly string[],
): string[] {
  const allowed = Array.isArray(allowedFor) ? allowedFor : [allowedFor];
  const allowedSet = new Set(allowed);
  return classKeys.filter((key) => {
    const entry = lookupTopologyLayoutClassKey(key.trim());
    return entry?.allowedFor.some((role) => allowedSet.has(role)) ?? false;
  });
}

/** Resolve concrete CSS className string for preview (explicit failure → empty string). */
export function resolveLayoutClassPreviewClassName(
  classKeys: readonly string[],
  allowedFor: string | readonly string[],
): string {
  const filtered = filterLayoutClassRefsByAllowedFor(classKeys, allowedFor);
  if (filtered.length === 0) return "";
  const resolved = resolveTopologyLayoutClassRefs(filtered);
  return resolved.ok ? resolved.className : "";
}

/** Canvas outer frame: layout_root / layout_section / layout_row roles only. */
export function resolveCanvasRootPreviewClassName(
  classKeys: readonly string[],
): string {
  const filtered = classKeys.filter((key) => {
    const entry = lookupTopologyLayoutClassKey(key.trim());
    return entry?.allowedFor.some((role) => CANVAS_ROOT_ALLOWED.has(role)) ?? false;
  });
  if (filtered.length === 0) return "";
  const resolved = resolveTopologyLayoutClassRefs(filtered);
  return resolved.ok ? resolved.className : "";
}

/** Per-node component wrapper preview (component_wrapper + preview_state when selected). */
export function resolveNodeWrapperPreviewClassName(
  classKeys: readonly string[],
  isSelected: boolean,
): string {
  const roles = isSelected
    ? ["component_wrapper", "preview_state"]
    : ["component_wrapper"];
  return resolveLayoutClassPreviewClassName(classKeys, roles);
}
