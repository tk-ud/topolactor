import {
  TOPOLOGY_LAYOUT_CLASS_DICTIONARY,
  type TopologyLayoutClassDictionaryEntry,
} from "./topologyLayoutClassDictionary.ts";

export type ResolveTopologyLayoutClassRefsResult =
  | { ok: true; classNames: string[]; className: string; entries: TopologyLayoutClassDictionaryEntry[] }
  | { ok: false; error: string; code: "UNKNOWN_TOPOLOGY_LAYOUT_CLASS_REF" | "TOPOLOGY_LAYOUT_CLASS_REF_EMPTY" };

export function resolveTopologyLayoutClassRefs(
  classKeys: readonly string[],
): ResolveTopologyLayoutClassRefsResult {
  if (classKeys.length === 0) {
    return { ok: false, error: "TOPOLOGY_LAYOUT_CLASS_REF_EMPTY", code: "TOPOLOGY_LAYOUT_CLASS_REF_EMPTY" };
  }

  const entries: TopologyLayoutClassDictionaryEntry[] = [];
  const classNames: string[] = [];

  for (const key of classKeys) {
    const trimmed = key.trim();
    if (!trimmed) continue;
    const entry = TOPOLOGY_LAYOUT_CLASS_DICTIONARY.find((e) => e.classKey === trimmed);
    if (!entry) {
      return {
        ok: false,
        error: `UNKNOWN_TOPOLOGY_LAYOUT_CLASS_REF:${trimmed}`,
        code: "UNKNOWN_TOPOLOGY_LAYOUT_CLASS_REF",
      };
    }
    entries.push(entry);
    classNames.push(entry.className);
  }

  if (classNames.length === 0) {
    return { ok: false, error: "TOPOLOGY_LAYOUT_CLASS_REF_EMPTY", code: "TOPOLOGY_LAYOUT_CLASS_REF_EMPTY" };
  }

  return { ok: true, classNames, className: classNames.join(" "), entries };
}

export function lookupTopologyLayoutClassKey(
  classKey: string,
): TopologyLayoutClassDictionaryEntry | undefined {
  return TOPOLOGY_LAYOUT_CLASS_DICTIONARY.find((e) => e.classKey === classKey);
}
