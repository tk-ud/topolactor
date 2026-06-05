import type { VNode } from "preact";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import type { RuntimeComponentSpec } from "./runtimeComponentAdapter.ts";
import {
  ensureRuntimeComponentRegistryInitialized,
  hasRuntimeComponentFactory,
  resolveRuntimeComponentFactory,
} from "./runtimeComponentRegistry.ts";

export type LayoutPreviewNodeInput = {
  nodeId: string;
  componentKey: string;
  componentKind?: string;
  componentId?: string;
  nodeKind?: "catalog_component" | "structural_html";
  htmlTag?: string;
  inlineText?: string;
  linkHref?: string;
  isDraftOnly?: boolean;
  x: number;
  y: number;
  width: number;
  height: number;
};

export type LayoutPreviewDefaultSize = { width: number; height: number };

export type LayoutPreviewRenderResult =
  | { ok: true; node: VNode }
  | { ok: false; code: string; reason: string };

const LAYOUT_PREVIEW_KEY_ALIASES: Record<string, string> = {
  button: "button.primitive",
  card: "card.primitive",
  input: "input.primitive",
  table: "table.primitive",
  box: "box.primitive",
};

const LAYOUT_PREVIEW_DEFAULT_SIZES: Record<string, LayoutPreviewDefaultSize> = {
  "action/button": { width: 148, height: 44 },
  "display/card": { width: 240, height: 152 },
  "data_display/table": { width: 320, height: 180 },
  "data_display/data_grid": { width: 320, height: 180 },
  "data_display/list": { width: 260, height: 140 },
  "form_input/input": { width: 220, height: 44 },
  "form_input/textarea": { width: 260, height: 96 },
  "form_input/search_input": { width: 240, height: 44 },
  "form_input/form_field": { width: 240, height: 72 },
  "layout/box": { width: 180, height: 96 },
  "disclosure_structure/panel": { width: 240, height: 140 },
  "disclosure_structure/section": { width: 280, height: 160 },
};

/** Map bare catalog keys (e.g. button) to catalog SSOT entries (button.primitive). */
export function normalizeLayoutPreviewComponentKey(componentKey: string): string {
  const trimmed = componentKey.trim();
  if (!trimmed) return trimmed;
  if (COMPONENT_CATALOG_ENTRIES.some((entry) => entry.componentKey === trimmed)) {
    return trimmed;
  }
  const alias = LAYOUT_PREVIEW_KEY_ALIASES[trimmed];
  if (alias && COMPONENT_CATALOG_ENTRIES.some((entry) => entry.componentKey === alias)) {
    return alias;
  }
  const primitive = `${trimmed}.primitive`;
  if (COMPONENT_CATALOG_ENTRIES.some((entry) => entry.componentKey === primitive)) {
    return primitive;
  }
  return trimmed;
}

export function getLayoutPreviewDefaultSize(componentKind: string): LayoutPreviewDefaultSize {
  return LAYOUT_PREVIEW_DEFAULT_SIZES[componentKind] ?? { width: 160, height: 72 };
}

/** Inert binding shape for previewMode factory render (no runtime dispatch). */
const PREVIEW_INERT_CLICK_BINDING = { eventType: "click" as const, payload: {} };

/** Resolve componentKind from catalog entry or registry key. */
export function resolveComponentKindForLayoutPreview(
  componentKey: string,
  componentKindHint?: string,
): string | null {
  const hint = componentKindHint?.trim();
  if (hint) return hint;
  const normalizedKey = normalizeLayoutPreviewComponentKey(componentKey);
  const catalog = COMPONENT_CATALOG_ENTRIES.find((c) =>
    c.componentKey === normalizedKey || c.componentKey === componentKey.trim()
  );
  if (catalog?.componentKind) return catalog.componentKind;
  ensureRuntimeComponentRegistryInitialized();
  if (hasRuntimeComponentFactory(normalizedKey)) return normalizedKey;
  if (hasRuntimeComponentFactory(componentKey.trim())) return componentKey.trim();
  return null;
}

function layoutPreviewDisplayLabel(componentKey: string): string {
  const normalized = normalizeLayoutPreviewComponentKey(componentKey);
  return normalized
    .split("/")
    .pop()
    ?.replace(/\.(primitive|template)$/, "") ?? componentKey;
}

/** Safe placeholder props for UI Builder canvas preview (no runtime wiring). */
export function buildLayoutPreviewPlaceholderProps(
  componentKind: string,
  componentKey: string,
): Record<string, unknown> {
  const shortLabel = layoutPreviewDisplayLabel(componentKey);

  switch (componentKind) {
    case "action/button":
      return {
        data: { label: shortLabel || "Button", variant: "primary", disabled: true },
      };
    case "form_input/input":
    case "form_input/textarea":
    case "form_input/search_input":
      return {
        data: {
          label: shortLabel,
          placeholder: "プレビュー",
          value: "",
          disabled: false,
        },
      };
    case "display/card":
    case "disclosure_structure/panel":
    case "disclosure_structure/section":
      return {
        data: { title: shortLabel, body: "プレビュー本文" },
      };
    case "data_display/table":
    case "data_display/data_grid":
    case "data_display/list":
      return {
        table: {
          columns: [
            { key: "name", header: "名前" },
            { key: "value", header: "値" },
          ],
          rows: [
            { id: "1", name: "サンプル A", value: "100" },
            { id: "2", name: "サンプル B", value: "200" },
          ],
          emptyMessage: "データなし",
        },
      };
    case "layout/box":
      return { "aria-label": `${shortLabel} container` };
    case "form_input/form_field":
      return { data: { label: "フィールド", help: "プレビュー" } };
    default:
      return {
        title: shortLabel,
        value: "",
        items: [],
        preview: "プレビュー",
      };
  }
}

export function buildLayoutPreviewRuntimeSpec(input: {
  componentKey: string;
  componentKind?: string;
  componentId?: string;
}):
  | { ok: true; spec: RuntimeComponentSpec }
  | { ok: false; code: string; reason: string } {
  const normalizedKey = normalizeLayoutPreviewComponentKey(input.componentKey);
  const componentKind = resolveComponentKindForLayoutPreview(
    normalizedKey,
    input.componentKind,
  );
  if (!componentKind) {
    return {
      ok: false,
      code: "KIND_UNRESOLVED",
      reason: "componentKind を解決できません",
    };
  }
  ensureRuntimeComponentRegistryInitialized();
  if (!hasRuntimeComponentFactory(componentKind)) {
    return {
      ok: false,
      code: "FACTORY_MISSING",
      reason: `ランタイム factory 未登録: ${componentKind}`,
    };
  }
  return {
    ok: true,
    spec: {
      componentId: input.componentId ?? `preview:${normalizedKey}`,
      componentType: componentKind,
      props: buildLayoutPreviewPlaceholderProps(componentKind, normalizedKey),
      eventBinding: {
        click: PREVIEW_INERT_CLICK_BINDING,
        change: { eventType: "change", payload: {} },
        select: { eventType: "select", payload: {} },
        submit: { eventType: "submit", payload: {} },
        focus: { eventType: "focus", payload: {} },
        blur: { eventType: "blur", payload: {} },
      },
      previewMode: true,
    },
  };
}

/** Read-only component preview for UI Builder canvas (no runtime event dispatch). */
export function renderLayoutComponentPreview(input: {
  componentKey: string;
  componentKind?: string;
  componentId?: string;
  isDraftOnly?: boolean;
}): LayoutPreviewRenderResult {
  if (input.isDraftOnly) {
    return {
      ok: false,
      code: "DRAFT_ONLY",
      reason: "部品が未接続 — 部品登録タブで配置可能化してください",
    };
  }
  const built = buildLayoutPreviewRuntimeSpec(input);
  if (!built.ok) return built;
  const factory = resolveRuntimeComponentFactory(built.spec.componentType);
  if (!factory) {
    return {
      ok: false,
      code: "FACTORY_MISSING",
      reason: `factory 解決失敗: ${built.spec.componentType}`,
    };
  }
  const result = factory.render(built.spec);
  if (!result.ok) {
    return {
      ok: false,
      code: "RENDER_FAILED",
      reason: result.error,
    };
  }
  return { ok: true, node: result.node };
}

const LEGACY_CANVAS_NODE_WIDTH = 140;
const LEGACY_CANVAS_NODE_HEIGHT = 60;

export function enrichLayoutPreviewNodes<T extends {
  componentKey: string;
  componentKind?: string;
  isDraftOnly?: boolean;
  width?: number;
  height?: number;
}>(
  nodes: T[],
  paletteEntries: Array<{ componentKey: string; componentKind: string; isDraftOnly: boolean }> = [],
): Array<T & { componentKind?: string; width: number; height: number }> {
  const paletteByKey = new Map(paletteEntries.map((entry) => [entry.componentKey, entry]));
  return nodes.map((node) => {
    const palette = paletteByKey.get(node.componentKey);
    const componentKind = node.componentKind ??
      palette?.componentKind ??
      resolveComponentKindForLayoutPreview(node.componentKey) ??
      undefined;
    const defaults = componentKind ? getLayoutPreviewDefaultSize(componentKind) : { width: 160, height: 72 };
    const legacyDefaultSize = node.width === LEGACY_CANVAS_NODE_WIDTH &&
      node.height === LEGACY_CANVAS_NODE_HEIGHT;
    const width = typeof node.width === "number" && node.width > 0
      ? (legacyDefaultSize && defaults.width > node.width ? defaults.width : node.width)
      : defaults.width;
    const height = typeof node.height === "number" && node.height > 0
      ? (legacyDefaultSize && defaults.height > node.height ? defaults.height : node.height)
      : defaults.height;
    return {
      ...node,
      componentKind,
      isDraftOnly: node.isDraftOnly ?? palette?.isDraftOnly ?? false,
      width,
      height,
    };
  });
}
