import { assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildLayoutPreviewPlaceholderProps,
  buildLayoutPreviewRuntimeSpec,
  renderLayoutComponentPreview,
  resolveComponentKindForLayoutPreview,
} from "../runtime/layoutComponentPreview.ts";
import Box from "../components/Box.tsx";
import { ensureRuntimeComponentRegistryInitialized } from "../runtime/runtimeComponentRegistry.ts";

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
