import { h, type VNode } from "preact";
import { Button } from "../components/Button.tsx";
import { Card } from "../components/Card.tsx";
import { Input } from "../components/Input.tsx";
import { Table } from "../components/Table.tsx";
import { AutoCompleteInput } from "../components/AutoCompleteInput.tsx";
import { SearchCombobox } from "../components/SearchCombobox.tsx";
import { CandidateConfidenceBadge } from "../components/CandidateConfidenceBadge.tsx";
import { InlineEditableField } from "../components/InlineEditableField.tsx";
import { PatchPreviewPanel } from "../components/PatchPreviewPanel.tsx";
import { ApplyConfirmDialog } from "../components/ApplyConfirmDialog.tsx";
import { StyleTokenPicker } from "../components/StyleTokenPicker.tsx";
import { ThemePreviewPanel } from "../components/ThemePreviewPanel.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
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
];
