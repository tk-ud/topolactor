/**
 * TeamMarkdownDashboard — team shared saved Markdown view dashboard island.
 * SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml §dashboard_surface_contract
 *
 * Responsibilities:
 *   - Search input for saved Markdown views (title, search_index_text, source_table_ref)
 *   - Saved view result cards with click-to-expand drawer/panel
 *   - Display rendered Markdown in expanded view via MdViewer component
 *   - Show preset seed summary and binding summary in expanded view
 *   - Seed-driven md translation authoring surface using existing component bucket parts
 *
 * Prohibited (per SSOT):
 *   - Direct DB write from frontend (all mutations via backend admin action)
 *   - Markdown body as runtime SSOT
 *   - AI inference for binding
 *   - Silent fallback when seed is missing
 *   - Mutation of active topology
 *   - Search mutating saved views
 */

import { useCallback, useEffect, useState } from "preact/hooks";
import {
  archiveSavedView,
  archiveTemplate,
  buildMdTranslationAuthoringSeedCandidate,
  buildPlaceholderSchemaFromMarkdown,
  createSavedView,
  createTemplate,
  extractMarkdownPlaceholders,
  getSavedView,
  getTemplate,
  listTemplates,
  refreshSavedView,
  cloneSavedView,
  rebindSavedView,
  type PlaceholderBinding,
  type SavedViewCard,
  type SavedViewDetail,
  searchSavedViews,
  type TemplateListItem,
  updateTemplate,
  viewerGetSavedView,
  viewerSearchSavedViews,
} from "../api/teamMarkdownApi.ts";
import { MdViewer } from "../components/MdViewer.tsx";
import { SavedViewAdjustmentAuthoringPanel } from "../components/SavedViewAdjustmentAuthoringPanel.tsx";
import { SavedViewOperationPanel, type SavedViewOperationMode } from "../components/SavedViewOperationPanel.tsx";

// ─── search card component ────────────────────────────────────────────────────

function SavedViewResultCard({
  card,
  onExpand,
}: {
  card: SavedViewCard;
  onExpand: (savedViewId: string) => void;
}) {
  const metadata = card.cardMetadataJson as Record<string, unknown>;
  const excerpt = String(metadata.excerpt ?? "");
  const tags = Array.isArray(metadata.tags)
    ? metadata.tags.filter((tag): tag is string => typeof tag === "string")
    : [];
  return (
    <article
      class="md-dashboard-card"
      onClick={() => onExpand(card.savedViewId)}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") onExpand(card.savedViewId);
      }}
      role="button"
      tabIndex={0}
      aria-label={`Open saved view: ${card.title}`}
    >
      <h3 class="md-dashboard-card-title">{card.title}</h3>
      <div class="md-dashboard-card-meta">
        <span class="md-dashboard-card-template">{card.templateKey}</span>
        <span class="md-dashboard-card-source">{card.sourceTableRef}</span>
        <span class="md-dashboard-card-status">{card.status}</span>
        <span class="md-dashboard-card-updated">{card.updatedAt}</span>
      </div>
      {excerpt && <p class="md-dashboard-card-excerpt">{excerpt}</p>}
      {tags.length > 0 && (
        <div class="md-dashboard-card-tags" aria-label="Saved view tags">
          {tags.map((tag) => <span key={tag} class="md-dashboard-card-tag">#{tag}</span>)}
        </div>
      )}
    </article>
  );
}

// ─── main island ─────────────────────────────────────────────────────────────

type Props = {
  defaultStatus?: string;
  placement?: "admin_route" | "ui_builder_child_surface";
};

const EXPLICIT_PAYLOAD_REQUIRED_REASON =
  "Action requires explicit user-selected template/source payload; Markdown body is not used as binding authority.";

function splitCsv(value: string): string[] {
  return value.split(",").map((item) => item.trim()).filter(Boolean);
}

function MdTranslationAuthoringSeedSurface({
  onCreated,
}: {
  onCreated: () => Promise<void>;
}) {
  const [templates, setTemplates] = useState<TemplateListItem[]>([]);
  const [templateId, setTemplateId] = useState("");
  const [templateKey, setTemplateKey] = useState("daily_note_seed");
  const [templateLabel, setTemplateLabel] = useState("Daily note seed");
  const [templateMarkdown, setTemplateMarkdown] = useState(
    "# {{title}}\n\nOwner: {{owner}}\n\n{{optional_note}}",
  );
  const [optionalKeys, setOptionalKeys] = useState("optional_note");
  const [title, setTitle] = useState("Saved Markdown projection");
  const [sourceTableRef, setSourceTableRef] = useState(
    "topology.example_source",
  );
  const [sourceRecordRef, setSourceRecordRef] = useState("record-1");
  const [bindingDraft, setBindingDraft] = useState<
    Record<string, PlaceholderBinding>
  >({});
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const placeholderSchema = buildPlaceholderSchemaFromMarkdown(
    templateMarkdown,
    splitCsv(optionalKeys),
  );
  const placeholders = extractMarkdownPlaceholders(
    templateMarkdown,
    placeholderSchema,
  );

  const reloadTemplates = useCallback(async () => {
    try {
      const response = await listTemplates("active");
      if (!Array.isArray(response.templates)) {
        throw new Error(
          "[TEAM_MARKDOWN_TEMPLATE_LIST_RESPONSE_INVALID] templates array missing",
        );
      }
      setTemplates(response.templates);
      if (!templateId && response.templates[0]?.templateId) {
        setTemplateId(response.templates[0].templateId);
        setTemplateKey(response.templates[0].templateKey);
        setTemplateLabel(response.templates[0].templateLabel);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Template list failed");
    }
  }, [templateId]);

  useEffect(() => {
    reloadTemplates();
  }, []);

  const upsertBinding = (
    placeholderKey: string,
    patch: Partial<PlaceholderBinding>,
  ) => {
    const required = placeholders.requiredPlaceholderKeys.includes(
      placeholderKey,
    );
    setBindingDraft((current) => ({
      ...current,
      [placeholderKey]: {
        bindingKind: "static_text",
        ...current[placeholderKey],
        ...patch,
        placeholderKey,
        required,
      },
    }));
  };

  const handleRegisterTemplate = async () => {
    setBusy(true);
    setError(null);
    setStatus(null);
    try {
      const created = templateId
        ? await updateTemplate(
          templateId,
          templateLabel,
          templateMarkdown,
          "active",
        )
        : await createTemplate(
          templateKey,
          templateLabel,
          templateMarkdown,
          placeholderSchema,
        );
      if (templateId && created.ok) {
        setStatus(`Template seed metadata updated: ${templateId}`);
      } else if (created.ok && "templateId" in created && created.templateId) {
        setTemplateId(created.templateId);
        setStatus(`Template seed metadata registered: ${created.templateId}`);
      } else {
        throw new Error(
          "[TEAM_MARKDOWN_TEMPLATE_RESPONSE_INVALID] template registration response invalid",
        );
      }
      await reloadTemplates();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Template registration failed",
      );
    } finally {
      setBusy(false);
    }
  };

  const handleArchiveTemplate = async () => {
    if (!templateId) return;
    setBusy(true);
    setError(null);
    setStatus(null);
    try {
      await archiveTemplate(templateId);
      setTemplateId("");
      setStatus(`Template seed metadata archived: ${templateId}`);
      await reloadTemplates();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Template archive failed");
    } finally {
      setBusy(false);
    }
  };

  const handleSelectTemplate = async (selectedTemplateId: string) => {
    setTemplateId(selectedTemplateId);
    if (!selectedTemplateId) return;
    setError(null);
    try {
      const response = await getTemplate(selectedTemplateId);
      const selected = response.template;
      setTemplateKey(selected.templateKey);
      setTemplateLabel(selected.templateLabel);
      setTemplateMarkdown(selected.templateMarkdown);
      const optional = extractMarkdownPlaceholders(
        selected.templateMarkdown,
        selected.placeholderSchemaJson,
      ).optionalPlaceholderKeys;
      setOptionalKeys(optional.join(","));
      setStatus(
        `Selected template seed metadata ${selected.templateKey}. Edit fields and save to update metadata, or create a new key by clearing the selection.`,
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "Template get failed");
    }
  };

  const buildCandidate = () =>
    buildMdTranslationAuthoringSeedCandidate({
      template: {
        templateId: templateId || "00000000-0000-0000-0000-000000000000",
        templateKey,
        templateLabel,
        templateMarkdown,
        placeholderSchemaJson: placeholderSchema,
      },
      source: { sourceTableRef, sourceRecordRef },
      bindings: placeholders.placeholderKeys.map((key) =>
        bindingDraft[key] ?? {
          placeholderKey: key,
          required: placeholders.requiredPlaceholderKeys.includes(key),
        }
      ),
      title,
      dashboard: { tags: ["md_translation_authoring_seed_registration"] },
    });

  const handleCreateSavedView = async () => {
    setBusy(true);
    setError(null);
    setStatus(null);
    try {
      if (!templateId) {
        throw new Error(
          "[TEMPLATE_SEED_REF_REQUIRED] register or select a template seed before saved view create",
        );
      }
      const candidate = buildCandidate();
      const created = await createSavedView({
        templateId,
        title,
        sourceTableRef,
        sourceRecordRef,
        ...candidate,
      });
      setStatus(
        `Saved Markdown projection created from seed candidate: ${created.savedViewId}`,
      );
      await onCreated();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Saved view create failed");
    } finally {
      setBusy(false);
    }
  };

  let candidatePreview = "";
  try {
    candidatePreview = JSON.stringify(
      buildCandidate().completedPresetSeedJson,
      null,
      2,
    );
  } catch (err) {
    candidatePreview = err instanceof Error
      ? err.message
      : "Seed candidate unavailable";
  }

  return (
    <section
      class="md-dashboard-authoring-surface"
      aria-label="Seed-driven md translation authoring surface"
      data-authoring-bundle="md_translation_authoring_seed_registration"
      data-component-bucket-parts="panel input select textarea button existing_bucket_parts"
    >
      <h2>Seed-driven Markdown authoring</h2>
      <p class="md-dashboard-authoring-note">
        Uses existing component bucket parts as containers; this is not a
        Markdown-only Modal/Drawer/Form component.
        Template/source/binding/adjustment metadata drive the seed candidate,
        and rendered Markdown remains a projection.
      </p>

      <div class="md-dashboard-authoring-grid">
        <label>
          Template seed registry
          <select
            value={templateId}
            onInput={(e) =>
              void handleSelectTemplate((e.target as HTMLSelectElement).value)}
          >
            <option value="">New template seed metadata</option>
            {templates.map((template) => (
              <option key={template.templateId} value={template.templateId}>
                {template.templateKey}
              </option>
            ))}
          </select>
        </label>
        <label>
          Template key
          <input
            value={templateKey}
            onInput={(e) =>
              setTemplateKey((e.target as HTMLInputElement).value)}
          />
        </label>
        <label>
          Template label
          <input
            value={templateLabel}
            onInput={(e) =>
              setTemplateLabel((e.target as HTMLInputElement).value)}
          />
        </label>
        <label>
          Optional placeholders (comma-separated)
          <input
            value={optionalKeys}
            onInput={(e) =>
              setOptionalKeys((e.target as HTMLInputElement).value)}
          />
        </label>
      </div>

      <label class="md-dashboard-authoring-block">
        Markdown template seed body (metadata input, not runtime SSOT)
        <textarea
          rows={5}
          value={templateMarkdown}
          onInput={(e) =>
            setTemplateMarkdown((e.target as HTMLTextAreaElement).value)}
        />
      </label>

      <div class="md-dashboard-authoring-actions">
        <button type="button" onClick={handleRegisterTemplate} disabled={busy}>
          {templateId
            ? "Update template seed metadata"
            : "Register template seed metadata"}
        </button>
        <button
          type="button"
          onClick={handleArchiveTemplate}
          disabled={busy || !templateId}
        >
          Archive template seed metadata
        </button>
      </div>

      <div class="md-dashboard-authoring-grid">
        <label>
          Saved view title
          <input
            value={title}
            onInput={(e) => setTitle((e.target as HTMLInputElement).value)}
          />
        </label>
        <label>
          Source table ref
          <input
            value={sourceTableRef}
            onInput={(e) =>
              setSourceTableRef((e.target as HTMLInputElement).value)}
          />
        </label>
        <label>
          Source record ref
          <input
            value={sourceRecordRef}
            onInput={(e) =>
              setSourceRecordRef((e.target as HTMLInputElement).value)}
          />
        </label>
      </div>

      <div
        class="md-dashboard-placeholder-panel"
        aria-label="Explicit placeholder bindings"
      >
        <h3>Explicit placeholder bindings</h3>
        {placeholders.placeholderKeys.length === 0 && (
          <p>No placeholders extracted.</p>
        )}
        {placeholders.placeholderKeys.map((key) => {
          const current = bindingDraft[key] ??
            {
              placeholderKey: key,
              required: placeholders.requiredPlaceholderKeys.includes(key),
              bindingKind: "static_text" as const,
            };
          return (
            <div
              class="md-dashboard-binding-row"
              key={key}
              data-placeholder-key={key}
            >
              <strong>{key}</strong>
              <span>{current.required ? "required" : "optional"}</span>
              <select
                value={current.bindingKind ?? ""}
                onInput={(e) =>
                  upsertBinding(key, {
                    bindingKind: (e.target as HTMLSelectElement)
                      .value as PlaceholderBinding["bindingKind"],
                  })}
              >
                <option value="">Explicit empty/unbound</option>
                <option value="static_text">Static text</option>
                <option value="source_field">Source field</option>
                <option value="jsonb_path">JSONB path</option>
                <option value="saved_query_field">Saved query field</option>
              </select>
              <input
                aria-label={`Binding ref for ${key}`}
                placeholder="field/path/static text"
                value={current.staticText ?? current.sourceFieldRef ??
                  current.jsonbPath ?? current.savedQueryFieldRef ?? ""}
                onInput={(e) => {
                  const value = (e.target as HTMLInputElement).value;
                  const bindingKind = current.bindingKind ?? "static_text";
                  upsertBinding(key, {
                    bindingKind,
                    staticText: bindingKind === "static_text"
                      ? value
                      : undefined,
                    sourceFieldRef: bindingKind === "source_field"
                      ? value
                      : undefined,
                    jsonbPath: bindingKind === "jsonb_path" ? value : undefined,
                    savedQueryFieldRef: bindingKind === "saved_query_field"
                      ? value
                      : undefined,
                  });
                }}
              />
              <input
                aria-label={`Preview value for ${key}`}
                placeholder="explicit preview value for projection"
                value={current.previewValue ?? ""}
                onInput={(e) =>
                  upsertBinding(key, {
                    previewValue: (e.target as HTMLInputElement).value,
                  })}
              />
            </div>
          );
        })}
      </div>

      <div class="md-dashboard-authoring-actions">
        <button type="button" onClick={handleCreateSavedView} disabled={busy}>
          Create saved Markdown projection through team_markdown API
        </button>
      </div>
      {status && (
        <div class="md-dashboard-action-notice" role="status">{status}</div>
      )}
      {error && <div class="md-dashboard-error" role="alert">{error}</div>}
      <details>
        <summary>completed_preset_seed candidate preview</summary>
        <pre>{candidatePreview}</pre>
      </details>
    </section>
  );
}

export default function TeamMarkdownDashboard({
  defaultStatus = "active",
  placement = "admin_route",
}: Props) {
  const [query, setQuery] = useState("");
  const [status] = useState(defaultStatus);
  const [cards, setCards] = useState<SavedViewCard[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [expandedView, setExpandedView] = useState<
    { detail: SavedViewDetail; seedValid: boolean; seedError?: string } | null
  >(null);
  const [expandLoading, setExpandLoading] = useState(false);
  const [actionNotice, setActionNotice] = useState<string | null>(null);

  const doSearch = useCallback(async (q: string) => {
    setLoading(true);
    setSearchError(null);
    setActionNotice(null);
    try {
      const result = await searchSavedViews({ query: q || undefined, status });
      if (!Array.isArray(result.savedViews)) {
        throw new Error(
          "[TEAM_MARKDOWN_SEARCH_RESPONSE_INVALID] saved_view:search response missing savedViews array",
        );
      }
      setCards(result.savedViews);
    } catch (err) {
      setCards([]);
      setSearchError(err instanceof Error ? err.message : "Search failed");
    } finally {
      setLoading(false);
    }
  }, [status]);

  useEffect(() => {
    doSearch(query);
  }, []);

  const handleSearchSubmit = (e: Event) => {
    e.preventDefault();
    doSearch(query);
  };

  const handleExpand = async (savedViewId: string) => {
    if (expandLoading) return; // prevent concurrent expand requests
    setExpandLoading(true);
    try {
      const result = await getSavedView(savedViewId);
      setExpandedView({
        detail: result.savedView,
        seedValid: result.seedValid,
        seedError: result.seedError,
      });
    } catch (err) {
      setSearchError(
        err instanceof Error ? err.message : "Failed to load saved view",
      );
    } finally {
      setExpandLoading(false);
    }
  };

  const handleClose = () => setExpandedView(null);

  const handleArchive = async (savedViewId: string) => {
    setActionNotice(null);
    try {
      await archiveSavedView(savedViewId);
      setExpandedView(null);
      await doSearch(query);
    } catch (err) {
      setSearchError(err instanceof Error ? err.message : "Archive failed");
    }
  };

  const handleOpenSourceRecord = (
    sourceTableRef: string,
    sourceRecordRef: string,
  ) => {
    setActionNotice(
      `Source record candidate: ${sourceTableRef} / ${sourceRecordRef}. Opening a source-record editor is a UI-only boundary in this bundle.`,
    );
  };

  const handleEditAdjustment = (savedViewId: string) => {
    setActionNotice(
      `Adjustment edit candidate for ${savedViewId}. Inline adjustment editor UI is not completed in this bundle.`,
    );
  };

  const handleCreateTodoCandidate = (savedViewId: string) => {
    setActionNotice(
      `Follow-up todo candidate prepared for saved view ${savedViewId}. Canonical TODO persistence is intentionally not performed from this projection surface.`,
    );
  };

  const handleRefresh = (_savedViewId: string) => {
    void refreshSavedView;
    setActionNotice(
      `Refresh backend action is registered, but this dashboard requires explicit templateMarkdown and sourceRecordJson payload before dispatch. ${EXPLICIT_PAYLOAD_REQUIRED_REASON}`,
    );
  };

  const handleClone = (_savedViewId: string) => {
    void cloneSavedView;
    setActionNotice(
      `Clone backend action is registered, but this dashboard requires explicit targetSourceTableRef, targetSourceRecordRef, templateMarkdown, and sourceRecordJson before dispatch. ${EXPLICIT_PAYLOAD_REQUIRED_REASON}`,
    );
  };

  const handleRebind = (_savedViewId: string) => {
    void rebindSavedView;
    setActionNotice(
      `Rebind backend action is registered, but this dashboard requires a user-selected bindingJson/completedPresetSeedJson plus templateMarkdown and sourceRecordJson before dispatch. ${EXPLICIT_PAYLOAD_REQUIRED_REASON}`,
    );
  };

  return (
    <div
      class="md-dashboard"
      aria-label="Team Markdown Dashboard"
      data-placement={placement}
    >
      <header class="md-dashboard-header">
        <p class="md-dashboard-placement-label">
          {placement === "ui_builder_child_surface"
            ? "UIBuilder preset_ecosystem child surface — projection only"
            : "Admin route dashboard — projection only"}
        </p>
        <h1 class="md-dashboard-heading">Markdown Dashboard</h1>
      </header>

      <form
        class="md-dashboard-search-form"
        onSubmit={handleSearchSubmit}
        role="search"
      >
        <label
          for="md-dashboard-search-input"
          class="md-dashboard-search-label"
        >
          Search saved views
        </label>
        <input
          id="md-dashboard-search-input"
          type="search"
          class="md-dashboard-search-input"
          value={query}
          onInput={(e) => setQuery((e.target as HTMLInputElement).value)}
          placeholder="Search by title, content, or source table..."
          aria-label="Search saved Markdown views"
        />
        <button
          type="submit"
          class="md-dashboard-search-btn"
          disabled={loading}
        >
          {loading ? "Searching…" : "Search"}
        </button>
      </form>

      <MdTranslationAuthoringSeedSurface onCreated={() => doSearch(query)} />

      {searchError && (
        <div class="md-dashboard-error" role="alert" aria-live="assertive">
          {searchError}
        </div>
      )}

      {actionNotice && (
        <div
          class="md-dashboard-action-notice"
          role="status"
          aria-live="polite"
        >
          {actionNotice}
        </div>
      )}

      {expandLoading && (
        <div class="md-dashboard-loading" role="status" aria-live="polite">
          Loading saved view…
        </div>
      )}

      {cards.length === 0 && !loading && !searchError && (
        <div class="md-dashboard-empty" role="status">
          No saved views found{query ? ` for "${query}"` : ""}.
        </div>
      )}

      <section
        class="md-dashboard-results"
        aria-label="Search results"
        aria-live="polite"
      >
        {cards.map((card) => (
          <SavedViewResultCard
            key={card.savedViewId}
            card={card}
            onExpand={handleExpand}
          />
        ))}
      </section>

      {expandedView && (
        <div
          class="md-dashboard-drawer-overlay"
          role="dialog"
          aria-modal="true"
        >
          <MdViewer
            savedView={expandedView.detail}
            seedValid={expandedView.seedValid}
            seedError={expandedView.seedError}
            onClose={handleClose}
            onArchive={handleArchive}
            onOpenSourceRecord={handleOpenSourceRecord}
            onEditAdjustment={handleEditAdjustment}
            onCreateTodoCandidate={handleCreateTodoCandidate}
            onRefresh={handleRefresh}
            onClone={handleClone}
            onRebind={handleRebind}
            disabledActionReasons={{
              refresh: expandedView.seedValid ? undefined : EXPLICIT_PAYLOAD_REQUIRED_REASON,
              clone: expandedView.seedValid ? undefined : EXPLICIT_PAYLOAD_REQUIRED_REASON,
              rebind: expandedView.seedValid ? undefined : EXPLICIT_PAYLOAD_REQUIRED_REASON,
            }}
          />
        </div>
      )}
    </div>
  );
}

// ─── role-split surfaces for the authenticated /dashboard/team route ──────────
// Normal and admin sessions share TeamMarkdownViewer (read/search/filter only, backed by the
// plain-JWT viewer read endpoints — never the admin_runtime dispatch lane). Only admin sessions
// additionally mount TeamMarkdownAuthoring (backed by the admin_runtime team_markdown dispatch
// actions, which independently reject non-admin JWTs server-side regardless of what renders here).

/** Normal + admin shared viewer: read/search/filter only, no mutation-capable component mounted. */
export function TeamMarkdownViewer({ defaultStatus = "active" }: { defaultStatus?: string }) {
  const [query, setQuery] = useState("");
  const [cards, setCards] = useState<SavedViewCard[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [expandedView, setExpandedView] = useState<
    { detail: SavedViewDetail; seedValid: boolean; seedError?: string } | null
  >(null);
  const [expandLoading, setExpandLoading] = useState(false);

  const doSearch = useCallback(async (q: string) => {
    setLoading(true);
    setSearchError(null);
    try {
      const savedViews = await viewerSearchSavedViews({ query: q || undefined, status: defaultStatus });
      setCards(savedViews);
    } catch (err) {
      setCards([]);
      setSearchError(err instanceof Error ? err.message : "Search failed");
    } finally {
      setLoading(false);
    }
  }, [defaultStatus]);

  useEffect(() => {
    doSearch(query);
  }, []);

  const handleExpand = async (savedViewId: string) => {
    if (expandLoading) return;
    setExpandLoading(true);
    try {
      const detail = await viewerGetSavedView(savedViewId);
      setExpandedView({ detail, seedValid: true });
    } catch (err) {
      setSearchError(err instanceof Error ? err.message : "Failed to load saved view");
    } finally {
      setExpandLoading(false);
    }
  };

  return (
    <div class="md-dashboard" aria-label="Team Dashboard viewer" data-placement="normal_dashboard_viewer">
      <header class="md-dashboard-header">
        <p class="md-dashboard-placement-label">Viewer — read/search/filter only</p>
        <h1 class="md-dashboard-heading">Team Markdown Dashboard</h1>
      </header>

      <form
        class="md-dashboard-search-form"
        role="search"
        onSubmit={(e) => { e.preventDefault(); doSearch(query); }}
      >
        <label for="md-dashboard-viewer-search" class="md-dashboard-search-label">Search saved views</label>
        <input
          id="md-dashboard-viewer-search"
          type="search"
          class="md-dashboard-search-input"
          value={query}
          onInput={(e) => setQuery((e.target as HTMLInputElement).value)}
          placeholder="Search by title, content, or source table..."
        />
        <button type="submit" class="md-dashboard-search-btn" disabled={loading}>
          {loading ? "Searching…" : "Search"}
        </button>
      </form>

      {searchError && <div class="md-dashboard-error" role="alert">{searchError}</div>}
      {expandLoading && <div class="md-dashboard-loading" role="status">Loading saved view…</div>}
      {cards.length === 0 && !loading && !searchError && (
        <div class="md-dashboard-empty" role="status">No saved views found{query ? ` for "${query}"` : ""}.</div>
      )}

      <section class="md-dashboard-results" aria-label="Search results">
        {cards.map((card) => (
          <SavedViewResultCard key={card.savedViewId} card={card} onExpand={handleExpand} />
        ))}
      </section>

      {expandedView && (
        <div class="md-dashboard-drawer-overlay" role="dialog" aria-modal="true">
          <MdViewer
            savedView={expandedView.detail}
            seedValid={expandedView.seedValid}
            seedError={expandedView.seedError}
            onClose={() => setExpandedView(null)}
            authoringEnabled={false}
          />
        </div>
      )}
    </div>
  );
}

/**
 * Admin-only authoring surface: template/saved-view registration plus the full
 * preview/validate/confirm/write/diff adjustment workflow. Must only be mounted for admin-role
 * sessions (display-only client gate); every mutation it calls is independently re-checked and
 * rejected server-side for non-admin JWTs by AdminRuntime.TeamMarkdown's ExecuteTeamMarkdownAsync.
 */
export function TeamMarkdownAuthoring({ defaultStatus = "active" }: { defaultStatus?: string }) {
  const [query, setQuery] = useState("");
  const [cards, setCards] = useState<SavedViewCard[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [expandedView, setExpandedView] = useState<
    { detail: SavedViewDetail; seedValid: boolean; seedError?: string } | null
  >(null);
  const [editing, setEditing] = useState(false);
  const [pendingOperation, setPendingOperation] = useState<SavedViewOperationMode | null>(null);
  const [expandLoading, setExpandLoading] = useState(false);
  const [actionNotice, setActionNotice] = useState<string | null>(null);

  const doSearch = useCallback(async (q: string) => {
    setLoading(true);
    setSearchError(null);
    try {
      const result = await searchSavedViews({ query: q || undefined, status: defaultStatus });
      setCards(Array.isArray(result.savedViews) ? result.savedViews : []);
    } catch (err) {
      setCards([]);
      setSearchError(err instanceof Error ? err.message : "Search failed");
    } finally {
      setLoading(false);
    }
  }, [defaultStatus]);

  useEffect(() => {
    doSearch(query);
  }, []);

  const handleExpand = async (savedViewId: string) => {
    if (expandLoading) return;
    setExpandLoading(true);
    setEditing(false);
    try {
      const result = await getSavedView(savedViewId);
      setExpandedView({ detail: result.savedView, seedValid: result.seedValid, seedError: result.seedError });
    } catch (err) {
      setSearchError(err instanceof Error ? err.message : "Failed to load saved view");
    } finally {
      setExpandLoading(false);
    }
  };

  const handleArchive = async (savedViewId: string) => {
    try {
      await archiveSavedView(savedViewId);
      setExpandedView(null);
      await doSearch(query);
    } catch (err) {
      setSearchError(err instanceof Error ? err.message : "Archive failed");
    }
  };

  return (
    <div class="md-dashboard" aria-label="Team Dashboard authoring" data-placement="normal_dashboard_authoring">
      <header class="md-dashboard-header">
        <p class="md-dashboard-placement-label">Authoring — admin only</p>
        <h1 class="md-dashboard-heading">Team Markdown Authoring</h1>
      </header>

      <form
        class="md-dashboard-search-form"
        role="search"
        onSubmit={(e) => { e.preventDefault(); doSearch(query); }}
      >
        <label for="md-dashboard-authoring-search" class="md-dashboard-search-label">Search saved views</label>
        <input
          id="md-dashboard-authoring-search"
          type="search"
          class="md-dashboard-search-input"
          value={query}
          onInput={(e) => setQuery((e.target as HTMLInputElement).value)}
        />
        <button type="submit" class="md-dashboard-search-btn" disabled={loading}>
          {loading ? "Searching…" : "Search"}
        </button>
      </form>

      <MdTranslationAuthoringSeedSurface onCreated={() => doSearch(query)} />

      {searchError && <div class="md-dashboard-error" role="alert">{searchError}</div>}
      {actionNotice && <div class="md-dashboard-action-notice" role="status">{actionNotice}</div>}
      {expandLoading && <div class="md-dashboard-loading" role="status">Loading saved view…</div>}

      <section class="md-dashboard-results" aria-label="Search results">
        {cards.map((card) => (
          <SavedViewResultCard key={card.savedViewId} card={card} onExpand={handleExpand} />
        ))}
      </section>

      {expandedView && !editing && !pendingOperation && (
        <div class="md-dashboard-drawer-overlay" role="dialog" aria-modal="true">
          <MdViewer
            savedView={expandedView.detail}
            seedValid={expandedView.seedValid}
            seedError={expandedView.seedError}
            onClose={() => setExpandedView(null)}
            onArchive={handleArchive}
            onEditAdjustment={() => setEditing(true)}
            onRefresh={() => setPendingOperation("refresh")}
            onClone={() => setPendingOperation("clone")}
            onRebind={() => setPendingOperation("rebind")}
            authoringEnabled
          />
        </div>
      )}

      {expandedView && editing && (
        <div class="md-dashboard-drawer-overlay" role="dialog" aria-modal="true">
          <SavedViewAdjustmentAuthoringPanel
            savedView={expandedView.detail}
            onCancel={() => setEditing(false)}
            onWritten={() => { setEditing(false); void doSearch(query); }}
          />
        </div>
      )}

      {expandedView && pendingOperation && (
        <div class="md-dashboard-drawer-overlay" role="dialog" aria-modal="true">
          <SavedViewOperationPanel
            mode={pendingOperation}
            savedView={expandedView.detail}
            onCancel={() => setPendingOperation(null)}
            onWritten={async () => {
              const writtenSavedViewId = expandedView.detail.savedViewId;
              setPendingOperation(null);
              await doSearch(query);
              // Refresh/rebind mutate the same saved view in place; refetch it so the drawer
              // (if reopened) and any still-visible summary reflect the post-write state rather
              // than the stale pre-write detail. Clone creates a new saved view — closing the
              // drawer here (rather than refetching the OLD one) is correct: nothing to refetch it into.
              if (pendingOperation !== "clone") {
                try {
                  const result = await getSavedView(writtenSavedViewId);
                  setExpandedView({ detail: result.savedView, seedValid: result.seedValid, seedError: result.seedError });
                } catch {
                  setExpandedView(null);
                }
              } else {
                setExpandedView(null);
              }
            }}
          />
        </div>
      )}
    </div>
  );
}
