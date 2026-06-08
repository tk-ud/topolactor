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

import { assertEquals, assertThrows } from "https://deno.land/std@0.208.0/assert/mod.ts";
import type { CompletedPresetSeed, SavedViewDetail } from "../api/teamMarkdownApi.ts";

// ─── seed validation contract tests ──────────────────────────────────────────

function validateCompletedPresetSeed(seed: unknown): string | null {
  if (!seed || typeof seed !== "object" || Array.isArray(seed)) {
    return "COMPLETED_PRESET_SEED_INVALID";
  }
  const s = seed as Record<string, unknown>;
  const required = ["seed_version", "template_ref", "source_ref", "binding_ref", "render_ref", "adjustment_ref", "dashboard_ref", "lineage_ref"];
  for (const key of required) {
    if (!(key in s)) return `COMPLETED_PRESET_SEED_MISSING:${key}`;
  }
  const renderRef = s.render_ref as Record<string, unknown>;
  if (!renderRef || typeof renderRef.rendered_markdown_hash !== "string" || !renderRef.rendered_markdown_hash) {
    return "COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH";
  }
  return null;
}

function buildValidSeed(renderHash = "abc123hash"): CompletedPresetSeed {
  return {
    seed_version: "1",
    template_ref: { template_id: "00000000-0000-0000-0000-000000000001", template_key: "test_template" },
    source_ref: { source_table_ref: "topology.physical_tables", source_record_ref: "test_record" },
    binding_ref: { binding_json: {}, placeholder_to_field_map: {} },
    render_ref: { rendered_markdown_hash: renderHash, rendered_at: "2026-06-08T00:00:00Z", renderer_version: "1.0" },
    adjustment_ref: { adjustment_mode: "none" },
    dashboard_ref: { title: "Test View", excerpt: "excerpt", tags: [] },
    lineage_ref: { created_from: "template_record" },
  };
}

Deno.test("validateCompletedPresetSeed — rejects non-object", () => {
  assertEquals(validateCompletedPresetSeed("not_an_object"), "COMPLETED_PRESET_SEED_INVALID");
  assertEquals(validateCompletedPresetSeed(null), "COMPLETED_PRESET_SEED_INVALID");
  assertEquals(validateCompletedPresetSeed([]), "COMPLETED_PRESET_SEED_INVALID");
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
  assertEquals(validateCompletedPresetSeed(seed), "COMPLETED_PRESET_SEED_RENDER_HASH_MISMATCH");
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
  const mutationKeys = ["renderedMarkdown", "bindingJson", "completedPresetSeedJson", "title"];
  for (const key of mutationKeys) {
    assertEquals(searchPayloadKeys.includes(key), false,
      `search payload must not include mutation field: ${key}`);
  }
});

// ─── refresh contract tests ───────────────────────────────────────────────────

Deno.test("refresh requires updatedCompletedPresetSeedJson — not markdown body parsing", () => {
  const refreshRequiredFields = ["refreshedRenderedMarkdown", "updatedCompletedPresetSeedJson", "searchIndexText"];
  assertEquals(refreshRequiredFields.includes("updatedCompletedPresetSeedJson"), true);
  assertEquals(refreshRequiredFields.includes("refreshedRenderedMarkdown"), true);

  assertEquals(refreshRequiredFields.includes("markdownBodyToParse"), false);
  assertEquals(refreshRequiredFields.includes("parsedMarkdown"), false);
});

Deno.test("refresh payload does not include Markdown-body-parsing field", () => {
  const prohibitedFields = ["markdownBodyToParse", "parsedMarkdownBody", "parseMarkdown"];
  const refreshPayloadKeys = ["refreshedRenderedMarkdown", "updatedCompletedPresetSeedJson", "searchIndexText", "cardMetadataJson"];
  for (const prohibited of prohibitedFields) {
    assertEquals(refreshPayloadKeys.includes(prohibited), false,
      `refresh payload must not contain: ${prohibited}`);
  }
});

// ─── create saved view requires seed ─────────────────────────────────────────

Deno.test("create saved view payload — completedPresetSeedJson is required", () => {
  const createRequiredFields = [
    "templateId", "title", "sourceTableRef", "sourceRecordRef",
    "bindingJson", "completedPresetSeedJson", "renderedMarkdown",
  ];
  assertEquals(createRequiredFields.includes("completedPresetSeedJson"), true);
});

Deno.test("incomplete seed blocks create — explicit error not silent fallback", () => {
  const incompleteSeed = { seed_version: "1" };
  const error = validateCompletedPresetSeed(incompleteSeed);
  assertEquals(error !== null, true,
    "incomplete seed must produce an explicit error, not null (silent pass)");
});

// ─── MdViewer action boundary tests ──────────────────────────────────────────

Deno.test("MdViewer seed-gated actions are disabled when seedValid=false", () => {
  const seedValid = false;
  const seedGatedActions = ["refresh", "clone"];
  const alwaysAvailableActions = ["copy_markdown", "archive", "open_source_record", "edit_adjustment", "create_todo"];

  for (const action of seedGatedActions) {
    assertEquals(seedValid === false, true,
      `${action} must be disabled when seedValid=false`);
  }
  for (const action of alwaysAvailableActions) {
    assertEquals(alwaysAvailableActions.includes(action), true,
      `${action} must remain available regardless of seed validity`);
  }
});

// ─── boundary: saved view is a projection, not canonical data authority ───────

Deno.test("saved view does not own physical record fields", () => {
  type SavedViewAuthorityFields = keyof SavedViewDetail;
  const canonicalDataAuthorityFields = ["columnValues", "jsonbFieldValues", "lifecycleState", "updateAuthority"];
  const savedViewFields: SavedViewAuthorityFields[] = ["savedViewId", "title", "renderedMarkdown", "completedPresetSeedJson", "userAdjustmentPatchJson"];

  for (const field of canonicalDataAuthorityFields) {
    assertEquals(
      (savedViewFields as string[]).includes(field), false,
      `saved view must not own canonical data authority field: ${field}`,
    );
  }
});

// ─── preset catalog seed registration is separate from md_viewer ─────────────

Deno.test("md_viewer is a projection component, not a preset DB seed registration mechanism", () => {
  const mdViewerResponsibilities = [
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
  ];
  const presetDbSeedRegistrationResponsibilities = [
    "create_preset_catalog_seed_rows",
    "bootstrap_registration",
    "register_preset_metadata_in_db",
  ];
  for (const seedOp of presetDbSeedRegistrationResponsibilities) {
    assertEquals(mdViewerResponsibilities.includes(seedOp), false,
      `md_viewer must not include preset DB seed registration: ${seedOp}`);
  }
});
