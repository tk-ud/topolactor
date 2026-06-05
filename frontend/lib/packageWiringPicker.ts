import { mergeWiringKindSuggestions } from "./packageWiringOptions.ts";
import type { ScreenReadQueryWiringCandidate } from "./screenReadQueryWiring.ts";

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
