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
  return sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;
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
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code ?? "DISPATCH_FAILED";
    const msg = result.errors?.[0]?.message ?? result.errors?.[0]?.Message ?? "dispatch failed";
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

export type CompletedPresetSeed = {
  seed_version: string;
  template_ref: Record<string, unknown>;
  source_ref: Record<string, unknown>;
  binding_ref: Record<string, unknown>;
  render_ref: { rendered_markdown_hash: string; rendered_at: string; renderer_version: string; unresolved_placeholder_keys?: string[] };
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
    payload: { templateKey, templateLabel, templateMarkdown, placeholderSchemaJson },
  }) as Promise<{ ok: boolean; templateId?: string; templateKey?: string }>;
}

export async function listTemplates(status = "active"): Promise<{ ok: boolean; templates: TemplateListItem[] }> {
  return dispatchTeamMarkdown("template:list", { payload: { status } }) as Promise<{ ok: boolean; templates: TemplateListItem[] }>;
}

export async function getTemplate(templateId: string): Promise<{ ok: boolean; template: TemplateDetail }> {
  return dispatchTeamMarkdown("template:get", { idOrHubId: templateId }) as Promise<{ ok: boolean; template: TemplateDetail }>;
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

export async function archiveTemplate(templateId: string): Promise<{ ok: boolean; templateId: string }> {
  return dispatchTeamMarkdown("template:archive", { idOrHubId: templateId }) as Promise<{ ok: boolean; templateId: string }>;
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
  return dispatchTeamMarkdown("saved_view:create", {
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
  }) as Promise<{ ok: boolean; savedViewId?: string }>;
}

export async function searchSavedViews(params: {
  query?: string;
  status?: string;
  limit?: number;
} = {}): Promise<{ ok: boolean; savedViews: SavedViewCard[] }> {
  return dispatchTeamMarkdown("saved_view:search", {
    payload: {
      query: params.query,
      status: params.status ?? "active",
      limit: params.limit ?? 50,
    },
  }) as Promise<{ ok: boolean; savedViews: SavedViewCard[] }>;
}

export async function getSavedView(savedViewId: string): Promise<{
  ok: boolean;
  savedView: SavedViewDetail;
  seedValid: boolean;
  seedError?: string;
}> {
  return dispatchTeamMarkdown("saved_view:get", { idOrHubId: savedViewId }) as Promise<{
    ok: boolean;
    savedView: SavedViewDetail;
    seedValid: boolean;
    seedError?: string;
  }>;
}

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
    },
  }) as Promise<{ ok: boolean; savedViewId: string }>;
}

export async function updateSavedView(
  savedViewId: string,
  updates: {
    title?: string;
    renderedMarkdown?: string;
    userAdjustmentPatchJson?: Record<string, unknown>;
    completedPresetSeedJson?: CompletedPresetSeed;
    searchIndexText?: string;
    cardMetadataJson?: Record<string, unknown>;
  },
): Promise<{ ok: boolean; savedViewId: string }> {
  return dispatchTeamMarkdown("saved_view:update", {
    idOrHubId: savedViewId,
    payload: updates,
  }) as Promise<{ ok: boolean; savedViewId: string }>;
}

export async function archiveSavedView(savedViewId: string): Promise<{ ok: boolean; savedViewId: string }> {
  return dispatchTeamMarkdown("saved_view:archive", { idOrHubId: savedViewId }) as Promise<{ ok: boolean; savedViewId: string }>;
}
