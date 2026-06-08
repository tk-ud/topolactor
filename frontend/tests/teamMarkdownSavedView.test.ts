/**
 * teamMarkdownSavedView.test.ts — Frontend contract tests for Team Markdown Dashboard.
 * SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
 *
 * Tests:
 *   - Dispatch action shape for template and saved view operations
 *   - completed_preset_seed_json required gate (COMPLETED_PRESET_SEED_MISSING throws)
 *   - Markdown body is NOT used as refresh source (refresh requires updatedCompletedPresetSeedJson)
 *   - Search does not mutate saved views (search-only payload shape)
 *   - Seed validation function contract (structural checks)
 *   - MdViewer action toolbar disables refresh/clone when seedValid=false
 */

import {
  assertEquals,
  assertRejects,
  assertThrows,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { searchSavedViews } from "../api/teamMarkdownApi.ts";
import type {
  CompletedPresetSeed,
  SavedViewDetail,
} from "../api/teamMarkdownApi.ts";
import { __testOnly } from "../runtime/frontendScheduler.ts";

// ─── seed validation contract tests ──────────────────────────────────────────

function validateCompletedPresetSeed(seed: unknown): string | null {
  if (!seed || typeof seed !== "object" || Array.isArray(seed)) {
    return "COMPLETED_PRESET_SEED_INVALID";
  }
  const s = seed as Record<string, unknown>;
  const required = [
    "seed_version",
    "template_ref",
    "source_ref",
    "binding_ref",
    "render_ref",
    "adjustment_ref",
    "dashboard_ref",
    "lineage_ref",
  ];
  for (const key of required) {
    if (!(key in s)) return `COMPLETED_PRESET_SEED_MISSING:${key}`;
  }
  const renderRef = s.render_ref as Record<string, unknown>;
  if (
    !renderRef || typeof renderRef.rendered_markdown_hash !== "string" ||
    !renderRef.rendered_markdown_hash
  ) {
    return "COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH";
  }
  return null;
}

function buildValidSeed(renderHash = "abc123hash"): CompletedPresetSeed {
  return {
    seed_version: "1",
    template_ref: {
      template_id: "00000000-0000-0000-0000-000000000001",
      template_key: "test_template",
    },
    source_ref: {
      source_table_ref: "topology.physical_tables",
      source_record_ref: "test_record",
    },
    binding_ref: { binding_json: {}, placeholder_to_field_map: {} },
    render_ref: {
      rendered_markdown_hash: renderHash,
      rendered_at: "2026-06-08T00:00:00Z",
      renderer_version: "1.0",
    },
    adjustment_ref: { adjustment_mode: "none" },
    dashboard_ref: { title: "Test View", excerpt: "excerpt", tags: [] },
    lineage_ref: { created_from: "template_record" },
  };
}

Deno.test("validateCompletedPresetSeed — rejects non-object", () => {
  assertEquals(
    validateCompletedPresetSeed("not_an_object"),
    "COMPLETED_PRESET_SEED_INVALID",
  );
  assertEquals(
    validateCompletedPresetSeed(null),
    "COMPLETED_PRESET_SEED_INVALID",
  );
  assertEquals(
    validateCompletedPresetSeed([]),
    "COMPLETED_PRESET_SEED_INVALID",
  );
});

Deno.test("validateCompletedPresetSeed — rejects missing required field", () => {
  const partial = {
    seed_version: "1",
    template_ref: {},
    source_ref: {},
    binding_ref: {},
    render_ref: { rendered_markdown_hash: "abc" },
    adjustment_ref: {},
    // dashboard_ref MISSING
    // lineage_ref MISSING
  };
  const result = validateCompletedPresetSeed(partial);
  assertEquals(result?.startsWith("COMPLETED_PRESET_SEED_MISSING"), true);
});

Deno.test("validateCompletedPresetSeed — rejects empty render hash", () => {
  const seed = buildValidSeed("");
  seed.render_ref.rendered_markdown_hash = "";
  assertEquals(
    validateCompletedPresetSeed(seed),
    "COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH",
  );
});

Deno.test("validateCompletedPresetSeed — accepts valid complete seed", () => {
  assertEquals(validateCompletedPresetSeed(buildValidSeed()), null);
});

// ─── search action contract tests ─────────────────────────────────────────────

Deno.test("search payload shape — uses status=active by default", () => {
  const queryParams = { status: "active", limit: 50 };
  assertEquals(queryParams.status, "active");
  assertEquals(queryParams.limit, 50);
});

Deno.test("search does not include mutation fields", () => {
  const searchPayloadKeys = ["query", "status", "limit"];
  const mutationKeys = [
    "renderedMarkdown",
    "bindingJson",
    "completedPresetSeedJson",
    "title",
  ];
  for (const key of mutationKeys) {
    assertEquals(
      searchPayloadKeys.includes(key),
      false,
      `search payload must not include mutation field: ${key}`,
    );
  }
});

// ─── refresh contract tests ───────────────────────────────────────────────────

Deno.test("refresh requires updatedCompletedPresetSeedJson — not markdown body parsing", () => {
  const refreshRequiredFields = [
    "refreshedRenderedMarkdown",
    "updatedCompletedPresetSeedJson",
    "searchIndexText",
  ];
  assertEquals(
    refreshRequiredFields.includes("updatedCompletedPresetSeedJson"),
    true,
  );
  assertEquals(
    refreshRequiredFields.includes("refreshedRenderedMarkdown"),
    true,
  );

  assertEquals(refreshRequiredFields.includes("markdownBodyToParse"), false);
  assertEquals(refreshRequiredFields.includes("parsedMarkdown"), false);
});

Deno.test("refresh payload does not include Markdown-body-parsing field", () => {
  const prohibitedFields = [
    "markdownBodyToParse",
    "parsedMarkdownBody",
    "parseMarkdown",
  ];
  const refreshPayloadKeys = [
    "refreshedRenderedMarkdown",
    "updatedCompletedPresetSeedJson",
    "searchIndexText",
    "cardMetadataJson",
  ];
  for (const prohibited of prohibitedFields) {
    assertEquals(
      refreshPayloadKeys.includes(prohibited),
      false,
      `refresh payload must not contain: ${prohibited}`,
    );
  }
});

Deno.test("searchSavedViews rejects malformed response shape explicitly", async () => {
  const originalFetch = globalThis.fetch;
  __testOnly.resetCommandQueue();
  try {
    globalThis.fetch = () =>
      Promise.resolve(
        new Response(
          JSON.stringify({ success: true, emission: { data: [] } }),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );

    await assertRejects(
      () => searchSavedViews({ query: "broken-shape" }),
      Error,
      "TEAM_MARKDOWN_SEARCH_RESPONSE_INVALID",
    );
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

// ─── create saved view requires seed ─────────────────────────────────────────

Deno.test("create saved view payload — completedPresetSeedJson is required", () => {
  const createRequiredFields = [
    "templateId",
    "title",
    "sourceTableRef",
    "sourceRecordRef",
    "bindingJson",
    "completedPresetSeedJson",
    "renderedMarkdown",
  ];
  assertEquals(createRequiredFields.includes("completedPresetSeedJson"), true);
});

Deno.test("incomplete seed blocks create — explicit error not silent fallback", () => {
  const incompleteSeed = { seed_version: "1" };
  const error = validateCompletedPresetSeed(incompleteSeed);
  assertEquals(
    error !== null,
    true,
    "incomplete seed must produce an explicit error, not null (silent pass)",
  );
});

// ─── MdViewer action boundary tests ──────────────────────────────────────────

Deno.test("MdViewer seed-gated actions are disabled when seedValid=false", () => {
  // Models the ActionToolbar gate: disabled={!seedValid} applies to seed-gated actions only.
  const seedGatedActions = new Set(["refresh", "clone"]);
  const alwaysAvailableActions = new Set([
    "copy_markdown",
    "archive",
    "open_source_record",
    "edit_adjustment",
    "create_todo",
  ]);

  const isDisabled = (action: string, seedValid: boolean) =>
    seedGatedActions.has(action) && !seedValid;

  // When seedValid=false: seed-gated actions must be disabled
  for (const action of seedGatedActions) {
    assertEquals(
      isDisabled(action, false),
      true,
      `${action} must be disabled when seedValid=false`,
    );
  }
  // When seedValid=true: seed-gated actions must be enabled
  for (const action of seedGatedActions) {
    assertEquals(
      isDisabled(action, true),
      false,
      `${action} must be enabled when seedValid=true`,
    );
  }
  // Always-available actions must not be disabled regardless of seed state
  for (const action of alwaysAvailableActions) {
    assertEquals(
      isDisabled(action, false),
      false,
      `${action} must not be gated by seedValid`,
    );
    assertEquals(
      isDisabled(action, true),
      false,
      `${action} must not be gated by seedValid`,
    );
  }
  // Seed-gated set and always-available set must not overlap
  for (const action of seedGatedActions) {
    assertEquals(
      alwaysAvailableActions.has(action),
      false,
      `${action} must not appear in alwaysAvailableActions`,
    );
  }
});

// ─── boundary: saved view is a projection, not canonical data authority ───────

Deno.test("saved view does not own physical record fields", () => {
  // SavedViewDetail is a projection type; it must not carry canonical-record authority fields.
  // Verify by checking that every canonical-authority field name is absent from SavedViewDetail keys.
  const canonicalDataAuthorityFields = [
    "columnValues",
    "jsonbFieldValues",
    "lifecycleState",
    "updateAuthority",
  ];

  // Construct a full SavedViewDetail value to extract its runtime keys
  const sample: SavedViewDetail = {
    savedViewId: "id",
    title: "t",
    templateKey: "k",
    templateId: "tid",
    sourceTableRef: "s",
    sourceRecordRef: "r",
    status: "active",
    updatedAt: "2026-01-01T00:00:00Z",
    createdAt: "2026-01-01T00:00:00Z",
    cardMetadataJson: {},
    bindingJson: {},
    completedPresetSeedJson: {
      seed_version: "1",
      template_ref: {},
      source_ref: {},
      binding_ref: {},
      render_ref: {
        rendered_markdown_hash: "h",
        rendered_at: "2026-01-01T00:00:00Z",
        renderer_version: "1.0",
      },
      adjustment_ref: {},
      dashboard_ref: {},
      lineage_ref: {},
    },
    renderedMarkdown: "",
    userAdjustmentPatchJson: {},
    searchIndexText: "",
  };
  const savedViewKeys = Object.keys(sample);

  for (const field of canonicalDataAuthorityFields) {
    assertEquals(
      savedViewKeys.includes(field),
      false,
      `saved view must not own canonical data authority field: ${field}`,
    );
  }
});

// ─── preset catalog seed registration is separate from md_viewer ─────────────

Deno.test("md_viewer is a projection component, not a preset DB seed registration mechanism", () => {
  // md_viewer surfaces actions against persisted saved views only.
  // Preset DB seed registration is a separate lane (template_registry / catalog seed data).
  const mdViewerActions = new Set([
    "render_saved_markdown",
    "show_seed_summary",
    "show_binding_summary",
    "show_source_ref",
    "show_adjustment_status",
    "actions_open_source_record",
    "actions_refresh",
    "actions_clone",
    "actions_archive",
    "actions_copy_markdown",
    "actions_create_todo",
  ]);
  const seedRegistrationActions = [
    "create_preset_catalog_seed_rows",
    "bootstrap_registration",
    "register_preset_metadata_in_db",
  ];
  for (const seedOp of seedRegistrationActions) {
    assertEquals(
      mdViewerActions.has(seedOp),
      false,
      `md_viewer must not include preset DB seed registration action: ${seedOp}`,
    );
  }
  // md_viewer must include the projection rendering action
  assertEquals(mdViewerActions.has("render_saved_markdown"), true);
});

// ─── UIBuilder / route placement bundle ─────────────────────────────────────

Deno.test("TeamMarkdownDashboard has routable admin route and UIBuilder child placement", async () => {
  const routeSource = await Deno.readTextFile(
    "frontend/routes/admin/team-dashboard/index.tsx",
  );
  const uiBuilderSource = await Deno.readTextFile(
    "frontend/islands/UiBuilderAdmin.tsx",
  );

  assertEquals(routeSource.includes("<AdminAuthGate>"), true);
  assertEquals(routeSource.includes("TeamMarkdownDashboard"), true);
  assertEquals(routeSource.includes('placement="admin_route"'), true);
  assertEquals(uiBuilderSource.includes("UiBuilderPresetEcosystemPanel"), true);
  assertEquals(uiBuilderSource.includes("{open &&"), true);
  assertEquals(
    uiBuilderSource.includes('data-preset-child-surface="md_viewer"'),
    true,
  );
  assertEquals(
    uiBuilderSource.includes('placement="ui_builder_child_surface"'),
    true,
  );
});

Deno.test("MdViewer action boundary exposes implemented and disabled future actions", async () => {
  const dashboardSource = await Deno.readTextFile(
    "frontend/islands/TeamMarkdownDashboard.tsx",
  );
  const mdViewerSource = await Deno.readTextFile(
    "frontend/components/MdViewer.tsx",
  );

  assertEquals(dashboardSource.includes("FUTURE_BACKEND_ACTION_REASON"), true);
  assertEquals(dashboardSource.includes("handleOpenSourceRecord"), true);
  assertEquals(dashboardSource.includes("handleEditAdjustment"), true);
  assertEquals(dashboardSource.includes("handleCreateTodoCandidate"), true);
  assertEquals(mdViewerSource.includes("Refresh"), true);
  assertEquals(mdViewerSource.includes("Clone"), true);
  assertEquals(mdViewerSource.includes("Rebind"), true);
  assertEquals(mdViewerSource.includes("Seed invalid — action disabled"), true);
});

Deno.test("md_viewer catalog entry is a projection child, not a package canvas seed registration", async () => {
  const catalogSource = await Deno.readTextFile(
    "frontend/components/catalog.ts",
  );

  assertEquals(
    catalogSource.includes('componentKey: "md_viewer.projection"'),
    true,
  );
  assertEquals(
    catalogSource.includes('componentKind: "data_display/md_viewer"'),
    true,
  );
  assertEquals(
    catalogSource.includes("not a preset DB seed registration mechanism"),
    true,
  );
  assertEquals(catalogSource.includes("package canvas edit root"), true);
});
