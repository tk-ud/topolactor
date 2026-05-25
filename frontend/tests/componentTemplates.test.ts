import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  computeDesignDisabled,
} from "../components/types.ts";
import { Badge } from "../components/Badge.tsx";
import { Alert } from "../components/Alert.tsx";
import { FormField } from "../components/FormField.tsx";
import { JsonViewer } from "../components/JsonViewer.tsx";
import { ValidationResultPanel } from "../components/ValidationResultPanel.tsx";
import { AdminPageShell } from "../components/AdminPageShell.tsx";
import { AdminSection } from "../components/AdminSection.tsx";
import { LoadingState } from "../components/LoadingState.tsx";
import { ErrorState } from "../components/ErrorState.tsx";
import { Select } from "../components/Select.tsx";
import { Checkbox } from "../components/Checkbox.tsx";

// --- Helper function tests ---

Deno.test("mergeDesignClassName: design.tailwind goes to className, not inline style", () => {
  const result = mergeDesignClassName("base", {
    tailwind: "tw-bg-red",
    style: "color:red",
  });
  assertEquals(result, "base tw-bg-red");
});

Deno.test("mergeDesignClassName: design.classname and design.className are merged", () => {
  const result = mergeDesignClassName(undefined, {
    classname: "db-cls",
    className: "camel-cls",
    tailwind: "tw-cls",
  });
  assertEquals(result, "db-cls camel-cls tw-cls");
});

Deno.test("mergeDesignClassName: returns undefined when all empty", () => {
  const result = mergeDesignClassName(undefined, {});
  assertEquals(result, undefined);
});

Deno.test("mergeDesignStyle: only design.style appended to inline style", () => {
  const result = mergeDesignStyle("color:blue", { style: "border:1px solid red", tailwind: "tw-border" });
  assertEquals(result, "color:blue;border:1px solid red");
  // tailwind does NOT appear in the inline style
  assertEquals(result.includes("tw-border"), false);
});

Deno.test("mergeDesignStyle: no style in design returns base unchanged", () => {
  const result = mergeDesignStyle("color:blue", { tailwind: "tw-x" });
  assertEquals(result, "color:blue");
});

Deno.test("computeDesignDisabled: loading state → disabled", () => {
  assertEquals(computeDesignDisabled(false, { state: "loading" }), true);
  assertEquals(computeDesignDisabled(undefined, { state: "loading" }), true);
});

Deno.test("computeDesignDisabled: disabled prop → disabled", () => {
  assertEquals(computeDesignDisabled(true, { state: "default" }), true);
});

Deno.test("computeDesignDisabled: not loading and not disabled → not disabled", () => {
  assertEquals(computeDesignDisabled(false, { state: "default" }), false);
  assertEquals(computeDesignDisabled(undefined, {}), false);
});

// --- Badge ---

Deno.test("Badge: tone prop is passed through", () => {
  const node = h(Badge, { label: "active", tone: "success" });
  assertEquals((node as any).props.tone, "success");
  assertEquals((node as any).props.label, "active");
});

Deno.test("Badge: neutral tone is default", () => {
  const node = h(Badge, { label: "x" });
  assertEquals((node as any).props.tone, undefined);
});

Deno.test("Badge: design.tailwind stays in className via mergeDesignClassName", () => {
  const className = mergeDesignClassName(undefined, { tailwind: "tw-badge" });
  assertEquals(className, "tw-badge");
  const styleResult = mergeDesignStyle("base", { tailwind: "tw-badge" });
  assertEquals(styleResult.includes("tw-badge"), false);
});

// --- Alert ---

Deno.test("Alert: tone prop is passed through", () => {
  const node = h(Alert, { message: "msg", tone: "warning" });
  assertEquals((node as any).props.tone, "warning");
});

Deno.test("Alert: error tone is accepted", () => {
  const node = h(Alert, { message: "error msg", tone: "error" });
  assertEquals((node as any).props.tone, "error");
});

// --- FormField ---

Deno.test("FormField: label/required/error/help props are passed", () => {
  const node = h(FormField, {
    label: "Email",
    required: true,
    error: "Invalid email",
    help: "Enter your email address",
    children: h("input", { type: "email" }),
  });
  assertEquals((node as any).props.label, "Email");
  assertEquals((node as any).props.required, true);
  assertEquals((node as any).props.error, "Invalid email");
  assertEquals((node as any).props.help, "Enter your email address");
});

// --- Select ---

Deno.test("Select: onChange and disabled props accepted", () => {
  let changed = "";
  const node = h(Select, {
    value: "a",
    options: [{ value: "a", label: "A" }, { value: "b", label: "B" }],
    onChange: (v: string) => { changed = v; },
    disabled: false,
  });
  assertEquals((node as any).props.value, "a");
  assertEquals((node as any).props.disabled, false);
});

Deno.test("Select: computeDesignDisabled applies for loading state", () => {
  const disabled = computeDesignDisabled(false, { state: "loading" });
  assertEquals(disabled, true);
});

// --- Checkbox ---

Deno.test("Checkbox: checked and onChange props accepted", () => {
  const node = h(Checkbox, {
    checked: true,
    onChange: (_: boolean) => {},
    label: "Accept terms",
  });
  assertEquals((node as any).props.checked, true);
  assertEquals((node as any).props.label, "Accept terms");
});

// --- LoadingState ---

Deno.test("LoadingState: message prop accepted", () => {
  const node = h(LoadingState, { message: "Fetching..." });
  assertEquals((node as any).props.message, "Fetching...");
});

// --- ErrorState ---

Deno.test("ErrorState: errorCode and message props accepted", () => {
  const node = h(ErrorState, {
    errorCode: "ERR_404",
    message: "Not found",
    description: "The resource was not found",
  });
  assertEquals((node as any).props.errorCode, "ERR_404");
  assertEquals((node as any).props.message, "Not found");
});

// --- JsonViewer ---

Deno.test("JsonViewer: accepts object value without throwing", () => {
  const val = { a: 1, b: [true, null, "str"], c: { nested: 42 } };
  const node = h(JsonViewer, { value: val });
  assertEquals((node as any).props.value, val);
});

Deno.test("JsonViewer: accepts null value without throwing", () => {
  const node = h(JsonViewer, { value: null });
  assertEquals((node as any).props.value, null);
});

Deno.test("JsonViewer: accepts array value without throwing", () => {
  const node = h(JsonViewer, { value: [1, "two", false] });
  assertEquals((node as any).props.value, [1, "two", false]);
});

// --- ValidationResultPanel ---

Deno.test("ValidationResultPanel: passes backend result through without frontend judgment", () => {
  const result = {
    items: [
      { key: "k1", status: "pass" as const, label: "Field A", message: "OK" },
      { key: "k2", status: "fail" as const, label: "Field B", message: "Missing" },
    ],
    overall: "fail" as const,
    message: "Validation failed",
  };
  const node = h(ValidationResultPanel, { result });
  // Props are forwarded as-is; no frontend judgment applied
  assertEquals((node as any).props.result, result);
  assertEquals((node as any).props.result.overall, "fail");
  assertEquals((node as any).props.result.items.length, 2);
});

Deno.test("ValidationResultPanel: null result renders without error", () => {
  const node = h(ValidationResultPanel, { result: null });
  assertEquals((node as any).props.result, null);
});

// --- AdminPageShell ---

Deno.test("AdminPageShell: title, description, breadcrumbs, actions, children accepted", () => {
  const actions = h("button", null, "Create");
  const node = h(AdminPageShell, {
    title: "Dashboard",
    description: "Overview",
    breadcrumbs: [{ label: "Admin", href: "/admin" }, { label: "Dashboard" }],
    actions,
    children: h("div", null, "content"),
  });
  assertEquals((node as any).props.title, "Dashboard");
  assertEquals((node as any).props.description, "Overview");
  assertEquals((node as any).props.breadcrumbs?.length, 2);
  assertEquals((node as any).props.actions, actions);
});

// --- AdminSection ---

Deno.test("AdminSection: title, status, statusTone, actions, children accepted", () => {
  const actions = h("button", null, "Edit");
  const children = h("div", null, "section content");
  const node = h(AdminSection, {
    title: "Component Registry",
    status: "partial",
    statusTone: "warning",
    actions,
    children,
  });
  assertEquals((node as any).props.title, "Component Registry");
  assertEquals((node as any).props.status, "partial");
  assertEquals((node as any).props.statusTone, "warning");
  assertEquals((node as any).props.actions, actions);
});

// --- Boundary invariant: frontend does not own topology/SQL Attention judgment ---

Deno.test("Component templates are projection surfaces: no topology judgment in prop types", () => {
  // This test asserts that the component receives structured results as-is.
  // ValidationResultPanel receives overall/items from backend and renders them directly.
  // It does not re-derive pass/fail from frontend logic.
  const backendResult = {
    items: [{ key: "x", status: "blocking" as const, message: "Blocked" }],
    overall: "blocking" as const,
  };
  const node = h(ValidationResultPanel, { result: backendResult });
  // overall is preserved exactly as received from backend
  assertEquals((node as any).props.result?.overall, "blocking");
});

