/**
 * mdViewerRuntimeCompletion.test.ts — md_viewer runtime factory completion tests.
 * SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
 *       docs/design/component-catalog-classification-ssot.yaml
 *
 * Completion conditions tested:
 * - mdViewerPreviewFactory renders MdViewer component (not placeholder div)
 * - preview props include savedView with completedPresetSeedJson
 * - explicit error (not silent fallback) when savedView props missing/invalid
 * - runtime renderer has no active topology authority / physical record authority / saved view authority
 * - runtime renderer is not a preset DB seed registration mechanism
 * - DashboardCandidatePalette / registrationRequired boundary intact
 */
import {
  assertEquals,
  assertExists,
  assert,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import {
  ensureRuntimeComponentRegistryInitialized,
  resolveRuntimeComponentFactory,
} from "../runtime/runtimeComponentRegistry.ts";
import {
  renderLayoutComponentPreview,
  buildLayoutPreviewPlaceholderProps,
} from "../runtime/layoutComponentPreview.ts";

ensureRuntimeComponentRegistryInitialized();

// ─── Catalog integrity ──────────────────────────────────────────────────────

Deno.test("md_viewer.projection: catalog entry is runtimeConnected:true", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "md_viewer.projection");
  assertExists(entry, "md_viewer.projection must exist in catalog");
  assertEquals(entry!.runtimeConnected, true, "md_viewer.projection must be runtimeConnected:true");
});

Deno.test("md_viewer.projection: registrationRequired:false (no DB bucket registration needed)", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "md_viewer.projection");
  assertExists(entry, "md_viewer.projection must exist in catalog");
  assertEquals(entry!.registrationRequired, false, "md_viewer must not require DB bucket registration");
});

Deno.test("md_viewer.projection: dashboard_placement_candidate tag", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "md_viewer.projection");
  assertExists(entry, "md_viewer.projection must exist in catalog");
  assert(
    entry!.capabilityTags.includes("dashboard_placement_candidate"),
    "md_viewer.projection must have dashboard_placement_candidate tag",
  );
});

Deno.test("md_viewer.projection: componentKind is data_display/md_viewer", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "md_viewer.projection");
  assertExists(entry, "md_viewer.projection must exist in catalog");
  assertEquals(entry!.componentKind, "data_display/md_viewer");
});

// ─── Factory registration ───────────────────────────────────────────────────

Deno.test("data_display/md_viewer: factory is registered in RUNTIME_COMPONENT_FACTORIES", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "data_display/md_viewer must have a registered runtime factory");
});

// ─── Preview render tests ────────────────────────────────────────────────────

Deno.test("mdViewerPreviewFactory: renderLayoutComponentPreview returns ok:true VNode", () => {
  const result = renderLayoutComponentPreview({
    componentKey: "md_viewer.projection",
    componentKind: "data_display/md_viewer",
  });
  assert(
    result.ok,
    `md_viewer.projection preview failed: ${!result.ok ? `${result.code}: ${result.reason}` : ""}`,
  );
  if (result.ok) {
    assertExists(result.node, "preview must return a VNode");
  }
});

Deno.test("mdViewerPreviewFactory: preview props include savedView with completedPresetSeedJson", () => {
  const props = buildLayoutPreviewPlaceholderProps("data_display/md_viewer", "md_viewer.projection");
  assert(
    typeof props.savedView === "object" && props.savedView !== null,
    "preview props must include savedView object",
  );
  const savedView = props.savedView as Record<string, unknown>;
  assert(typeof savedView.savedViewId === "string", "savedView must have savedViewId");
  assert(typeof savedView.renderedMarkdown === "string", "savedView must have renderedMarkdown");
  assert(
    typeof savedView.completedPresetSeedJson === "object" && savedView.completedPresetSeedJson !== null,
    "savedView must have completedPresetSeedJson (not placeholder-only props)",
  );
  assertEquals(props.seedValid, true, "preview seedValid must be true");
});

Deno.test("mdViewerPreviewFactory: preview props are not a placeholder-only title shape", () => {
  const props = buildLayoutPreviewPlaceholderProps("data_display/md_viewer", "md_viewer.projection");
  // Old placeholder returned { title: "..." } — the new implementation must return { savedView, seedValid }
  assert(
    "savedView" in props,
    "preview props must have savedView key (old placeholder div shape is not acceptable)",
  );
  assert(
    !("title" in props) || typeof props.title !== "string",
    "preview props must not be the old title-only placeholder shape",
  );
});

// ─── Explicit error (no silent fallback) ────────────────────────────────────

Deno.test("mdViewerPreviewFactory: explicit error when savedView props missing (non-preview mode)", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const spec = {
    componentId: "test-md-viewer",
    componentType: "data_display/md_viewer",
    props: {},
    eventBinding: {},
    previewMode: false,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, false, "must fail when savedView is missing — no silent fallback");
  if (!result.ok) {
    assert(
      result.error.includes("RUNTIME_MD_VIEWER_MISSING_SAVED_VIEW_PROPS"),
      `expected RUNTIME_MD_VIEWER_MISSING_SAVED_VIEW_PROPS, got: ${result.error}`,
    );
  }
});

Deno.test("mdViewerPreviewFactory: explicit error when savedView props invalid (non-preview mode)", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const spec = {
    componentId: "test-md-viewer",
    componentType: "data_display/md_viewer",
    props: { savedView: { title: "incomplete — missing savedViewId and renderedMarkdown" } },
    eventBinding: {},
    previewMode: false,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, false, "must fail when savedView is invalid — no silent fallback");
  if (!result.ok) {
    assert(
      result.error.includes("RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS"),
      `expected RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS, got: ${result.error}`,
    );
  }
});

// ─── Authority boundary tests ─────────────────────────────────────────────────

Deno.test("md_viewer runtime renderer: preview renders MdViewer (not placeholder div)", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const props = buildLayoutPreviewPlaceholderProps("data_display/md_viewer", "md_viewer.projection");
  const spec = {
    componentId: "preview:md_viewer.projection",
    componentType: "data_display/md_viewer",
    props,
    eventBinding: {},
    previewMode: true,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, true, "preview render must succeed");
  if (result.ok) {
    assertExists(result.node, "must return a VNode");
    // Must be a function component (MdViewer), not a plain div element
    assertEquals(
      typeof result.node.type,
      "function",
      "VNode type must be a function component (MdViewer), not a placeholder div string",
    );
  }
});

Deno.test("md_viewer runtime renderer: no mutation action callbacks in factory output", () => {
  // The factory does NOT provide onRefresh, onClone, onRebind, onEditAdjustment, onArchive, onCreateTodoCandidate.
  // Saved view mutation authority stays at /admin/team-dashboard.
  // Verify by checking that a valid render does not throw about missing mutation handlers.
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const props = buildLayoutPreviewPlaceholderProps("data_display/md_viewer", "md_viewer.projection");
  const spec = {
    componentId: "preview:md_viewer.projection",
    componentType: "data_display/md_viewer",
    props,
    eventBinding: {},
    previewMode: true,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  // If it succeeds, the factory correctly omits mutation callbacks without error
  assertEquals(result.ok, true, "factory must succeed without providing mutation action callbacks");
});

// ─── DB registration / topology authority boundary ───────────────────────────

Deno.test("md_viewer: not a DB bucket registration candidate (registrationRequired:false)", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "md_viewer.projection");
  assertExists(entry, "md_viewer.projection must exist in catalog");
  assertEquals(
    entry!.registrationRequired,
    false,
    "md_viewer must not be a DB bucket registration mechanism (registrationRequired must be false)",
  );
});

Deno.test("md_viewer: composite family — not a primitive or template for topology promotion", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "md_viewer.projection");
  assertExists(entry, "md_viewer.projection must exist in catalog");
  assertEquals(entry!.componentFamily, "composite", "md_viewer must be composite family");
});

Deno.test("md_viewer: data_viewer semantic role — read-only display, not topology authority", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "md_viewer.projection");
  assertExists(entry, "md_viewer.projection must exist in catalog");
  assertEquals(
    entry!.semanticRole,
    "data_viewer",
    "md_viewer must be data_viewer semantic role — not an active topology authority",
  );
});

// ─── DashboardCandidatePalette boundary ──────────────────────────────────────

Deno.test("DashboardCandidatePalette: md_viewer.projection appears in palette without DB registration (registrationRequired:false + dashboard_placement_candidate)", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "md_viewer.projection");
  assertExists(entry);
  assertEquals(entry!.registrationRequired, false);
  assert(entry!.capabilityTags.includes("dashboard_placement_candidate"));
  assertEquals(entry!.runtimeConnected, true);
});
