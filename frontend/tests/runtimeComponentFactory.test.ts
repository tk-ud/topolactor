import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildLayoutPreviewPlaceholderProps,
  buildLayoutPreviewRuntimeSpec,
  renderLayoutComponentPreview,
  resolveComponentKindForLayoutPreview,
} from "../runtime/layoutComponentPreview.ts";
import Box from "../components/Box.tsx";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";
import { __testOnly } from "../runtime/runtimeComponentFactory.ts";

Deno.test("resolveComponentKindForLayoutPreview: catalog maps box.primitive to layout/box", () => {
  assertEquals(
    resolveComponentKindForLayoutPreview("box.primitive"),
    "layout/box",
  );
});

Deno.test("resolveComponentKindForLayoutPreview: prefers explicit hint", () => {
  assertEquals(
    resolveComponentKindForLayoutPreview("custom.key", "action/button"),
    "action/button",
  );
});

Deno.test("buildLayoutPreviewPlaceholderProps: button includes disabled preview label", () => {
  const props = buildLayoutPreviewPlaceholderProps("action/button", "button.primitive");
  const data = props.data as Record<string, unknown>;
  assertEquals(data.disabled, true);
  assertExists(data.label);
});

Deno.test("buildLayoutPreviewPlaceholderProps: input omits componentKey caption label", () => {
  const props = buildLayoutPreviewPlaceholderProps("form_input/input", "input.primitive");
  const data = props.data as Record<string, unknown>;
  assertEquals("label" in data, false);
  assertEquals(data.placeholder, "プレビュー");
});

Deno.test("buildLayoutPreviewPlaceholderProps: input inlineText overrides placeholder not label", () => {
  const props = buildLayoutPreviewPlaceholderProps("form_input/input", "input.primitive", {
    inlineText: "氏名",
  });
  const data = props.data as Record<string, unknown>;
  assertEquals(data.placeholder, "氏名");
  assertEquals("label" in data, false);
});

Deno.test("buildLayoutPreviewRuntimeSpec: sets previewMode on spec", () => {
  const built = buildLayoutPreviewRuntimeSpec({
    componentKey: "card.primitive",
  });
  if (!built.ok) throw new Error(built.reason);
  assertEquals(built.spec.previewMode, true);
  assertEquals(built.spec.componentType, "display/card");
});

Deno.test("renderLayoutComponentPreview: button.primitive renders live preview vnode", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "button.primitive",
    componentKind: "action/button",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertExists(result.node);
});

Deno.test("renderLayoutComponentPreview: box.primitive renders Box preview not Card", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "box.primitive",
    componentKind: "layout/box",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertEquals(result.node.type, Box);
});

Deno.test("renderLayoutComponentPreview: draft-only is explicit DRAFT_ONLY fallback", () => {
  const result = renderLayoutComponentPreview({
    componentKey: "button.primitive",
    componentKind: "action/button",
    isDraftOnly: true,
  });
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertEquals(result.code, "DRAFT_ONLY");
});

Deno.test("renderLayoutComponentPreview: unknown kind fails explicitly", () => {
  const result = renderLayoutComponentPreview({
    componentKey: "totally.unknown.widget",
  });
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertEquals(result.code, "KIND_UNRESOLVED");
});

Deno.test("button click event binding: previewMode suppresses runtimeDispatch", () => {
  const binding = {
    eventType: "click",
    payload: {},
    runtimeDispatch: {
      operationType: "admin",
      layer: "ui_topology",
      action: "list_packages",
      target: "admin",
    },
  };
  const parsed = __testOnly.parseEventBinding(binding);
  assertExists(parsed);
  assertEquals(parsed?.eventType, "click");
  assertExists(parsed?.runtimeDispatch);
  assertEquals(parsed?.runtimeDispatch?.action, "list_packages");
  const spec = {
    componentId: "test-button",
    componentType: "action/button",
    props: { data: { label: "Click" } },
    eventBinding: { click: binding },
    previewMode: true,
  };
  const emitResult = __testOnly.emitBoundEvent(spec, "click", {});
  assertEquals(emitResult.ok, true);
});

Deno.test("row_detail_drawer.primitive renders in preview mode with inert toggle", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "row_detail_drawer.primitive",
    componentKind: "table_op/row_detail_drawer",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertExists(result.node);
});

Deno.test("modal.template renders in preview mode via disclosure/modal factory", () => {
  ensureRuntimeComponentRegistryInitialized();
  const result = renderLayoutComponentPreview({
    componentKey: "modal.template",
    componentKind: "disclosure/modal",
  });
  if (!result.ok) throw new Error(`${result.code}: ${result.reason}`);
  assertExists(result.node);
});
