/**
 * MdTranslationAuthoringSeedSurface — Registry-driven md translation authoring surface.
 * SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
 *
 * Responsibilities:
 *   - Registry-driven template selection (from template:list API)
 *   - Explicit manual fallback for source_table_ref / source_record_ref
 *     (physical table listing not available via current team_markdown API)
 *   - Per-placeholder binding editor using existing component bucket parts
 *   - Required / optional / explicit_optional_empty / unresolved_required state
 *   - Blocks create/update when required placeholders are unresolved
 *   - Seed assembly via buildMdTranslationAuthoringSeedCandidate
 *   - Save via createSavedView / updateSavedView team_markdown API
 *
 * Prohibited:
 *   - AI inference for binding decisions
 *   - Markdown body parsing for binding
 *   - Direct DB write from frontend
 *   - Silent fallback for unresolved required placeholders
 *   - Mutating UIBuilder canvas/package edit root
 */

import type { JSX } from "preact";
import { useCallback, useEffect, useState } from "preact/hooks";
import {
  createSavedView,
  createTemplate,
  getTemplate,
  listTemplates,
  type TemplateListItem,
} from "../api/teamMarkdownApi.ts";
import {
  buildMdTranslationAuthoringSeedCandidate,
  extractPlaceholderKeys,
  type PlaceholderBindingEntry,
  simpleContentHash,
} from "../lib/mdTranslationSeedBuilder.ts";
import type { SelectOption } from "./Select.tsx";
import { Select } from "./Select.tsx";

// ─── Binding source kind options ──────────────────────────────────────────────

const BINDING_SOURCE_KIND_OPTIONS: SelectOption[] = [
  { value: "", label: "— select source type —" },
  { value: "physical_table_column", label: "Physical table column" },
  { value: "physical_table_jsonb_path", label: "Physical table JSONB path" },
  { value: "saved_query_result_field", label: "Saved query result field" },
  { value: "static_text", label: "Static text" },
];

// ─── Single placeholder binding row ──────────────────────────────────────────

function PlaceholderBindingRow({
  entry,
  onChange,
}: {
  entry: PlaceholderBindingEntry;
  onChange: (updated: PlaceholderBindingEntry) => void;
}): JSX.Element {
  const isUnresolved = entry.required && !entry.sourceKind && !entry.fieldRef;
  const isOptionalEmpty = !entry.required && !entry.sourceKind && !entry.fieldRef;

  return (
    <div
      class="mb-3 rounded border border-gray-200 bg-gray-50 p-3"
      data-placeholder-key={entry.placeholderKey}
      data-binding-required={String(entry.required)}
      data-binding-state={
        isUnresolved
          ? "unresolved_required"
          : isOptionalEmpty
          ? "explicit_optional_empty"
          : "bound"
      }
    >
      <div class="mb-1 flex items-center gap-2">
        <span class="font-mono text-xs text-gray-700">
          {"{{"}
          {entry.placeholderKey}
          {"}}"}
        </span>
        {isUnresolved && (
          <span
            class="rounded bg-red-100 px-1 py-0.5 text-xs font-semibold text-red-700"
            role="alert"
          >
            UNRESOLVED_REQUIRED — save blocked
          </span>
        )}
        {isOptionalEmpty && (
          <span class="rounded bg-yellow-50 px-1 py-0.5 text-xs text-yellow-700">
            explicit_optional_empty
          </span>
        )}
        {!isUnresolved && !isOptionalEmpty && (
          <span class="rounded bg-green-50 px-1 py-0.5 text-xs text-green-700">
            bound
          </span>
        )}
      </div>

      <div class="mb-2 flex items-center gap-2">
        <label class="text-xs text-gray-600" for={`sk-${entry.placeholderKey}`}>
          Source kind:
        </label>
        <Select
          value={entry.sourceKind}
          options={BINDING_SOURCE_KIND_OPTIONS}
          onChange={(v) =>
            onChange({
              ...entry,
              sourceKind: v as PlaceholderBindingEntry["sourceKind"],
            })
          }
          placeholder="select source type"
        />
      </div>

      <div class="mb-2 flex items-center gap-2">
        <label class="text-xs text-gray-600" for={`fr-${entry.placeholderKey}`}>
          Field ref:
        </label>
        <input
          id={`fr-${entry.placeholderKey}`}
          type="text"
          class="flex-1 rounded border border-gray-300 px-2 py-1 text-xs"
          value={entry.fieldRef}
          onInput={(e) =>
            onChange({
              ...entry,
              fieldRef: (e.target as HTMLInputElement).value,
            })
          }
          placeholder={
            entry.sourceKind === "static_text"
              ? "static text value"
              : entry.sourceKind === "physical_table_jsonb_path"
              ? "jsonb path e.g. data->summary"
              : "column or field name"
          }
          aria-label={`Field ref for ${entry.placeholderKey}`}
        />
      </div>

      <div class="flex items-center gap-2">
        <label class="flex items-center gap-1 text-xs text-gray-600">
          <input
            type="checkbox"
            checked={entry.required}
            onChange={(e) =>
              onChange({
                ...entry,
                required: (e.target as HTMLInputElement).checked,
              })
            }
            aria-label={`Mark ${entry.placeholderKey} as required`}
          />
          Required (unresolved blocks save)
        </label>
      </div>
    </div>
  );
}

// ─── Template registration form ───────────────────────────────────────────────

type TemplateFormState = {
  templateKey: string;
  templateLabel: string;
  templateMarkdown: string;
};

function MarkdownTemplateRegistryForm({
  onCreated,
  onCancel,
}: {
  onCreated: (templateId: string, templateKey: string) => void;
  onCancel: () => void;
}): JSX.Element {
  const [form, setForm] = useState<TemplateFormState>({
    templateKey: "",
    templateLabel: "",
    templateMarkdown: "",
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    if (!form.templateKey.trim() || !form.templateLabel.trim()) {
      setError("Template key and label are required");
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const result = await createTemplate(
        form.templateKey.trim(),
        form.templateLabel.trim(),
        form.templateMarkdown,
        { placeholders: extractPlaceholderKeysFromMarkdown(form.templateMarkdown) },
      );
      if (!result.ok || !result.templateId) {
        throw new Error("[TEMPLATE_CREATE_FAILED] Template creation returned ok=false");
      }
      onCreated(result.templateId, result.templateKey ?? form.templateKey.trim());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Template creation failed");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form
      class="rounded border border-blue-100 bg-blue-50 p-3"
      onSubmit={handleSubmit}
      aria-label="Register new Markdown template"
    >
      <h4 class="mb-2 text-sm font-semibold text-blue-900">
        Register new Markdown template
      </h4>
      {error && (
        <div class="mb-2 rounded bg-red-50 p-2 text-xs text-red-700" role="alert">
          {error}
        </div>
      )}
      <div class="mb-2">
        <label class="mb-0.5 block text-xs text-gray-600">
          Template key (unique identifier)
        </label>
        <input
          type="text"
          class="w-full rounded border border-gray-300 px-2 py-1 text-xs"
          value={form.templateKey}
          onInput={(e) =>
            setForm({ ...form, templateKey: (e.target as HTMLInputElement).value })
          }
          placeholder="e.g. work_log_weekly"
          required
        />
      </div>
      <div class="mb-2">
        <label class="mb-0.5 block text-xs text-gray-600">Template label</label>
        <input
          type="text"
          class="w-full rounded border border-gray-300 px-2 py-1 text-xs"
          value={form.templateLabel}
          onInput={(e) =>
            setForm({
              ...form,
              templateLabel: (e.target as HTMLInputElement).value,
            })
          }
          placeholder="e.g. Weekly Work Log"
          required
        />
      </div>
      <div class="mb-2">
        <label class="mb-0.5 block text-xs text-gray-600">
          Template Markdown (use {"{{placeholder}}"} syntax)
        </label>
        <textarea
          class="w-full rounded border border-gray-300 px-2 py-1 text-xs font-mono"
          rows={6}
          value={form.templateMarkdown}
          onInput={(e) =>
            setForm({
              ...form,
              templateMarkdown: (e.target as HTMLTextAreaElement).value,
            })
          }
          placeholder={"# {{record.summary}}\n\n## 説明\n{{record.description}}"}
          aria-label="Template Markdown body"
        />
        <p class="mt-0.5 text-xs text-gray-500">
          Placeholder keys will be extracted from {"{{key}}"} patterns. Markdown body is not runtime SSOT.
        </p>
      </div>
      <div class="flex gap-2">
        <button
          type="submit"
          class="rounded bg-blue-600 px-3 py-1 text-xs text-white hover:bg-blue-700 disabled:opacity-50"
          disabled={submitting}
        >
          {submitting ? "Registering…" : "Register template"}
        </button>
        <button
          type="button"
          class="rounded border border-gray-300 px-3 py-1 text-xs hover:bg-gray-100"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
    </form>
  );
}

/** Extract {{key}} placeholders from Markdown text (used by template registration form). */
function extractPlaceholderKeysFromMarkdown(markdown: string): string[] {
  const matches = markdown.matchAll(/\{\{([^}]+)\}\}/g);
  const keys: string[] = [];
  const seen = new Set<string>();
  for (const match of matches) {
    const key = match[1].trim();
    if (key && !seen.has(key)) {
      seen.add(key);
      keys.push(key);
    }
  }
  return keys;
}

// ─── Main authoring surface ───────────────────────────────────────────────────

export type MdTranslationAuthoringSeedSurfaceProps = {
  onSaved?: (savedViewId: string) => void;
  onCancel?: () => void;
  placement?: "admin_route" | "ui_builder_child_surface";
};

export function MdTranslationAuthoringSeedSurface({
  onSaved,
  onCancel,
  placement = "admin_route",
}: MdTranslationAuthoringSeedSurfaceProps): JSX.Element {
  const [templates, setTemplates] = useState<TemplateListItem[]>([]);
  const [loadingTemplates, setLoadingTemplates] = useState(false);
  const [templateLoadError, setTemplateLoadError] = useState<string | null>(null);
  const [showTemplateForm, setShowTemplateForm] = useState(false);

  const [selectedTemplateId, setSelectedTemplateId] = useState("");
  const [selectedTemplateKey, setSelectedTemplateKey] = useState("");
  const [placeholderKeys, setPlaceholderKeys] = useState<string[]>([]);
  const [loadingTemplate, setLoadingTemplate] = useState(false);

  // source refs — explicit manual input (physical table registry not available via current API)
  const [sourceTableRef, setSourceTableRef] = useState("");
  const [sourceRecordRef, setSourceRecordRef] = useState("");

  const [bindingEntries, setBindingEntries] = useState<PlaceholderBindingEntry[]>([]);
  const [title, setTitle] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [savedViewId, setSavedViewId] = useState<string | null>(null);

  const loadTemplates = useCallback(async () => {
    setLoadingTemplates(true);
    setTemplateLoadError(null);
    try {
      const result = await listTemplates("active");
      if (!result.ok || !Array.isArray(result.templates)) {
        throw new Error("[TEMPLATE_LIST_FAILED] Could not load templates from registry");
      }
      setTemplates(result.templates);
    } catch (err) {
      setTemplateLoadError(
        err instanceof Error ? err.message : "Template list load failed",
      );
    } finally {
      setLoadingTemplates(false);
    }
  }, []);

  useEffect(() => {
    loadTemplates();
  }, []);

  const handleTemplateSelect = useCallback(async (templateId: string) => {
    setSelectedTemplateId(templateId);
    setPlaceholderKeys([]);
    setBindingEntries([]);
    if (!templateId) {
      setSelectedTemplateKey("");
      return;
    }
    setLoadingTemplate(true);
    try {
      const result = await getTemplate(templateId);
      if (!result.ok || !result.template) {
        throw new Error("[TEMPLATE_GET_FAILED] Could not load template detail");
      }
      const t = result.template;
      setSelectedTemplateKey(t.templateKey);
      const keys = extractPlaceholderKeys(
        t.placeholderSchemaJson as Record<string, unknown>,
      );
      setPlaceholderKeys(keys);
      setBindingEntries(
        keys.map((key) => ({
          placeholderKey: key,
          sourceKind: "",
          fieldRef: "",
          required: true,
        })),
      );
    } catch (err) {
      setTemplateLoadError(
        err instanceof Error ? err.message : "Template load failed",
      );
    } finally {
      setLoadingTemplate(false);
    }
  }, []);

  const handleTemplateCreated = useCallback(
    async (newTemplateId: string, newTemplateKey: string) => {
      setShowTemplateForm(false);
      await loadTemplates();
      await handleTemplateSelect(newTemplateId);
      setSelectedTemplateKey(newTemplateKey);
    },
    [loadTemplates, handleTemplateSelect],
  );

  const updateBinding = useCallback(
    (index: number, updated: PlaceholderBindingEntry) => {
      setBindingEntries((prev) => {
        const next = [...prev];
        next[index] = updated;
        return next;
      });
    },
    [],
  );

  // Compute seed candidate and unresolved required keys live
  const seedResult =
    selectedTemplateId && title.trim() && sourceTableRef.trim()
      ? buildMdTranslationAuthoringSeedCandidate({
          templateId: selectedTemplateId,
          templateKey: selectedTemplateKey,
          sourceTableRef: sourceTableRef.trim(),
          sourceRecordRef: sourceRecordRef.trim(),
          bindingEntries,
          renderedMarkdown: "",
          renderedMarkdownHash: simpleContentHash(
            selectedTemplateId + sourceTableRef + sourceRecordRef + title,
          ),
          title: title.trim(),
          excerpt: title.trim().slice(0, 80),
        })
      : null;

  const unresolvedRequired = seedResult?.unresolvedRequiredKeys ?? [];
  const canSave =
    Boolean(selectedTemplateId) &&
    Boolean(title.trim()) &&
    Boolean(sourceTableRef.trim()) &&
    unresolvedRequired.length === 0 &&
    !submitting;

  const handleSave = async (e: Event) => {
    e.preventDefault();
    if (!canSave || !seedResult) return;
    setSubmitting(true);
    setSaveError(null);
    try {
      const result = await createSavedView({
        templateId: selectedTemplateId,
        title: title.trim(),
        sourceTableRef: sourceTableRef.trim(),
        sourceRecordRef: sourceRecordRef.trim(),
        bindingJson: seedResult.candidate.binding_ref as Record<string, unknown>,
        completedPresetSeedJson: seedResult.candidate,
        renderedMarkdown: "",
        searchIndexText: title.trim(),
        cardMetadataJson: {
          title: title.trim(),
          excerpt: title.trim().slice(0, 80),
          tags: [],
        },
      });
      if (!result.ok || !result.savedViewId) {
        throw new Error(
          "[SAVED_VIEW_CREATE_FAILED] Saved view creation returned ok=false",
        );
      }
      setSavedViewId(result.savedViewId);
      onSaved?.(result.savedViewId);
    } catch (err) {
      setSaveError(
        err instanceof Error ? err.message : "Saved view creation failed",
      );
    } finally {
      setSubmitting(false);
    }
  };

  const templateSelectOptions: SelectOption[] = [
    { value: "", label: "— select template —" },
    ...templates.map((t) => ({
      value: t.templateId,
      label: `${t.templateLabel} (${t.templateKey})`,
    })),
  ];

  if (savedViewId) {
    return (
      <div
        class="rounded border border-green-200 bg-green-50 p-4"
        role="status"
        aria-live="polite"
        data-authoring-state="saved"
      >
        <p class="text-sm text-green-800">
          Saved view created: <code class="font-mono text-xs">{savedViewId}</code>
        </p>
        <button
          type="button"
          class="mt-2 rounded border border-green-300 px-3 py-1 text-xs hover:bg-green-100"
          onClick={() => {
            setSavedViewId(null);
            setSelectedTemplateId("");
            setSelectedTemplateKey("");
            setPlaceholderKeys([]);
            setBindingEntries([]);
            setTitle("");
            setSourceTableRef("");
            setSourceRecordRef("");
          }}
        >
          Create another
        </button>
      </div>
    );
  }

  return (
    <div
      class="md-translation-authoring-surface rounded border border-gray-200 bg-white p-4"
      data-authoring-surface="md_translation"
      data-placement={placement}
    >
      <div class="mb-3 flex items-start justify-between">
        <div>
          <h3 class="text-sm font-semibold text-gray-800">
            Create saved Markdown view
          </h3>
          <p class="mt-0.5 text-xs text-gray-500">
            Registry-driven template selection; source ref uses explicit manual input
            (physical table listing not available in this bundle).
            All bindings are user-selected — no AI inference.
          </p>
        </div>
        {onCancel && (
          <button
            type="button"
            class="ml-2 text-xs text-gray-400 hover:text-gray-600"
            onClick={onCancel}
            aria-label="Cancel authoring"
          >
            ✕
          </button>
        )}
      </div>

      {/* ── Template selection (registry-driven) ── */}
      <section class="mb-4" aria-label="Template selection">
        <div class="mb-1 flex items-center justify-between">
          <label class="text-xs font-medium text-gray-700">
            Template (registry-driven selection)
          </label>
          <button
            type="button"
            class="text-xs text-blue-600 underline hover:text-blue-800"
            onClick={() => setShowTemplateForm((v) => !v)}
          >
            {showTemplateForm ? "Cancel new template" : "+ Register new template"}
          </button>
        </div>

        {loadingTemplates && (
          <p class="text-xs text-gray-400">Loading templates from registry…</p>
        )}
        {templateLoadError && (
          <div
            class="rounded bg-red-50 p-2 text-xs text-red-700"
            role="alert"
          >
            {templateLoadError}
          </div>
        )}

        {showTemplateForm ? (
          <MarkdownTemplateRegistryForm
            onCreated={handleTemplateCreated}
            onCancel={() => setShowTemplateForm(false)}
          />
        ) : (
          <Select
            value={selectedTemplateId}
            options={templateSelectOptions}
            onChange={handleTemplateSelect}
            placeholder="Select a template"
            disabled={loadingTemplates || loadingTemplate}
          />
        )}

        {loadingTemplate && (
          <p class="mt-1 text-xs text-gray-400">Loading template detail…</p>
        )}
      </section>

      {/* ── Source refs (explicit manual fallback) ── */}
      <section class="mb-4" aria-label="Source reference inputs">
        <p class="mb-1 text-xs font-medium text-gray-700">
          Source reference{" "}
          <span class="font-normal text-gray-400">
            (manual input — physical table registry not available via current team_markdown API)
          </span>
        </p>
        <div class="mb-2">
          <label class="mb-0.5 block text-xs text-gray-600">
            Source table ref
          </label>
          <input
            type="text"
            class="w-full rounded border border-gray-300 px-2 py-1 text-xs"
            value={sourceTableRef}
            onInput={(e) =>
              setSourceTableRef((e.target as HTMLInputElement).value)
            }
            placeholder="e.g. topology.physical_tables"
            aria-label="Source table ref"
          />
        </div>
        <div class="mb-2">
          <label class="mb-0.5 block text-xs text-gray-600">
            Source record ref
          </label>
          <input
            type="text"
            class="w-full rounded border border-gray-300 px-2 py-1 text-xs"
            value={sourceRecordRef}
            onInput={(e) =>
              setSourceRecordRef((e.target as HTMLInputElement).value)
            }
            placeholder="e.g. record primary key or locator"
            aria-label="Source record ref"
          />
        </div>
      </section>

      {/* ── Placeholder binding editor (component bucket driven) ── */}
      {placeholderKeys.length > 0 && (
        <section class="mb-4" aria-label="Placeholder binding editor">
          <p class="mb-2 text-xs font-medium text-gray-700">
            Placeholder bindings{" "}
            <span class="font-normal text-gray-400">
              (user-selected — no AI inference; required unresolved blocks save)
            </span>
          </p>
          {bindingEntries.map((entry, i) => (
            <PlaceholderBindingRow
              key={entry.placeholderKey}
              entry={entry}
              onChange={(updated) => updateBinding(i, updated)}
            />
          ))}
        </section>
      )}

      {/* ── View title ── */}
      {selectedTemplateId && (
        <section class="mb-4" aria-label="View title">
          <label class="mb-0.5 block text-xs font-medium text-gray-700">
            Saved view title
          </label>
          <input
            type="text"
            class="w-full rounded border border-gray-300 px-2 py-1 text-xs"
            value={title}
            onInput={(e) => setTitle((e.target as HTMLInputElement).value)}
            placeholder="Enter a title for this saved view"
            aria-label="Saved view title"
          />
        </section>
      )}

      {/* ── Unresolved required placeholder warning ── */}
      {unresolvedRequired.length > 0 && (
        <div
          class="mb-3 rounded border border-red-200 bg-red-50 p-2"
          role="alert"
          aria-live="assertive"
          data-gate="REQUIRED_PLACEHOLDER_UNBOUND"
        >
          <p class="text-xs font-semibold text-red-700">
            Save blocked: required placeholder(s) unresolved
          </p>
          <ul class="mt-1 list-inside list-disc text-xs text-red-600">
            {unresolvedRequired.map((k) => (
              <li key={k}>
                <code class="font-mono">{k}</code>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* ── Save error ── */}
      {saveError && (
        <div
          class="mb-3 rounded bg-red-50 p-2 text-xs text-red-700"
          role="alert"
        >
          {saveError}
        </div>
      )}

      {/* ── Save button ── */}
      {selectedTemplateId && (
        <div class="flex gap-2">
          <button
            type="button"
            class="rounded bg-indigo-600 px-4 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-50"
            disabled={!canSave}
            onClick={handleSave}
            aria-disabled={!canSave}
            aria-describedby={
              unresolvedRequired.length > 0 ? "unresolved-notice" : undefined
            }
          >
            {submitting ? "Saving…" : "Save Markdown view"}
          </button>
          {onCancel && (
            <button
              type="button"
              class="rounded border border-gray-300 px-4 py-1.5 text-xs hover:bg-gray-100"
              onClick={onCancel}
            >
              Cancel
            </button>
          )}
        </div>
      )}
    </div>
  );
}
