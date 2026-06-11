export type LinkHrefInterpolationContext = {
  routeValues?: Record<string, string>;
  dataValues?: Record<string, unknown>;
};

export type LinkHrefInterpolationResult =
  | { ok: true; value: string; placeholders: string[] }
  | { ok: false; code: string; message: string; placeholders: string[] };

const PLACEHOLDER_RE = /\{\{\s*([a-zA-Z0-9_.-]+)\s*\}\}/g;

function readPath(source: Record<string, unknown> | undefined, path: string): unknown {
  if (!source) return undefined;
  const parts = path.split(".").filter(Boolean);
  let cur: unknown = source;
  for (const part of parts) {
    if (typeof cur !== "object" || cur === null || Array.isArray(cur)) return undefined;
    cur = (cur as Record<string, unknown>)[part];
  }
  return cur;
}

/**
 * Read-only interpolation for authored linkHref preview/runtime validation.
 * Does not mutate the canonical authored href. Unknown placeholders are explicit errors.
 */
export function interpolateLinkHrefReadOnly(
  authoredHref: string | undefined,
  context: LinkHrefInterpolationContext = {},
): LinkHrefInterpolationResult {
  const href = authoredHref?.trim() ?? "";
  if (!href) return { ok: true, value: "", placeholders: [] };
  const placeholders: string[] = [];
  let failed = "";
  const value = href.replace(PLACEHOLDER_RE, (_match, key: string) => {
    placeholders.push(key);
    if (key.startsWith("route.")) {
      const routeKey = key.slice("route.".length);
      const routeValue = context.routeValues?.[routeKey];
      if (typeof routeValue === "string" && routeValue.trim()) return routeValue;
      failed = key;
      return "";
    }
    if (key.startsWith("data.")) {
      const dataPath = key.slice("data.".length);
      const dataValue = readPath(context.dataValues, dataPath);
      if (typeof dataValue === "string" || typeof dataValue === "number" || typeof dataValue === "boolean") {
        return encodeURIComponent(String(dataValue));
      }
      failed = key;
      return "";
    }
    failed = key;
    return "";
  });
  if (failed) {
    return {
      ok: false,
      code: "LINK_HREF_PLACEHOLDER_UNRESOLVED",
      message: `linkHref placeholder "${failed}" could not be resolved; no fallback href was applied.`,
      placeholders,
    };
  }
  return { ok: true, value, placeholders };
}

export function findLinkHrefPlaceholders(authoredHref: string | undefined): string[] {
  const href = authoredHref?.trim() ?? "";
  return [...href.matchAll(PLACEHOLDER_RE)].map((m) => String(m[1]));
}
