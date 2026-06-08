/**
 * MdViewer — hardcoded projection component for saved Markdown views.
 * SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml §component_surface_candidates
 *
 * Responsibilities:
 *   - Display rendered Markdown from a persisted saved view (projection, not authority)
 *   - Show binding summary, preset seed summary, source record ref, adjustment status
 *   - Surface actions: open source record, edit adjustment, refresh, clone, archive, copy, todo
 *   - Show explicit error when completed_preset_seed_json is missing or invalid
 *
 * Prohibited (per SSOT):
 *   - Markdown body as runtime SSOT
 *   - Refresh by Markdown body parsing
 *   - Direct DB write from frontend
 *   - AI inference for binding
 *   - Silent fallback when seed is missing
 *   - Mutation of active topology
 */

import type { SavedViewDetail, CompletedPresetSeed } from "../api/teamMarkdownApi.ts";

// ─── types ────────────────────────────────────────────────────────────────────

export type MdViewerProps = {
  savedView: SavedViewDetail;
  seedValid: boolean;
  seedError?: string;
  onClose: () => void;
  onRefresh?: (savedViewId: string) => void;
  onEditAdjustment?: (savedViewId: string) => void;
  onArchive?: (savedViewId: string) => void;
  onClone?: (savedViewId: string) => void;
  onOpenSourceRecord?: (sourceTableRef: string, sourceRecordRef: string) => void;
  onCreateTodoCandidate?: (savedViewId: string) => void;
};

// ─── seed summary ─────────────────────────────────────────────────────────────

function SeedSummary({ seed, seedValid, seedError }: {
  seed: CompletedPresetSeed;
  seedValid: boolean;
  seedError?: string;
}) {
  if (!seedValid) {
    return (
      <div class="md-viewer-seed-error" role="alert" aria-live="assertive">
        <strong>Preset seed invalid — refresh, rebind, and clone are disabled.</strong>
        {seedError && <p class="md-viewer-seed-error-detail">{seedError}</p>}
      </div>
    );
  }
  return (
    <div class="md-viewer-seed-summary" aria-label="Preset seed summary">
      <div class="md-viewer-seed-row">
        <span class="md-viewer-seed-label">Seed version:</span>
        <span class="md-viewer-seed-value">{seed.seed_version}</span>
      </div>
      {seed.template_ref && (
        <div class="md-viewer-seed-row">
          <span class="md-viewer-seed-label">Template:</span>
          <span class="md-viewer-seed-value">
            {String((seed.template_ref as Record<string, unknown>).template_key ?? "")}
          </span>
        </div>
      )}
      {seed.render_ref && (
        <div class="md-viewer-seed-row">
          <span class="md-viewer-seed-label">Rendered at:</span>
          <span class="md-viewer-seed-value">{seed.render_ref.rendered_at}</span>
        </div>
      )}
      {seed.render_ref?.unresolved_placeholder_keys?.length > 0 && (
        <div class="md-viewer-seed-unresolved" role="status">
          Optional placeholders empty: {seed.render_ref.unresolved_placeholder_keys.join(", ")}
        </div>
      )}
    </div>
  );
}

// ─── binding summary ──────────────────────────────────────────────────────────

function BindingSummary({ bindingJson }: { bindingJson: Record<string, unknown> }) {
  const entries = Object.entries(bindingJson);
  if (entries.length === 0) return null;
  return (
    <div class="md-viewer-binding-summary" aria-label="Binding summary">
      <h4 class="md-viewer-section-label">Bindings</h4>
      <ul class="md-viewer-binding-list">
        {entries.map(([placeholder, fieldRef]) => (
          <li key={placeholder} class="md-viewer-binding-item">
            <code class="md-viewer-placeholder">{placeholder}</code>
            <span class="md-viewer-binding-arrow">→</span>
            <code class="md-viewer-field-ref">{String(fieldRef)}</code>
          </li>
        ))}
      </ul>
    </div>
  );
}

// ─── adjustment status ────────────────────────────────────────────────────────

function AdjustmentStatus({ patchJson }: { patchJson: Record<string, unknown> }) {
  const hasAdjustment = Object.keys(patchJson).length > 0;
  return (
    <div class="md-viewer-adjustment-status" aria-label="Adjustment status">
      <span class="md-viewer-adjustment-badge" data-has-adjustment={String(hasAdjustment)}>
        {hasAdjustment ? "Has user adjustment" : "No adjustment"}
      </span>
    </div>
  );
}

// ─── action toolbar ───────────────────────────────────────────────────────────

function ActionToolbar({
  savedView,
  seedValid,
  onRefresh,
  onEditAdjustment,
  onArchive,
  onClone,
  onOpenSourceRecord,
  onCreateTodoCandidate,
  onCopyMarkdown,
}: {
  savedView: SavedViewDetail;
  seedValid: boolean;
  onRefresh?: (id: string) => void;
  onEditAdjustment?: (id: string) => void;
  onArchive?: (id: string) => void;
  onClone?: (id: string) => void;
  onOpenSourceRecord?: (tableRef: string, recordRef: string) => void;
  onCreateTodoCandidate?: (id: string) => void;
  onCopyMarkdown: () => void;
}) {
  return (
    <div class="md-viewer-action-toolbar" role="toolbar" aria-label="Saved view actions">
      {onOpenSourceRecord && (
        <button
          type="button"
          class="md-viewer-action-btn"
          onClick={() => onOpenSourceRecord(savedView.sourceTableRef, savedView.sourceRecordRef)}
          aria-label="Open source record"
        >
          Open source record
        </button>
      )}
      {onEditAdjustment && (
        <button
          type="button"
          class="md-viewer-action-btn"
          onClick={() => onEditAdjustment(savedView.savedViewId)}
          aria-label="Edit saved view adjustment"
        >
          Edit adjustment
        </button>
      )}
      {onRefresh && (
        <button
          type="button"
          class="md-viewer-action-btn"
          disabled={!seedValid}
          onClick={() => onRefresh(savedView.savedViewId)}
          aria-label="Refresh from source record"
          title={seedValid ? "Refresh from source record" : "Seed invalid — refresh disabled"}
        >
          Refresh
        </button>
      )}
      {onClone && (
        <button
          type="button"
          class="md-viewer-action-btn"
          disabled={!seedValid}
          onClick={() => onClone(savedView.savedViewId)}
          aria-label="Clone saved view to another record"
          title={seedValid ? "Clone to another record" : "Seed invalid — clone disabled"}
        >
          Clone
        </button>
      )}
      <button
        type="button"
        class="md-viewer-action-btn"
        onClick={onCopyMarkdown}
        aria-label="Copy Markdown to clipboard"
      >
        Copy Markdown
      </button>
      {onCreateTodoCandidate && (
        <button
          type="button"
          class="md-viewer-action-btn"
          onClick={() => onCreateTodoCandidate(savedView.savedViewId)}
          aria-label="Create follow-up todo candidate"
        >
          Create todo
        </button>
      )}
      {onArchive && savedView.status !== "archived" && (
        <button
          type="button"
          class="md-viewer-action-btn md-viewer-action-destructive"
          onClick={() => onArchive(savedView.savedViewId)}
          aria-label="Archive saved view"
        >
          Archive
        </button>
      )}
    </div>
  );
}

// ─── rendered markdown display ────────────────────────────────────────────────

function RenderedMarkdownPanel({ markdown }: { markdown: string }) {
  return (
    <div class="md-viewer-rendered-markdown" aria-label="Rendered Markdown content">
      <pre class="md-viewer-markdown-pre">{markdown}</pre>
    </div>
  );
}

// ─── main MdViewer component ──────────────────────────────────────────────────

export function MdViewer({
  savedView,
  seedValid,
  seedError,
  onClose,
  onRefresh,
  onEditAdjustment,
  onArchive,
  onClone,
  onOpenSourceRecord,
  onCreateTodoCandidate,
}: MdViewerProps) {
  const handleCopyMarkdown = () => {
    if (typeof globalThis.navigator?.clipboard?.writeText === "function") {
      globalThis.navigator.clipboard.writeText(savedView.renderedMarkdown).catch(() => undefined);
    }
  };

  return (
    <div class="md-viewer-panel" role="dialog" aria-modal="true" aria-label={`Saved view: ${savedView.title}`}>
      <div class="md-viewer-header">
        <h2 class="md-viewer-title">{savedView.title}</h2>
        <button
          type="button"
          class="md-viewer-close-btn"
          onClick={onClose}
          aria-label="Close saved view panel"
        >
          ✕
        </button>
      </div>

      <div class="md-viewer-meta-row">
        <span class="md-viewer-source-ref" aria-label="Source record reference">
          <strong>Source:</strong> {savedView.sourceTableRef} / {savedView.sourceRecordRef}
        </span>
        <span class="md-viewer-template-ref">
          <strong>Template:</strong> {savedView.templateKey}
        </span>
        <span class="md-viewer-updated-at">
          <strong>Updated:</strong> {savedView.updatedAt}
        </span>
      </div>

      <AdjustmentStatus patchJson={savedView.userAdjustmentPatchJson} />

      <ActionToolbar
        savedView={savedView}
        seedValid={seedValid}
        onRefresh={onRefresh}
        onEditAdjustment={onEditAdjustment}
        onArchive={onArchive}
        onClone={onClone}
        onOpenSourceRecord={onOpenSourceRecord}
        onCreateTodoCandidate={onCreateTodoCandidate}
        onCopyMarkdown={handleCopyMarkdown}
      />

      <div class="md-viewer-body">
        <RenderedMarkdownPanel markdown={savedView.renderedMarkdown} />
      </div>

      <div class="md-viewer-details">
        <BindingSummary bindingJson={savedView.bindingJson} />
        <SeedSummary
          seed={savedView.completedPresetSeedJson}
          seedValid={seedValid}
          seedError={seedError}
        />
      </div>
    </div>
  );
}
