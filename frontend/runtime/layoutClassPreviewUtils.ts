import {
  lookupTopologyLayoutClassKey,
  resolveTopologyLayoutClassRefs,
} from "./topologyLayoutClassResolver.ts";
import {
  TOPOLOGY_LAYOUT_CLASS_DICTIONARY,
  type TopologyLayoutClassDictionaryEntry,
} from "./topologyLayoutClassDictionary.ts";

/** Roles for canvas / projection root wrapper. */
export const FLOW_ROOT_ROLES = [
  "layout_root",
  "layout_section",
  "layout_row",
] as const;

/** Roles for structural container nodes (includes component_wrapper for surface on sections). */
export const FLOW_CONTAINER_ROLES = [
  "layout_root",
  "layout_section",
  "layout_row",
  "component_wrapper",
] as const;

/** Roles for catalog / leaf component wrappers. */
export const FLOW_LEAF_ROLES = ["component_wrapper"] as const;

const CANVAS_ROOT_ALLOWED = new Set<string>(FLOW_ROOT_ROLES);

/** Categories that define structural containers (not sizing/align-only tweaks). */
const STRUCTURE_CONTAINER_CATEGORIES = new Set([
  "layout",
  "direction",
  "container",
  "form_layout",
  "shell",
  "responsive",
]);

const DIRECTION_DISPLAY_CLASS_KEYS = new Set([
  "layout.root.grid",
  "layout.section.stack",
  "layout.row.flex",
  "layout.direction.stack",
  "layout.direction.row",
  "layout.direction.wrap",
]);

/**
 * Resolve conflictGroup collisions for flow preview (last ref wins per group).
 * When grid_cols is set, skip direction-group display containers so grid/flex do not fight.
 */
export function filterLayoutClassRefsForFlowPreview(
  classKeys: readonly string[],
): string[] {
  const keys = classKeys.map((key) => key.trim()).filter(Boolean);
  const hasGridCols = keys.some((key) =>
    lookupTopologyLayoutClassKey(key)?.conflictGroup === "grid_cols"
  );

  const byGroup = new Map<string, string>();
  const ungrouped: string[] = [];

  for (const key of keys) {
    const entry = lookupTopologyLayoutClassKey(key);
    if (!entry) continue;
    if (
      hasGridCols &&
      entry.conflictGroup === "direction" &&
      DIRECTION_DISPLAY_CLASS_KEYS.has(key)
    ) {
      continue;
    }
    if (entry.conflictGroup) {
      byGroup.set(entry.conflictGroup, key);
    } else {
      ungrouped.push(key);
    }
  }

  return [...ungrouped, ...byGroup.values()];
}

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

/**
 * Unified flow-node className resolution for canvas and projection tree.
 * Maps layoutClassRefs → topology-layout.css classes via allowedFor roles.
 */
export function resolveFlowNodePreviewClassName(
  classKeys: readonly string[],
  options: {
    allowedFor: readonly string[];
    isSelected?: boolean;
  },
): string {
  const parts: string[] = [];
  const normalized = filterLayoutClassRefsForFlowPreview(classKeys);
  const main = resolveLayoutClassPreviewClassName(normalized, options.allowedFor);
  if (main) parts.push(main);
  if (options.isSelected) {
    const selected = resolveLayoutClassPreviewClassName(
      ["layout.state.selected"],
      "preview_state",
    );
    if (selected) parts.push(selected);
  }
  return parts.join(" ");
}

/** True when a class key defines a structural container (row/stack/form), not align/width-only. */
export function isLayoutStructureContainerClassKey(classKey: string): boolean {
  const entry = lookupTopologyLayoutClassKey(classKey.trim());
  if (!entry) return false;
  return STRUCTURE_CONTAINER_CATEGORIES.has(entry.category);
}

export function isLayoutStructureContainerClassRefs(
  classKeys: readonly string[],
): boolean {
  return classKeys.some((key) => isLayoutStructureContainerClassKey(key));
}

/** Canvas outer frame: layout_root / layout_section / layout_row roles only. */
export function resolveCanvasRootPreviewClassName(
  classKeys: readonly string[],
): string {
  return resolveFlowNodePreviewClassName(classKeys, { allowedFor: FLOW_ROOT_ROLES });
}

/** Flow container nodes: full layout vocabulary for the node's allowed roles. */
export function resolveFlowContainerPreviewClassName(
  classKeys: readonly string[],
  isSelected = false,
): string {
  return resolveFlowNodePreviewClassName(classKeys, {
    allowedFor: FLOW_CONTAINER_ROLES,
    isSelected,
  });
}

/** Per-node component wrapper preview (component_wrapper + preview_state when selected). */
export function resolveNodeWrapperPreviewClassName(
  classKeys: readonly string[],
  isSelected: boolean,
): string {
  return resolveFlowNodePreviewClassName(classKeys, {
    allowedFor: FLOW_LEAF_ROLES,
    isSelected,
  });
}

/** Coverage map: which preview context each dictionary entry can project in. */
export function topologyLayoutClassPreviewCoverage(): Array<{
  classKey: string;
  category: string;
  root: boolean;
  container: boolean;
  leaf: boolean;
}> {
  return TOPOLOGY_LAYOUT_CLASS_DICTIONARY
    .filter((e) => !e.allowedFor.every((r) => r === "preview_state"))
    .map((entry) => ({
      classKey: entry.classKey,
      category: entry.category,
      root: entry.allowedFor.some((r) => CANVAS_ROOT_ALLOWED.has(r)),
      container: entry.allowedFor.some((r) =>
        (FLOW_CONTAINER_ROLES as readonly string[]).includes(r)
      ),
      leaf: entry.allowedFor.some((r) =>
        (FLOW_LEAF_ROLES as readonly string[]).includes(r)
      ),
    }));
}

export function entryProjectsInContext(
  entry: TopologyLayoutClassDictionaryEntry,
  context: "root" | "container" | "leaf",
): boolean {
  const roles = context === "root"
    ? FLOW_ROOT_ROLES
    : context === "container"
    ? FLOW_CONTAINER_ROLES
    : FLOW_LEAF_ROLES;
  return entry.allowedFor.some((r) => (roles as readonly string[]).includes(r));
}
