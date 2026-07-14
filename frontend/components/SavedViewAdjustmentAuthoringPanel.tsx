/**
 * SavedViewAdjustmentAuthoringPanel — Team Dashboard authoring workflow for editing a saved view's
 * title / rendered Markdown / user_adjustment_patch_json.
 *
 * Workflow (SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml
 * normal.dashboard.mutation_flow.required_order): preview -> validate -> explicit_confirm -> write
 * -> diff_log. preview/validate run through the same backend dryRun call (non-mutating — the
 * backend runs full validation and returns before any repository write); explicit_confirm is a
 * real confirmation dialog, not an implicit click; write only fires after that confirmation and
 * resends the identical input that was previewed; the backend re-validates that input again and
 * persists the mutation plus its diff-evidence event atomically (see
 * NpgsqlTeamMarkdownRepository.UpdateSavedViewWithDiffEvidenceAsync). Admin-only: this component
 * must only be mounted for admin-role sessions — the backend also independently rejects non-admin
 * JWTs on every team_markdown mutation action regardless of what the frontend renders.
 */
import { useState } from "preact/hooks";
import {
  previewSavedViewUpdate,
  writeSavedViewUpdate,
  type SavedViewDetail,
  type SavedViewUpdateInput,
} from "../api/teamMarkdownApi.ts";
import { PatchPreviewPanel, type PatchField } from "./PatchPreviewPanel.tsx";
import { ValidationResultPanel, type ValidationResultPayload } from "./ValidationResultPanel.tsx";
import { ApplyConfirmDialog } from "./ApplyConfirmDialog.tsx";

type Step = "editing" | "previewed" | "confirming" | "written";

export function SavedViewAdjustmentAuthoringPanel({
  savedView,
  onWritten,
  onCancel,
}: {
  savedView: SavedViewDetail;
  onWritten: () => void;
  onCancel: () => void;
}) {
  const [title, setTitle] = useState(savedView.title);
  const [adjustmentText, setAdjustmentText] = useState(
    JSON.stringify(savedView.userAdjustmentPatchJson ?? {}, null, 2),
  );
  const [step, setStep] = useState<Step>("editing");
  const [preview, setPreview] = useState<{ title: string; renderedMarkdown: string } | null>(null);
  const [validation, setValidation] = useState<ValidationResultPayload | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [diffEventId, setDiffEventId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const buildInput = (): SavedViewUpdateInput | null => {
    let adjustmentJson: Record<string, unknown>;
    try {
      adjustmentJson = JSON.parse(adjustmentText || "{}");
    } catch {
      setError("[USER_ADJUSTMENT_PATCH_INVALID_JSON] adjustment patch must be valid JSON");
      return null;
    }
    return {
      title,
      completedPresetSeedJson: savedView.completedPresetSeedJson,
      userAdjustmentPatchJson: adjustmentJson,
    };
  };

  const handlePreview = async () => {
    setError(null);
    setDiffEventId(null);
    const input = buildInput();
    if (!input) return;
    setBusy(true);
    try {
      const result = await previewSavedViewUpdate(savedView.savedViewId, input);
      setPreview(result.preview);
      setValidation({
        overall: result.valid ? "pass" : "fail",
        items: [{
          key: "preview_validate",
          status: result.valid ? "pass" : "fail",
          label: "preview / validate (non-mutating)",
          message: result.valid ? "Backend validated this change without writing it." : "Validation failed.",
        }],
      });
      setStep("previewed");
    } catch (err) {
      setValidation({
        overall: "blocking",
        items: [{ key: "preview_validate", status: "blocking", label: "preview / validate", message: err instanceof Error ? err.message : String(err) }],
      });
      setError(err instanceof Error ? err.message : "Preview failed");
    } finally {
      setBusy(false);
    }
  };

  const handleWrite = async () => {
    const input = buildInput();
    if (!input) return;
    setBusy(true);
    setError(null);
    try {
      const result = await writeSavedViewUpdate(savedView.savedViewId, input);
      setDiffEventId(result.diffEventId);
      setStep("written");
      setConfirmOpen(false);
      onWritten();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Write failed");
      setConfirmOpen(false);
    } finally {
      setBusy(false);
    }
  };

  const patchFields: PatchField[] = preview
    ? [
      { fieldLabel: "title", before: savedView.title, after: preview.title },
      {
        fieldLabel: "renderedMarkdown",
        before: savedView.renderedMarkdown.slice(0, 80),
        after: preview.renderedMarkdown.slice(0, 80),
      },
    ]
    : [];

  return (
    <section class="md-dashboard-authoring-surface" aria-label="Saved view adjustment authoring">
      <h3>Edit saved view (preview → validate → confirm → write)</h3>
      <label class="md-dashboard-authoring-block">
        Title
        <input value={title} onInput={(e) => { setTitle((e.target as HTMLInputElement).value); setStep("editing"); }} />
      </label>
      <label class="md-dashboard-authoring-block">
        User adjustment patch (JSON)
        <textarea
          rows={6}
          value={adjustmentText}
          onInput={(e) => { setAdjustmentText((e.target as HTMLTextAreaElement).value); setStep("editing"); }}
        />
      </label>

      <div class="md-dashboard-authoring-actions">
        <button type="button" onClick={handlePreview} disabled={busy}>
          Preview / validate
        </button>
        <button
          type="button"
          onClick={() => setConfirmOpen(true)}
          disabled={busy || step !== "previewed" || validation?.overall !== "pass"}
        >
          Confirm write…
        </button>
        <button type="button" onClick={onCancel} disabled={busy}>Cancel</button>
      </div>

      {validation && <ValidationResultPanel result={validation} title="Validation result" />}
      {preview && <PatchPreviewPanel fields={patchFields} title="Diff preview (non-mutating)" />}
      {error && <div class="md-dashboard-error" role="alert">{error}</div>}
      {diffEventId && (
        <div class="md-dashboard-action-notice" role="status">
          Write committed. Diff evidence event: {diffEventId}
        </div>
      )}

      <ApplyConfirmDialog
        open={confirmOpen}
        title="Confirm write"
        description="This will persist the change and its diff-evidence event in one transaction. This step cannot be skipped — write is rejected server-side without it."
        confirmLabel="Write"
        cancelLabel="Back"
        onConfirm={() => void handleWrite()}
        onCancel={() => setConfirmOpen(false)}
      />
    </section>
  );
}
