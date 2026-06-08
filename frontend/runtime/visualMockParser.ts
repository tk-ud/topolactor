/**
 * visualMockParser.ts — SVG/XML/generic visual mock parser.
 * SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml §source_intake_contract
 *
 * Parser rules (SSOT §parser_rules):
 *   - Extracts visual structure ONLY (geometry, grouping, text, z-order)
 *   - Does NOT infer component identity
 *   - Does NOT infer event semantics
 *   - Does NOT infer data binding semantics
 *   - Preserves unknown objects as unassigned
 *   - Failures are explicit, not silent fallback
 *
 * Supported source kinds: svg_xml | generic_xml
 * Figma JSON: returns parse_error (not implemented in this version).
 *
 * Uses a pure TypeScript XML parser (no browser DOM / DOMParser dependency)
 * so it works identically in browser, Deno tests, and server-side contexts.
 */

export type VisualSourceKind = "svg_xml" | "generic_xml" | "figma_json" | "ui_builder_canvas";

export type VisualObjectNode = {
  source_object_id: string;
  object_type: string;
  parent_source_object_id: string | null;
  z_index: number;
  bbox: { x: number; y: number; width: number; height: number };
  transform: string;
  text_content: string;
  style_attributes: Record<string, string>;
  children: VisualObjectNode[];
};

export type VisualTreeParseResult =
  | { ok: true; sourceKind: VisualSourceKind; nodes: VisualObjectNode[]; nodeCount: number }
  | { ok: false; errorCode: string; message: string };

// ─── entry point ──────────────────────────────────────────────────────────────

export function parseVisualMockSource(
  rawSource: string,
  hintSourceKind?: VisualSourceKind,
): VisualTreeParseResult {
  const trimmed = rawSource.trim();
  if (!trimmed) {
    return { ok: false, errorCode: "SOURCE_EMPTY", message: "Source input is empty" };
  }

  const detectedKind = hintSourceKind ?? detectSourceKind(trimmed);

  switch (detectedKind) {
    case "svg_xml":
    case "generic_xml":
      return parseSvgOrXml(trimmed, detectedKind);
    case "figma_json":
      return {
        ok: false,
        errorCode: "FIGMA_JSON_NOT_IMPLEMENTED",
        message: "Figma JSON intake is not yet implemented in this version. Use SVG or XML source.",
      };
    case "ui_builder_canvas":
      return {
        ok: false,
        errorCode: "UI_BUILDER_CANVAS_SOURCE_NOT_SUPPORTED_HERE",
        message: "ui_builder_canvas source kind is saved via the 'Save as Preset' button, not the uploader.",
      };
    default:
      return {
        ok: false,
        errorCode: "SOURCE_KIND_UNRECOGNIZED",
        message: `Could not detect source kind from input. Provide SVG or XML content.`,
      };
  }
}

// ─── source kind detection ────────────────────────────────────────────────────

function detectSourceKind(source: string): VisualSourceKind {
  const lower = source.toLowerCase();
  if (lower.startsWith("{") || lower.startsWith("[")) {
    return "figma_json";
  }
  if (lower.includes("<svg") || lower.includes("xmlns:svg") || lower.includes("sodipodi")) {
    return "svg_xml";
  }
  if (lower.startsWith("<?xml") || lower.startsWith("<")) {
    return "generic_xml";
  }
  return "generic_xml";
}

// ─── Pure TS XML parser ───────────────────────────────────────────────────────
// Handles SVG/XML structure without any browser DOM dependency.

type XmlElement = {
  tag: string;
  attrs: Record<string, string>;
  children: XmlElement[];
  textContent: string;
};

type ParseState = {
  src: string;
  pos: number;
  error: string | null;
};

function parseXmlDocument(src: string): XmlElement | { error: string } {
  const state: ParseState = { src, pos: 0, error: null };
  skipProlog(state);
  if (state.error) return { error: state.error };
  const root = parseXmlElement(state);
  if (state.error) return { error: state.error };
  if (!root) return { error: "No root element found" };
  return root;
}

function skipProlog(state: ParseState) {
  skipWs(state);
  while (state.pos < state.src.length) {
    if (startsWith(state, "<?")) {
      skipUntil(state, "?>");
      skipWs(state);
    } else if (startsWith(state, "<!--")) {
      skipUntil(state, "-->");
      skipWs(state);
    } else if (startsWith(state, "<!")) {
      skipUntil(state, ">");
      skipWs(state);
    } else {
      break;
    }
  }
}

function parseXmlElement(state: ParseState): XmlElement | null {
  skipWs(state);
  if (state.pos >= state.src.length || state.src[state.pos] !== "<") return null;
  if (startsWith(state, "</")) return null; // end tag — caller handles
  if (startsWith(state, "<!--")) { skipUntil(state, "-->"); return null; }
  if (startsWith(state, "<?")) { skipUntil(state, "?>"); return null; }
  if (startsWith(state, "<!")) { skipUntil(state, ">"); return null; }

  state.pos++; // skip <
  const tag = parseName(state);
  if (!tag) { state.error = "Expected element name after <"; return null; }

  const attrs = parseAttrs(state);
  skipWs(state);

  const selfClosing = state.src[state.pos] === "/";
  if (selfClosing) state.pos++;
  if (state.pos >= state.src.length || state.src[state.pos] !== ">") {
    state.error = `Expected '>' to close <${tag}>`;
    return null;
  }
  state.pos++; // consume >

  const el: XmlElement = { tag, attrs, children: [], textContent: "" };
  if (selfClosing) return el;

  let text = "";
  let foundEnd = false;

  while (state.pos < state.src.length) {
    const ch = state.src[state.pos];
    if (ch !== "<") {
      text += ch;
      state.pos++;
      continue;
    }

    if (startsWith(state, "</")) {
      state.pos += 2;
      parseName(state); // consume end tag name (don't validate match for simplicity)
      skipWs(state);
      if (state.pos < state.src.length && state.src[state.pos] === ">") state.pos++;
      foundEnd = true;
      break;
    }

    if (startsWith(state, "<!--")) { skipUntil(state, "-->"); continue; }
    if (startsWith(state, "<?")) { skipUntil(state, "?>"); continue; }
    if (startsWith(state, "<![CDATA[")) {
      state.pos += 9;
      const end = state.src.indexOf("]]>", state.pos);
      if (end < 0) { state.error = "Unclosed CDATA section"; return null; }
      text += state.src.slice(state.pos, end);
      state.pos = end + 3;
      continue;
    }
    if (startsWith(state, "<!")) { skipUntil(state, ">"); continue; }

    // child element
    if (text.trim()) { el.textContent += text.trim() + " "; text = ""; }
    const child = parseXmlElement(state);
    if (state.error) return null;
    if (child) el.children.push(child);
  }

  el.textContent = (el.textContent + text.trim()).trim();

  if (!foundEnd) {
    state.error = `Missing end tag for <${tag}>`;
    return null;
  }
  return el;
}

function parseAttrs(state: ParseState): Record<string, string> {
  const attrs: Record<string, string> = {};
  while (state.pos < state.src.length) {
    skipWs(state);
    const ch = state.src[state.pos];
    if (ch === ">" || ch === "/") break;
    const name = parseName(state);
    if (!name) { state.pos++; continue; } // skip unknown char
    skipWs(state);
    if (state.pos < state.src.length && state.src[state.pos] === "=") {
      state.pos++; // skip =
      skipWs(state);
      attrs[name] = parseAttrValue(state);
    } else {
      attrs[name] = "";
    }
  }
  return attrs;
}

function parseAttrValue(state: ParseState): string {
  const quote = state.src[state.pos];
  if (quote !== '"' && quote !== "'") return "";
  state.pos++;
  let value = "";
  while (state.pos < state.src.length && state.src[state.pos] !== quote) {
    if (state.src[state.pos] === "&") {
      const semi = state.src.indexOf(";", state.pos);
      if (semi >= 0 && semi - state.pos <= 10) {
        const entity = state.src.slice(state.pos + 1, semi);
        const entityMap: Record<string, string> = {
          amp: "&", lt: "<", gt: ">", quot: '"', apos: "'",
        };
        value += entityMap[entity] ?? `&${entity};`;
        state.pos = semi + 1;
      } else {
        value += state.src[state.pos++];
      }
    } else {
      value += state.src[state.pos++];
    }
  }
  if (state.pos < state.src.length) state.pos++; // closing quote
  return value;
}

function parseName(state: ParseState): string {
  let name = "";
  while (state.pos < state.src.length && /[a-zA-Z0-9_:.\-]/.test(state.src[state.pos])) {
    name += state.src[state.pos++];
  }
  return name;
}

function startsWith(state: ParseState, prefix: string): boolean {
  return state.src.startsWith(prefix, state.pos);
}

function skipUntil(state: ParseState, end: string) {
  const idx = state.src.indexOf(end, state.pos);
  state.pos = idx >= 0 ? idx + end.length : state.src.length;
}

function skipWs(state: ParseState) {
  while (state.pos < state.src.length && /\s/.test(state.src[state.pos])) state.pos++;
}

// ─── SVG/XML to VisualObjectNode conversion ───────────────────────────────────

const SKIP_TAGS = new Set(["defs", "style", "script", "metadata", "title", "desc"]);

function parseSvgOrXml(source: string, kind: VisualSourceKind): VisualTreeParseResult {
  const xmlResult = parseXmlDocument(source);
  if ("error" in xmlResult) {
    return { ok: false, errorCode: "XML_PARSE_ERROR", message: `XML parse error: ${xmlResult.error}` };
  }

  const idCounter = { value: 0 };
  const nodes = convertChildren(xmlResult.children, null, idCounter);

  return { ok: true, sourceKind: kind, nodes, nodeCount: countNodes(nodes) };
}

function convertChildren(
  elements: XmlElement[],
  parentSourceObjectId: string | null,
  idCounter: { value: number },
): VisualObjectNode[] {
  const result: VisualObjectNode[] = [];
  for (const el of elements) {
    const node = convertElement(el, parentSourceObjectId, idCounter);
    if (node) result.push(node);
  }
  return result;
}

function convertElement(
  el: XmlElement,
  parentSourceObjectId: string | null,
  idCounter: { value: number },
): VisualObjectNode | null {
  const tagName = el.tag.toLowerCase();
  if (SKIP_TAGS.has(tagName)) return null;

  idCounter.value += 1;
  const sourceObjectId = el.attrs["id"] ??
    el.attrs["inkscape:label"] ??
    `node_${idCounter.value}`;

  const bbox = extractBboxFromAttrs(el.attrs);
  const transform = el.attrs["transform"] ?? "";
  const styleAttributes = extractStyleAttributesFromAttrs(el.attrs);
  const zIndex = extractZIndexFromAttrs(el.attrs, idCounter.value);
  const children = convertChildren(el.children, sourceObjectId, idCounter);

  return {
    source_object_id: sourceObjectId,
    object_type: tagName,
    parent_source_object_id: parentSourceObjectId,
    z_index: zIndex,
    bbox,
    transform,
    text_content: el.textContent,
    style_attributes: styleAttributes,
    children,
  };
}

function extractBboxFromAttrs(
  attrs: Record<string, string>,
): { x: number; y: number; width: number; height: number } {
  const x = parseAttrFloat(attrs, "x") ?? parseAttrFloat(attrs, "cx") ?? 0;
  const y = parseAttrFloat(attrs, "y") ?? parseAttrFloat(attrs, "cy") ?? 0;
  const width = parseAttrFloat(attrs, "width") ?? parseAttrFloat(attrs, "r") ?? 0;
  const height = parseAttrFloat(attrs, "height") ?? parseAttrFloat(attrs, "r") ?? 0;
  return { x, y, width, height };
}

function parseAttrFloat(attrs: Record<string, string>, key: string): number | null {
  const raw = attrs[key];
  if (raw === undefined) return null;
  const n = parseFloat(raw);
  return isNaN(n) ? null : n;
}

function extractStyleAttributesFromAttrs(attrs: Record<string, string>): Record<string, string> {
  const result: Record<string, string> = {};
  const styleAttr = attrs["style"];
  if (styleAttr) {
    for (const part of styleAttr.split(";")) {
      const idx = part.indexOf(":");
      if (idx < 0) continue;
      const key = part.slice(0, idx).trim();
      const value = part.slice(idx + 1).trim();
      if (key) result[key] = value;
    }
  }
  for (const attr of ["fill", "stroke", "opacity", "font-size", "font-family", "color"]) {
    const val = attrs[attr];
    if (val !== undefined) result[attr] = val;
  }
  return result;
}

function extractZIndexFromAttrs(attrs: Record<string, string>, fallback: number): number {
  const raw = attrs["z-index"] ?? attrs["data-z-index"];
  if (raw !== undefined) {
    const n = parseInt(raw, 10);
    if (!isNaN(n)) return n;
  }
  return fallback;
}

// ─── utilities ────────────────────────────────────────────────────────────────

function countNodes(nodes: VisualObjectNode[]): number {
  let count = 0;
  for (const node of nodes) {
    count += 1 + countNodes(node.children);
  }
  return count;
}

// ─── flat tree helper ─────────────────────────────────────────────────────────

export function flattenVisualTree(nodes: VisualObjectNode[]): VisualObjectNode[] {
  const result: VisualObjectNode[] = [];
  function walk(arr: VisualObjectNode[]) {
    for (const node of arr) {
      result.push(node);
      walk(node.children);
    }
  }
  walk(nodes);
  return result;
}

// ─── source hash ─────────────────────────────────────────────────────────────

export function computeSourceHash(source: string): string {
  let hash = 0;
  for (let i = 0; i < source.length; i++) {
    hash = (Math.imul(31, hash) + source.charCodeAt(i)) | 0;
  }
  return (hash >>> 0).toString(16);
}
