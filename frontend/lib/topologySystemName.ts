/**
 * Topology system name (topology.name): kebab-case system identifier.
 * SSOT for route path, primary physical table name, and UI Builder key derivation.
 * Distinct from userFacingTopologyLabel (display name), which is optional.
 */

/** Recommended pattern: lowercase alphanumeric segments separated by single hyphens. */
export const TOPOLOGY_SYSTEM_NAME_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export function isValidTopologySystemName(name: string): boolean {
  return TOPOLOGY_SYSTEM_NAME_PATTERN.test(name.trim());
}

export function topologySystemNameValidationError(name: string): string | null {
  const trimmed = name.trim();
  if (!trimmed) return "トポロジーIDを入力してください。";
  if (!/^[a-z0-9-]+$/.test(trimmed)) return "トポロジーIDは英小文字・数字・ハイフンのみ使用できます。";
  if (trimmed.startsWith("-") || trimmed.endsWith("-"))
    return "トポロジーIDの先頭・末尾にハイフンは使えません。";
  if (/--/.test(trimmed)) return "トポロジーIDに連続ハイフンは使えません。";
  if (!TOPOLOGY_SYSTEM_NAME_PATTERN.test(trimmed))
    return "トポロジーIDは英小文字・数字・ハイフンのみで、先頭・末尾・連続ハイフン禁止です。";
  return null;
}

/** Derive the canonical admin route path from topology.name. */
export function topologySystemNameToRoute(name: string): string {
  return `/admin/${name.trim()}`;
}

/** Derive the primary physical table name from topology.name. */
export function topologySystemNameToPhysicalTable(name: string): string {
  return name.trim().replaceAll("-", "_");
}

/** Derive the UI Builder key from topology.name. */
export function topologySystemNameToUiBuilderKey(name: string): string {
  return name.trim();
}

/**
 * Resolve visible display name: prefer displayName/screenLabel, fall back to topologySystemName.
 * Never generates route/table/key from displayName.
 */
export function resolveVisibleTopologyName(
  displayName: string | null | undefined,
  topologySystemName: string | null | undefined,
): string {
  const d = displayName?.trim();
  if (d) return d;
  const s = topologySystemName?.trim();
  if (s) return s;
  return "";
}
