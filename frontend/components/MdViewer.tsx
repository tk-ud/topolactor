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

import { useState } from "preact/hooks";
import type {
  CompletedPresetSeed,
  SavedViewDetail,
} from "../api/teamMarkdownApi.ts";
import { renderMarkdownToVNodes } from "../lib/markdownRenderer.ts";

// ─── types ────────────────────────────────────────────────────────────────────

export type MdViewerDisabledActionReasons = Partial<
  Record<
    | "refresh"
    | "clone"
    | "rebind"
    | "editAdjustment"
    | "openSourceRecord"
    | "createTodoCandidate",
    string
  >
>;

export type MdViewerProps = {
  savedView: SavedViewDetail;
  seedValid: boolean;
  seedError?: string;
  onClose: () => void;
  onRefresh?: (savedViewId: string) => void;
  onEditAdjustment?: (savedViewId: string) => void;
  onArchive?: (savedViewId: string) => void;
  onClone?: (savedViewId: string) => void;
  onRebind?: (savedViewId: string) => void;
  onOpenSourceRecord?: (
    sourceTableRef: string,
    sourceRecordRef: string,
  ) => void;
  onCreateTodoCandidate?: (savedViewId: string) => void;
  disabledActionReasons?: MdViewerDisabledActionReasons;
  /**
   * Default true (preserves existing admin-route/UI-Builder-child-surface behavior). When false,
   * every mutation-capable action (edit adjustment, refresh, clone, rebind, archive, create-todo)
   * is not rendered at all — not merely disabled — so a Normal-role viewer never sees authoring
   * controls it has no backend authority to use. Read-only actions (open source record, copy
   * Markdown) remain available regardless.
   */
  authoringEnabled?: boolean;
};

// ─── seed summary ─────────────────────────────────────────────────────────────

function SeedSummary({ seed, seedValid, seedError }: {
  seed: CompletedPresetSeed;
  seedValid: boolean;
  seedError?: string;
}) {
  const unresolvedPlaceholderKeys =
    seed.render_ref?.unresolved_placeholder_keys ?? [];

  if (!seedValid) {
    return (
      <div class="md-viewer-seed-error" role="alert" aria-live="assertive">
        <strong>
          Preset seed invalid — refresh, rebind, and clone are disabled.
        </strong>
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
            {String(
              (seed.template_ref as Record<string, unknown>).template_key ?? "",
            )}
          </span>
        </div>
      )}
      {seed.render_ref && (
        <div class="md-viewer-seed-row">
          <span class="md-viewer-seed-label">Rendered at:</span>
          <span class="md-viewer-seed-value">
            {seed.render_ref.rendered_at}
          </span>
        </div>
      )}
      {unresolvedPlaceholderKeys.length > 0 && (
        <div class="md-viewer-seed-unresolved" role="status">
          Optional placeholders empty: {unresolvedPlaceholderKeys.join(", ")}
        </div>
      )}
    </div>
  );
}

// ─── binding summary ──────────────────────────────────────────────────────────

function BindingSummary(
  { bindingJson }: { bindingJson: Record<string, unknown> },
) {
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

function AdjustmentStatus(
  { patchJson }: { patchJson: Record<string, unknown> },
) {
  const hasAdjustment = Object.keys(patchJson).length > 0;
  return (
    <div class="md-viewer-adjustment-status" aria-label="Adjustment status">
      <span
        class="md-viewer-adjustment-badge"
        data-has-adjustment={String(hasAdjustment)}
      >
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
  onRebind,
  onOpenSourceRecord,
  onCreateTodoCandidate,
  onCopyMarkdown,
  copyStatus,
  disabledActionReasons = {},
  authoringEnabled = true,
}: {
  savedView: SavedViewDetail;
  seedValid: boolean;
  onRefresh?: (id: string) => void;
  onEditAdjustment?: (id: string) => void;
  onArchive?: (id: string) => void;
  onClone?: (id: string) => void;
  onRebind?: (id: string) => void;
  onOpenSourceRecord?: (tableRef: string, recordRef: string) => void;
  onCreateTodoCandidate?: (id: string) => void;
  onCopyMarkdown: () => void;
  copyStatus: "idle" | "copied" | "error";
  disabledActionReasons?: MdViewerDisabledActionReasons;
  authoringEnabled?: boolean;
}) {
  const seedInvalidReason = !seedValid
    ? "Seed invalid — action disabled"
    : undefined;
  const refreshDisabledReason = seedInvalidReason ??
    disabledActionReasons.refresh;
  const cloneDisabledReason = seedInvalidReason ?? disabledActionReasons.clone;
  const openSourceDisabledReason = disabledActionReasons.openSourceRecord;
  const editDisabledReason = disabledActionReasons.editAdjustment;
  const createTodoDisabledReason = disabledActionReasons.createTodoCandidate;
  const rebindDisabledReason = seedInvalidReason ?? disabledActionReasons.rebind;

  return (
    <div
      class="md-viewer-action-toolbar"
      role="toolbar"
      aria-label="Saved view actions"
    >
      <button
        type="button"
        class="md-viewer-action-btn"
        disabled={!onOpenSourceRecord || Boolean(openSourceDisabledReason)}
        onClick={() =>
          onOpenSourceRecord?.(
            savedView.sourceTableRef,
            savedView.sourceRecordRef,
          )}
        aria-label="Open source record"
        title={openSourceDisabledReason ?? "Open source record candidate"}
      >
        Open source record
      </button>
      {authoringEnabled && (
        <button
          type="button"
          class="md-viewer-action-btn"
          disabled={!onEditAdjustment || Boolean(editDisabledReason)}
          onClick={() => onEditAdjustment?.(savedView.savedViewId)}
          aria-label="Edit saved view adjustment"
          title={editDisabledReason ?? "Edit saved view adjustment candidate"}
        >
          Edit adjustment
        </button>
      )}
      {authoringEnabled && (
        <button
          type="button"
          class="md-viewer-action-btn"
          disabled={!onRefresh || Boolean(refreshDisabledReason)}
          onClick={() => onRefresh?.(savedView.savedViewId)}
          aria-label="Refresh from source record"
          title={refreshDisabledReason ?? "Refresh from source record"}
        >
          Refresh
        </button>
      )}
      {authoringEnabled && (
        <button
          type="button"
          class="md-viewer-action-btn"
          disabled={!onClone || Boolean(cloneDisabledReason)}
          onClick={() => onClone?.(savedView.savedViewId)}
          aria-label="Clone saved view to another record"
          title={cloneDisabledReason ?? "Clone to another record"}
        >
          Clone
        </button>
      )}
      {authoringEnabled && (
        <button
          type="button"
          class="md-viewer-action-btn"
          disabled={!onRebind || Boolean(rebindDisabledReason)}
          onClick={() => onRebind?.(savedView.savedViewId)}
          aria-label="Rebind saved view to another source"
          title={rebindDisabledReason ?? "Rebind saved view to another source"}
        >
          Rebind
        </button>
      )}
      <button
        type="button"
        class="md-viewer-action-btn"
        onClick={onCopyMarkdown}
        aria-label="Copy Markdown to clipboard"
        aria-live="polite"
      >
        {copyStatus === "copied"
          ? "Copied!"
          : copyStatus === "error"
          ? "Copy failed"
          : "Copy Markdown"}
      </button>
      {authoringEnabled && (
        <button
          type="button"
          class="md-viewer-action-btn"
          disabled={!onCreateTodoCandidate || Boolean(createTodoDisabledReason)}
          onClick={() => onCreateTodoCandidate?.(savedView.savedViewId)}
          aria-label="Create follow-up todo candidate"
          title={createTodoDisabledReason ??
            "Create local follow-up todo candidate"}
        >
          Create todo
        </button>
      )}
      {authoringEnabled && onArchive && savedView.status !== "archived" && (
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
    <div
      class="md-viewer-rendered-markdown"
      aria-label="Rendered Markdown content"
    >
      {renderMarkdownToVNodes(markdown)}
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
  onRebind,
  onOpenSourceRecord,
  onCreateTodoCandidate,
  disabledActionReasons,
  authoringEnabled = true,
}: MdViewerProps) {
  const [copyStatus, setCopyStatus] = useState<"idle" | "copied" | "error">(
    "idle",
  );

  const handleCopyMarkdown = () => {
    if (typeof globalThis.navigator?.clipboard?.writeText === "function") {
      globalThis.navigator.clipboard.writeText(savedView.renderedMarkdown)
        .then(() => {
          setCopyStatus("copied");
          globalThis.setTimeout(() => setCopyStatus("idle"), 2000);
        })
        .catch(() => {
          setCopyStatus("error");
          globalThis.setTimeout(() => setCopyStatus("idle"), 3000);
        });
    } else {
      setCopyStatus("error");
      globalThis.setTimeout(() => setCopyStatus("idle"), 3000);
    }
  };

  return (
    <div
      class="md-viewer-panel"
      role="dialog"
      aria-modal="true"
      aria-label={`Saved view: ${savedView.title}`}
    >
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
          <strong>Source:</strong> {savedView.sourceTableRef} /{" "}
          {savedView.sourceRecordRef}
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
        onRebind={onRebind}
        onOpenSourceRecord={onOpenSourceRecord}
        onCreateTodoCandidate={onCreateTodoCandidate}
        onCopyMarkdown={handleCopyMarkdown}
        copyStatus={copyStatus}
        disabledActionReasons={disabledActionReasons}
        authoringEnabled={authoringEnabled}
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
