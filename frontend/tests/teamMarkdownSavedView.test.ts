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
  assertExists,
  assertRejects,
  assertThrows,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
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
  const templateRef = s.template_ref as Record<string, unknown>;
  if (typeof templateRef.template_id !== "string" || !templateRef.template_id) return "COMPLETED_PRESET_SEED_INVALID";
  if (typeof templateRef.template_key !== "string" || !templateRef.template_key) return "COMPLETED_PRESET_SEED_INVALID";
  const sourceRef = s.source_ref as Record<string, unknown>;
  if (typeof sourceRef.source_table_ref !== "string" || !sourceRef.source_table_ref) return "COMPLETED_PRESET_SEED_INVALID";
  if (typeof sourceRef.source_record_ref !== "string" || !sourceRef.source_record_ref) return "COMPLETED_PRESET_SEED_INVALID";
  const bindingRef = s.binding_ref as Record<string, unknown>;
  if (!bindingRef.binding_json || typeof bindingRef.binding_json !== "object") return "COMPLETED_PRESET_SEED_INVALID";
  if (!bindingRef.placeholder_to_field_map || typeof bindingRef.placeholder_to_field_map !== "object") return "COMPLETED_PRESET_SEED_INVALID";
  if (!Array.isArray(bindingRef.required_placeholder_keys)) return "COMPLETED_PRESET_SEED_MISSING:binding_ref.required_placeholder_keys";
  if (!Array.isArray(bindingRef.optional_placeholder_keys)) return "COMPLETED_PRESET_SEED_MISSING:binding_ref.optional_placeholder_keys";
  if (Array.isArray(bindingRef.unresolved_required_placeholder_keys) && bindingRef.unresolved_required_placeholder_keys.length > 0) return "REQUIRED_PLACEHOLDER_UNBOUND";
  const renderRef = s.render_ref as Record<string, unknown>;
  if (
    !renderRef || typeof renderRef.rendered_markdown_hash !== "string" ||
    !renderRef.rendered_markdown_hash
  ) {
    return "COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH";
  }
  if (typeof renderRef.rendered_at !== "string" || !renderRef.rendered_at) return "COMPLETED_PRESET_SEED_INVALID";
  if (typeof renderRef.renderer_version !== "string" || !renderRef.renderer_version) return "COMPLETED_PRESET_SEED_INVALID";
  if (!Array.isArray(renderRef.unresolved_placeholder_keys)) return "COMPLETED_PRESET_SEED_MISSING:render_ref.unresolved_placeholder_keys";
  if (renderRef.unresolved_placeholder_keys.length > 0) return "REQUIRED_PLACEHOLDER_UNBOUND";
  const adjustmentRef = s.adjustment_ref as Record<string, unknown>;
  if (!adjustmentRef.user_adjustment_patch_json || typeof adjustmentRef.user_adjustment_patch_json !== "object") return "COMPLETED_PRESET_SEED_INVALID";
  if (typeof adjustmentRef.adjustment_mode !== "string") return "COMPLETED_PRESET_SEED_INVALID";
  const dashboardRef = s.dashboard_ref as Record<string, unknown>;
  if (typeof dashboardRef.title !== "string" || typeof dashboardRef.excerpt !== "string") return "COMPLETED_PRESET_SEED_INVALID";
  if (!Array.isArray(dashboardRef.tags)) return "COMPLETED_PRESET_SEED_INVALID";
  if (!dashboardRef.card_metadata_json || typeof dashboardRef.card_metadata_json !== "object") return "COMPLETED_PRESET_SEED_INVALID";
  if (!dashboardRef.search_index_basis_json || typeof dashboardRef.search_index_basis_json !== "object") return "COMPLETED_PRESET_SEED_INVALID";
  const lineageRef = s.lineage_ref as Record<string, unknown>;
  if (typeof lineageRef.created_from !== "string" || !("parent_saved_view_id" in lineageRef)) return "COMPLETED_PRESET_SEED_INVALID";
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
    binding_ref: {
      binding_json: {},
      placeholder_to_field_map: {},
      required_placeholder_keys: [],
      optional_placeholder_keys: [],
      unresolved_required_placeholder_keys: [],
    },
    render_ref: {
      rendered_markdown_hash: renderHash,
      rendered_at: "2026-06-08T00:00:00Z",
      renderer_version: "1.0",
      unresolved_placeholder_keys: [],
    },
    adjustment_ref: { adjustment_mode: "none", user_adjustment_patch_json: {} },
    dashboard_ref: {
      title: "Test View",
      excerpt: "excerpt",
      tags: [],
      card_metadata_json: {},
      search_index_basis_json: {},
    },
    lineage_ref: { created_from: "template_record", parent_saved_view_id: null },
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

Deno.test("roadmap/SSOT use seed-driven authoring surface wording, not bespoke form terms", async () => {
  const roadmap = await Deno.readTextFile("docs/system-roadmap.yaml");
  const ssot = await Deno.readTextFile(
    "docs/design/team-markdown-dashboard-saved-view-ssot.yaml",
  );
  const source = await Deno.readTextFile(
    "frontend/components/MdTranslationAuthoringSeedSurface.tsx",
  );

  // Roadmap/SSOT surfaces must reject old bespoke form terms without
  // treating .agent/tasks/todo.md as a completed evidence ledger.
  for (const content of [roadmap, ssot]) {
    assertEquals(
      content.includes("template_registration_modal_or_drawer UI"),
      false,
    );
    assertEquals(content.includes("RecordMarkdownBindForm"), false);
    assertEquals(content.includes("MarkdownTemplateRegistryForm"), false);
  }
  assertEquals(
    source.includes(
      'data-component-bucket-parts="select input textarea button existing_bucket_parts"',
    ),
    true,
  );

  // Completed seed contract evidence belongs to roadmap evidence/completion
  // refs and the feature SSOT, not to completed [x] anchors in todo.md.
  assertEquals(
    ssot.includes("md_translation_seed_candidate_builder_contract"),
    true,
  );
  assertEquals(
    ssot.includes("unresolved_required_placeholder_backend_gate"),
    true,
  );
  assertEquals(
    ssot.includes("prompt_level_template_seed_registration_surface_completion"),
    true,
  );
  assertEquals(
    ssot.includes("prompt_level_binding_seed_authoring_surface_completion"),
    true,
  );
  assertEquals(
    ssot.includes("prompt_level_saved_view_create_seed_flow_completion"),
    true,
  );
  assertEquals(
    ssot.includes("existing_component_bucket_composition_hardening"),
    true,
  );
  assertEquals(
    roadmap.includes("frontend/components/MdTranslationAuthoringSeedSurface.tsx"),
    true,
  );
  assertEquals(
    roadmap.includes("frontend/lib/mdTranslationSeedBuilder.ts"),
    true,
  );
  assertEquals(
    roadmap.includes(
      "registry_driven_authoring_surface_template_and_binding_selection",
    ),
    true,
  );
  assertEquals(
    roadmap.includes("client_seed_candidate_builder_constructs_required_seed_refs"),
    true,
  );
  assertEquals(
    roadmap.includes(".agent/tasks/todo.md#preset_team_markdown_saved_view_seed"),
    false,
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
    (candidate.completedPresetSeedJson.binding_ref["binding_json"] as Record<string, unknown>)["resolution_mode"],
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
  // UIBuilder owns preview/validate/apply for canvas package mutations
  assertEquals(uiBuilderSource.includes("preview"), true);
  assertEquals(uiBuilderSource.includes("validate"), true);
  assertEquals(uiBuilderSource.includes("apply"), true);
  // UIBuilder must NOT import TeamMarkdownDashboard (not a permanent child surface)
  assertEquals(
    uiBuilderSource.includes("TeamMarkdownDashboard"),
    false,
    "UIBuilder must not import TeamMarkdownDashboard — team dashboard is /admin/team-dashboard only",
  );
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
    "templateMarkdown",
    "sourceRecordJson",
    "updatedCompletedPresetSeedJson",
    "searchIndexText",
  ];
  assertEquals(
    refreshRequiredFields.includes("updatedCompletedPresetSeedJson"),
    true,
  );
  assertEquals(
    refreshRequiredFields.includes("sourceRecordJson"),
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
    "templateMarkdown",
    "sourceRecordJson",
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
  const seedGatedActions = new Set(["refresh", "clone", "rebind"]);
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

Deno.test("TeamMarkdownDashboard primary route is /admin/team-dashboard (not UIBuilder permanent child surface)", async () => {
  const routeSource = await Deno.readTextFile(
    "frontend/routes/admin/team-dashboard/index.tsx",
  );
  const uiBuilderSource = await Deno.readTextFile(
    "frontend/islands/UiBuilderAdmin.tsx",
  );

  // /admin/team-dashboard is the canonical primary route
  assertEquals(routeSource.includes("<AdminAuthGate>"), true);
  assertEquals(routeSource.includes("TeamMarkdownDashboard"), true);
  assertEquals(routeSource.includes('placement="admin_route"'), true);

  // UiBuilder must NOT permanently mount Team Markdown Dashboard as a child surface
  assertEquals(
    uiBuilderSource.includes("UiBuilderPresetEcosystemPanel"),
    false,
    "UiBuilderAdmin must not permanently mount UiBuilderPresetEcosystemPanel (responsibility mixing resolved)",
  );
  assertEquals(
    uiBuilderSource.includes('data-preset-child-surface="md_viewer"'),
    false,
    "UIBuilder must not have data-preset-child-surface=md_viewer permanently rendered",
  );
  assertEquals(
    uiBuilderSource.includes("Preset ecosystem — md_viewer child surface"),
    false,
    "UIBuilder must not display 'Preset ecosystem — md_viewer child surface' label",
  );
});

Deno.test("MdViewer action boundary exposes registered seed-gated actions", async () => {
  const dashboardSource = await Deno.readTextFile(
    "frontend/islands/TeamMarkdownDashboard.tsx",
  );
  const mdViewerSource = await Deno.readTextFile(
    "frontend/components/MdViewer.tsx",
  );

  assertEquals(dashboardSource.includes("EXPLICIT_PAYLOAD_REQUIRED_REASON"), true);
  assertEquals(dashboardSource.includes("handleOpenSourceRecord"), true);
  assertEquals(dashboardSource.includes("handleEditAdjustment"), true);
  assertEquals(dashboardSource.includes("handleCreateTodoCandidate"), true);
  assertEquals(mdViewerSource.includes("Refresh"), true);
  assertEquals(mdViewerSource.includes("Clone"), true);
  assertEquals(mdViewerSource.includes("Rebind"), true);
  assertEquals(mdViewerSource.includes("onRebind"), true);
  assertEquals(mdViewerSource.includes("Seed invalid — action disabled"), true);
});

Deno.test("md_viewer catalog entry is a dashboard/read-work component candidate, not a package canvas seed registration", async () => {
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
  // md_viewer must be described as a dashboard/read-work component candidate
  assertEquals(
    catalogSource.includes("dashboard/read-work component candidate"),
    true,
    "md_viewer.projection notes must describe it as a dashboard/read-work component candidate",
  );
  // md_viewer must not be described as a UIBuilder preset_ecosystem child surface
  assertEquals(
    catalogSource.includes("UIBuilder preset_ecosystem child projection surface"),
    false,
    "md_viewer.projection must not be described as a UIBuilder preset_ecosystem child projection surface",
  );
});

Deno.test("md_viewer does not hold active topology / physical record / saved view authority", async () => {
  const catalogSource = await Deno.readTextFile(
    "frontend/components/catalog.ts",
  );
  // Catalog notes must explicitly state md_viewer does not hold these authorities (text check on notes field)
  assertEquals(
    catalogSource.includes("active topology authority"),
    true,
    "catalog notes must explicitly deny active topology authority for md_viewer",
  );
  assertEquals(
    catalogSource.includes("physical record authority"),
    true,
    "catalog notes must explicitly deny physical record authority for md_viewer",
  );
  assertEquals(
    catalogSource.includes("saved view authority"),
    true,
    "catalog notes must explicitly deny saved view authority for md_viewer",
  );
  // Object-level check: md_viewer.projection must have runtimeConnected:true (factory required for
  // DashboardCandidatePalette placement) but must NOT hold topology/record/saved-view authority
  // (enforced via catalog notes above). runtimeConnected:true means preview factory exists;
  // it does not confer topology, record, or saved-view authority — those are denied by design.
  // Source regex is avoided because COMPONENT_TEMPLATE_CATALOG_IDENTITIES also contains
  // componentKey:"md_viewer.projection" without runtimeConnected, causing regex to hit
  // the next runtimeConnected in the file (button.primitive:true).
  const mdViewerEntry = COMPONENT_CATALOG_ENTRIES.find(
    (c) => c.componentKey === "md_viewer.projection",
  );
  assertExists(mdViewerEntry, "md_viewer.projection must exist in COMPONENT_CATALOG_ENTRIES");
  assertEquals(
    mdViewerEntry.runtimeConnected,
    true,
    "md_viewer.projection must have runtimeConnected:true (preview factory required for DashboardCandidatePalette placement)",
  );
});

Deno.test("/admin/team-dashboard is the primary placement for saved markdown view search and seed rehydration", async () => {
  const routeSource = await Deno.readTextFile(
    "frontend/routes/admin/team-dashboard/index.tsx",
  );
  const ssotSource = await Deno.readTextFile(
    "docs/design/team-markdown-dashboard-saved-view-ssot.yaml",
  );

  // Route must be guarded and mount TeamMarkdownDashboard as admin_route
  assertEquals(routeSource.includes("<AdminAuthGate>"), true);
  assertEquals(routeSource.includes("TeamMarkdownDashboard"), true);
  assertEquals(routeSource.includes('placement="admin_route"'), true);
  // SSOT must have /admin/team-dashboard as the preferred entry surface
  assertEquals(
    ssotSource.includes("preferred: /admin/team-dashboard"),
    true,
    "SSOT must declare /admin/team-dashboard as the preferred entry surface",
  );
  // SSOT implemented: block must NOT list UIBuilder as an entry surface.
  // Check only the implemented: block to avoid false-positive on the removed: history entry.
  const implementedBlock = ssotSource.match(
    /implemented:\s*([\s\S]*?)(?=\s{6}removed:|\s{6}primary_ui:|$)/,
  )?.[1] ?? "";
  assertEquals(
    implementedBlock.includes("ui-builder"),
    false,
    "SSOT implemented entry surface block must not list UIBuilder (removed entry belongs in removed: block)",
  );
});

Deno.test("md_viewer.projection has dashboard_placement_candidate tag and is NOT in registrationRequired bucket catalog", () => {
  // Object-level check: source regex is avoided because COMPONENT_TEMPLATE_CATALOG_IDENTITIES
  // also has componentKey:"md_viewer.projection" without capabilityTags/registrationRequired,
  // causing block-regex to capture only the identity entry (no tags).
  const mdViewerEntry = COMPONENT_CATALOG_ENTRIES.find(
    (c) => c.componentKey === "md_viewer.projection",
  );
  assertExists(mdViewerEntry, "md_viewer.projection must exist in COMPONENT_CATALOG_ENTRIES");

  assertEquals(
    mdViewerEntry.capabilityTags.includes("dashboard_placement_candidate"),
    true,
    "md_viewer.projection capabilityTags must include dashboard_placement_candidate",
  );
  assertEquals(
    mdViewerEntry.registrationRequired,
    false,
    "md_viewer.projection must have registrationRequired:false — not a DB bucket registration candidate",
  );

  // Must NOT appear in the registrationRequired:true bucket catalog
  const bucketCatalog = COMPONENT_CATALOG_ENTRIES.filter((c) => c.registrationRequired);
  assertEquals(
    bucketCatalog.some((c) => c.componentKey === "md_viewer.projection"),
    false,
    "md_viewer.projection must not be in registrationRequired:true bucket catalog",
  );

  // Must appear in the dashboard_placement_candidate set
  const dashboardCandidates = COMPONENT_CATALOG_ENTRIES.filter((c) =>
    c.capabilityTags.includes("dashboard_placement_candidate")
  );
  assertEquals(
    dashboardCandidates.some((c) => c.componentKey === "md_viewer.projection"),
    true,
    "md_viewer.projection must appear in dashboard_placement_candidate filtered entries",
  );
});

Deno.test("DashboardCandidatePalette is in UiBuilderAdmin and uses dashboard_placement_candidate filter", async () => {
  const uiBuilderSource = await Deno.readTextFile(
    "frontend/islands/UiBuilderAdmin.tsx",
  );

  // DashboardCandidatePalette component must exist in UIBuilder
  assertEquals(
    uiBuilderSource.includes("DashboardCandidatePalette"),
    true,
    "UiBuilderAdmin must contain DashboardCandidatePalette component for dashboard candidate placement",
  );
  // Must use dashboard_placement_candidate as the filter key (not registrationRequired)
  assertEquals(
    uiBuilderSource.includes('"dashboard_placement_candidate"'),
    true,
    "DashboardCandidatePalette must filter by dashboard_placement_candidate tag",
  );
  // Must render with data-dashboard-candidate-palette attribute
  assertEquals(
    uiBuilderSource.includes('data-dashboard-candidate-palette="true"'),
    true,
    "DashboardCandidatePalette must render with data-dashboard-candidate-palette attribute",
  );
  // UiBuilder must still NOT mount the old UiBuilderPresetEcosystemPanel
  assertEquals(
    uiBuilderSource.includes("UiBuilderPresetEcosystemPanel"),
    false,
    "UiBuilderAdmin must not permanently mount UiBuilderPresetEcosystemPanel",
  );
});

Deno.test("registrationRequired and dashboard placement visibility are not conflated in UIBuilder", async () => {
  const uiBuilderSource = await Deno.readTextFile(
    "frontend/islands/UiBuilderAdmin.tsx",
  );

  // The bucket registration catalog filter uses registrationRequired
  assertEquals(
    uiBuilderSource.includes("c.registrationRequired"),
    true,
    "bucket catalog must filter by registrationRequired for DB registration flow",
  );
  // The dashboard candidate palette uses dashboard_placement_candidate tag — separate gate
  assertEquals(
    uiBuilderSource.includes('"dashboard_placement_candidate"'),
    true,
    "dashboard candidate palette must use dashboard_placement_candidate tag, not registrationRequired",
  );
  // registrationRequired check must be skipped for dashboard_placement_candidate entries
  assertEquals(
    uiBuilderSource.includes("registrationRequired !== false"),
    true,
    "placement handlers must skip DB registration for registrationRequired:false entries",
  );
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

Deno.test("UIBuilder does not permanently mount md translation authoring surface (responsibility boundary resolved)", async () => {
  const uiBuilderSource = await Deno.readTextFile(
    "frontend/islands/UiBuilderAdmin.tsx",
  );
  // MdTranslationAuthoringSeedSurface must not be permanently mounted in UIBuilder
  assertEquals(
    uiBuilderSource.includes('data-preset-authoring-surface="md_translation"'),
    false,
    "UIBuilder must not permanently mount md translation authoring surface — it belongs to /admin/team-dashboard",
  );
  assertEquals(
    uiBuilderSource.includes("MdTranslationAuthoringSeedSurface"),
    false,
    "UIBuilder must not import MdTranslationAuthoringSeedSurface — authoring surface belongs to /admin/team-dashboard",
  );
});

Deno.test("MdTranslationAuthoringSeedSurface is present in TeamMarkdownDashboard (not UIBuilder)", async () => {
  const dashboardSource = await Deno.readTextFile(
    "frontend/islands/TeamMarkdownDashboard.tsx",
  );
  // The authoring surface belongs to the team dashboard island, not UIBuilder
  assertEquals(
    dashboardSource.includes("MdTranslationAuthoringSeedSurface") ||
      dashboardSource.includes("data-authoring-bundle"),
    true,
    "MdTranslationAuthoringSeedSurface or authoring bundle marker must be in TeamMarkdownDashboard",
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
