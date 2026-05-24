import { h, type VNode } from "preact";
import { Button } from "../components/Button.tsx";
import { Card } from "../components/Card.tsx";
import { Input } from "../components/Input.tsx";
import { Table } from "../components/Table.tsx";
import { emitComponentOperationEvent, type NormalizedComponentEventType } from "./frontendScheduler.ts";
import type { RuntimeComponentSpec } from "./runtimeComponentAdapter.ts";

type RenderResult = { ok: true; node: VNode<any> } | { ok: false; error: string };

type EventBindingValue = {
  eventType: NormalizedComponentEventType;
  actorOrSource?: string;
  payload?: Record<string, unknown>;
};

function parseEventBinding(value: unknown): EventBindingValue | null {
  if (typeof value !== "object" || value === null || Array.isArray(value)) return null;
  const eventType = (value as Record<string, unknown>).eventType;
  if (typeof eventType !== "string") return null;
  const valid: NormalizedComponentEventType[] = ["click", "change", "select", "toggle", "expand", "collapse", "submit", "focus", "blur", "drag", "drop"];
  if (!valid.includes(eventType as NormalizedComponentEventType)) return null;
  const actorOrSource = (value as Record<string, unknown>).actorOrSource;
  if (actorOrSource !== undefined && typeof actorOrSource !== "string") return null;
  const payload = (value as Record<string, unknown>).payload;
  if (payload !== undefined && (typeof payload !== "object" || payload === null || Array.isArray(payload))) return null;
  return { eventType: eventType as NormalizedComponentEventType, actorOrSource, payload: (payload as Record<string, unknown> | undefined) ?? {} };
}

function emitBoundEvent(spec: RuntimeComponentSpec, trigger: string, payload: Record<string, unknown>): { ok: true } | { ok: false; error: string } {
  const binding = parseEventBinding(spec.eventBinding[trigger]);
  if (!binding) return { ok: false, error: `RUNTIME_PRIMITIVE_RENDERER_INVALID_EVENT_BINDING: ${trigger}` };
  return emitComponentOperationEvent({
    componentId: spec.componentId,
    packageId: spec.packageId,
    layoutId: spec.layoutId,
    eventType: binding.eventType,
    actorOrSource: binding.actorOrSource ?? "runtime_primitive_renderer",
    payload: { ...binding.payload, ...payload },
  });
}

function requireBinding(spec: RuntimeComponentSpec, trigger: string): { ok: true } | { ok: false; error: string } {
  if (!(trigger in spec.eventBinding)) return { ok: false, error: `RUNTIME_PRIMITIVE_RENDERER_MISSING_EVENT_BINDING: ${trigger}` };
  const parsed = parseEventBinding(spec.eventBinding[trigger]);
  if (!parsed) return { ok: false, error: `RUNTIME_PRIMITIVE_RENDERER_INVALID_EVENT_BINDING: ${trigger}` };
  return { ok: true };
}

export function renderRuntimeComponent(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  switch (spec.componentType) {
    case "action/button": {
      if (typeof props.label !== "string") return { ok: false, error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_BUTTON_PROPS" };
      const bindingCheck = requireBinding(spec, "click");
      if (!bindingCheck.ok) return bindingCheck;
      return {
        ok: true,
        node: h(Button, {
          label: props.label,
          disabled: props.disabled as boolean | undefined,
          variant: props.variant as "primary" | "secondary" | "danger" | undefined,
          type: props.type as "button" | "submit" | "reset" | undefined,
          onClick: () => {
            const result = emitBoundEvent(spec, "click", {});
            if (!result.ok) throw new Error(result.error);
          },
        }),
      };
    }
    case "form_input/input":
    case "form_input/textarea":
    case "form_input/search_input": {
      if (props.value !== undefined && typeof props.value !== "string") return { ok: false, error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_INPUT_PROPS" };
      const bindingCheck = requireBinding(spec, "change");
      if (!bindingCheck.ok) return bindingCheck;
      return {
        ok: true,
        node: h(Input, {
          value: (props.value as string | undefined) ?? "",
          placeholder: props.placeholder as string | undefined,
          disabled: props.disabled as boolean | undefined,
          label: props.label as string | undefined,
          type: (spec.componentType === "form_input/search_input"
            ? "text"
            : props.type) as "text" | "password" | "number" | "email" | undefined,
          onChange: (value: string) => {
            const result = emitBoundEvent(spec, "change", { value });
            if (!result.ok) throw new Error(result.error);
          },
        }),
      };
    }
    case "display/card":
    case "disclosure_structure/panel":
    case "disclosure_structure/section":
      return {
        ok: true,
        node: h(Card, {
          title: props.title as string | undefined,
          variant: props.variant as "default" | "info" | "warning" | "error" | undefined,
          children: h("div", null, (props.body as string | undefined) ?? ""),
          footer: props.footer as string | undefined,
        }),
      };
    case "data_display/table":
    case "data_display/data_grid":
    case "data_display/list": {
      if (!Array.isArray(props.columns) || !Array.isArray(props.rows)) return { ok: false, error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_TABLE_PROPS" };
      const columns = props.columns as Array<{ key: string; header: string }>;
      const rows = props.rows as Array<Record<string, unknown>>;
      return {
        ok: true,
        node: h(Table<Record<string, unknown>>, {
          columns,
          rows,
          rowKey: (row) => String(row.id ?? JSON.stringify(row)),
          emptyMessage: (props.emptyMessage as string | undefined) ?? "No data.",
        }),
      };
    }
    default:
      return { ok: false, error: `RUNTIME_PRIMITIVE_RENDERER_UNSUPPORTED_COMPONENT_KIND: ${spec.componentType}` };
  }
}
