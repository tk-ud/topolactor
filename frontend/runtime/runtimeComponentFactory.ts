import { h, type VNode } from "preact";
import { Button } from "../components/Button.tsx";
import { Card } from "../components/Card.tsx";
import { Input } from "../components/Input.tsx";
import { Table } from "../components/Table.tsx";
import { SelectImportDialog } from "../components/SelectImportDialog.tsx";
import { AutoCompleteInput } from "../components/AutoCompleteInput.tsx";
import { SearchCombobox } from "../components/SearchCombobox.tsx";
import { CandidateConfidenceBadge } from "../components/CandidateConfidenceBadge.tsx";
import { InlineEditableField } from "../components/InlineEditableField.tsx";
import { PatchPreviewPanel } from "../components/PatchPreviewPanel.tsx";
import { ApplyConfirmDialog } from "../components/ApplyConfirmDialog.tsx";
import { StyleTokenPicker } from "../components/StyleTokenPicker.tsx";
import { ThemePreviewPanel } from "../components/ThemePreviewPanel.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { SuggestInput } from "../components/SuggestInput.tsx";
import { RecentInputSuggest } from "../components/RecentInputSuggest.tsx";
import { RelationCandidatePicker } from "../components/RelationCandidatePicker.tsx";
import { DuplicateMergeCandidatePanel } from "../components/DuplicateMergeCandidatePanel.tsx";
import { RelationPathPreview } from "../components/RelationPathPreview.tsx";
import { FieldResolverInspector } from "../components/FieldResolverInspector.tsx";
import { SchemaPromotionCandidatePanel } from "../components/SchemaPromotionCandidatePanel.tsx";
import { InlineEditableJsonbField } from "../components/InlineEditableJsonbField.tsx";
import { DiffStrikeText } from "../components/DiffStrikeText.tsx";
import { AuditDiffDrawer } from "../components/AuditDiffDrawer.tsx";
import { OptimisticUpdateBoundary } from "../components/OptimisticUpdateBoundary.tsx";
import { ConfirmedUpdateButton } from "../components/ConfirmedUpdateButton.tsx";
import { UndoTimeline } from "../components/UndoTimeline.tsx";
import { ConflictResolutionPanel } from "../components/ConflictResolutionPanel.tsx";
import { FacetedFilterBar } from "../components/FacetedFilterBar.tsx";
import { ColumnFilter } from "../components/ColumnFilter.tsx";
import { ColumnVisibilityEditor } from "../components/ColumnVisibilityEditor.tsx";
import { SortControl } from "../components/SortControl.tsx";
import { GroupByControl } from "../components/GroupByControl.tsx";
import { SavedViewSelector } from "../components/SavedViewSelector.tsx";
import { BulkActionPanel } from "../components/BulkActionPanel.tsx";
import { VirtualizedDataTable } from "../components/VirtualizedDataTable.tsx";
import { RowDetailDrawer } from "../components/RowDetailDrawer.tsx";
import { PaginationControl } from "../components/PaginationControl.tsx";
import { ExportCandidatePanel } from "../components/ExportCandidatePanel.tsx";
import { FontTokenEditor } from "../components/FontTokenEditor.tsx";
import { BackgroundColorEditor } from "../components/BackgroundColorEditor.tsx";
import { TextColorEditor } from "../components/TextColorEditor.tsx";
import { SpacingTokenEditor } from "../components/SpacingTokenEditor.tsx";
import { BorderRadiusEditor } from "../components/BorderRadiusEditor.tsx";
import { CssVariablePreview } from "../components/CssVariablePreview.tsx";
import { ShadowTokenEditor } from "../components/ShadowTokenEditor.tsx";
import { AnimationTokenEditor } from "../components/AnimationTokenEditor.tsx";
import { CommandPalette } from "../components/CommandPalette.tsx";
import { EmptyStateActionPanel } from "../components/EmptyStateActionPanel.tsx";
import { OperationGuardBanner } from "../components/OperationGuardBanner.tsx";
import { MutationBoundaryInspector } from "../components/MutationBoundaryInspector.tsx";
import { PermissionHintPanel } from "../components/PermissionHintPanel.tsx";
import { DryRunResultPanel } from "../components/DryRunResultPanel.tsx";
import { RollbackCandidatePanel } from "../components/RollbackCandidatePanel.tsx";
import { OperationAuditLogPanel } from "../components/OperationAuditLogPanel.tsx";
import type { RuntimeComponentFactory } from "../components/runtimeContract.ts";
import {
  emitComponentOperationEvent,
  type NormalizedComponentEventType,
} from "./frontendScheduler.ts";
import type { RuntimeComponentSpec } from "./runtimeComponentAdapter.ts";

type RenderResult = { ok: true; node: VNode<any> } | {
  ok: false;
  error: string;
};

type EventBindingValue = {
  eventType: NormalizedComponentEventType;
  actorOrSource?: string;
  payload?: Record<string, unknown>;
};

function parseEventBinding(value: unknown): EventBindingValue | null {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return null;
  }
  const eventType = (value as Record<string, unknown>).eventType;
  if (typeof eventType !== "string") return null;
  const valid: NormalizedComponentEventType[] = [
    "click",
    "change",
    "select",
    "toggle",
    "expand",
    "collapse",
    "submit",
    "focus",
    "blur",
    "drag",
    "drop",
  ];
  if (!valid.includes(eventType as NormalizedComponentEventType)) return null;
  const actorOrSource = (value as Record<string, unknown>).actorOrSource;
  if (actorOrSource !== undefined && typeof actorOrSource !== "string") {
    return null;
  }
  const payload = (value as Record<string, unknown>).payload;
  if (
    payload !== undefined &&
    (typeof payload !== "object" || payload === null || Array.isArray(payload))
  ) return null;
  return {
    eventType: eventType as NormalizedComponentEventType,
    actorOrSource,
    payload: (payload as Record<string, unknown> | undefined) ?? {},
  };
}

function emitBoundEvent(
  spec: RuntimeComponentSpec,
  trigger: string,
  payload: Record<string, unknown>,
): { ok: true } | { ok: false; error: string } {
  const binding = parseEventBinding(spec.eventBinding[trigger]);
  if (!binding) {
    return {
      ok: false,
      error: `RUNTIME_PRIMITIVE_RENDERER_INVALID_EVENT_BINDING: ${trigger}`,
    };
  }
  return emitComponentOperationEvent({
    componentId: spec.componentId,
    packageId: spec.packageId,
    layoutId: spec.layoutId,
    wiringId: spec.wiringId,
    eventType: binding.eventType,
    actorOrSource: binding.actorOrSource ?? "runtime_primitive_renderer",
    payload: { ...binding.payload, ...payload },
  });
}

function requireBinding(
  spec: RuntimeComponentSpec,
  trigger: string,
): { ok: true } | { ok: false; error: string } {
  if (!(trigger in spec.eventBinding)) {
    return {
      ok: false,
      error: `RUNTIME_PRIMITIVE_RENDERER_MISSING_EVENT_BINDING: ${trigger}`,
    };
  }
  const parsed = parseEventBinding(spec.eventBinding[trigger]);
  if (!parsed) {
    return {
      ok: false,
      error: `RUNTIME_PRIMITIVE_RENDERER_INVALID_EVENT_BINDING: ${trigger}`,
    };
  }
  return { ok: true };
}

function buttonFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (typeof data.label !== "string") {
    return {
      ok: false,
      error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_BUTTON_PROPS",
    };
  }
  const bindingCheck = requireBinding(spec, "click");
  if (!bindingCheck.ok) return bindingCheck;
  return {
      ok: true,
      node: h(Button, {
      label: data.label as string,
      disabled: data.disabled as boolean | undefined,
      variant: data.variant as "primary" | "secondary" | "danger" | undefined,
      type: data.type as "button" | "submit" | "reset" | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onClick: () => {
        const result = emitBoundEvent(spec, "click", {});
        if (!result.ok) throw new Error(result.error);
      },
    }),
  };
}

function inputFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (data.value !== undefined && typeof data.value !== "string") {
    return {
      ok: false,
      error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_INPUT_PROPS",
    };
  }
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(Input, {
      value: (data.value as string | undefined) ?? "",
      placeholder: data.placeholder as string | undefined,
      disabled: data.disabled as boolean | undefined,
      label: data.label as string | undefined,
      type: (spec.componentType === "form_input/search_input"
        ? "text"
        : data.type) as "text" | "password" | "number" | "email" | undefined,
      onChange: (value: string) => {
        const result = emitBoundEvent(spec, "change", { value });
        if (!result.ok) {
          throw new Error(result.error);
        }
      },
      onFocus: spec.eventBinding.focus
        ? () => {
          const result = emitBoundEvent(spec, "focus", {});
          if (!result.ok) {
            throw new Error(result.error);
          }
        }
        : undefined,
      className: spec.className,
      design: spec.design ?? {},
      onBlur: spec.eventBinding.blur
        ? () => {
          const result = emitBoundEvent(spec, "blur", {});
          if (!result.ok) {
            throw new Error(result.error);
          }
        }
        : undefined,
    }),
  };
}

function cardFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(Card, {
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      variant: data.variant as
        | "default"
        | "info"
        | "warning"
        | "error"
        | undefined,
      children: h("div", null, (data.body as string | undefined) ?? ""),
      footer: data.footer as string | undefined,
      onClick: spec.eventBinding.click
        ? () => {
          const result = emitBoundEvent(spec, "click", {});
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function tableFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const table = (typeof props.table === "object" && props.table !== null && !Array.isArray(props.table))
    ? props.table as Record<string, unknown>
    : props;
  if (!Array.isArray(table.columns) || !Array.isArray(table.rows)) {
    return {
      ok: false,
      error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_TABLE_PROPS",
    };
  }
  const columns = table.columns as Array<{ key: string; header: string }>;
  const rows = table.rows as Array<Record<string, unknown>>;
  return {
    ok: true,
    node: h(Table<Record<string, unknown>>, {
      columns,
      className: spec.className,
      design: spec.design ?? {},
      rows,
      rowKey: (row) => String(row.id ?? JSON.stringify(row)),
      emptyMessage: (table.emptyMessage as string | undefined) ?? "No data.",
      onRowClick: spec.eventBinding.select
        ? (row) => {
          const result = emitBoundEvent(spec, "select", { row });
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function autoCompleteInputFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (data.value !== undefined && typeof data.value !== "string") {
    return { ok: false, error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_AUTOCOMPLETE_INPUT_PROPS" };
  }
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const suggestions = Array.isArray(data.suggestions)
    ? data.suggestions.filter((s): s is string => typeof s === "string")
    : [];
  return {
    ok: true,
    node: h(AutoCompleteInput, {
      value: (data.value as string | undefined) ?? "",
      suggestions,
      placeholder: data.placeholder as string | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const result = emitBoundEvent(spec, "change", { value });
        if (!result.ok) throw new Error(result.error);
      },
      onSelect: spec.eventBinding.select
        ? (value: string) => {
          const result = emitBoundEvent(spec, "select", { value });
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function searchComboboxFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (data.value !== undefined && typeof data.value !== "string") {
    return { ok: false, error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_SEARCH_COMBOBOX_PROPS" };
  }
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawOptions = Array.isArray(data.options) ? data.options : [];
  const options = rawOptions
    .filter((o): o is { label: string; value: string } =>
      typeof o === "object" && o !== null &&
      typeof (o as Record<string, unknown>).value === "string"
    )
    .map((o) => ({
      label: typeof o.label === "string" ? o.label : o.value,
      value: o.value,
    }));
  return {
    ok: true,
    node: h(SearchCombobox, {
      value: (data.value as string | undefined) ?? "",
      options,
      placeholder: data.placeholder as string | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const result = emitBoundEvent(spec, "change", { value });
        if (!result.ok) throw new Error(result.error);
      },
      onSelect: spec.eventBinding.select
        ? (value: string) => {
          const result = emitBoundEvent(spec, "select", { value });
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function candidateConfidenceBadgeFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (typeof data.label !== "string") {
    return { ok: false, error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_CANDIDATE_CONFIDENCE_BADGE_PROPS" };
  }
  const rawConf = data.confidence;
  const confidence = rawConf === "high" || rawConf === "medium" || rawConf === "low"
    ? rawConf
    : "unknown";
  const score = typeof data.score === "number" ? data.score : undefined;
  return {
    ok: true,
    node: h(CandidateConfidenceBadge, {
      label: data.label as string,
      confidence,
      score,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function inlineEditableFieldFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (data.value !== undefined && typeof data.value !== "string") {
    return { ok: false, error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_INLINE_EDITABLE_FIELD_PROPS" };
  }
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(InlineEditableField, {
      value: (data.value as string | undefined) ?? "",
      editing: data.editing as boolean | undefined,
      label: data.label as string | undefined,
      placeholder: data.placeholder as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const result = emitBoundEvent(spec, "change", { value });
        if (!result.ok) throw new Error(result.error);
      },
      onToggle: spec.eventBinding.toggle
        ? (editing: boolean) => {
          const result = emitBoundEvent(spec, "toggle", { editing });
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function patchPreviewPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawFields = Array.isArray(data.fields) ? data.fields : [];
  const fields = rawFields.filter(
    (f): f is { fieldLabel: string; before: string; after: string } =>
      typeof f === "object" && f !== null &&
      typeof (f as Record<string, unknown>).fieldLabel === "string",
  ).map((f) => ({
    fieldLabel: f.fieldLabel,
    before: typeof f.before === "string" ? f.before : "",
    after: typeof f.after === "string" ? f.after : "",
  }));
  return {
    ok: true,
    node: h(PatchPreviewPanel, {
      fields,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function applyConfirmDialogFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const submitCheck = requireBinding(spec, "submit");
  if (!submitCheck.ok) return submitCheck;
  const toggleCheck = requireBinding(spec, "toggle");
  if (!toggleCheck.ok) return toggleCheck;
  return {
    ok: true,
    node: h(ApplyConfirmDialog, {
      open: data.open as boolean ?? false,
      title: data.title as string | undefined,
      description: data.description as string | undefined,
      confirmLabel: data.confirmLabel as string | undefined,
      cancelLabel: data.cancelLabel as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onConfirm: () => {
        const result = emitBoundEvent(spec, "submit", {});
        if (!result.ok) throw new Error(result.error);
      },
      onCancel: () => {
        const result = emitBoundEvent(spec, "toggle", { open: false });
        if (!result.ok) throw new Error(result.error);
      },
    }),
  };
}

function styleTokenPickerFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "select");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(StyleTokenPicker, {
      value: data.value as string | undefined,
      tokens,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: (token) => {
        const result = emitBoundEvent(spec, "select", { token });
        if (!result.ok) throw new Error(result.error);
      },
    }),
  };
}

function themePreviewPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; description?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(ThemePreviewPanel, {
      tokens,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function validationErrorPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawErrors = Array.isArray(data.errors) ? data.errors : [];
  const errors = rawErrors.filter(
    (e): e is { message: string; field?: string; code?: string } =>
      typeof e === "object" && e !== null &&
      typeof (e as Record<string, unknown>).message === "string",
  );
  return {
    ok: true,
    node: h(ValidationErrorPanel, {
      errors,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

// === Cat A: Search / Suggest / Candidate UI ===

function suggestInputFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(SuggestInput, {
      value: typeof data.value === "string" ? data.value : "",
      suggestions: Array.isArray(data.suggestions)
        ? data.suggestions.filter((s): s is string => typeof s === "string")
        : [],
      placeholder: data.placeholder as string | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
      onSelect: spec.eventBinding.select
        ? (value: string) => {
          const r = emitBoundEvent(spec, "select", { value });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function recentInputSuggestFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(RecentInputSuggest, {
      value: typeof data.value === "string" ? data.value : "",
      recentItems: Array.isArray(data.recentItems)
        ? data.recentItems.filter((s): s is string => typeof s === "string")
        : [],
      placeholder: data.placeholder as string | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
      onSelect: spec.eventBinding.select
        ? (value: string) => {
          const r = emitBoundEvent(spec, "select", { value });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function relationCandidatePickerFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "select");
  if (!bindingCheck.ok) return bindingCheck;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { id: string; label: string; score?: number } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(RelationCandidatePicker, {
      candidates,
      selectedId: data.selectedId as string | undefined,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: (id: string) => {
        const r = emitBoundEvent(spec, "select", { id });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function duplicateMergeCandidatePanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { id: string; label: string; similarity?: number } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(DuplicateMergeCandidatePanel, {
      candidates,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function relationPathPreviewFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawSegments = Array.isArray(data.segments) ? data.segments : [];
  const segments = rawSegments.filter(
    (s): s is { label: string; kind?: string } =>
      typeof s === "object" && s !== null &&
      typeof (s as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(RelationPathPreview, {
      segments,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function fieldResolverInspectorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(FieldResolverInspector, {
      fieldName: data.fieldName as string | undefined,
      resolvedValue: data.resolvedValue as string | undefined,
      resolverKind: data.resolverKind as string | undefined,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function schemaPromotionCandidatePanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { id: string; label: string; kind?: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(SchemaPromotionCandidatePanel, {
      candidates,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function selectImportDialogFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const submitCheck = requireBinding(spec, "submit");
  if (!submitCheck.ok) return submitCheck;
  const toggleCheck = requireBinding(spec, "toggle");
  if (!toggleCheck.ok) return toggleCheck;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { label: string; value: string; description?: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).label === "string" &&
      typeof (c as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(SelectImportDialog, {
      open: Boolean(data.open),
      title: data.title as string | undefined,
      candidates,
      selectedValue: data.selectedValue as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (value: string) => {
          const r = emitBoundEvent(spec, "select", { value });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
      onSubmit: () => {
        const r = emitBoundEvent(spec, "submit", {});
        if (!r.ok) throw new Error(r.error);
      },
      onCancel: () => {
        const r = emitBoundEvent(spec, "toggle", { open: false });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

// === Cat B: Inline Edit / Preview Update / Audit UI ===

function inlineEditableJsonbFieldFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const value = (typeof data.value === "object" && data.value !== null && !Array.isArray(data.value))
    ? data.value as Record<string, unknown>
    : undefined;
  return {
    ok: true,
    node: h(InlineEditableJsonbField, {
      value,
      editing: data.editing as boolean | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (raw: string) => {
        const r = emitBoundEvent(spec, "change", { raw });
        if (!r.ok) throw new Error(r.error);
      },
      onToggle: spec.eventBinding.toggle
        ? (editing: boolean) => {
          const r = emitBoundEvent(spec, "toggle", { editing });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function diffStrikeTextFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(DiffStrikeText, {
      before: data.before as string | undefined,
      after: data.after as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function auditDiffDrawerFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "toggle");
  if (!bindingCheck.ok) return bindingCheck;
  const rawEntries = Array.isArray(data.entries) ? data.entries : [];
  const entries = rawEntries.filter(
    (e): e is { field: string; before: string; after: string; timestamp?: string } =>
      typeof e === "object" && e !== null &&
      typeof (e as Record<string, unknown>).field === "string",
  );
  return {
    ok: true,
    node: h(AuditDiffDrawer, {
      open: typeof data.open === "boolean" ? data.open : false,
      entries,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onClose: () => {
        const r = emitBoundEvent(spec, "toggle", { open: false });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function optimisticUpdateBoundaryFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(OptimisticUpdateBoundary, {
      pending: data.pending as boolean | undefined,
      error: data.error as string | undefined,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function confirmedUpdateButtonFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "submit");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(ConfirmedUpdateButton, {
      label: data.label as string | undefined,
      confirmLabel: data.confirmLabel as string | undefined,
      description: data.description as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onConfirm: () => {
        const r = emitBoundEvent(spec, "submit", {});
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function undoTimelineFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawItems = Array.isArray(data.items) ? data.items : [];
  const items = rawItems.filter(
    (it): it is { id: string; label: string; timestamp?: string } =>
      typeof it === "object" && it !== null &&
      typeof (it as Record<string, unknown>).id === "string" &&
      typeof (it as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(UndoTimeline, {
      items,
      selectedId: data.selectedId as string | undefined,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function conflictResolutionPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawConflicts = Array.isArray(data.conflicts) ? data.conflicts : [];
  const conflicts = rawConflicts.filter(
    (c): c is { field: string; localValue: string; remoteValue: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).field === "string",
  );
  return {
    ok: true,
    node: h(ConflictResolutionPanel, {
      conflicts,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (field: string) => {
          const r = emitBoundEvent(spec, "select", { field });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

// === Cat C: Table / List / View Operation UI ===

function facetedFilterBarFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawFilters = Array.isArray(data.filters) ? data.filters : [];
  const filters = rawFilters.filter(
    (f): f is { key: string; label: string; value: string } =>
      typeof f === "object" && f !== null &&
      typeof (f as Record<string, unknown>).key === "string" &&
      typeof (f as Record<string, unknown>).label === "string",
  ).map((f) => ({ key: f.key, label: f.label, value: typeof f.value === "string" ? f.value : "" }));
  return {
    ok: true,
    node: h(FacetedFilterBar, {
      filters,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (key: string, value: string) => {
        const r = emitBoundEvent(spec, "change", { key, value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function columnFilterFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(ColumnFilter, {
      column: data.column as string | undefined,
      value: data.value as string | undefined,
      placeholder: data.placeholder as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function columnVisibilityEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawColumns = Array.isArray(data.columns) ? data.columns : [];
  const columns = rawColumns.filter(
    (c): c is { key: string; label: string; visible: boolean } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).key === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  ).map((c) => ({ key: c.key, label: c.label, visible: typeof c.visible === "boolean" ? c.visible : true }));
  return {
    ok: true,
    node: h(ColumnVisibilityEditor, {
      columns,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (key: string, visible: boolean) => {
        const r = emitBoundEvent(spec, "change", { key, visible });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function sortControlFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawFields = Array.isArray(data.fields) ? data.fields : [];
  const fields = rawFields.filter(
    (f): f is { key: string; label: string } =>
      typeof f === "object" && f !== null &&
      typeof (f as Record<string, unknown>).key === "string",
  ).map((f) => ({ key: f.key, label: typeof f.label === "string" ? f.label : f.key }));
  const rawDir = data.direction;
  const direction = rawDir === "asc" || rawDir === "desc" ? rawDir : null;
  return {
    ok: true,
    node: h(SortControl, {
      field: data.field as string | undefined,
      direction,
      fields,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (field: string, dir: "asc" | "desc") => {
        const r = emitBoundEvent(spec, "change", { field, direction: dir });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function groupByControlFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawOptions = Array.isArray(data.options) ? data.options : [];
  const options = rawOptions.filter(
    (o): o is { key: string; label: string } =>
      typeof o === "object" && o !== null &&
      typeof (o as Record<string, unknown>).key === "string",
  ).map((o) => ({ key: o.key, label: typeof o.label === "string" ? o.label : o.key }));
  return {
    ok: true,
    node: h(GroupByControl, {
      field: data.field as string | undefined,
      options,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (field: string) => {
        const r = emitBoundEvent(spec, "change", { field });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function savedViewSelectorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawViews = Array.isArray(data.views) ? data.views : [];
  const views = rawViews.filter(
    (v): v is { id: string; label: string } =>
      typeof v === "object" && v !== null &&
      typeof (v as Record<string, unknown>).id === "string" &&
      typeof (v as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(SavedViewSelector, {
      views,
      selectedId: data.selectedId as string | undefined,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function bulkActionPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "select");
  if (!bindingCheck.ok) return bindingCheck;
  const rawActions = Array.isArray(data.actions) ? data.actions : [];
  const actions = rawActions.filter(
    (a): a is { id: string; label: string } =>
      typeof a === "object" && a !== null &&
      typeof (a as Record<string, unknown>).id === "string" &&
      typeof (a as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(BulkActionPanel, {
      selectedCount: typeof data.selectedCount === "number" ? data.selectedCount : 0,
      actions,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: (id: string) => {
        const r = emitBoundEvent(spec, "select", { id });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function virtualizedDataTableFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawColumns = Array.isArray(data.columns) ? data.columns : [];
  const columns = rawColumns.filter(
    (c): c is { key: string; header: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).key === "string",
  ).map((c) => ({ key: c.key, header: typeof c.header === "string" ? c.header : c.key }));
  const rows = Array.isArray(data.rows)
    ? data.rows.filter((r): r is Record<string, unknown> => typeof r === "object" && r !== null && !Array.isArray(r))
    : [];
  return {
    ok: true,
    node: h(VirtualizedDataTable, {
      columns,
      rows,
      emptyMessage: data.emptyMessage as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onRowSelect: spec.eventBinding.select
        ? (row: Record<string, unknown>) => {
          const r = emitBoundEvent(spec, "select", { row });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function rowDetailDrawerFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "toggle");
  if (!bindingCheck.ok) return bindingCheck;
  const row = (typeof data.row === "object" && data.row !== null && !Array.isArray(data.row))
    ? data.row as Record<string, unknown>
    : undefined;
  return {
    ok: true,
    node: h(RowDetailDrawer, {
      open: typeof data.open === "boolean" ? data.open : false,
      row,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onClose: () => {
        const r = emitBoundEvent(spec, "toggle", { open: false });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function paginationControlFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(PaginationControl, {
      page: typeof data.page === "number" ? data.page : 1,
      pageSize: typeof data.pageSize === "number" ? data.pageSize : 10,
      total: typeof data.total === "number" ? data.total : 0,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (page: number) => {
        const r = emitBoundEvent(spec, "change", { page });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function exportCandidatePanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { id: string; label: string; format?: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(ExportCandidatePanel, {
      candidates,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

// === Cat E: Design Token / Style Token / Layout Token UI ===

function fontTokenEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(FontTokenEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function backgroundColorEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(BackgroundColorEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function textColorEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(TextColorEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function spacingTokenEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(SpacingTokenEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function borderRadiusEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(BorderRadiusEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function cssVariablePreviewFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawVariables = Array.isArray(data.variables) ? data.variables : [];
  const variables = rawVariables.filter(
    (v): v is { key: string; value: string; description?: string } =>
      typeof v === "object" && v !== null &&
      typeof (v as Record<string, unknown>).key === "string" &&
      typeof (v as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(CssVariablePreview, {
      variables,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function shadowTokenEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(ShadowTokenEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function animationTokenEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(AnimationTokenEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

// === Cat H: Safety / Inspector / Operation Guard UI ===

function commandPaletteFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const toggleCheck = requireBinding(spec, "toggle");
  if (!toggleCheck.ok) return toggleCheck;
  const selectCheck = requireBinding(spec, "select");
  if (!selectCheck.ok) return selectCheck;
  const rawCommands = Array.isArray(data.commands) ? data.commands : [];
  const commands = rawCommands.filter(
    (c): c is { id: string; label: string; description?: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(CommandPalette, {
      open: typeof data.open === "boolean" ? data.open : false,
      commands,
      placeholder: data.placeholder as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onClose: () => {
        const r = emitBoundEvent(spec, "toggle", { open: false });
        if (!r.ok) throw new Error(r.error);
      },
      onSelect: (id: string) => {
        const r = emitBoundEvent(spec, "select", { id });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function emptyStateActionPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawActions = Array.isArray(data.actions) ? data.actions : [];
  const actions = rawActions.filter(
    (a): a is { id: string; label: string } =>
      typeof a === "object" && a !== null &&
      typeof (a as Record<string, unknown>).id === "string" &&
      typeof (a as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(EmptyStateActionPanel, {
      title: data.title as string | undefined,
      description: data.description as string | undefined,
      actions,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function operationGuardBannerFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawLevel = data.level;
  const level = rawLevel === "info" || rawLevel === "warning" || rawLevel === "error"
    ? rawLevel
    : undefined;
  return {
    ok: true,
    node: h(OperationGuardBanner, {
      message: data.message as string | undefined,
      level,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function mutationBoundaryInspectorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(MutationBoundaryInspector, {
      operationKind: data.operationKind as string | undefined,
      fieldName: data.fieldName as string | undefined,
      currentValue: data.currentValue as string | undefined,
      pendingValue: data.pendingValue as string | undefined,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function permissionHintPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(PermissionHintPanel, {
      requiredPermission: data.requiredPermission as string | undefined,
      message: data.message as string | undefined,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function dryRunResultPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawResults = Array.isArray(data.results) ? data.results : [];
  const results = rawResults.filter(
    (r): r is { id: string; label: string; kind?: string; impact?: string } =>
      typeof r === "object" && r !== null &&
      typeof (r as Record<string, unknown>).id === "string" &&
      typeof (r as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(DryRunResultPanel, {
      results,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function rollbackCandidatePanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { id: string; label: string; timestamp?: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(RollbackCandidatePanel, {
      candidates,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function operationAuditLogPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null && !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawEntries = Array.isArray(data.entries) ? data.entries : [];
  const entries = rawEntries.filter(
    (e): e is { id: string; operation: string; timestamp?: string; actor?: string } =>
      typeof e === "object" && e !== null &&
      typeof (e as Record<string, unknown>).id === "string" &&
      typeof (e as Record<string, unknown>).operation === "string",
  );
  return {
    ok: true,
    node: h(OperationAuditLogPanel, {
      entries,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

export const RUNTIME_COMPONENT_FACTORIES: RuntimeComponentFactory[] = [
  { componentKinds: ["action/button"], render: buttonFactory },
  {
    componentKinds: [
      "form_input/input",
      "form_input/textarea",
      "form_input/search_input",
    ],
    render: inputFactory,
  },
  {
    componentKinds: [
      "display/card",
      "disclosure_structure/panel",
      "disclosure_structure/section",
    ],
    render: cardFactory,
  },
  {
    componentKinds: [
      "data_display/table",
      "data_display/data_grid",
      "data_display/list",
    ],
    render: tableFactory,
  },
  {
    componentKinds: ["search_suggest/autocomplete_input"],
    render: autoCompleteInputFactory,
  },
  {
    componentKinds: ["search_suggest/search_combobox"],
    render: searchComboboxFactory,
  },
  {
    componentKinds: ["search_suggest/candidate_confidence_badge"],
    render: candidateConfidenceBadgeFactory,
  },
  {
    componentKinds: ["inline_edit/inline_editable_field"],
    render: inlineEditableFieldFactory,
  },
  {
    componentKinds: ["inline_edit/patch_preview_panel"],
    render: patchPreviewPanelFactory,
  },
  {
    componentKinds: ["safety_guard/apply_confirm_dialog"],
    render: applyConfirmDialogFactory,
  },
  {
    componentKinds: ["design_token/style_token_picker"],
    render: styleTokenPickerFactory,
  },
  {
    componentKinds: ["design_token/theme_preview_panel"],
    render: themePreviewPanelFactory,
  },
  {
    componentKinds: ["safety_guard/validation_error_panel"],
    render: validationErrorPanelFactory,
  },
  { componentKinds: ["search_suggest/suggest_input"], render: suggestInputFactory },
  { componentKinds: ["search_suggest/recent_input_suggest"], render: recentInputSuggestFactory },
  { componentKinds: ["search_suggest/relation_candidate_picker"], render: relationCandidatePickerFactory },
  { componentKinds: ["search_suggest/duplicate_merge_candidate_panel"], render: duplicateMergeCandidatePanelFactory },
  { componentKinds: ["search_suggest/relation_path_preview"], render: relationPathPreviewFactory },
  { componentKinds: ["search_suggest/field_resolver_inspector"], render: fieldResolverInspectorFactory },
  { componentKinds: ["search_suggest/schema_promotion_candidate_panel"], render: schemaPromotionCandidatePanelFactory },
  { componentKinds: ["search_suggest/select_import_dialog"], render: selectImportDialogFactory },
  { componentKinds: ["inline_edit/inline_editable_jsonb_field"], render: inlineEditableJsonbFieldFactory },
  { componentKinds: ["inline_edit/diff_strike_text"], render: diffStrikeTextFactory },
  { componentKinds: ["inline_edit/audit_diff_drawer"], render: auditDiffDrawerFactory },
  { componentKinds: ["inline_edit/optimistic_update_boundary"], render: optimisticUpdateBoundaryFactory },
  { componentKinds: ["inline_edit/confirmed_update_button"], render: confirmedUpdateButtonFactory },
  { componentKinds: ["inline_edit/undo_timeline"], render: undoTimelineFactory },
  { componentKinds: ["inline_edit/conflict_resolution_panel"], render: conflictResolutionPanelFactory },
  { componentKinds: ["table_op/faceted_filter_bar"], render: facetedFilterBarFactory },
  { componentKinds: ["table_op/column_filter"], render: columnFilterFactory },
  { componentKinds: ["table_op/column_visibility_editor"], render: columnVisibilityEditorFactory },
  { componentKinds: ["table_op/sort_control"], render: sortControlFactory },
  { componentKinds: ["table_op/group_by_control"], render: groupByControlFactory },
  { componentKinds: ["table_op/saved_view_selector"], render: savedViewSelectorFactory },
  { componentKinds: ["table_op/bulk_action_panel"], render: bulkActionPanelFactory },
  { componentKinds: ["table_op/virtualized_data_table"], render: virtualizedDataTableFactory },
  { componentKinds: ["table_op/row_detail_drawer"], render: rowDetailDrawerFactory },
  { componentKinds: ["table_op/pagination_control"], render: paginationControlFactory },
  { componentKinds: ["table_op/export_candidate_panel"], render: exportCandidatePanelFactory },
  { componentKinds: ["design_token/font_token_editor"], render: fontTokenEditorFactory },
  { componentKinds: ["design_token/background_color_editor"], render: backgroundColorEditorFactory },
  { componentKinds: ["design_token/text_color_editor"], render: textColorEditorFactory },
  { componentKinds: ["design_token/spacing_token_editor"], render: spacingTokenEditorFactory },
  { componentKinds: ["design_token/border_radius_editor"], render: borderRadiusEditorFactory },
  { componentKinds: ["design_token/css_variable_preview"], render: cssVariablePreviewFactory },
  { componentKinds: ["design_token/shadow_token_editor"], render: shadowTokenEditorFactory },
  { componentKinds: ["design_token/animation_token_editor"], render: animationTokenEditorFactory },
  { componentKinds: ["safety_guard/command_palette"], render: commandPaletteFactory },
  { componentKinds: ["safety_guard/empty_state_action_panel"], render: emptyStateActionPanelFactory },
  { componentKinds: ["safety_guard/operation_guard_banner"], render: operationGuardBannerFactory },
  { componentKinds: ["safety_guard/mutation_boundary_inspector"], render: mutationBoundaryInspectorFactory },
  { componentKinds: ["safety_guard/permission_hint_panel"], render: permissionHintPanelFactory },
  { componentKinds: ["safety_guard/dry_run_result_panel"], render: dryRunResultPanelFactory },
  { componentKinds: ["safety_guard/rollback_candidate_panel"], render: rollbackCandidatePanelFactory },
  { componentKinds: ["safety_guard/operation_audit_log_panel"], render: operationAuditLogPanelFactory },
];
