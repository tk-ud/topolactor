/**
 * TeamMarkdownDashboard — team shared saved Markdown view dashboard island.
 * SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml §dashboard_surface_contract
 *
 * Responsibilities:
 *   - Search input for saved Markdown views (title, search_index_text, source_table_ref)
 *   - Saved view result cards with click-to-expand drawer/panel
 *   - Display rendered Markdown in expanded view via MdViewer component
 *   - Show preset seed summary and binding summary in expanded view
 *   - Template registration modal/drawer entry point
 *
 * Prohibited (per SSOT):
 *   - Direct DB write from frontend (all mutations via backend admin action)
 *   - Markdown body as runtime SSOT
 *   - AI inference for binding
 *   - Silent fallback when seed is missing
 *   - Mutation of active topology
 *   - Search mutating saved views
 */

import { useState, useEffect, useCallback } from "preact/hooks";
import {
  searchSavedViews,
  getSavedView,
  archiveSavedView,
  type SavedViewCard,
  type SavedViewDetail,
} from "../api/teamMarkdownApi.ts";
import { MdViewer } from "../components/MdViewer.tsx";

// ─── search card component ────────────────────────────────────────────────────

function SavedViewResultCard({
  card,
  onExpand,
}: {
  card: SavedViewCard;
  onExpand: (savedViewId: string) => void;
}) {
  const excerpt = String((card.cardMetadataJson as Record<string, unknown>).excerpt ?? "");
  return (
    <article
      class="md-dashboard-card"
      onClick={() => onExpand(card.savedViewId)}
      onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") onExpand(card.savedViewId); }}
      role="button"
      tabIndex={0}
      aria-label={`Open saved view: ${card.title}`}
    >
      <h3 class="md-dashboard-card-title">{card.title}</h3>
      <div class="md-dashboard-card-meta">
        <span class="md-dashboard-card-template">{card.templateKey}</span>
        <span class="md-dashboard-card-source">{card.sourceTableRef}</span>
        <span class="md-dashboard-card-updated">{card.updatedAt}</span>
      </div>
      {excerpt && <p class="md-dashboard-card-excerpt">{excerpt}</p>}
    </article>
  );
}

// ─── main island ─────────────────────────────────────────────────────────────

type Props = {
  defaultStatus?: string;
};

export default function TeamMarkdownDashboard({ defaultStatus = "active" }: Props) {
  const [query, setQuery] = useState("");
  const [status] = useState(defaultStatus);
  const [cards, setCards] = useState<SavedViewCard[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [expandedView, setExpandedView] = useState<{ detail: SavedViewDetail; seedValid: boolean; seedError?: string } | null>(null);
  const [expandLoading, setExpandLoading] = useState(false);

  const doSearch = useCallback(async (q: string) => {
    setLoading(true);
    setSearchError(null);
    try {
      const result = await searchSavedViews({ query: q || undefined, status });
      setCards(result.savedViews);
    } catch (err) {
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
    setExpandLoading(true);
    try {
      const result = await getSavedView(savedViewId);
      setExpandedView({ detail: result.savedView, seedValid: result.seedValid, seedError: result.seedError });
    } catch (err) {
      setSearchError(err instanceof Error ? err.message : "Failed to load saved view");
    } finally {
      setExpandLoading(false);
    }
  };

  const handleClose = () => setExpandedView(null);

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
    <div class="md-dashboard" aria-label="Team Markdown Dashboard">
      <header class="md-dashboard-header">
        <h1 class="md-dashboard-heading">Markdown Dashboard</h1>
      </header>

      <form class="md-dashboard-search-form" onSubmit={handleSearchSubmit} role="search">
        <label for="md-dashboard-search-input" class="md-dashboard-search-label">
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
        <button type="submit" class="md-dashboard-search-btn" disabled={loading}>
          {loading ? "Searching…" : "Search"}
        </button>
      </form>

      {searchError && (
        <div class="md-dashboard-error" role="alert" aria-live="assertive">
          {searchError}
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

      <section class="md-dashboard-results" aria-label="Search results" aria-live="polite">
        {cards.map((card) => (
          <SavedViewResultCard
            key={card.savedViewId}
            card={card}
            onExpand={handleExpand}
          />
        ))}
      </section>

      {expandedView && (
        <div class="md-dashboard-drawer-overlay" role="dialog" aria-modal="true">
          <MdViewer
            savedView={expandedView.detail}
            seedValid={expandedView.seedValid}
            seedError={expandedView.seedError}
            onClose={handleClose}
            onArchive={handleArchive}
          />
        </div>
      )}
    </div>
  );
}
