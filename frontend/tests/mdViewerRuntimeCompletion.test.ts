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

// ─── Bare-markdown mode (props.markdown, no persisted saved-view record) ────

Deno.test("mdViewerPreviewFactory: bare-markdown mode renders safely when props.savedView is absent but props.markdown is a string", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const spec = {
    componentId: "test-md-viewer-bare",
    componentType: "data_display/md_viewer",
    props: { markdown: "# Bare markdown\n\nSome **bold** text." },
    eventBinding: {},
    previewMode: false,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, true, "bare-markdown mode must succeed without a savedView object");
  if (result.ok) {
    assertExists(result.node, "bare-markdown mode must return a VNode");
  }
});

Deno.test("mdViewerPreviewFactory: props.savedView takes priority over props.markdown when both are present (existing contract unchanged)", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const spec = {
    componentId: "test-md-viewer-both",
    componentType: "data_display/md_viewer",
    props: {
      markdown: "# should not be used",
      savedView: {
        savedViewId: "00000000-0000-0000-0000-0000000000aa",
        title: "Real saved view",
        renderedMarkdown: "# Real",
        completedPresetSeedJson: {
          seed_version: "1.0",
          template_ref: { templateId: "tpl-001", templateName: "Test Template" },
          source_ref: { sourceTable: "entity", sourceRecordId: "rec-001" },
          binding_ref: {
            required_placeholder_keys: ["{{name}}"],
            optional_placeholder_keys: ["{{notes}}"],
          },
          render_ref: {
            rendered_markdown_hash: "sha256-abc123def456",
            rendered_at: "2024-01-01T00:00:00Z",
            renderer_version: "1.0",
            unresolved_placeholder_keys: [],
          },
          adjustment_ref: { user_adjustment_patch_json: null },
          dashboard_ref: { tags: [], card_metadata_json: {} },
          lineage_ref: { created_from_preset: "md_viewer.projection" },
        },
      },
    },
    eventBinding: {},
    previewMode: false,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, true, "savedView-shaped props must still take the full MdViewer path");
});

Deno.test("mdViewerPreviewFactory: bare-markdown mode never uses dangerouslySetInnerHTML — script tag stays inert", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const spec = {
    componentId: "test-md-viewer-bare-security",
    componentType: "data_display/md_viewer",
    props: { markdown: "<script>window.__pwned = true;</script>" },
    eventBinding: {},
    previewMode: false,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, true);
  if (result.ok) {
    // deno-lint-ignore no-explicit-any
    const props = (result.node as any).props;
    assert(
      props?.dangerouslySetInnerHTML === undefined,
      "bare-markdown mode must never set dangerouslySetInnerHTML",
    );
  }
});

// ─── completedPresetSeedJson structure validation ───────────────────────────

Deno.test("normalizeMdViewerSavedView: completedPresetSeedJson:{} fails — RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const spec = {
    componentId: "test-md-viewer",
    componentType: "data_display/md_viewer",
    props: {
      savedView: {
        savedViewId: "sv-test-1",
        title: "Test View",
        renderedMarkdown: "# Test",
        completedPresetSeedJson: {},
      },
    },
    eventBinding: {},
    previewMode: false,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, false, "empty completedPresetSeedJson must be rejected — no silent fallback");
  if (!result.ok) {
    assert(
      result.error.includes("RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS"),
      `expected RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS, got: ${result.error}`,
    );
  }
});

Deno.test("normalizeMdViewerSavedView: missing render_ref.rendered_markdown_hash fails validation", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const spec = {
    componentId: "test-md-viewer",
    componentType: "data_display/md_viewer",
    props: {
      savedView: {
        savedViewId: "sv-test-2",
        title: "Test View",
        renderedMarkdown: "# Test",
        completedPresetSeedJson: {
          seed_version: "1.0",
          template_ref: {},
          source_ref: {},
          binding_ref: { required_placeholder_keys: [], optional_placeholder_keys: [] },
          render_ref: {
            // rendered_markdown_hash intentionally omitted
            rendered_at: "2024-01-01T00:00:00Z",
            renderer_version: "1.0",
            unresolved_placeholder_keys: [],
          },
          adjustment_ref: {},
          dashboard_ref: {},
          lineage_ref: {},
        },
      },
    },
    eventBinding: {},
    previewMode: false,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, false, "missing render_ref.rendered_markdown_hash must be rejected");
  if (!result.ok) {
    assert(
      result.error.includes("RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS"),
      `expected RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS, got: ${result.error}`,
    );
  }
});

Deno.test("normalizeMdViewerSavedView: missing binding_ref.required_placeholder_keys fails validation", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const spec = {
    componentId: "test-md-viewer",
    componentType: "data_display/md_viewer",
    props: {
      savedView: {
        savedViewId: "sv-test-3",
        title: "Test View",
        renderedMarkdown: "# Test",
        completedPresetSeedJson: {
          seed_version: "1.0",
          template_ref: {},
          source_ref: {},
          binding_ref: {
            // required_placeholder_keys intentionally omitted
            optional_placeholder_keys: [],
          },
          render_ref: {
            rendered_markdown_hash: "abc123hash",
            rendered_at: "2024-01-01T00:00:00Z",
            renderer_version: "1.0",
            unresolved_placeholder_keys: [],
          },
          adjustment_ref: {},
          dashboard_ref: {},
          lineage_ref: {},
        },
      },
    },
    eventBinding: {},
    previewMode: false,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, false, "missing binding_ref.required_placeholder_keys must be rejected");
  if (!result.ok) {
    assert(
      result.error.includes("RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS"),
      `expected RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS, got: ${result.error}`,
    );
  }
});

Deno.test("normalizeMdViewerSavedView: missing binding_ref.optional_placeholder_keys fails validation", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const spec = {
    componentId: "test-md-viewer",
    componentType: "data_display/md_viewer",
    props: {
      savedView: {
        savedViewId: "sv-test-4",
        title: "Test View",
        renderedMarkdown: "# Test",
        completedPresetSeedJson: {
          seed_version: "1.0",
          template_ref: {},
          source_ref: {},
          binding_ref: {
            required_placeholder_keys: [],
            // optional_placeholder_keys intentionally omitted
          },
          render_ref: {
            rendered_markdown_hash: "abc123hash",
            rendered_at: "2024-01-01T00:00:00Z",
            renderer_version: "1.0",
            unresolved_placeholder_keys: [],
          },
          adjustment_ref: {},
          dashboard_ref: {},
          lineage_ref: {},
        },
      },
    },
    eventBinding: {},
    previewMode: false,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, false, "missing binding_ref.optional_placeholder_keys must be rejected");
  if (!result.ok) {
    assert(
      result.error.includes("RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS"),
      `expected RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS, got: ${result.error}`,
    );
  }
});

Deno.test("normalizeMdViewerSavedView: fully valid completedPresetSeedJson passes validation and renders", () => {
  const factory = resolveRuntimeComponentFactory("data_display/md_viewer");
  assertExists(factory, "factory must exist");
  const spec = {
    componentId: "test-md-viewer",
    componentType: "data_display/md_viewer",
    props: {
      savedView: {
        savedViewId: "sv-test-valid",
        title: "Valid Test View",
        renderedMarkdown: "# Valid Markdown",
        completedPresetSeedJson: {
          seed_version: "1.0",
          template_ref: { templateId: "tpl-001", templateName: "Test Template" },
          source_ref: { sourceTable: "entity", sourceRecordId: "rec-001" },
          binding_ref: {
            required_placeholder_keys: ["{{name}}"],
            optional_placeholder_keys: ["{{notes}}"],
          },
          render_ref: {
            rendered_markdown_hash: "sha256-abc123def456",
            rendered_at: "2024-01-01T00:00:00Z",
            renderer_version: "1.0",
            unresolved_placeholder_keys: [],
          },
          adjustment_ref: { user_adjustment_patch_json: null },
          dashboard_ref: { tags: [], card_metadata_json: {} },
          lineage_ref: { created_from_preset: "md_viewer.projection" },
        },
      },
      seedValid: true,
    },
    eventBinding: {},
    previewMode: false,
  };
  // deno-lint-ignore no-explicit-any
  const result = factory!.render(spec as any);
  assertEquals(result.ok, true, "fully valid completedPresetSeedJson must pass validation and render");
  if (result.ok) {
    assertExists(result.node, "must return a VNode");
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
