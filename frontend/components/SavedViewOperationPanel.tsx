/**
 * SavedViewOperationPanel — real Refresh / Clone / Rebind implementation for TeamMarkdownAuthoring,
 * replacing the previous stubNotice() placeholders. Backed by the existing, already-implemented
 * backend actions (team_markdown:saved_view:refresh/clone/rebind — AdminRuntime.TeamMarkdown.cs).
 *
 * None of these three backend actions expose a non-mutating dryRun mode (unlike saved_view:update),
 * so the preview step here is a client-side, non-mutating review of the exact payload that is about
 * to be sent — never a backend call — followed by an explicit confirm (ApplyConfirmDialog) before
 * the real write. Admin-only: this must only be mounted for admin-role sessions; the backend also
 * independently rejects non-admin JWTs on every team_markdown mutation action regardless of what
 * renders here.
 */
import { useState } from "preact/hooks";
import {
  cloneSavedView,
  rebindSavedView,
  refreshSavedViewFromSource,
  type SavedViewDetail,
} from "../api/teamMarkdownApi.ts";
import { ApplyConfirmDialog } from "./ApplyConfirmDialog.tsx";

export type SavedViewOperationMode = "refresh" | "clone" | "rebind";

type Step = "editing" | "previewed" | "written";

function parseJsonField(
  text: string,
  label: string,
): { ok: true; value: Record<string, unknown> } | { ok: false; error: string } {
  try {
    const value = JSON.parse(text || "{}");
    if (typeof value !== "object" || value === null || Array.isArray(value)) {
      return { ok: false, error: `${label} must be a JSON object` };
    }
    return { ok: true, value: value as Record<string, unknown> };
  } catch {
    return { ok: false, error: `${label} must be valid JSON` };
  }
}

export function SavedViewOperationPanel({
  mode,
  savedView,
  onWritten,
  onCancel,
}: {
  mode: SavedViewOperationMode;
  savedView: SavedViewDetail;
  onWritten: () => void;
  onCancel: () => void;
}) {
  const [templateMarkdown, setTemplateMarkdown] = useState(savedView.renderedMarkdown);
  const [sourceRecordJsonText, setSourceRecordJsonText] = useState("{}");
  const [searchIndexText, setSearchIndexText] = useState(savedView.searchIndexText);
  const [title, setTitle] = useState(savedView.title);
  const [targetSourceTableRef, setTargetSourceTableRef] = useState("");
  const [targetSourceRecordRef, setTargetSourceRecordRef] = useState("");
  const [bindingJsonText, setBindingJsonText] = useState(
    JSON.stringify(savedView.bindingJson ?? {}, null, 2),
  );
  const [completedPresetSeedJsonText, setCompletedPresetSeedJsonText] = useState(
    JSON.stringify(savedView.completedPresetSeedJson, null, 2),
  );

  const [step, setStep] = useState<Step>("editing");
  const [previewPayload, setPreviewPayload] = useState<Record<string, unknown> | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [writtenSavedViewId, setWrittenSavedViewId] = useState<string | null>(null);

  const resetStep = () => {
    setStep("editing");
    setPreviewPayload(null);
  };

  const buildPayload = ():
    | { ok: true; payload: Record<string, unknown> }
    | { ok: false; error: string } => {
    const sourceRecord = parseJsonField(sourceRecordJsonText, "sourceRecordJson");
    if (!sourceRecord.ok) return sourceRecord;

    if (mode === "refresh") {
      return {
        ok: true,
        payload: { templateMarkdown, sourceRecordJson: sourceRecord.value, searchIndexText },
      };
    }

    if (mode === "clone") {
      if (!targetSourceTableRef.trim()) return { ok: false, error: "targetSourceTableRef is required" };
      if (!targetSourceRecordRef.trim()) return { ok: false, error: "targetSourceRecordRef is required" };
      return {
        ok: true,
        payload: {
          targetSourceTableRef: targetSourceTableRef.trim(),
          targetSourceRecordRef: targetSourceRecordRef.trim(),
          templateMarkdown,
          sourceRecordJson: sourceRecord.value,
          title,
          searchIndexText,
        },
      };
    }

    // rebind
    const binding = parseJsonField(bindingJsonText, "bindingJson");
    if (!binding.ok) return binding;
    const seed = parseJsonField(completedPresetSeedJsonText, "completedPresetSeedJson");
    if (!seed.ok) return seed;
    return {
      ok: true,
      payload: {
        bindingJson: binding.value,
        completedPresetSeedJson: seed.value,
        templateMarkdown,
        sourceRecordJson: sourceRecord.value,
        searchIndexText,
      },
    };
  };

  const handlePreview = () => {
    setError(null);
    const result = buildPayload();
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setPreviewPayload(result.payload);
    setStep("previewed");
  };

  const handleWrite = async () => {
    const result = buildPayload();
    if (!result.ok) {
      setError(result.error);
      setConfirmOpen(false);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      let writtenId: string;
      if (mode === "refresh") {
        const p = result.payload as {
          templateMarkdown: string;
          sourceRecordJson: Record<string, unknown>;
          searchIndexText?: string;
        };
        const res = await refreshSavedViewFromSource(
          savedView.savedViewId,
          p.templateMarkdown,
          p.sourceRecordJson,
          p.searchIndexText,
        );
        writtenId = res.savedViewId;
      } else if (mode === "clone") {
        const p = result.payload as {
          targetSourceTableRef: string;
          targetSourceRecordRef: string;
          templateMarkdown: string;
          sourceRecordJson: Record<string, unknown>;
          title?: string;
          searchIndexText?: string;
        };
        const res = await cloneSavedView(savedView.savedViewId, p);
        writtenId = res.savedViewId;
      } else {
        const p = result.payload as {
          bindingJson: Record<string, unknown>;
          completedPresetSeedJson: Record<string, unknown>;
          templateMarkdown: string;
          sourceRecordJson: Record<string, unknown>;
          searchIndexText?: string;
        };
        const res = await rebindSavedView(savedView.savedViewId, {
          bindingJson: p.bindingJson,
          // deno-lint-ignore no-explicit-any
          completedPresetSeedJson: p.completedPresetSeedJson as any,
          templateMarkdown: p.templateMarkdown,
          sourceRecordJson: p.sourceRecordJson,
          searchIndexText: p.searchIndexText,
        });
        writtenId = res.savedViewId;
      }
      setWrittenSavedViewId(writtenId);
      setStep("written");
      setConfirmOpen(false);
      onWritten();
    } catch (err) {
      setError(err instanceof Error ? err.message : `${mode} failed`);
      setConfirmOpen(false);
    } finally {
      setBusy(false);
    }
  };

  const heading = mode === "refresh"
    ? "Refresh saved view from source"
    : mode === "clone"
    ? "Clone saved view to a new target record"
    : "Rebind saved view to a new binding";

  return (
    <section class="md-dashboard-authoring-surface" aria-label={`Saved view ${mode}`}>
      <h3>{heading} (preview → confirm → write)</h3>

      {mode === "clone" && (
        <>
          <label class="md-dashboard-authoring-block">
            Target source table ref
            <input
              value={targetSourceTableRef}
              onInput={(e) => {
                setTargetSourceTableRef((e.target as HTMLInputElement).value);
                resetStep();
              }}
            />
          </label>
          <label class="md-dashboard-authoring-block">
            Target source record ref
            <input
              value={targetSourceRecordRef}
              onInput={(e) => {
                setTargetSourceRecordRef((e.target as HTMLInputElement).value);
                resetStep();
              }}
            />
          </label>
          <label class="md-dashboard-authoring-block">
            Title
            <input
              value={title}
              onInput={(e) => {
                setTitle((e.target as HTMLInputElement).value);
                resetStep();
              }}
            />
          </label>
        </>
      )}

      {mode === "rebind" && (
        <>
          <label class="md-dashboard-authoring-block">
            Binding (JSON) — user-selected, not inferred
            <textarea
              rows={6}
              value={bindingJsonText}
              onInput={(e) => {
                setBindingJsonText((e.target as HTMLTextAreaElement).value);
                resetStep();
              }}
            />
          </label>
          <label class="md-dashboard-authoring-block">
            Completed preset seed (JSON)
            <textarea
              rows={8}
              value={completedPresetSeedJsonText}
              onInput={(e) => {
                setCompletedPresetSeedJsonText((e.target as HTMLTextAreaElement).value);
                resetStep();
              }}
            />
          </label>
        </>
      )}

      <label class="md-dashboard-authoring-block">
        Template markdown
        <textarea
          rows={6}
          value={templateMarkdown}
          onInput={(e) => {
            setTemplateMarkdown((e.target as HTMLTextAreaElement).value);
            resetStep();
          }}
        />
      </label>
      <label class="md-dashboard-authoring-block">
        Source record (JSON)
        <textarea
          rows={6}
          value={sourceRecordJsonText}
          onInput={(e) => {
            setSourceRecordJsonText((e.target as HTMLTextAreaElement).value);
            resetStep();
          }}
        />
      </label>
      <label class="md-dashboard-authoring-block">
        Search index text
        <input
          value={searchIndexText}
          onInput={(e) => {
            setSearchIndexText((e.target as HTMLInputElement).value);
            resetStep();
          }}
        />
      </label>

      <div class="md-dashboard-authoring-actions">
        <button type="button" onClick={handlePreview} disabled={busy}>
          Preview
        </button>
        <button
          type="button"
          onClick={() => setConfirmOpen(true)}
          disabled={busy || step !== "previewed"}
        >
          Confirm {mode}…
        </button>
        <button type="button" onClick={onCancel} disabled={busy}>Cancel</button>
      </div>

      {previewPayload && (
        <pre class="md-dashboard-preview-json" aria-label="Payload preview (non-mutating)">
          {JSON.stringify(previewPayload, null, 2)}
        </pre>
      )}
      {error && <div class="md-dashboard-error" role="alert">{error}</div>}
      {writtenSavedViewId && (
        <div class="md-dashboard-action-notice" role="status">
          {mode === "clone" ? "Cloned to" : "Written to"} saved view: {writtenSavedViewId}
        </div>
      )}

      <ApplyConfirmDialog
        open={confirmOpen}
        title={`Confirm ${mode}`}
        description="This will persist the change and its event evidence. This step cannot be skipped — write is rejected server-side without it."
        confirmLabel="Write"
        cancelLabel="Back"
        onConfirm={() => void handleWrite()}
        onCancel={() => setConfirmOpen(false)}
      />
    </section>
  );
}
