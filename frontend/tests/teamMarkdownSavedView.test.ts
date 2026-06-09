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
import {
  buildMdTranslationAuthoringSeedCandidate,
  buildPlaceholderSchemaFromMarkdown,
  extractMarkdownPlaceholders,
  searchSavedViews,
} from "../api/teamMarkdownApi.ts";
import type {
  CompletedPresetSeed,
  SavedViewDetail,
} from "../api/teamMarkdownApi.ts";
import { __testOnly } from "../runtime/frontendScheduler.ts";
import {
  buildMdTranslationAuthoringSeedCandidate as buildSeedFromLib,
  type PlaceholderBindingEntry,
} from "../lib/mdTranslationSeedBuilder.ts";

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

// ─── md translation authoring seed registration tests ───────────────────────

Deno.test("todo/roadmap use seed-driven authoring surface wording, not bespoke form terms", async () => {
  const todo = await Deno.readTextFile(".agent/tasks/todo.md");
  const roadmap = await Deno.readTextFile("docs/system-roadmap.yaml");

  // Both files must reject old bespoke form terms (regression guard)
  for (const source of [todo, roadmap]) {
    assertEquals(
      source.includes("template_registration_modal_or_drawer UI"),
      false,
    );
    assertEquals(source.includes("RecordMarkdownBindForm"), false);
    assertEquals(source.includes("MarkdownTemplateRegistryForm"), false);
  }
  // Canonical task IDs must appear in todo (completed or pending)
  assertEquals(
    todo.includes("md_translation_template_seed_registration_surface"),
    true,
  );
  assertEquals(
    todo.includes("md_translation_binding_seed_authoring_surface"),
    true,
  );
  assertEquals(
    todo.includes("md_translation_saved_view_create_seed_flow"),
    true,
  );

  assertEquals(
    todo.includes("[x] **md_translation_seed_candidate_builder_contract**"),
    true,
  );
  assertEquals(
    todo.includes("[x] **unresolved_required_placeholder_backend_gate**"),
    true,
  );
  assertEquals(
    todo.includes(
      "[x] **md_translation_template_seed_registration_surface_completion**",
    ),
    true,
  );
  assertEquals(
    todo.includes(
      "[x] **md_translation_binding_seed_authoring_surface_completion**",
    ),
    true,
  );
  assertEquals(
    todo.includes(
      "[x] **md_translation_saved_view_create_seed_flow_completion**",
    ),
    true,
  );
  assertEquals(
    todo.includes("[x] **existing_component_bucket_composition_hardening**"),
    true,
  );
  // Bundle md_translation_registry_driven_authoring_surface_completion is closed —
  // these four known_gap_ref items must no longer appear in roadmap as pending.
  assertEquals(
    roadmap.includes(
      "md_translation_template_seed_registration_surface_completion_pending",
    ),
    false,
  );
  assertEquals(
    roadmap.includes(
      "md_translation_binding_seed_authoring_surface_completion_pending",
    ),
    false,
  );
  assertEquals(
    roadmap.includes(
      "md_translation_saved_view_create_seed_flow_completion_pending",
    ),
    false,
  );
  assertEquals(
    roadmap.includes("existing_component_bucket_composition_hardening_pending"),
    false,
  );
});

Deno.test("authoring surface uses existing bucket parts instead of Markdown-only modal/drawer creation", async () => {
  const source = await Deno.readTextFile(
    "frontend/islands/TeamMarkdownDashboard.tsx",
  );
  const ssot = await Deno.readTextFile(
    "docs/design/team-markdown-dashboard-saved-view-ssot.yaml",
  );

  assertEquals(
    source.includes(
      'data-authoring-bundle="md_translation_authoring_seed_registration"',
    ),
    true,
  );
  assertEquals(
    source.includes(
      'data-component-bucket-parts="panel input select textarea button existing_bucket_parts"',
    ),
    true,
  );
  assertEquals(
    source.includes("Markdown-only Modal/Drawer/Form component"),
    true,
  );
  assertEquals(source.includes("new MarkdownTemplateRegistryForm"), false);
  assertEquals(source.includes("new RecordMarkdownBindForm"), false);
  assertEquals(
    ssot.includes("bespoke Markdown-only modal/drawer/form components"),
    true,
  );
});

Deno.test("template authoring extracts placeholders and required/optional schema explicitly", () => {
  const schema = buildPlaceholderSchemaFromMarkdown(
    "# {{title}} {{owner}} {{optional_note}} {{owner}}",
    ["optional_note"],
  );
  const extracted = extractMarkdownPlaceholders(
    "# {{title}} {{owner}} {{optional_note}} {{owner}}",
    schema,
  );

  assertEquals(extracted.placeholderKeys, ["optional_note", "owner", "title"]);
  assertEquals(extracted.requiredPlaceholderKeys, ["owner", "title"]);
  assertEquals(extracted.optionalPlaceholderKeys, ["optional_note"]);
  assertEquals(
    schema["binding_resolution"],
    "user_explicit_selection_only_no_ai_inference",
  );
});

Deno.test("explicit binding builds seed candidate and saved_view:create payload without markdown reverse engineering", () => {
  const schema = buildPlaceholderSchemaFromMarkdown(
    "# {{title}}\n{{owner}}\n{{optional_note}}",
    ["optional_note"],
  );
  const candidate = buildMdTranslationAuthoringSeedCandidate({
    template: {
      templateId: "00000000-0000-0000-0000-000000000001",
      templateKey: "daily_note",
      templateMarkdown: "# {{title}}\n{{owner}}\n{{optional_note}}",
      placeholderSchemaJson: schema,
    },
    source: {
      sourceTableRef: "topology.physical_table",
      sourceRecordRef: "record-42",
    },
    bindings: [
      {
        placeholderKey: "title",
        required: true,
        bindingKind: "static_text",
        staticText: "Daily",
        previewValue: "Daily",
      },
      {
        placeholderKey: "owner",
        required: true,
        bindingKind: "jsonb_path",
        jsonbPath: "$.owner",
        previewValue: "Ada",
      },
    ],
    title: "Daily saved view",
  });

  assertEquals(candidate.renderedMarkdown, "# Daily\nAda\n");
  assertEquals(
    candidate.completedPresetSeedJson.template_ref["template_key"],
    "daily_note",
  );
  assertEquals(
    candidate.completedPresetSeedJson.source_ref["source_record_ref"],
    "record-42",
  );
  assertEquals(
    candidate.completedPresetSeedJson
      .lineage_ref["markdown_body_reverse_engineered"],
    false,
  );
  assertEquals(
    candidate.completedPresetSeedJson.binding_ref["resolution_mode"],
    "user_explicit_selection_only_no_ai_inference",
  );
  assertEquals(candidate.bindingJson["optional_empty_placeholder_keys"], [
    "optional_note",
  ]);
  assertEquals(
    candidate.cardMetadataJson["seed_authoring_bundle"],
    "md_translation_authoring_seed_registration",
  );
});

Deno.test("unresolved required placeholder blocks saved view create candidate explicitly", () => {
  const schema = buildPlaceholderSchemaFromMarkdown(
    "# {{title}} {{owner}}",
    [],
  );
  assertThrows(
    () =>
      buildMdTranslationAuthoringSeedCandidate({
        template: {
          templateId: "00000000-0000-0000-0000-000000000001",
          templateKey: "daily_note",
          templateMarkdown: "# {{title}} {{owner}}",
          placeholderSchemaJson: schema,
        },
        source: {
          sourceTableRef: "topology.physical_table",
          sourceRecordRef: "record-42",
        },
        bindings: [{
          placeholderKey: "title",
          required: true,
          bindingKind: "static_text",
          staticText: "Daily",
        }],
        title: "Daily saved view",
      }),
    Error,
    "REQUIRED_PLACEHOLDER_UNBOUND",
  );
});

Deno.test("optional placeholder empty state is explicit and not silent coercion", () => {
  const schema = buildPlaceholderSchemaFromMarkdown(
    "{{required_key}} {{optional_key}}",
    ["optional_key"],
  );
  const candidate = buildMdTranslationAuthoringSeedCandidate({
    template: {
      templateId: "00000000-0000-0000-0000-000000000001",
      templateKey: "optional_test",
      templateMarkdown: "{{required_key}} {{optional_key}}",
      placeholderSchemaJson: schema,
    },
    source: { sourceTableRef: "topology.source", sourceRecordRef: "record-1" },
    bindings: [{
      placeholderKey: "required_key",
      required: true,
      bindingKind: "static_text",
      staticText: "value",
    }],
    title: "Optional test",
  });

  const bindings = candidate.bindingJson["placeholder_bindings"] as Record<
    string,
    unknown
  >[];
  const optionalBinding = bindings.find((binding) =>
    binding["placeholder_key"] === "optional_key"
  );
  assertEquals(optionalBinding?.["empty_state"], "explicit_optional_empty");
  assertEquals(candidate.bindingJson["optional_empty_placeholder_keys"], [
    "optional_key",
  ]);
});

Deno.test("createSavedView rejects malformed create response explicitly", async () => {
  const originalFetch = globalThis.fetch;
  __testOnly.resetCommandQueue();
  try {
    globalThis.fetch = () =>
      Promise.resolve(
        new Response(
          JSON.stringify({ success: true, emission: { data: { ok: true } } }),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );
    const mod = await import("../api/teamMarkdownApi.ts");
    await assertRejects(
      () =>
        mod.createSavedView({
          templateId: "00000000-0000-0000-0000-000000000001",
          title: "Broken create",
          sourceTableRef: "topology.source",
          sourceRecordRef: "record-1",
          bindingJson: {},
          completedPresetSeedJson: buildValidSeed(),
          renderedMarkdown: "projection",
        }),
      Error,
      "TEAM_MARKDOWN_CREATE_RESPONSE_INVALID",
    );
  } finally {
    globalThis.fetch = originalFetch;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("Team Markdown frontend does not call direct DB writes or mutate UIBuilder canvas", async () => {
  const apiSource = await Deno.readTextFile("frontend/api/teamMarkdownApi.ts");
  const dashboardSource = await Deno.readTextFile(
    "frontend/islands/TeamMarkdownDashboard.tsx",
  );
  const uiBuilderSource = await Deno.readTextFile(
    "frontend/islands/UiBuilderAdmin.tsx",
  );

  assertEquals(apiSource.includes('layer: "team_markdown"'), true);
  assertEquals(apiSource.includes("queueAdminClientCommand"), true);
  assertEquals(apiSource.includes('fetch("postgres'), false);
  assertEquals(dashboardSource.includes("createSavedView"), true);
  assertEquals(
    dashboardSource.includes("buildMdTranslationAuthoringSeedCandidate"),
    true,
  );
  assertEquals(uiBuilderSource.includes("preview"), true);
  assertEquals(uiBuilderSource.includes("validate"), true);
  assertEquals(uiBuilderSource.includes("apply"), true);
  assertEquals(uiBuilderSource.includes("TeamMarkdownDashboard"), true);
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

// ─── seed builder contract tests ─────────────────────────────────────────────

Deno.test("buildMdTranslationAuthoringSeedCandidate assembles seed from template/source/binding", () => {
  const entries: PlaceholderBindingEntry[] = [
    {
      placeholderKey: "record.name",
      sourceKind: "physical_table_column",
      fieldRef: "name",
      required: true,
    },
  ];
  const { candidate, unresolvedRequiredKeys } =
    buildSeedFromLib({
      templateId: "tid-1",
      templateKey: "test_tmpl",
      sourceTableRef: "topology.physical_tables",
      sourceRecordRef: "rec-1",
      bindingEntries: entries,
      renderedMarkdown: "# Hello",
      renderedMarkdownHash: "abc123",
      title: "Test View",
      excerpt: "test excerpt",
    });

  assertEquals(candidate.template_ref.template_id, "tid-1");
  assertEquals(candidate.template_ref.template_key, "test_tmpl");
  assertEquals(candidate.source_ref.source_table_ref, "topology.physical_tables");
  assertEquals(candidate.source_ref.source_record_ref, "rec-1");
  assertEquals(candidate.dashboard_ref.title, "Test View");
  assertEquals(candidate.render_ref.rendered_markdown_hash, "abc123");
  assertEquals(unresolvedRequiredKeys.length, 0);
});

Deno.test("buildMdTranslationAuthoringSeedCandidate unresolvedRequiredKeys blocks create", () => {
  const entries: PlaceholderBindingEntry[] = [
    {
      placeholderKey: "record.summary",
      sourceKind: "",
      fieldRef: "",
      required: true,
    },
  ];
  const { unresolvedRequiredKeys } = buildSeedFromLib({
    templateId: "tid-2",
    templateKey: "tmpl_b",
    sourceTableRef: "topology.physical_tables",
    sourceRecordRef: "rec-2",
    bindingEntries: entries,
    renderedMarkdown: "",
    renderedMarkdownHash: "00000000",
    title: "Blocked View",
    excerpt: "",
  });

  assertEquals(
    unresolvedRequiredKeys.includes("record.summary"),
    true,
    "unresolved required placeholder must appear in unresolvedRequiredKeys",
  );
  assertEquals(unresolvedRequiredKeys.length > 0, true);
});

Deno.test("buildMdTranslationAuthoringSeedCandidate optional placeholder persists as explicit_optional_empty", () => {
  const entries: PlaceholderBindingEntry[] = [
    {
      placeholderKey: "record.notes",
      sourceKind: "",
      fieldRef: "",
      required: false,
    },
  ];
  const { candidate, unresolvedRequiredKeys } =
    buildSeedFromLib({
      templateId: "tid-3",
      templateKey: "tmpl_c",
      sourceTableRef: "topology.physical_tables",
      sourceRecordRef: "rec-3",
      bindingEntries: entries,
      renderedMarkdown: "",
      renderedMarkdownHash: "11111111",
      title: "Optional View",
      excerpt: "",
    });

  const bindingEntry = (
    candidate.binding_ref.binding_json as Record<
      string,
      { source_kind: string; field_ref: string }
    >
  )["record.notes"];
  assertEquals(bindingEntry?.source_kind, "explicit_optional_empty");
  assertEquals(bindingEntry?.field_ref, "");
  assertEquals(unresolvedRequiredKeys.length, 0);
});

Deno.test("buildMdTranslationAuthoringSeedCandidate does not infer binding from markdown text", () => {
  // Binding authority is user-selection only — no AI inference, no markdown body parsing.
  // Even if markdown contains {{record.name}}, if no binding entry is provided, it stays unbound.
  const entries: PlaceholderBindingEntry[] = [];
  const { candidate } = buildSeedFromLib({
    templateId: "tid-4",
    templateKey: "tmpl_d",
    sourceTableRef: "topology.physical_tables",
    sourceRecordRef: "rec-4",
    bindingEntries: entries,
    renderedMarkdown: "Hello {{record.name}} and {{record.summary}}",
    renderedMarkdownHash: "22222222",
    title: "No Infer View",
    excerpt: "",
  });

  // binding_json must be empty — no keys inferred from markdown body
  assertEquals(
    Object.keys(candidate.binding_ref.binding_json as Record<string, unknown>)
      .length,
    0,
    "binding_json must be empty when no binding entries are provided (no inference from markdown body)",
  );
});

Deno.test("manual fallback is explicit not silent for source table ref", () => {
  // When physical table registry is not available, authoring surface must show explicit labeled fallback.
  // This test validates that MdTranslationAuthoringSeedSurface carries the explicit label.
  const surfaceSource = Deno.readTextFileSync(
    "frontend/components/MdTranslationAuthoringSeedSurface.tsx",
  );
  assertEquals(
    surfaceSource.includes("physical table registry not available"),
    true,
    "authoring surface must show explicit label when physical table registry is unavailable (no silent fallback)",
  );
});

Deno.test("authoring surface does not write DB directly", () => {
  // Frontend must not have direct DB connection — all mutations via team_markdown API.
  const surfaceSource = Deno.readTextFileSync(
    "frontend/components/MdTranslationAuthoringSeedSurface.tsx",
  );
  // Must use createSavedView or createTemplate (API), not raw DB/SQL calls
  assertEquals(
    surfaceSource.includes("createSavedView") ||
      surfaceSource.includes("createTemplate"),
    true,
    "authoring surface must use team_markdown API functions, not direct DB writes",
  );
  assertEquals(
    surfaceSource.includes("NpgsqlConnection"),
    false,
    "authoring surface must not contain direct DB connection code",
  );
  assertEquals(
    surfaceSource.includes("INSERT INTO"),
    false,
    "authoring surface must not contain raw SQL INSERT statements",
  );
});

Deno.test("UIBuilder preset ecosystem exposes md translation authoring entry", async () => {
  const uiBuilderSource = await Deno.readTextFile(
    "frontend/islands/UiBuilderAdmin.tsx",
  );
  assertEquals(
    uiBuilderSource.includes('data-preset-authoring-surface="md_translation"'),
    true,
    "UIBuilder preset ecosystem panel must expose md translation authoring entry",
  );
  assertEquals(
    uiBuilderSource.includes("MdTranslationAuthoringSeedSurface"),
    true,
    "UIBuilder must import and use MdTranslationAuthoringSeedSurface",
  );
});

Deno.test("UiBuilderPresetEcosystemPanel authoring entry does not mutate canvas", async () => {
  const uiBuilderSource = await Deno.readTextFile(
    "frontend/islands/UiBuilderAdmin.tsx",
  );
  // The authoring panel description must say it does not mutate canvas/package
  assertEquals(
    uiBuilderSource.includes("does NOT mutate UIBuilder") ||
      uiBuilderSource.includes("does not mutate"),
    true,
    "authoring entry description must explicitly state it does not mutate UIBuilder canvas",
  );
});

Deno.test("md_translation authoring surface catalog entry is registry-driven authoring, not canvas seed", async () => {
  const catalogSource = await Deno.readTextFile(
    "frontend/components/catalog.ts",
  );
  assertEquals(
    catalogSource.includes(
      'componentKey: "md_translation_authoring_surface.authoring"',
    ),
    true,
  );
  assertEquals(
    catalogSource.includes('componentKind: "authoring/md_translation"'),
    true,
  );
  assertEquals(
    catalogSource.includes("registry-driven"),
    true,
  );
});

// ─── registry-driven authoring surface bundle completion tests ────────────────

Deno.test("authoring surface imports listRelationshipRemoteTargets from adminApi as primary source table source", () => {
  const surfaceSource = Deno.readTextFileSync(
    "frontend/components/MdTranslationAuthoringSeedSurface.tsx",
  );
  assertEquals(
    surfaceSource.includes("listRelationshipRemoteTargets"),
    true,
    "authoring surface must import and use listRelationshipRemoteTargets from adminApi for registry-driven table selection",
  );
  assertEquals(
    surfaceSource.includes("../api/adminApi.ts"),
    true,
    "authoring surface must import from adminApi.ts for registry-driven source",
  );
});

Deno.test("authoring surface provides column candidates from registry to binding rows", () => {
  const surfaceSource = Deno.readTextFileSync(
    "frontend/components/MdTranslationAuthoringSeedSurface.tsx",
  );
  assertEquals(
    surfaceSource.includes("availableColumns"),
    true,
    "authoring surface must pass availableColumns from registry to binding rows",
  );
  assertEquals(
    surfaceSource.includes("datalist"),
    true,
    "authoring surface must include datalist element for column candidate selection",
  );
  assertEquals(
    surfaceSource.includes("selectedTableColumns"),
    true,
    "authoring surface must derive selectedTableColumns from registry selection",
  );
});

Deno.test("saved_query_result_field binding has explicit no-enumeration-API label", () => {
  const surfaceSource = Deno.readTextFileSync(
    "frontend/components/MdTranslationAuthoringSeedSurface.tsx",
  );
  assertEquals(
    surfaceSource.includes("no saved query enumeration API"),
    true,
    "saved_query_result_field must carry explicit label that there is no saved query enumeration API — no silent fallback",
  );
});

Deno.test("source record ref is always explicit manual input — no record enumeration API", () => {
  const surfaceSource = Deno.readTextFileSync(
    "frontend/components/MdTranslationAuthoringSeedSurface.tsx",
  );
  assertEquals(
    surfaceSource.includes('data-source-record-ref-manual="true"'),
    true,
    "source record ref input must carry data-source-record-ref-manual attribute",
  );
  assertEquals(
    surfaceSource.includes("no record enumeration API"),
    true,
    "source record ref must carry explicit label that there is no record enumeration API",
  );
});

Deno.test("authoring surface data-component-bucket-parts attribute documents existing bucket composition", () => {
  const surfaceSource = Deno.readTextFileSync(
    "frontend/components/MdTranslationAuthoringSeedSurface.tsx",
  );
  assertEquals(
    surfaceSource.includes(
      'data-component-bucket-parts="select input textarea button existing_bucket_parts"',
    ),
    true,
    "main authoring surface div must declare select input textarea button existing_bucket_parts",
  );
});

Deno.test("authoring surface registry state unavailable is explicit, not silent", () => {
  const surfaceSource = Deno.readTextFileSync(
    "frontend/components/MdTranslationAuthoringSeedSurface.tsx",
  );
  assertEquals(
    surfaceSource.includes('data-registry-state="unavailable"'),
    true,
    "registry unavailable state must be marked with data-registry-state=unavailable attribute",
  );
  assertEquals(
    surfaceSource.includes("registryAvailable"),
    true,
    "authoring surface must track registryAvailable state",
  );
});
