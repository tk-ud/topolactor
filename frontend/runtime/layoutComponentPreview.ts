import type { VNode } from "preact";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import type { RuntimeComponentSpec } from "./runtimeComponentAdapter.ts";
import {
  ensureRuntimeComponentRegistryInitialized,
  hasRuntimeComponentFactory,
  resolveRuntimeComponentFactory,
} from "./runtimeComponentRegistry.ts";

export type LayoutPreviewRenderResult =
  | { ok: true; node: VNode }
  | { ok: false; code: string; reason: string };

const PREVIEW_EVENT_STUB = { eventType: "click" as const, payload: {} };

/** Resolve componentKind from catalog entry or registry key. */
export function resolveComponentKindForLayoutPreview(
  componentKey: string,
  componentKindHint?: string,
): string | null {
  const hint = componentKindHint?.trim();
  if (hint) return hint;
  const catalog = COMPONENT_CATALOG_ENTRIES.find((c) =>
    c.componentKey === componentKey
  );
  if (catalog?.componentKind) return catalog.componentKind;
  ensureRuntimeComponentRegistryInitialized();
  if (hasRuntimeComponentFactory(componentKey)) return componentKey;
  return null;
}

/** Safe placeholder props for UI Builder canvas preview (no runtime wiring). */
export function buildLayoutPreviewPlaceholderProps(
  componentKind: string,
  componentKey: string,
): Record<string, unknown> {
  const shortLabel = componentKey
    .split("/")
    .pop()
    ?.replace(/\.(primitive|template)$/, "") ?? componentKey;

  switch (componentKind) {
    case "action/button":
      return {
        data: { label: shortLabel || "Button", variant: "secondary", disabled: true },
      };
    case "form_input/input":
    case "form_input/textarea":
    case "form_input/search_input":
      return {
        data: {
          label: "入力",
          placeholder: "プレビュー",
          value: "",
          disabled: true,
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
  const componentKind = resolveComponentKindForLayoutPreview(
    input.componentKey,
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
      componentId: input.componentId ?? `preview:${input.componentKey}`,
      componentType: componentKind,
      props: buildLayoutPreviewPlaceholderProps(componentKind, input.componentKey),
      eventBinding: {
        click: PREVIEW_EVENT_STUB,
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
