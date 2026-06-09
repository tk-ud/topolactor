/**
 * MdTranslationAuthoringSeedSurface — Registry-driven md translation authoring surface.
 * SSOT: docs/design/team-markdown-dashboard-saved-view-ssot.yaml
 *
 * Responsibilities:
 *   - Registry-driven template selection (from template:list API)
 *   - Registry-driven source table ref selection (from listRelationshipRemoteTargets / manifest API)
 *   - Registry-driven column/field candidates for physical_table_column and physical_table_jsonb_path
 *   - Explicit manual fallback when registry unavailable — labeled, not silent
 *   - Explicit manual input for source_record_ref (no record enumeration API)
 *   - Explicit manual input for saved_query_result_field (no enumeration API)
 *   - Per-placeholder binding editor using existing component bucket parts
 *   - Required / optional / explicit_optional_empty / unresolved_required state
 *   - Blocks create/update when required placeholders are unresolved
 *   - Seed assembly via buildMdTranslationAuthoringSeedCandidate
 *   - Save via createSavedView team_markdown API
 *
 * Component bucket composition:
 *   - Select (existing select.template), input (input.primitive), textarea (textarea.template),
 *     button (button.primitive) — existing bucket parts only; no bespoke Modal/Drawer/Form created.
 *   - data-component-bucket-parts="select input textarea button existing_bucket_parts"
 *
 * Prohibited:
 *   - AI inference for binding decisions
 *   - Markdown body parsing for binding
 *   - Direct DB write from frontend
 *   - Silent fallback for unresolved required placeholders or unavailable registry
 *   - Mutating UIBuilder canvas/package edit root
 *   - Creating new physical table columns or mutating schema
 */

import type { JSX } from "preact";
import { useCallback, useEffect, useState } from "preact/hooks";
import {
  type RelationshipRemoteTarget,
  type RelationshipRemoteTargetTable,
  listRelationshipRemoteTargets,
} from "../api/adminApi.ts";
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

// ─── Registry table candidate types ──────────────────────────────────────────

type RegistryTableEntry = {
  tableName: string;
  columns: string[];
};

const MANUAL_VALUE = "__manual__";

function buildTableOptions(
  registryTables: RegistryTableEntry[],
  registryAvailable: boolean,
): SelectOption[] {
  if (!registryAvailable || registryTables.length === 0) return [];
  return [
    { value: "", label: "— select source table —" },
    ...registryTables.map((t) => ({ value: t.tableName, label: t.tableName })),
    { value: MANUAL_VALUE, label: "Other / enter manually" },
  ];
}

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
  availableColumns,
  onChange,
}: {
  entry: PlaceholderBindingEntry;
  availableColumns: string[];
  onChange: (updated: PlaceholderBindingEntry) => void;
}): JSX.Element {
  const isUnresolved = entry.required && !entry.sourceKind && !entry.fieldRef;
  const isOptionalEmpty = !entry.required && !entry.sourceKind && !entry.fieldRef;
  const datalistId = `cols-${entry.placeholderKey}`;

  const showColumnCandidates =
    availableColumns.length > 0 &&
    (entry.sourceKind === "physical_table_column" ||
      entry.sourceKind === "physical_table_jsonb_path");

  const fieldRefPlaceholder = (() => {
    if (entry.sourceKind === "static_text") return "static text value";
    if (entry.sourceKind === "physical_table_jsonb_path") {
      return showColumnCandidates
        ? "select or enter jsonb column / path"
        : "jsonb path e.g. data->summary";
    }
    if (entry.sourceKind === "saved_query_result_field") {
      return "saved query field ref (no enumeration API — enter manually)";
    }
    return showColumnCandidates ? "select or enter column name" : "column or field name";
  })();

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
      data-component-bucket-parts="select input existing_bucket_parts"
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
              fieldRef: "",
            })
          }
          placeholder="select source type"
        />
      </div>

      {entry.sourceKind === "saved_query_result_field" && (
        <p class="mb-1 text-xs text-amber-700">
          Saved query field ref — no saved query enumeration API available; enter field ref manually.
        </p>
      )}

      <div class="mb-2 flex items-center gap-2">
        <label class="text-xs text-gray-600" for={`fr-${entry.placeholderKey}`}>
          Field ref:
        </label>
        <input
          id={`fr-${entry.placeholderKey}`}
          type="text"
          list={showColumnCandidates ? datalistId : undefined}
          class="flex-1 rounded border border-gray-300 px-2 py-1 text-xs"
          value={entry.fieldRef}
          onInput={(e) =>
            onChange({
              ...entry,
              fieldRef: (e.target as HTMLInputElement).value,
            })
          }
          placeholder={fieldRefPlaceholder}
          aria-label={`Field ref for ${entry.placeholderKey}`}
        />
        {showColumnCandidates && (
          <datalist id={datalistId}>
            {availableColumns.map((col) => (
              <option key={col} value={col} />
            ))}
          </datalist>
        )}
      </div>

      {entry.sourceKind && !showColumnCandidates &&
        entry.sourceKind !== "static_text" &&
        entry.sourceKind !== "saved_query_result_field" && (
        <p class="mb-1 text-xs text-gray-400">
          No column candidates from registry for this source kind — enter field ref manually.
        </p>
      )}

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
      data-component-bucket-parts="input textarea button existing_bucket_parts"
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
            setForm({ ...form, templateLabel: (e.target as HTMLInputElement).value })
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
            setForm({ ...form, templateMarkdown: (e.target as HTMLTextAreaElement).value })
          }
          placeholder={"# {{record.summary}}\n\n## 説明\n{{record.description}}"}
          aria-label="Template Markdown body"
        />
        <p class="mt-0.5 text-xs text-gray-500">
          Placeholder keys extracted from {"{{key}}"} patterns. Markdown body is not runtime SSOT.
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

// ─── Registry candidate loader ────────────────────────────────────────────────

function extractRegistryTables(
  targets: RelationshipRemoteTarget[],
): RegistryTableEntry[] {
  const seen = new Set<string>();
  const result: RegistryTableEntry[] = [];
  for (const target of targets) {
    for (const table of (target.logicalTables ?? []) as RelationshipRemoteTargetTable[]) {
      if (!seen.has(table.tableName)) {
        seen.add(table.tableName);
        result.push({
          tableName: table.tableName,
          columns: (table.columns ?? []).map((c) => c.name).filter(Boolean),
        });
      }
    }
  }
  return result.sort((a, b) => a.tableName.localeCompare(b.tableName));
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
  // ── Template registry ──
  const [templates, setTemplates] = useState<TemplateListItem[]>([]);
  const [loadingTemplates, setLoadingTemplates] = useState(false);
  const [templateLoadError, setTemplateLoadError] = useState<string | null>(null);
  const [showTemplateForm, setShowTemplateForm] = useState(false);
  const [selectedTemplateId, setSelectedTemplateId] = useState("");
  const [selectedTemplateKey, setSelectedTemplateKey] = useState("");
  const [placeholderKeys, setPlaceholderKeys] = useState<string[]>([]);
  const [loadingTemplate, setLoadingTemplate] = useState(false);

  // ── Source table registry (from manifest/relationship API) ──
  const [registryTables, setRegistryTables] = useState<RegistryTableEntry[]>([]);
  const [registryLoading, setRegistryLoading] = useState(false);
  const [registryError, setRegistryError] = useState<string | null>(null);
  // null = not loaded yet; false = load failed; true = loaded OK (may be empty)
  const [registryAvailable, setRegistryAvailable] = useState<boolean | null>(null);

  // ── Source ref selection ──
  // selectedTableValue: registry table name, MANUAL_VALUE, or "" (unselected)
  const [selectedTableValue, setSelectedTableValue] = useState("");
  // manualTableRef: used when MANUAL_VALUE selected or registry unavailable
  const [manualTableRef, setManualTableRef] = useState("");
  const [sourceRecordRef, setSourceRecordRef] = useState("");

  // ── Binding entries ──
  const [bindingEntries, setBindingEntries] = useState<PlaceholderBindingEntry[]>([]);
  const [title, setTitle] = useState("");

  // ── Save state ──
  const [submitting, setSubmitting] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [savedViewId, setSavedViewId] = useState<string | null>(null);

  // Resolved source table ref (from registry selection or manual)
  const sourceTableRef =
    selectedTableValue === MANUAL_VALUE || registryAvailable === false
      ? manualTableRef
      : selectedTableValue;

  // Columns available for the currently selected registry table
  const selectedTableColumns: string[] =
    selectedTableValue && selectedTableValue !== MANUAL_VALUE
      ? (registryTables.find((t) => t.tableName === selectedTableValue)?.columns ?? [])
      : [];

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
      setTemplateLoadError(err instanceof Error ? err.message : "Template list load failed");
    } finally {
      setLoadingTemplates(false);
    }
  }, []);

  const loadRegistryTables = useCallback(async () => {
    setRegistryLoading(true);
    setRegistryError(null);
    try {
      const targets = await listRelationshipRemoteTargets();
      if (targets === null) {
        // Registry returned null — backend not configured or no manifests
        setRegistryAvailable(false);
        setRegistryTables([]);
        setRegistryError(
          "physical table registry not available (manifest API returned null — no active manifests or backend not configured); enter source table ref manually",
        );
      } else {
        const tables = extractRegistryTables(targets);
        setRegistryTables(tables);
        setRegistryAvailable(tables.length > 0);
        if (tables.length === 0) {
          setRegistryError(
            "physical table registry not available (no logical tables found in active manifests); enter source table ref manually",
          );
        }
      }
    } catch (err) {
      setRegistryAvailable(false);
      setRegistryError(
        `physical table registry not available (${err instanceof Error ? err.message : "load failed"}); enter source table ref manually`,
      );
    } finally {
      setRegistryLoading(false);
    }
  }, []);

  useEffect(() => {
    loadTemplates();
    loadRegistryTables();
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
      const keys = extractPlaceholderKeys(t.placeholderSchemaJson as Record<string, unknown>);
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
      setTemplateLoadError(err instanceof Error ? err.message : "Template load failed");
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

  const updateBinding = useCallback((index: number, updated: PlaceholderBindingEntry) => {
    setBindingEntries((prev) => {
      const next = [...prev];
      next[index] = updated;
      return next;
    });
  }, []);

  // Live seed candidate + unresolved required keys
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
        throw new Error("[SAVED_VIEW_CREATE_FAILED] Saved view creation returned ok=false");
      }
      setSavedViewId(result.savedViewId);
      onSaved?.(result.savedViewId);
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : "Saved view creation failed");
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

  const tableSelectOptions = buildTableOptions(registryTables, registryAvailable === true);
  const showTableManualInput =
    registryAvailable === false ||
    selectedTableValue === MANUAL_VALUE;

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
            setSelectedTableValue("");
            setManualTableRef("");
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
      data-component-bucket-parts="select input textarea button existing_bucket_parts"
    >
      <div class="mb-3 flex items-start justify-between">
        <div>
          <h3 class="text-sm font-semibold text-gray-800">
            Create saved Markdown view
          </h3>
          <p class="mt-0.5 text-xs text-gray-500">
            Registry-driven template and source table selection.
            All bindings are user-selected — no AI inference.
            Markdown body is not runtime SSOT.
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
            Template{" "}
            <span class="font-normal text-gray-400">(registry-driven — from template:list API)</span>
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
          <div class="rounded bg-red-50 p-2 text-xs text-red-700" role="alert">
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

      {/* ── Source table ref (registry-driven primary, explicit manual fallback) ── */}
      <section class="mb-4" aria-label="Source table selection">
        <label class="mb-1 block text-xs font-medium text-gray-700">
          Source table ref{" "}
          {registryAvailable === true
            ? <span class="font-normal text-gray-400">(registry-driven — from manifest logical tables)</span>
            : <span class="font-normal text-amber-600">
                (explicit manual fallback — physical table registry not available)
              </span>}
        </label>

        {registryLoading && (
          <p class="mb-1 text-xs text-gray-400">Loading source table registry…</p>
        )}

        {registryAvailable === true && tableSelectOptions.length > 0 && (
          <Select
            value={selectedTableValue}
            options={tableSelectOptions}
            onChange={(v) => {
              setSelectedTableValue(v);
              if (v !== MANUAL_VALUE) setManualTableRef("");
            }}
            placeholder="Select source table"
          />
        )}

        {registryError && (
          <div
            class="mb-1 rounded bg-amber-50 p-2 text-xs text-amber-800"
            role="status"
            data-registry-state="unavailable"
          >
            {registryError}
          </div>
        )}

        {showTableManualInput && (
          <div class="mt-1">
            {selectedTableValue === MANUAL_VALUE && (
              <p class="mb-0.5 text-xs text-gray-500">
                Explicit manual input — table not in registry or registry unavailable.
              </p>
            )}
            <input
              type="text"
              class="w-full rounded border border-gray-300 px-2 py-1 text-xs"
              value={manualTableRef}
              onInput={(e) => setManualTableRef((e.target as HTMLInputElement).value)}
              placeholder="e.g. topology.physical_tables"
              aria-label="Source table ref (manual input)"
            />
          </div>
        )}

        {selectedTableColumns.length > 0 && (
          <p class="mt-1 text-xs text-gray-500">
            {selectedTableColumns.length} column candidate(s) available from registry for binding.
          </p>
        )}
      </section>

      {/* ── Source record ref (explicit manual — no record enumeration API) ── */}
      <section class="mb-4" aria-label="Source record ref input">
        <label class="mb-0.5 block text-xs font-medium text-gray-700">
          Source record ref{" "}
          <span class="font-normal text-gray-400">
            (explicit manual input — no record enumeration API; enter record primary key or locator)
          </span>
        </label>
        <input
          type="text"
          class="w-full rounded border border-gray-300 px-2 py-1 text-xs"
          value={sourceRecordRef}
          onInput={(e) => setSourceRecordRef((e.target as HTMLInputElement).value)}
          placeholder="e.g. record primary key or locator"
          aria-label="Source record ref"
          data-source-record-ref-manual="true"
        />
      </section>

      {/* ── Placeholder binding editor (registry column candidates) ── */}
      {placeholderKeys.length > 0 && (
        <section class="mb-4" aria-label="Placeholder binding editor">
          <p class="mb-2 text-xs font-medium text-gray-700">
            Placeholder bindings{" "}
            <span class="font-normal text-gray-400">
              (user-selected — no AI inference; required unresolved blocks save;
              column candidates from registry where available)
            </span>
          </p>
          {bindingEntries.map((entry, i) => (
            <PlaceholderBindingRow
              key={entry.placeholderKey}
              entry={entry}
              availableColumns={selectedTableColumns}
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

      {/* ── Unresolved required placeholder gate ── */}
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
        <div class="mb-3 rounded bg-red-50 p-2 text-xs text-red-700" role="alert">
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
