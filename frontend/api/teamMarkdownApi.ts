/**
 * teamMarkdownApi.ts — Frontend API for Team Markdown Dashboard Saved View.
 * SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
 *
 * All operations go through AdminRuntime via admin dispatch.
 * Frontend does NOT write to topology.team_markdown_* directly.
 *
 * Authority invariants:
 *   - completed_preset_seed_json is required for saved view create/refresh/clone/projection
 *   - Markdown body is NOT runtime SSOT; refresh uses seed binding_json
 *   - active topology is NOT mutated by saved view operations
 */

import type { DispatchRequest } from "./dispatch.ts";
import { queueAdminClientCommand } from "../runtime/frontendScheduler.ts";
import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";

function getToken(): string | undefined {
  if (typeof globalThis.sessionStorage === "undefined") return undefined;
  const token = sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;
  if (!token) {
    console.warn(
      "[teamMarkdownApi] session token not found; admin dispatch may be rejected by backend auth",
    );
  }
  return token;
}

async function dispatchTeamMarkdown(
  action: string,
  options: { payload?: Record<string, unknown>; idOrHubId?: string } = {},
): Promise<unknown> {
  const request: Omit<DispatchRequest, "triggerKind"> = {
    operationType: "admin",
    target: "admin",
    layer: "team_markdown",
    action,
    payload: options.payload,
    idOrHubId: options.idOrHubId,
  };
  const result = await queueAdminClientCommand(request, getToken());
  if (!result.success) {
    // Backend uses camelCase (JsonNamingPolicy.CamelCase in Program.cs)
    const code = result.errors?.[0]?.code ?? "DISPATCH_FAILED";
    const msg = result.errors?.[0]?.message ?? "dispatch failed";
    throw new Error(`[${code}] ${msg}`);
  }
  if (!result.emission) throw new Error("dispatch: no emission in response");
  return result.emission.data;
}

// ─── types ────────────────────────────────────────────────────────────────────

export type TemplateListItem = {
  templateId: string;
  templateKey: string;
  templateLabel: string;
  status: string;
  createdAt: string;
  updatedAt: string;
};

export type TemplateDetail = TemplateListItem & {
  templateMarkdown: string;
  placeholderSchemaJson: Record<string, unknown>;
};

export type SavedViewCard = {
  savedViewId: string;
  title: string;
  templateKey: string;
  sourceTableRef: string;
  sourceRecordRef: string;
  status: string;
  updatedAt: string;
  cardMetadataJson: Record<string, unknown>;
};

export type SavedViewDetail = SavedViewCard & {
  templateId: string;
  bindingJson: Record<string, unknown>;
  completedPresetSeedJson: CompletedPresetSeed;
  renderedMarkdown: string;
  userAdjustmentPatchJson: Record<string, unknown>;
  searchIndexText: string;
  createdAt: string;
};

export type PlaceholderBindingKind =
  | "source_field"
  | "jsonb_path"
  | "saved_query_field"
  | "static_text";

export type PlaceholderBinding = {
  placeholderKey: string;
  required: boolean;
  bindingKind?: PlaceholderBindingKind;
  sourceFieldRef?: string;
  jsonbPath?: string;
  savedQueryFieldRef?: string;
  staticText?: string;
  previewValue?: string;
};

export type PlaceholderExtraction = {
  placeholderKeys: string[];
  requiredPlaceholderKeys: string[];
  optionalPlaceholderKeys: string[];
};

export type MdTranslationSeedCandidateInput = {
  template: {
    templateId: string;
    templateKey: string;
    templateLabel?: string;
    templateMarkdown: string;
    placeholderSchemaJson?: Record<string, unknown>;
  };
  source: {
    sourceTableRef: string;
    sourceRecordRef: string;
    sourceKind?: string;
  };
  bindings: PlaceholderBinding[];
  title: string;
  adjustment?: {
    userAdjustmentPatchJson?: Record<string, unknown>;
    adjustmentMode?: string;
  };
  dashboard?: {
    tags?: string[];
    excerpt?: string;
    cardMetadataJson?: Record<string, unknown>;
  };
  lineage?: Record<string, unknown>;
};

export type MdTranslationSeedCandidate = {
  bindingJson: Record<string, unknown>;
  completedPresetSeedJson: CompletedPresetSeed;
  renderedMarkdown: string;
  searchIndexText: string;
  cardMetadataJson: Record<string, unknown>;
  userAdjustmentPatchJson: Record<string, unknown>;
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function validateSavedViewSearchResponse(
  data: unknown,
): { ok: boolean; savedViews: SavedViewCard[] } {
  if (!isRecord(data) || !Array.isArray(data.savedViews)) {
    throw new Error(
      "[TEAM_MARKDOWN_SEARCH_RESPONSE_INVALID] saved_view:search response must be an object with savedViews array",
    );
  }
  return data as { ok: boolean; savedViews: SavedViewCard[] };
}

function validateCreateSavedViewResponse(
  data: unknown,
): { ok: boolean; savedViewId?: string } {
  if (
    !isRecord(data) || data.ok !== true ||
    typeof data.savedViewId !== "string" || !data.savedViewId
  ) {
    throw new Error(
      "[TEAM_MARKDOWN_CREATE_RESPONSE_INVALID] saved_view:create response must include ok=true and savedViewId",
    );
  }
  return data as { ok: boolean; savedViewId: string };
}

function hashRenderedMarkdown(markdown: string): string {
  let h = 2166136261;
  for (let i = 0; i < markdown.length; i++) {
    h ^= markdown.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return `fnv1a:${(h >>> 0).toString(16).padStart(8, "0")}`;
}

function readStringArray(value: unknown): string[] {
  if (!Array.isArray(value)) return [];
  return value.filter((item): item is string =>
    typeof item === "string" && item.trim().length > 0
  );
}

export function extractMarkdownPlaceholders(
  templateMarkdown: string,
  placeholderSchemaJson: Record<string, unknown> = {},
): PlaceholderExtraction {
  const found = new Set<string>();
  const pattern = /{{\s*([A-Za-z0-9_.-]+)\s*}}/g;
  for (const match of templateMarkdown.matchAll(pattern)) {
    found.add(match[1]);
  }
  const placeholderKeys = [...found].sort();
  const explicitRequired = new Set(
    readStringArray(
      placeholderSchemaJson.requiredPlaceholderKeys ??
        placeholderSchemaJson.required,
    ),
  );
  const explicitOptional = new Set(
    readStringArray(
      placeholderSchemaJson.optionalPlaceholderKeys ??
        placeholderSchemaJson.optional,
    ),
  );
  const requiredPlaceholderKeys = placeholderKeys.filter((key) =>
    explicitRequired.size > 0
      ? explicitRequired.has(key)
      : !explicitOptional.has(key)
  );
  const optionalPlaceholderKeys = placeholderKeys.filter((key) =>
    explicitOptional.has(key) && !requiredPlaceholderKeys.includes(key)
  );
  return { placeholderKeys, requiredPlaceholderKeys, optionalPlaceholderKeys };
}

export function buildPlaceholderSchemaFromMarkdown(
  templateMarkdown: string,
  optionalPlaceholderKeys: string[] = [],
): Record<string, unknown> {
  const extracted = extractMarkdownPlaceholders(templateMarkdown, {
    optionalPlaceholderKeys,
  });
  return {
    extraction: "handlebars_like_double_curly",
    placeholderKeys: extracted.placeholderKeys,
    requiredPlaceholderKeys: extracted.requiredPlaceholderKeys,
    optionalPlaceholderKeys: extracted.optionalPlaceholderKeys,
    binding_resolution: "user_explicit_selection_only_no_ai_inference",
  };
}

function bindingHasExplicitSource(binding: PlaceholderBinding): boolean {
  if (!binding.bindingKind) return false;
  if (binding.bindingKind === "static_text") {
    return binding.staticText !== undefined ||
      binding.previewValue !== undefined;
  }
  if (binding.bindingKind === "source_field") return !!binding.sourceFieldRef;
  if (binding.bindingKind === "jsonb_path") return !!binding.jsonbPath;
  if (binding.bindingKind === "saved_query_field") {
    return !!binding.savedQueryFieldRef;
  }
  return false;
}

function bindingPreviewValue(binding: PlaceholderBinding): string {
  if (typeof binding.previewValue === "string") return binding.previewValue;
  if (
    binding.bindingKind === "static_text" &&
    typeof binding.staticText === "string"
  ) return binding.staticText;
  return "";
}

export function buildMdTranslationAuthoringSeedCandidate(
  input: MdTranslationSeedCandidateInput,
): MdTranslationSeedCandidate {
  const extracted = extractMarkdownPlaceholders(
    input.template.templateMarkdown,
    input.template.placeholderSchemaJson ?? {},
  );
  const byKey = new Map(
    input.bindings.map((binding) => [binding.placeholderKey, binding]),
  );
  const unresolvedRequired = extracted.requiredPlaceholderKeys.filter((key) =>
    !bindingHasExplicitSource(
      byKey.get(key) ?? { placeholderKey: key, required: true },
    )
  );
  if (unresolvedRequired.length > 0) {
    throw new Error(
      `[REQUIRED_PLACEHOLDER_UNBOUND] unresolved required placeholders: ${
        unresolvedRequired.join(", ")
      }`,
    );
  }

  const optionalEmptyKeys = extracted.optionalPlaceholderKeys.filter((key) =>
    !bindingHasExplicitSource(
      byKey.get(key) ?? { placeholderKey: key, required: false },
    )
  );
  const renderedMarkdown = input.template.templateMarkdown.replace(
    /{{\s*([A-Za-z0-9_.-]+)\s*}}/g,
    (_match, key: string) => {
      const binding = byKey.get(key);
      if (
        !bindingHasExplicitSource(
          binding ?? { placeholderKey: key, required: false },
        )
      ) return "";
      return bindingPreviewValue(binding!);
    },
  );

  const bindingEntries = extracted.placeholderKeys.map((key) => {
    const binding = byKey.get(key);
    const required = extracted.requiredPlaceholderKeys.includes(key);
    return {
      placeholder_key: key,
      required,
      binding_kind: binding?.bindingKind ?? null,
      source_field_ref: binding?.sourceFieldRef ?? null,
      jsonb_path: binding?.jsonbPath ?? null,
      saved_query_field_ref: binding?.savedQueryFieldRef ?? null,
      static_text: binding?.bindingKind === "static_text"
        ? binding.staticText ?? binding.previewValue ?? ""
        : null,
      preview_value_present:
        bindingPreviewValue(binding ?? { placeholderKey: key, required })
          .length > 0,
      empty_state: optionalEmptyKeys.includes(key)
        ? "explicit_optional_empty"
        : null,
    };
  });

  const placeholderToFieldMap = Object.fromEntries(
    bindingEntries.map((entry) => [
      entry.placeholder_key,
      entry.source_field_ref ?? entry.jsonb_path ?? entry.saved_query_field_ref ??
        entry.static_text ?? "",
    ]),
  );

  const bindingJson = {
    binding_version: "md_translation_authoring_seed_registration.v1",
    resolution_mode: "user_explicit_selection_only_no_ai_inference",
    placeholder_bindings: bindingEntries,
    required_placeholder_keys: extracted.requiredPlaceholderKeys,
    optional_placeholder_keys: extracted.optionalPlaceholderKeys,
    optional_empty_placeholder_keys: optionalEmptyKeys,
    unresolved_required_placeholder_keys: unresolvedRequired,
  };
  const userAdjustmentPatchJson = input.adjustment?.userAdjustmentPatchJson ??
    {};
  const cardMetadataJson = {
    ...(input.dashboard?.cardMetadataJson ?? {}),
    excerpt: input.dashboard?.excerpt ?? renderedMarkdown.slice(0, 160),
    tags: input.dashboard?.tags ?? [],
    seed_authoring_bundle: "md_translation_authoring_seed_registration",
  };
  const renderedHash = hashRenderedMarkdown(renderedMarkdown);
  const completedPresetSeedJson: CompletedPresetSeed = {
    seed_version: "md_translation_authoring_seed_registration.v1",
    template_ref: {
      template_id: input.template.templateId,
      template_key: input.template.templateKey,
      template_label: input.template.templateLabel ??
        input.template.templateKey,
      placeholder_schema_json: input.template.placeholderSchemaJson ?? {},
      markdown_body_role: "template_seed_metadata_not_runtime_ssot",
    },
    source_ref: {
      source_kind: input.source.sourceKind ?? "physical_table_record",
      source_table_ref: input.source.sourceTableRef,
      source_record_ref: input.source.sourceRecordRef,
    },
    binding_ref: {
      binding_json: bindingJson,
      placeholder_to_field_map: placeholderToFieldMap,
      required_placeholder_keys: extracted.requiredPlaceholderKeys,
      optional_placeholder_keys: extracted.optionalPlaceholderKeys,
      explicit_optional_empty_placeholder_keys: optionalEmptyKeys,
      unresolved_required_placeholder_keys: unresolvedRequired,
    },
    render_ref: {
      rendered_markdown_hash: renderedHash,
      rendered_at: new Date().toISOString(),
      renderer_version: "client.seed_candidate.preview.v1",
      unresolved_placeholder_keys: unresolvedRequired,
    },
    adjustment_ref: {
      adjustment_mode: input.adjustment?.adjustmentMode ?? "none",
      user_adjustment_patch_json: userAdjustmentPatchJson,
    },
    dashboard_ref: {
      title: input.title,
      excerpt: String(cardMetadataJson.excerpt ?? ""),
      tags: input.dashboard?.tags ?? [],
      card_metadata_json: cardMetadataJson,
      search_index_basis_json: {
        fields: [
          "title",
          "source_table_ref",
          "source_record_ref",
          "explicit_binding_preview_values",
        ],
      },
    },
    lineage_ref: {
      created_from: "md_translation_authoring_seed_registration",
      parent_saved_view_id: null,
      markdown_body_reverse_engineered: false,
      ...(input.lineage ?? {}),
    },
  };

  return {
    bindingJson,
    completedPresetSeedJson,
    renderedMarkdown,
    searchIndexText: [
      input.title,
      input.source.sourceTableRef,
      input.source.sourceRecordRef,
      renderedMarkdown,
    ].join(" "),
    cardMetadataJson,
    userAdjustmentPatchJson,
  };
}

export type CompletedPresetSeed = {
  seed_version: string;
  template_ref: Record<string, unknown>;
  source_ref: Record<string, unknown>;
  binding_ref: Record<string, unknown>;
  render_ref: {
    rendered_markdown_hash: string;
    rendered_at: string;
    renderer_version: string;
    unresolved_placeholder_keys?: string[];
  };
  adjustment_ref: Record<string, unknown>;
  dashboard_ref: Record<string, unknown>;
  lineage_ref: Record<string, unknown>;
};

// ─── template actions ─────────────────────────────────────────────────────────

export async function createTemplate(
  templateKey: string,
  templateLabel: string,
  templateMarkdown: string,
  placeholderSchemaJson: Record<string, unknown> = {},
): Promise<{ ok: boolean; templateId?: string; templateKey?: string }> {
  return dispatchTeamMarkdown("template:create", {
    payload: {
      templateKey,
      templateLabel,
      templateMarkdown,
      placeholderSchemaJson,
    },
  }) as Promise<{ ok: boolean; templateId?: string; templateKey?: string }>;
}

export async function listTemplates(
  status = "active",
): Promise<{ ok: boolean; templates: TemplateListItem[] }> {
  return dispatchTeamMarkdown("template:list", {
    payload: { status },
  }) as Promise<{ ok: boolean; templates: TemplateListItem[] }>;
}

export async function getTemplate(
  templateId: string,
): Promise<{ ok: boolean; template: TemplateDetail }> {
  return dispatchTeamMarkdown("template:get", {
    idOrHubId: templateId,
  }) as Promise<{ ok: boolean; template: TemplateDetail }>;
}

export async function updateTemplate(
  templateId: string,
  templateLabel: string,
  templateMarkdown: string,
  status = "active",
): Promise<{ ok: boolean; templateId: string }> {
  return dispatchTeamMarkdown("template:update", {
    idOrHubId: templateId,
    payload: { templateLabel, templateMarkdown, status },
  }) as Promise<{ ok: boolean; templateId: string }>;
}

export async function archiveTemplate(
  templateId: string,
): Promise<{ ok: boolean; templateId: string }> {
  return dispatchTeamMarkdown("template:archive", {
    idOrHubId: templateId,
  }) as Promise<{ ok: boolean; templateId: string }>;
}

// ─── saved view actions ───────────────────────────────────────────────────────

export async function createSavedView(params: {
  templateId: string;
  title: string;
  sourceTableRef: string;
  sourceRecordRef: string;
  bindingJson: Record<string, unknown>;
  completedPresetSeedJson: CompletedPresetSeed;
  renderedMarkdown: string;
  userAdjustmentPatchJson?: Record<string, unknown>;
  searchIndexText?: string;
  cardMetadataJson?: Record<string, unknown>;
}): Promise<{ ok: boolean; savedViewId?: string }> {
  const data = await dispatchTeamMarkdown("saved_view:create", {
    payload: {
      templateId: params.templateId,
      title: params.title,
      sourceTableRef: params.sourceTableRef,
      sourceRecordRef: params.sourceRecordRef,
      bindingJson: params.bindingJson,
      completedPresetSeedJson: params.completedPresetSeedJson,
      renderedMarkdown: params.renderedMarkdown,
      userAdjustmentPatchJson: params.userAdjustmentPatchJson ?? {},
      searchIndexText: params.searchIndexText ?? "",
      cardMetadataJson: params.cardMetadataJson ?? {},
    },
  });
  return validateCreateSavedViewResponse(data);
}

export async function searchSavedViews(params: {
  query?: string;
  status?: string;
  limit?: number;
} = {}): Promise<{ ok: boolean; savedViews: SavedViewCard[] }> {
  const data = await dispatchTeamMarkdown("saved_view:search", {
    payload: {
      query: params.query,
      status: params.status ?? "active",
      limit: params.limit ?? 50,
    },
  });
  return validateSavedViewSearchResponse(data);
}

export async function getSavedView(savedViewId: string): Promise<{
  ok: boolean;
  savedView: SavedViewDetail;
  seedValid: boolean;
  seedError?: string;
}> {
  return dispatchTeamMarkdown("saved_view:get", {
    idOrHubId: savedViewId,
  }) as Promise<{
    ok: boolean;
    savedView: SavedViewDetail;
    seedValid: boolean;
    seedError?: string;
  }>;
}

/**
 * Refresh/Clone/Rebind write calls must only ever be invoked after an explicit user confirmation
 * step (ApplyConfirmDialog) — see SavedViewOperationPanel.tsx, the only caller. confirmed:true is
 * sent unconditionally here, not because the frontend display implies confirmation, but because
 * the backend independently re-checks payload.confirmed=true and rejects any write missing it
 * regardless of what this function sends — the frontend gate is defense-in-depth, not authority.
 */
export async function refreshSavedView(
  savedViewId: string,
  refreshedRenderedMarkdown: string,
  updatedCompletedPresetSeedJson: CompletedPresetSeed,
  searchIndexText: string,
  cardMetadataJson?: Record<string, unknown>,
): Promise<{ ok: boolean; savedViewId: string }> {
  return dispatchTeamMarkdown("saved_view:refresh", {
    idOrHubId: savedViewId,
    payload: {
      refreshedRenderedMarkdown,
      updatedCompletedPresetSeedJson,
      searchIndexText,
      cardMetadataJson,
      confirmed: true,
    },
  }) as Promise<{ ok: boolean; savedViewId: string }>;
}

/**
 * Seed-driven refresh: re-renders from the current template + a freshly-supplied source record,
 * re-deriving completedPresetSeedJson server-side (AdminRuntime.TeamMarkdown's
 * templateMarkdown+sourceRecordJson branch) rather than requiring the caller to pre-render.
 */
export async function refreshSavedViewFromSource(
  savedViewId: string,
  templateMarkdown: string,
  sourceRecordJson: Record<string, unknown>,
  searchIndexText?: string,
  cardMetadataJson?: Record<string, unknown>,
): Promise<{ ok: boolean; savedViewId: string }> {
  return dispatchTeamMarkdown("saved_view:refresh", {
    idOrHubId: savedViewId,
    payload: { templateMarkdown, sourceRecordJson, searchIndexText, cardMetadataJson, confirmed: true },
  }) as Promise<{ ok: boolean; savedViewId: string }>;
}

export type SavedViewUpdateInput = {
  title?: string;
  renderedMarkdown?: string;
  userAdjustmentPatchJson?: Record<string, unknown>;
  completedPresetSeedJson?: CompletedPresetSeed;
  searchIndexText?: string;
  cardMetadataJson?: Record<string, unknown>;
};

/**
 * Non-mutating preview/validate step of the authoring workflow (preview -> validate ->
 * explicit_confirm -> write -> diff_log). Backend runs the identical validation it runs on the
 * real write and returns without touching the DB (payload.dryRun=true short-circuits before the
 * repository call). Admin-only: backend rejects non-admin JWTs before this returns.
 */
export async function previewSavedViewUpdate(
  savedViewId: string,
  updates: SavedViewUpdateInput,
): Promise<{ ok: boolean; dryRun: true; valid: boolean; savedViewId: string; preview: { title: string; renderedMarkdown: string } }> {
  return dispatchTeamMarkdown("saved_view:update", {
    idOrHubId: savedViewId,
    payload: { ...updates, dryRun: true },
  }) as Promise<{ ok: boolean; dryRun: true; valid: boolean; savedViewId: string; preview: { title: string; renderedMarkdown: string } }>;
}

/**
 * Write step — only proceeds after an explicit user confirmation (confirmed:true). Must be called
 * with the exact same `updates` object that was just previewed/validated; the backend re-validates
 * this input again server-side regardless, and persists the mutation plus its diff-evidence event
 * atomically in one transaction (never a best-effort side call).
 */
export async function writeSavedViewUpdate(
  savedViewId: string,
  updates: SavedViewUpdateInput,
): Promise<{ ok: boolean; savedViewId: string; diffEventId: string | null }> {
  return dispatchTeamMarkdown("saved_view:update", {
    idOrHubId: savedViewId,
    payload: { ...updates, confirmed: true },
  }) as Promise<{ ok: boolean; savedViewId: string; diffEventId: string | null }>;
}

/** @deprecated Use previewSavedViewUpdate (dry run) then writeSavedViewUpdate (confirmed write). */
export async function updateSavedView(
  savedViewId: string,
  updates: SavedViewUpdateInput,
): Promise<{ ok: boolean; savedViewId: string }> {
  return writeSavedViewUpdate(savedViewId, updates);
}

export async function archiveSavedView(
  savedViewId: string,
): Promise<{ ok: boolean; savedViewId: string }> {
  return dispatchTeamMarkdown("saved_view:archive", {
    idOrHubId: savedViewId,
  }) as Promise<{ ok: boolean; savedViewId: string }>;
}


export async function cloneSavedView(
  savedViewId: string,
  params: {
    targetSourceTableRef: string;
    targetSourceRecordRef: string;
    templateMarkdown: string;
    sourceRecordJson: Record<string, unknown>;
    title?: string;
    searchIndexText?: string;
    cardMetadataJson?: Record<string, unknown>;
  },
): Promise<{ ok: boolean; savedViewId: string; sourceSavedViewId: string }> {
  return dispatchTeamMarkdown("saved_view:clone", {
    idOrHubId: savedViewId,
    payload: { ...params, confirmed: true },
  }) as Promise<{ ok: boolean; savedViewId: string; sourceSavedViewId: string }>;
}

export async function rebindSavedView(
  savedViewId: string,
  params: {
    bindingJson: Record<string, unknown>;
    completedPresetSeedJson: CompletedPresetSeed;
    templateMarkdown: string;
    sourceRecordJson: Record<string, unknown>;
    searchIndexText?: string;
    cardMetadataJson?: Record<string, unknown>;
  },
): Promise<{ ok: boolean; savedViewId: string }> {
  return dispatchTeamMarkdown("saved_view:rebind", {
    idOrHubId: savedViewId,
    payload: { ...params, confirmed: true },
  }) as Promise<{ ok: boolean; savedViewId: string }>;
}
