import type { VNode } from "preact";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import type { RuntimeComponentSpec } from "./runtimeComponentAdapter.ts";
import type { LayoutDimension } from "./visualLayoutUtils.ts";
import {
  ensureRuntimeComponentRegistryInitialized,
  hasRuntimeComponentFactory,
  resolveRuntimeComponentFactory,
} from "./runtimeComponentRegistry.ts";
import { buildPreviewInertEventBinding } from "./previewInertEventBinding.ts";

export type LayoutPreviewNodeInput = {
  nodeId: string;
  componentKey: string;
  componentKind?: string;
  componentId?: string;
  nodeKind?: "catalog_component" | "structural_html";
  htmlTag?: string;
  layoutClassRefs?: string[];
  inlineText?: string;
  linkHref?: string;
  isDraftOnly?: boolean;
  x: number;
  y: number;
  width: number | string;
  height: number | string;
  widthMode?: "auto" | "preset" | "custom";
  heightMode?: "auto" | "preset" | "custom";
  /** Flow tree structure — preserved from DraftNode for visual audit hierarchy. */
  parentNodeId?: string | null;
  orderIndex?: number;
  slotKey?: string;
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
  "display/card_list": { width: 280, height: 280 },
  "data_display/table": { width: 320, height: 180 },
  "data_display/data_grid": { width: 320, height: 180 },
  "data_display/list": { width: 260, height: 140 },
  "data_display/md_viewer": { width: 320, height: 120 },
  "form_input/input": { width: 220, height: 44 },
  "form_input/textarea": { width: 260, height: 96 },
  "form_input/textarea_template": { width: 260, height: 96 },
  "form_input/search_input": { width: 240, height: 44 },
  "form_input/select": { width: 220, height: 56 },
  "form_input/checkbox": { width: 180, height: 36 },
  "form_input/form_field": { width: 240, height: 72 },
  "display/badge": { width: 100, height: 32 },
  "display/status_badge": { width: 120, height: 32 },
  "display/alert": { width: 300, height: 72 },
  "feedback/loading": { width: 200, height: 56 },
  "feedback/empty": { width: 240, height: 100 },
  "feedback/error": { width: 240, height: 100 },
  "data_display/json": { width: 280, height: 160 },
  "shell/admin_page": { width: 360, height: 220 },
  "shell/admin_section": { width: 300, height: 140 },
  "validation/result": { width: 300, height: 160 },
  "disclosure/tabs": { width: 320, height: 120 },
  "data_display/tree": { width: 220, height: 160 },
  "layout/box": { width: 180, height: 96 },
  "disclosure_structure/panel": { width: 240, height: 140 },
  "disclosure_structure/section": { width: 280, height: 160 },
  "disclosure/modal": { width: 320, height: 200 },
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

export type LayoutPreviewDesignOverrides = {
  inlineText?: string;
  linkHref?: string;
  linkTarget?: string;
};

/** Safe placeholder props for UI Builder canvas preview (no runtime wiring). */
export function buildLayoutPreviewPlaceholderProps(
  componentKind: string,
  componentKey: string,
  overrides: LayoutPreviewDesignOverrides = {},
): Record<string, unknown> {
  const shortLabel = layoutPreviewDisplayLabel(componentKey);
  const inlineText = overrides.inlineText?.trim();

  switch (componentKind) {
    case "action/button":
      return {
        data: {
          label: inlineText || shortLabel || "Button",
          variant: "primary",
          disabled: true,
        },
      };
    case "form_input/input":
    case "form_input/textarea":
    case "form_input/search_input":
      // No componentKey-derived label — SSOT inlineText is field content, not a fake "<kind>" caption.
      return {
        data: {
          placeholder: inlineText?.trim() || "プレビュー",
          value: "",
          disabled: false,
        },
      };
    case "display/card":
    case "disclosure_structure/panel":
    case "disclosure_structure/section":
      return {
        data: {
          title: inlineText || shortLabel,
          body: inlineText ? "" : "プレビュー本文",
        },
      };
    case "display/card_list":
      return {
        items: [
          { id: "1", title: inlineText || "カード A", body: "サンプル本文 1" },
          { id: "2", title: "カード B", body: "サンプル本文 2" },
        ],
        emptyMessage: "データがありません",
      };
    case "disclosure/modal":
      return {
        data: {
          open: true,
          title: inlineText || shortLabel || "Modal",
          body: "プレビュー",
        },
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
    case "form_input/select":
      return {
        data: {
          value: "",
          options: [
            { value: "a", label: inlineText || "選択肢 A" },
            { value: "b", label: "選択肢 B" },
          ],
          placeholder: "選択してください",
        },
      };
    case "form_input/checkbox":
      return { data: { checked: false, label: inlineText || "チェックボックス" } };
    case "form_input/textarea_template":
      return { data: { value: "", placeholder: inlineText || "プレビュー" } };
    case "display/badge":
      return { data: { label: inlineText || shortLabel || "Badge", tone: "info" } };
    case "display/status_badge":
      return { data: { label: inlineText || shortLabel || "Status", tone: "neutral" } };
    case "display/alert":
      return { data: { message: inlineText || "アラートメッセージ", tone: "info" } };
    case "feedback/loading":
      return { data: { message: inlineText || "Loading..." } };
    case "feedback/empty":
      return { data: { message: inlineText || "データがありません", description: "プレビュー" } };
    case "feedback/error":
      return { data: { message: inlineText || "エラーが発生しました", description: "プレビュー" } };
    case "data_display/json":
      return { data: { value: { key: "value", number: 42, flag: true } } };
    case "shell/admin_page":
      return { data: { title: inlineText || shortLabel || "Admin Page", description: "プレビュー" } };
    case "shell/admin_section":
      return { data: { title: inlineText || shortLabel || "Section", description: "プレビュー" } };
    case "validation/result":
      return { data: { title: inlineText || "検証結果", result: null } };
    case "disclosure/tabs":
      return {
        data: {
          items: [
            { key: "tab1", label: inlineText || "タブ 1" },
            { key: "tab2", label: "タブ 2" },
          ],
          activeKey: "tab1",
        },
      };
    case "data_display/tree":
      return {
        data: {
          nodes: [
            { key: "n1", label: inlineText || "ノード 1", children: [{ key: "n1-1", label: "子ノード" }] },
            { key: "n2", label: "ノード 2" },
          ],
        },
      };
    case "data_display/md_viewer":
      return { title: inlineText || shortLabel || "Markdown View" };
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
  design?: LayoutPreviewDesignOverrides;
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
      props: buildLayoutPreviewPlaceholderProps(
        componentKind,
        normalizedKey,
        input.design,
      ),
      eventBinding: buildPreviewInertEventBinding(),
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
  inlineText?: string;
  linkHref?: string;
  linkTarget?: string;
}): LayoutPreviewRenderResult {
  if (input.isDraftOnly) {
    return {
      ok: false,
      code: "DRAFT_ONLY",
      reason: "部品が未接続 — 部品登録タブで配置可能化してください",
    };
  }
  const built = buildLayoutPreviewRuntimeSpec({
    componentKey: input.componentKey,
    componentKind: input.componentKind,
    componentId: input.componentId,
    design: {
      inlineText: input.inlineText,
      linkHref: input.linkHref,
      linkTarget: input.linkTarget,
    },
  });
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
  width?: LayoutDimension;
  height?: LayoutDimension;
}>(
  nodes: T[],
  paletteEntries: Array<{ componentKey: string; componentKind: string; isDraftOnly: boolean }> = [],
): Array<T & { componentKind?: string; width: LayoutDimension; height: LayoutDimension }> {
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
    const resolveDimension = (
      dim: LayoutDimension | undefined,
      fallback: number,
      defaultPx: number,
    ): LayoutDimension => {
      if (typeof dim === "string" && dim.trim()) return dim;
      if (typeof dim === "number" && dim > 0) {
        return legacyDefaultSize && defaultPx > dim ? defaultPx : dim;
      }
      return fallback;
    };
    const width = resolveDimension(node.width, defaults.width, defaults.width);
    const height = resolveDimension(node.height, defaults.height, defaults.height);
    return {
      ...node,
      componentKind,
      isDraftOnly: node.isDraftOnly ?? palette?.isDraftOnly ?? false,
      width,
      height,
    };
  });
}
