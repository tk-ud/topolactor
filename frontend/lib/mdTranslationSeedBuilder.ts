/**
 * mdTranslationSeedBuilder.ts — Client-side seed builder for md translation authoring surface.
 * SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml §completed_preset_seed_contract
 *
 * Builds a completed_preset_seed_json candidate from template, source ref, and binding map.
 * Does NOT parse or infer bindings from Markdown body text.
 * Required placeholder keys that are unresolved block create/update.
 * Optional placeholder keys with no binding persist as explicit_optional_empty in seed metadata.
 */

import type { CompletedPresetSeed } from "../api/teamMarkdownApi.ts";

export type BindingSourceKind =
  | "physical_table_column"
  | "physical_table_jsonb_path"
  | "saved_query_result_field"
  | "static_text"
  | "explicit_optional_empty"
  | "";

export type PlaceholderBindingEntry = {
  placeholderKey: string;
  sourceKind: BindingSourceKind;
  fieldRef: string;
  required: boolean;
};

export type MdTranslationAuthoringSeedCandidateParams = {
  templateId: string;
  templateKey: string;
  templateVersion?: string;
  sourceTableRef: string;
  sourceRecordRef: string;
  sourceRecordLabel?: string;
  bindingEntries: PlaceholderBindingEntry[];
  renderedMarkdown: string;
  renderedMarkdownHash: string;
  rendererVersion?: string;
  title: string;
  excerpt: string;
  tags?: string[];
  createdBy?: string;
};

export type SeedCandidateResult = {
  candidate: CompletedPresetSeed;
  unresolvedRequiredKeys: string[];
};

/**
 * Builds a completed_preset_seed_json candidate for a new or updated saved Markdown view.
 * Returns the candidate and unresolved required placeholder keys (non-empty blocks save).
 *
 * Binding authority: all bindings are user-selected — no AI inference, no Markdown body parsing.
 * Optional placeholder with no binding is recorded as explicit_optional_empty in seed metadata.
 */
export function buildMdTranslationAuthoringSeedCandidate(
  params: MdTranslationAuthoringSeedCandidateParams,
): SeedCandidateResult {
  const unresolvedRequiredKeys: string[] = [];
  const bindingJson: Record<string, unknown> = {};
  const placeholderToFieldMap: Record<string, unknown> = {};
  const requiredKeys: string[] = [];
  const optionalKeys: string[] = [];

  for (const entry of params.bindingEntries) {
    const key = entry.placeholderKey;
    const hasBinding = Boolean(entry.sourceKind && entry.fieldRef.trim());

    if (entry.required) {
      requiredKeys.push(key);
      if (!hasBinding) {
        unresolvedRequiredKeys.push(key);
      }
    } else {
      optionalKeys.push(key);
    }

    if (hasBinding) {
      bindingJson[key] = {
        source_kind: entry.sourceKind,
        field_ref: entry.fieldRef.trim(),
      };
      placeholderToFieldMap[key] = entry.fieldRef.trim();
    } else if (!entry.required) {
      // explicit_optional_empty: optional placeholder with no binding persists as empty-state metadata
      bindingJson[key] = { source_kind: "explicit_optional_empty", field_ref: "" };
    }
  }

  const candidate: CompletedPresetSeed = {
    seed_version: "1",
    template_ref: {
      template_id: params.templateId,
      template_key: params.templateKey,
      template_version: params.templateVersion ?? null,
    },
    source_ref: {
      source_table_ref: params.sourceTableRef,
      source_record_ref: params.sourceRecordRef,
      source_record_label: params.sourceRecordLabel ?? null,
    },
    binding_ref: {
      binding_json: bindingJson,
      placeholder_to_field_map: placeholderToFieldMap,
      required_placeholder_keys: requiredKeys,
      optional_placeholder_keys: optionalKeys,
      explicit_optional_empty_placeholder_keys: optionalKeys.filter((key) =>
        (bindingJson[key] as { source_kind?: string } | undefined)?.source_kind ===
          "explicit_optional_empty"
      ),
      unresolved_required_placeholder_keys: unresolvedRequiredKeys,
    },
    render_ref: {
      rendered_markdown_hash: params.renderedMarkdownHash,
      rendered_at: new Date().toISOString(),
      renderer_version: params.rendererVersion ?? "1.0",
      unresolved_placeholder_keys: unresolvedRequiredKeys,
    },
    adjustment_ref: {
      user_adjustment_patch_json: {},
      adjustment_mode: "none",
    },
    dashboard_ref: {
      title: params.title,
      excerpt: params.excerpt,
      tags: params.tags ?? [],
      card_metadata_json: {
        title: params.title,
        excerpt: params.excerpt,
        tags: params.tags ?? [],
      },
      search_index_basis_json: {
        title: params.title,
        excerpt: params.excerpt,
      },
    },
    lineage_ref: {
      created_from: "template_record",
      parent_saved_view_id: null,
      created_by: params.createdBy ?? null,
    },
  };

  return { candidate, unresolvedRequiredKeys };
}

/**
 * Extracts placeholder keys from a template's placeholderSchemaJson.
 * Returns an array of placeholder keys (e.g. "record.summary").
 * Supports { placeholders: string[] } and { properties: Record<string, unknown> } shapes.
 */
export function extractPlaceholderKeys(
  placeholderSchemaJson: Record<string, unknown>,
): string[] {
  if (Array.isArray(placeholderSchemaJson.placeholders)) {
    return (placeholderSchemaJson.placeholders as unknown[]).filter(
      (k): k is string => typeof k === "string",
    );
  }
  if (
    placeholderSchemaJson.properties &&
    typeof placeholderSchemaJson.properties === "object" &&
    !Array.isArray(placeholderSchemaJson.properties)
  ) {
    return Object.keys(
      placeholderSchemaJson.properties as Record<string, unknown>,
    );
  }
  return [];
}

/** Simple hash for a string — used for rendered_markdown_hash when no server hash available. */
export function simpleContentHash(content: string): string {
  let h = 0;
  for (let i = 0; i < content.length; i++) {
    const code = content.charCodeAt(i);
    h = ((h << 5) - h + code) | 0;
  }
  return Math.abs(h).toString(16).padStart(8, "0");
}
