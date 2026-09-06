// frontend/tests/layoutPatchApplyModalLabelBoundary.test.tsx
//
// Production-composition DOM proof (not a static source-string grep) that
// LayoutPatchApplyModal — the primary, routinely-used Apply confirmation surface for
// /admin/ui-builder — separates normal-view friendly primary text from raw/technical detail for
// DYNAMIC runtime values (routeKey / layoutId / layoutKey / validation error codes), not just
// static literals. A static grep over NORMAL_VIEW_BANNED_TERMS-style vocabulary cannot catch a
// raw dynamic value (e.g. a real routeKey string) leaking into the always-visible DOM, since the
// banned-term list has no way to know what a real routeKey will be at runtime — only mounting the
// real component with real dynamic props and inspecting the rendered DOM tree structure proves it.

import { assert, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import { LayoutPatchApplyModal, type LayoutPatchApplySummary } from "../components/LayoutPatchApplyModal.tsx";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

const ROUTE_KEY = "customer-management-orders";
const LAYOUT_ID = "77777777-7777-7777-7777-777777777777";
const LAYOUT_KEY = "orders_list_screen";

function visibleText(container: Element): string {
  // Text nodes that are NOT inside a <details> element (i.e. always-rendered primary content).
  // happy-dom renders <details> content in the DOM regardless of the open attribute, so the
  // ONLY reliable way to distinguish "always visible" from "behind a disclosure" is structural
  // containment, not computed visibility.
  const clone = container.cloneNode(true) as Element;
  for (const details of Array.from(clone.querySelectorAll("details"))) {
    details.remove();
  }
  return clone.textContent ?? "";
}

function technicalDisclosureText(container: Element): string {
  return Array.from(container.querySelectorAll("details")).map((d) => d.textContent ?? "").join("\n");
}

Deno.test("LayoutPatchApplyModal: dynamic routeKey/layoutId are NOT in the always-visible primary text — only inside a 技術情報 disclosure", async () => {
  const { container, cleanup } = setupDom();
  try {
    const summary: LayoutPatchApplySummary = {
      valid: true,
      message: "OK",
      nodeCount: 3,
      draftOnlyCount: 0,
      routeKey: ROUTE_KEY,
      layoutId: LAYOUT_ID,
      layoutKey: undefined,
      errors: [],
    };
    render(
      h(LayoutPatchApplyModal, {
        open: true,
        phase: "validated",
        summary,
        routeKey: ROUTE_KEY,
        layoutId: LAYOUT_ID,
        layoutLabel: "",
        loading: false,
        onClose: () => {},
        onConfirmApply: () => {},
        onGoDesign: () => {},
      }),
      container,
    );
    await flushUpdates();

    const primary = visibleText(container);
    assertFalse(primary.includes(ROUTE_KEY), "raw routeKey must not appear in the always-visible primary text");
    assertFalse(primary.includes(LAYOUT_ID), "raw layoutId must not appear in the always-visible primary text");
    assertFalse(primary.includes("layout_patch_json"), "raw internal table/module name must not be primary text");
    assertFalse(primary.includes("routeKey"), "the raw property name 'routeKey' must not be a primary label");
    assertFalse(primary.includes("layoutId"), "the raw property name 'layoutId' must not be a primary label");
    assert(primary.includes("名称未設定のレイアウト"), "a friendly placeholder must show when no layoutKey exists");

    const technical = technicalDisclosureText(container);
    assert(technical.includes(ROUTE_KEY), "routeKey must still be reachable in a 技術情報 disclosure");
    assert(technical.includes(LAYOUT_ID), "layoutId must still be reachable in a 技術情報 disclosure");
  } finally {
    render(null, container);
    cleanup();
  }
});

Deno.test("LayoutPatchApplyModal: a real layoutKey is shown as the friendly primary identity, not the raw property name", async () => {
  const { container, cleanup } = setupDom();
  try {
    const summary: LayoutPatchApplySummary = {
      valid: true,
      message: "OK",
      nodeCount: 1,
      draftOnlyCount: 0,
      routeKey: ROUTE_KEY,
      layoutId: LAYOUT_ID,
      layoutKey: LAYOUT_KEY,
      errors: [],
    };
    render(
      h(LayoutPatchApplyModal, {
        open: true,
        phase: "validated",
        summary,
        routeKey: ROUTE_KEY,
        layoutId: LAYOUT_ID,
        layoutLabel: LAYOUT_KEY,
        loading: false,
        onClose: () => {},
        onConfirmApply: () => {},
        onGoDesign: () => {},
      }),
      container,
    );
    await flushUpdates();

    const primary = visibleText(container);
    assert(primary.includes(LAYOUT_KEY), "the friendly layoutKey must appear in the primary text");
    assertFalse(primary.includes(LAYOUT_ID), "the raw layoutId must not leak into the primary text just because layoutKey exists");
  } finally {
    render(null, container);
    cleanup();
  }
});

Deno.test("LayoutPatchApplyModal: a validation error's raw code is reachable only inside a 技術情報 disclosure, never as always-visible primary text", async () => {
  const { container, cleanup } = setupDom();
  try {
    const summary: LayoutPatchApplySummary = {
      valid: false,
      message: "エラーがあります",
      nodeCount: 2,
      draftOnlyCount: 0,
      routeKey: ROUTE_KEY,
      layoutId: LAYOUT_ID,
      layoutKey: LAYOUT_KEY,
      errors: [{ code: "LAYOUT_PATCH_SCHEMA_COMPOSED_COMPONENT_KEY_IDENTITY_MISMATCH", message: "コンポーネントの種類が一致しません" }],
    };
    render(
      h(LayoutPatchApplyModal, {
        open: true,
        phase: "validated",
        summary,
        routeKey: ROUTE_KEY,
        layoutId: LAYOUT_ID,
        layoutLabel: LAYOUT_KEY,
        loading: false,
        onClose: () => {},
        onConfirmApply: () => {},
        onGoDesign: () => {},
      }),
      container,
    );
    await flushUpdates();

    const primary = visibleText(container);
    assert(primary.includes("コンポーネントの種類が一致しません"), "the human-readable error message must be primary");
    assertFalse(
      primary.includes("LAYOUT_PATCH_SCHEMA_COMPOSED_COMPONENT_KEY_IDENTITY_MISMATCH"),
      "the raw error code must not appear in the always-visible primary text",
    );
    const technical = technicalDisclosureText(container);
    assert(
      technical.includes("LAYOUT_PATCH_SCHEMA_COMPOSED_COMPONENT_KEY_IDENTITY_MISMATCH"),
      "the raw error code must still be reachable for diagnostics inside a 技術情報 disclosure",
    );
  } finally {
    render(null, container);
    cleanup();
  }
});

Deno.test("LayoutPatchApplyModal: success screen's next-steps use friendly business language, with internal module/field names reachable only in a 技術情報 disclosure", async () => {
  const { container, cleanup } = setupDom();
  try {
    const summary: LayoutPatchApplySummary = {
      valid: true,
      message: "OK",
      nodeCount: 4,
      draftOnlyCount: 0,
      routeKey: ROUTE_KEY,
      layoutId: LAYOUT_ID,
      layoutKey: LAYOUT_KEY,
      errors: [],
    };
    render(
      h(LayoutPatchApplyModal, {
        open: true,
        phase: "success",
        summary,
        routeKey: ROUTE_KEY,
        layoutId: LAYOUT_ID,
        layoutLabel: LAYOUT_KEY,
        loading: false,
        onClose: () => {},
        onConfirmApply: () => {},
        onGoDesign: () => {},
      }),
      container,
    );
    await flushUpdates();

    const primary = visibleText(container);
    assertFalse(primary.includes("layout_patch_json"), "raw internal table/module name must not be primary text");
    assertFalse(primary.includes("component_style_design"), "raw internal table name must not be primary text");
    assertFalse(primary.includes("renderEmission"), "raw internal module name must not be primary text");
    assertFalse(primary.includes("initialDataRows"), "raw internal field name must not be primary text");
    assertFalse(primary.includes("DB"), "raw 'DB' jargon must not be primary text");

    const technical = technicalDisclosureText(container);
    assert(technical.includes("component_style_design"));
    assert(technical.includes("renderEmission"));
    assert(technical.includes("initialDataRows"));
  } finally {
    render(null, container);
    cleanup();
  }
});
