import { mergeWiringKindSuggestions } from "./packageWiringOptions.ts";
import type { ScreenReadQueryWiringCandidate } from "./screenReadQueryWiring.ts";

/** Prefix for route navigation target_ref. Distinct from manifest:<uuid>:... */
export const ROUTE_NAV_PREFIX = "route:";

/** Encode a routeKey as a route navigation target_ref. */
export function encodeRouteNavigationTargetRef(routeKey: string): string {
  return `${ROUTE_NAV_PREFIX}${routeKey.trim()}`;
}

/** Extract routeKey from a route navigation target_ref, or null if not a route nav ref. */
export function parseRouteNavigationTargetRef(targetRef: string): string | null {
  const trimmed = targetRef.trim();
  if (!trimmed.startsWith(ROUTE_NAV_PREFIX)) return null;
  const key = trimmed.slice(ROUTE_NAV_PREFIX.length).trim();
  return key || null;
}

/** Returns true when target_ref encodes a route navigation wiring. */
export function isRouteNavigationTargetRef(targetRef: string | null | undefined): boolean {
  if (!targetRef) return false;
  return targetRef.trim().startsWith(ROUTE_NAV_PREFIX);
}

export type ManifestPickerOption = {
  manifestId: string;
  label: string;
  status: string;
};

const MANIFEST_TARGET_REF_RE =
  /^manifest:([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})(?::(.*))?$/i;

/** Persist manifest + wiringKey in target_ref for reloadable picker state. */
export function encodeManifestPackageTargetRef(
  manifestId: string,
  wiringKey: string,
): string {
  const key = wiringKey.trim();
  if (!key) return `manifest:${manifestId.trim()}`;
  return `manifest:${manifestId.trim()}:${key}`;
}

export function parseManifestPackageTargetRef(
  targetRef: string,
): { manifestId: string; wiringKey: string } | null {
  const trimmed = targetRef.trim();
  if (!trimmed) return null;
  const match = trimmed.match(MANIFEST_TARGET_REF_RE);
  if (!match) return null;
  return { manifestId: match[1], wiringKey: (match[2] ?? "").trim() };
}

/** Resolve wiringKey shown in manifest-surface radio list from stored target_ref. */
export function manifestWiringKeyFromTargetRef(
  targetRef: string,
  targetSurface: string,
): string {
  if (targetSurface !== "manifest") return targetRef.trim();
  const parsed = parseManifestPackageTargetRef(targetRef);
  if (parsed) return parsed.wiringKey;
  return targetRef.trim();
}

export function manifestIdFromTargetRef(
  targetRef: string,
  targetSurface: string,
): string {
  if (targetSurface !== "manifest") return "";
  return parseManifestPackageTargetRef(targetRef)?.manifestId ?? "";
}

export function buildWiringKindSelectOptions(
  componentKinds: string[],
  candidates: ScreenReadQueryWiringCandidate[],
): string[] {
  const fromCandidates = candidates.map((c) => c.wiringKey);
  return mergeWiringKindSuggestions([...componentKinds, ...fromCandidates]);
}

export function mergeManifestPickerOptions(
  active: ManifestPickerOption[],
  draft: ManifestPickerOption[],
): ManifestPickerOption[] {
  const seen = new Set<string>();
  const out: ManifestPickerOption[] = [];
  for (const item of [...active, ...draft]) {
    if (seen.has(item.manifestId)) continue;
    seen.add(item.manifestId);
    out.push(item);
  }
  return out;
}
