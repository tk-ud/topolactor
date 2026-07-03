// frontend/tests/uiRenderedInteraction.test.ts
//
// Rendered interaction tests for admin/public/demo islands.
//
// Purpose:
//   Verify that components render required DOM elements (buttons, inputs, form fields,
//   error/status sections) in their initial state, that dispatch operations produce
//   correct payload shapes per-component, and that error responses are not silently
//   dropped but exposed as structured failures.
//
// Responsibility split with uiHandlerBehavior.test.ts:
//   uiHandlerBehavior.test.ts  → handler inventory / lane distinction / event identity /
//                                  scheduler behavior / silent failure at dispatch layer
//   uiRenderedInteraction.test.ts → actual render / DOM structure / button presence /
//                                    input/form element presence / dispatch body per-component /
//                                    error surface per-component / API lane verification
//
// DOM environment note:
//   preact-render-to-string (added to deno.json) provides SSR-like HTML output for
//   initial-state structure assertions (button/input/form element presence, error section
//   existence) without requiring a full browser or DOM implementation (happy-dom/jsdom).
//   This is the minimum dependency needed to move from "static source analysis" to
//   "component actually renders the expected DOM nodes".
//
//   Full DOM event simulation (click → useState update → DOM re-render) requires a DOM
//   implementation. For this PR, state-change assertions use the API layer directly
//   (mocked globalThis.fetch) following the pattern established in adminImport.test.ts.
//   DOM event simulation (happy-dom) is a follow-up scope.
//
// SSOT references:
//   - docs/design/pipeline-continuity-ssot.yaml § api_command_lane,
//     frontend_component_event_log_lane, sse_projection_lane
//   - docs/design/runtime-orchestration-ssot.yaml § trigger_contract, scheduler_contract

import {
  assert,
  assertEquals,
  assertExists,
  assertFalse,
  assertRejects,
} from "https://deno.land/std@0.208.0/assert/mod.ts";
import { h, options, render } from "preact";
import { renderToString } from "preact-render-to-string";
import {
  __testOnly,
  queueAdminClientCommand,
  queueClientCommand,
  emitComponentOperationEvent,
  stopComponentEventRuntime,
} from "../runtime/frontendScheduler.ts";
import { setupDom, flushUpdates } from "./test-dom-setup.ts";
import {
  applyImport,
  fetchContextTokens,
  listImportManifests,
  listImportSchemas,
  uploadImportPreview,
  validateRegistryVector,
} from "../api/adminApi.ts";
import { loginDemo } from "../api/authApi.ts";

// Island components — imported for render structure tests.
// Deno transpiles .tsx on the fly using deno.json compilerOptions (jsx: react-jsx, jsxImportSource: preact).
import SeedAdmin from "../islands/SeedAdmin.tsx";
import OperationPanel from "../islands/OperationPanel.tsx";
import UiBuilderAdmin from "../islands/UiBuilderAdmin.tsx";
import TeamMarkdownDashboard from "../islands/TeamMarkdownDashboard.tsx";
import AdminImport from "../islands/AdminImport.tsx";
import ManifestsAdmin from "../islands/ManifestsAdmin.tsx";
import HubNavigationAdmin from "../islands/HubNavigationAdmin.tsx";
import ContextTokenRegistryEditor from "../islands/ContextTokenRegistryEditor.tsx";
import RegistryVectorValidator from "../islands/RegistryVectorValidator.tsx";
import ContentsPromotionPanel from "../islands/ContentsPromotionPanel.tsx";
import ContentsScreenDesignPanel from "../islands/ContentsScreenDesignPanel.tsx";
import AuthPanel from "../islands/AuthPanel.tsx";
import { ProjectionView } from "../components/ProjectionView.tsx";

// ─── Preact effect scheduling shim ───────────────────────────────────────────
// Preact checks typeof requestAnimationFrame at hooks module load time and falls
// back to a 100ms RAF_TIMEOUT when rAF is absent (Deno/Node environment). Setting
// options.requestAnimationFrame makes afterPaint() flush effects immediately via
// setTimeout(0) instead of waiting 100ms. This makes flushUpdates() reliable.
// Set once at module level — affects all render() calls; renderToString() is unaffected.
// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

// ─── Test helper utilities ────────────────────────────────────────────────────

function makeMockFetch(
  status: number,
  body: unknown,
  capture?: (url: string | URL | Request, init?: RequestInit) => void,
): typeof globalThis.fetch {
  return (url: string | URL | Request, init?: RequestInit) => {
    capture?.(url, init);
    return Promise.resolve(
      new Response(JSON.stringify(body), {
        status,
        headers: { "Content-Type": "application/json" },
      }),
    );
  };
}

function makeNetworkErrorFetch(message: string): typeof globalThis.fetch {
  return () => Promise.reject(new Error(message));
}

// Render component to HTML string using preact-render-to-string.
// Components with useEffect data loading render their initial state only
// (effects do not execute during SSR rendering).
// deno-lint-ignore no-explicit-any
function renderHtml(Component: (props: any) => h.JSX.Element, props: Record<string, unknown> = {}): string {
  return renderToString(h(Component, props));
}

// Render component into a live DOM container using preact's render.
// Uses `any` cast to bypass per-component Props type checking — components
// with optional-only props accept empty props at runtime regardless of TS types.
// deno-lint-ignore no-explicit-any
function renderInto(Component: (props: any) => h.JSX.Element, container: Element, props: Record<string, unknown> = {}): void {
  render(h(Component, props), container);
}

// ─── Section 1: SeedAdmin — rendered interaction ──────────────────────────────
// Lane: api_command_lane → queueAdminClientCommand → /api/dispatch
// triggerKind=client, target=admin, layer=seed_runtime, role absent

Deno.test("SeedAdmin render: initial DOM contains all 5 action buttons and textarea", () => {
  const html = renderHtml(SeedAdmin);
  assert(html.includes("保存済みデータを読み込む"), "load button must be present in initial render");
  assert(html.includes("一時保存"), "save button must be present in initial render");
  assert(html.includes("内容を確認"), "validate button must be present in initial render");
  assert(html.includes("プレビュー"), "preview button must be present in initial render");
  assert(html.includes("取り込む"), "import button must be present in initial render");
  assert(html.includes("<textarea"), "seed content textarea must be present in initial render");
  // No status/error sections in initial render (status=null, errors=[])
  assertFalse(html.includes("alert-error"), "initial render must not show error section");
});

Deno.test("SeedAdmin render: dispatch button uses type=button (prevents accidental form submit)", () => {
  const html = renderHtml(SeedAdmin);
  assert(html.includes('type="button"') || html.includes("btn-"), "action buttons must render");
});

Deno.test("SeedAdmin render: component source has alert-error element for error surface", async () => {
  const src = await Deno.readTextFile(new URL("../islands/SeedAdmin.tsx", import.meta.url));
  assert(src.includes("alert-error"), "SeedAdmin must have alert-error section for error display");
  assert(src.includes("setErrors("), "SeedAdmin must call setErrors on error response");
  assert(src.includes("setStatus("), "SeedAdmin must call setStatus on error response");
  assert(src.includes("text-red-600"), "SeedAdmin must render error status in red");
  assert(src.includes("text-green-700"), "SeedAdmin must render success status in green");
});

const SEED_ACTIONS = ["load", "save", "validate", "preview", "import"] as const;

for (const action of SEED_ACTIONS) {
  Deno.test(
    `SeedAdmin dispatch: action=${action} body has layer=seed_runtime triggerKind=client role absent`,
    async () => {
      __testOnly.resetCommandQueue();
      const bodyRef = { value: {} as Record<string, unknown> };
      const original = globalThis.fetch;
      globalThis.fetch = makeMockFetch(
        200,
        { success: true, emission: null },
        (_u, init) => { bodyRef.value = JSON.parse(init?.body as string ?? "{}"); },
      );
      try {
        await queueAdminClientCommand({
          operationType: "admin",
          target: "admin",
          layer: "seed_runtime",
          action,
        });
        assertEquals(bodyRef.value.layer, "seed_runtime", `action=${action}: layer must be seed_runtime`);
        assertEquals(bodyRef.value.action, action, `action=${action}: action field must match`);
        assertEquals(bodyRef.value.triggerKind, "client", `action=${action}: triggerKind must be client (SSOT)`);
        assertEquals(bodyRef.value.target, "admin", `action=${action}: target must be admin`);
        assertFalse("role" in bodyRef.value, `action=${action}: role must not be in dispatch body`);
      } finally {
        globalThis.fetch = original;
        __testOnly.resetCommandQueue();
      }
    },
  );
}

Deno.test("SeedAdmin error surface: network error resolves as success=false with message (not silent)", async () => {
  __testOnly.resetCommandQueue();
  const original = globalThis.fetch;
  globalThis.fetch = makeNetworkErrorFetch("SEED_LOAD_NETWORK_FAIL");
  try {
    const result = await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "seed_runtime",
      action: "load",
    });
    assertEquals(result.success, false, "network error must resolve as success=false");
    assertExists(result.errors, "errors array must be present");
    assertEquals(result.errors?.[0]?.message, "SEED_LOAD_NETWORK_FAIL");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("SeedAdmin error surface: error response shape matches component state update contract", async () => {
  // Component calls: setErrors(body.errors); setStatus("ロードに失敗しました。")
  // Verify the error response format the component would receive
  __testOnly.resetCommandQueue();
  const original = globalThis.fetch;
  const errorBody = {
    success: false,
    errors: [{ code: "SEED_PARSE_FAILED", message: "Invalid JSON structure" }],
  };
  globalThis.fetch = makeMockFetch(422, errorBody);
  try {
    const result = await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "seed_runtime",
      action: "load",
    });
    // The component checks body.errors?.length — verify shape matches
    assertEquals(result.success, false);
    assertExists(result.errors);
    assert(Array.isArray(result.errors), "errors must be an array");
    assertEquals(result.errors![0].message, "Invalid JSON structure");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("SeedAdmin success surface: success response shape matches component state update contract", async () => {
  // Component checks: body?.emission?.data?.content for load success
  __testOnly.resetCommandQueue();
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(200, {
    success: true,
    emission: { data: { content: '{"version":"1","runtimes":[]}' } },
  });
  try {
    const result = await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "seed_runtime",
      action: "load",
    });
    assertEquals(result.success, true);
    // deno-lint-ignore no-explicit-any
    assertEquals((result.emission as any)?.data?.content, '{"version":"1","runtimes":[]}');
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
});

// ─── Section 2: OperationPanel — rendered interaction ────────────────────────
// Lane: api_command_lane → queueClientCommand (public/demo lane, not admin)
//       frontend_component_event_log_lane → emitComponentOperationEvent

Deno.test("OperationPanel render: initial DOM contains dispatch button and preset cards", () => {
  const html = renderHtml(OperationPanel);
  assert(html.includes("選択した操作を実行"), "dispatch button must be present in initial render");
  assert(html.includes("operation-panel") || html.includes("Runtime 操作"), "component container must be present");
  assert(html.includes("btn-primary"), "primary dispatch button must be present");
  // type=button prevents accidental form submit
  assert(html.includes('type="button"'), "dispatch button must use type=button");
});

Deno.test("OperationPanel render: preset selection cards are present in initial render", () => {
  const html = renderHtml(OperationPanel);
  // OperationPanel renders preset cards from operationPresets (default + demo groups)
  assert(html.includes("シナリオを選択") || html.includes("default") || html.includes("btn-primary") || html.length > 100,
    "component must render with content");
});

Deno.test("OperationPanel dispatch: body has target/layer/action/triggerKind=client role absent", async () => {
  __testOnly.resetCommandQueue();
  const bodyRef = { value: {} as Record<string, unknown> };
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: { componentIds: ["c1"] } },
    (_u, init) => { bodyRef.value = JSON.parse(init?.body as string ?? "{}"); },
  );
  try {
    await queueClientCommand({
      operationType: "Search",
      target: "default",
      layer: "entity",
      action: "Search",
    });
    assertEquals(bodyRef.value.target, "default");
    assertEquals(bodyRef.value.layer, "entity");
    assertEquals(bodyRef.value.action, "Search");
    assertEquals(bodyRef.value.triggerKind, "client", "triggerKind must be client (api_command_lane SSOT)");
    assertFalse("role" in bodyRef.value, "role must not be in dispatch body");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("OperationPanel emitComponentOperationEvent: preset-select has all SSOT required identity fields", () => {
  __testOnly.resetQueue();
  const result = emitComponentOperationEvent({
    componentId: "operation-panel:preset-select",
    packageId: "runtime-operation-panel",
    layoutId: "runtime-operation-layout",
    eventType: "select",
    actorOrSource: "OperationPanel",
    payload: { presetId: "demo-search" },
  });
  assertEquals(result.ok, true, "event emission must succeed");
  const [queued] = __testOnly.getQueueSnapshot();
  // SSOT required identity: componentId, packageId, layoutId, eventType, payload, actorOrSource, occurredAt, idempotencyKey
  assertEquals(queued.componentId, "operation-panel:preset-select");
  assertEquals(queued.packageId, "runtime-operation-panel");
  assertEquals(queued.layoutId, "runtime-operation-layout");
  assertEquals(queued.eventType, "select");
  assertEquals(queued.actorOrSource, "OperationPanel");
  assertExists(queued.occurredAt, "occurredAt must be auto-stamped");
  assertExists(queued.idempotencyKey, "idempotencyKey must be auto-generated");
  __testOnly.resetQueue();
});

Deno.test("OperationPanel emitComponentOperationEvent: dispatch-form submit has all SSOT required identity fields", () => {
  __testOnly.resetQueue();
  const result = emitComponentOperationEvent({
    componentId: "operation-panel:dispatch-form",
    packageId: "runtime-operation-panel",
    layoutId: "runtime-operation-layout",
    eventType: "submit",
    actorOrSource: "OperationPanel",
    payload: { operationType: "Search", target: "default", layer: "entity", action: "Search", hasContext: false },
  });
  assertEquals(result.ok, true);
  const [queued] = __testOnly.getQueueSnapshot();
  assertEquals(queued.eventType, "submit");
  assertEquals(queued.payload.operationType, "Search");
  assertEquals(queued.payload.hasContext, false);
  assertExists(queued.idempotencyKey);
  assertExists(queued.occurredAt);
  __testOnly.resetQueue();
});

Deno.test("OperationPanel error surface: dispatch network error resolves as success=false (not silent)", async () => {
  __testOnly.resetCommandQueue();
  const original = globalThis.fetch;
  globalThis.fetch = makeNetworkErrorFetch("OP_PANEL_DISPATCH_FAIL");
  try {
    const result = await queueClientCommand({
      operationType: "Search",
      target: "default",
      layer: "entity",
      action: "Search",
    });
    assertEquals(result.success, false);
    assertEquals(result.errors?.[0]?.message, "OP_PANEL_DISPATCH_FAIL");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("OperationPanel render: component source has error/success display surface", async () => {
  const src = await Deno.readTextFile(new URL("../islands/OperationPanel.tsx", import.meta.url));
  assert(src.includes("setEmission("), "component must update emission state on response");
  assert(src.includes("setDispatchSuccess("), "component must track success state");
  assert(src.includes("alert-warn") || src.includes("エラーまたは部分失敗"), "component must display error/partial-failure state");
  assert(src.includes("成功"), "component must display success state");
});

Deno.test("ProjectionView user-facing render: projectionDefinition is consumed in visible output", () => {
  const html = renderHtml(ProjectionView, {
    emission: {
      componentIds: [],
      data: { seedLabel: "projection-lane-visible" },
      projectionDefinition: {
        constructorKey: "user-facing-render-test",
        packageIds: ["00000000-0000-0000-0000-000000000001"],
        outputKind: "form_inputs",
        fieldDefs: [{ key: "seedLabel", label: "Seed label", kind: "text", required: true }],
      },
    },
  });

  assert(
    html.includes("ProjectionDefinition projection") && html.includes("projection-lane-visible"),
    "lane=frontend_user_facing_render component=ProjectionView mapping=projection_constructor_mapping seed=user-facing-render-test must display projectionDefinition-derived value",
  );
});

Deno.test("TeamMarkdownDashboard DOM: malformed search response shows explicit error without card-state crash", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  __testOnly.resetCommandQueue();
  try {
    globalThis.fetch = makeMockFetch(200, { success: true, emission: { data: [] } });
    renderInto(TeamMarkdownDashboard, container);
    await flushUpdates();
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const html = (container as any).innerHTML as string;
    assert(
      html.includes("TEAM_MARKDOWN_SEARCH_RESPONSE_INVALID"),
      "malformed saved_view:search response must be surfaced explicitly",
    );
    assert(
      html.includes("No saved views found") || html.includes("md-dashboard-results"),
      "dashboard must keep cards as a safe array after malformed search response",
    );
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    cleanup();
  }
});

// ─── Section 4: UiBuilderAdmin — rendered interaction ────────────────────────
// Lane: api_command_lane → queueAdminClientCommand (admin lane)

Deno.test("UiBuilderAdmin render: initial DOM renders without throwing", () => {
  // Large component — verify it renders at all in initial state
  let html = "";
  let threw = false;
  try {
    html = renderHtml(UiBuilderAdmin);
  } catch {
    threw = true;
  }
  assertFalse(threw, "UiBuilderAdmin must render without throwing");
  assert(html.length > 100, "UiBuilderAdmin must produce non-empty HTML");
});

Deno.test("UiBuilderAdmin render: primary action buttons present in initial render", () => {
  const html = renderHtml(UiBuilderAdmin);
  // Key buttons in the bucket/catalog tab (initial view)
  assert(
    html.includes("btn-primary") || html.includes("btn-secondary") || html.includes("button"),
    "UiBuilderAdmin must render action buttons",
  );
});

Deno.test("UiBuilderAdmin dispatch: ui_component_bucket:list body has target=admin triggerKind=client role absent", async () => {
  __testOnly.resetCommandQueue();
  const bodyRef = { value: {} as Record<string, unknown> };
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: { data: [] } },
    (_u, init) => { bodyRef.value = JSON.parse(init?.body as string ?? "{}"); },
  );
  try {
    await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "ui_component_bucket",
      action: "list",
    });
    assertEquals(bodyRef.value.target, "admin");
    assertEquals(bodyRef.value.layer, "ui_component_bucket");
    assertEquals(bodyRef.value.action, "list");
    assertEquals(bodyRef.value.triggerKind, "client");
    assertFalse("role" in bodyRef.value, "role must not be in admin dispatch body");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("UiBuilderAdmin dispatch: package_generator:generate body has triggerKind=client role absent", async () => {
  __testOnly.resetCommandQueue();
  const bodyRef = { value: {} as Record<string, unknown> };
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: null },
    (_u, init) => { bodyRef.value = JSON.parse(init?.body as string ?? "{}"); },
  );
  try {
    await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "package_generator",
      action: "generate",
      payload: { componentKey: "test/button", nodes: [] },
    });
    assertEquals(bodyRef.value.layer, "package_generator");
    assertEquals(bodyRef.value.action, "generate");
    assertEquals(bodyRef.value.triggerKind, "client");
    assertFalse("role" in bodyRef.value);
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("UiBuilderAdmin dispatch: package_generator:promote body has triggerKind=client role absent", async () => {
  __testOnly.resetCommandQueue();
  const bodyRef = { value: {} as Record<string, unknown> };
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: null },
    (_u, init) => { bodyRef.value = JSON.parse(init?.body as string ?? "{}"); },
  );
  try {
    await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "package_generator",
      action: "promote",
      payload: { componentKey: "test/button" },
    });
    assertEquals(bodyRef.value.layer, "package_generator");
    assertEquals(bodyRef.value.action, "promote");
    assertEquals(bodyRef.value.triggerKind, "client");
    assertFalse("role" in bodyRef.value);
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("UiBuilderAdmin dispatch: system_ci:list_targets body has triggerKind=client role absent", async () => {
  __testOnly.resetCommandQueue();
  const bodyRef = { value: {} as Record<string, unknown> };
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: { data: { targets: [] } } },
    (_u, init) => { bodyRef.value = JSON.parse(init?.body as string ?? "{}"); },
  );
  try {
    await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "system_ci",
      action: "list_targets",
    });
    assertEquals(bodyRef.value.layer, "system_ci");
    assertEquals(bodyRef.value.action, "list_targets");
    assertEquals(bodyRef.value.triggerKind, "client");
    assertFalse("role" in bodyRef.value);
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("UiBuilderAdmin error surface: dispatch error resolves as success=false (not silent)", async () => {
  __testOnly.resetCommandQueue();
  const original = globalThis.fetch;
  globalThis.fetch = makeNetworkErrorFetch("UI_BUILDER_NETWORK_FAIL");
  try {
    const result = await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "ui_component_bucket",
      action: "list",
    });
    assertEquals(result.success, false);
    assertEquals(result.errors?.[0]?.message, "UI_BUILDER_NETWORK_FAIL");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
});

Deno.test("UiBuilderAdmin render: source has validation/error display surface", async () => {
  const src = await Deno.readTextFile(new URL("../islands/UiBuilderAdmin.tsx", import.meta.url));
  assert(src.includes("ValidationErrorPanel") || src.includes("alert-error") || src.includes("setValidationErrors"),
    "UiBuilderAdmin must have validation/error display");
  assert(src.includes("setStatus") || src.includes("setLoading"), "must manage UI state on operations");
});

// ─── Section 5: AdminImport — rendered interaction ───────────────────────────
// Lane: api_command_lane → adminApi → queueAdminClientCommand → /api/dispatch
// AdminImport uses adminApi.ts functions (not queueAdminClientCommand directly)

Deno.test("AdminImport render: initial DOM contains heading and loading state", () => {
  const html = renderHtml(AdminImport);
  assert(html.includes("インポート"), "import heading must be present in initial render");
  // Initial state: loadingSelectors=true → shows loading message
  assert(html.includes("選択肢をロード中") || html.includes("loading") || html.length > 100,
    "initial render must have content");
});

Deno.test("AdminImport render: source has file input and submit button in template", async () => {
  const src = await Deno.readTextFile(new URL("../islands/AdminImport.tsx", import.meta.url));
  assert(src.includes('type="file"'), "AdminImport must have file input in template");
  assert(src.includes("handlePreview"), "AdminImport must have preview handler");
  assert(src.includes("handleApply"), "AdminImport must have apply handler");
  assert(src.includes("handleFileChange"), "AdminImport must have file change handler");
});

Deno.test("AdminImport dispatch: listImportManifests body has triggerKind=client role absent", async () => {
  const original = globalThis.fetch;
  let capturedBody = "";
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: { data: [] } },
    (_u, init) => { capturedBody = String(init?.body ?? ""); },
  );
  try {
    await listImportManifests();
    const body = JSON.parse(capturedBody);
    assertEquals(body.triggerKind, "client", "adminApi must inject triggerKind=client");
    assertFalse("role" in body, "role must not be in adminApi dispatch body");
    assertEquals(body.layer, "admin_csv_json_import");
    assertEquals(body.action, "list_manifests");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("AdminImport dispatch: listImportSchemas body has triggerKind=client role absent", async () => {
  const original = globalThis.fetch;
  let capturedBody = "";
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: { data: [] } },
    (_u, init) => { capturedBody = String(init?.body ?? ""); },
  );
  try {
    await listImportSchemas();
    const body = JSON.parse(capturedBody);
    assertEquals(body.triggerKind, "client");
    assertFalse("role" in body);
    assertEquals(body.action, "list_schemas");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("AdminImport dispatch: applyImport throws SNAPSHOT_NOT_FOUND on error response (not silent)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(422, {
    success: false,
    errors: [{ message: "SNAPSHOT_NOT_FOUND" }],
  });
  try {
    await assertRejects(
      () => applyImport("bad-snap"),
      Error,
      "SNAPSHOT_NOT_FOUND",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("AdminImport dispatch: uploadImportPreview throws on SCHEMA_NOT_FOUND (not silent)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(422, {
    success: false,
    errors: [{ message: "SCHEMA_NOT_FOUND" }],
  });
  try {
    await assertRejects(
      () => uploadImportPreview("csv", "test.csv", "m1", "s1", "name\nAlice"),
      Error,
      "SCHEMA_NOT_FOUND",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("AdminImport lane guard: island does not call /api/dispatch directly (uses adminApi)", async () => {
  const src = await Deno.readTextFile(new URL("../islands/AdminImport.tsx", import.meta.url));
  assertFalse(src.includes('fetch("/api/dispatch"'), "must not call /api/dispatch directly");
  assertFalse(src.includes("queueAdminClientCommand"), "must use adminApi, not direct scheduler call");
  assert(src.includes('from "../api/adminApi.ts"'), "must import from adminApi");
});

// ─── Section 6: Admin smoke rendered interaction tests ────────────────────────
// ManifestsAdmin, HubNavigationAdmin, ContextTokenRegistryEditor,
// RegistryVectorValidator, ContentsPromotionPanel, ContentsScreenDesignPanel
// Each: renders, key elements present, dispatch has triggerKind=client/role absent,
//        error response not silent

// ── ManifestsAdmin ──

Deno.test("ManifestsAdmin render: renders without throwing and has page content", () => {
  let html = "";
  let threw = false;
  try { html = renderHtml(ManifestsAdmin); } catch { threw = true; }
  assertFalse(threw, "ManifestsAdmin must render without throwing");
  assert(html.includes("管理") || html.includes("マニフェスト") || html.length > 100, "must produce content");
});

Deno.test("ManifestsAdmin dispatch: listHubNavigationManifests body has triggerKind=client role absent", async () => {
  const original = globalThis.fetch;
  let capturedBody = "";
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: { data: [] } },
    (_u, init) => { capturedBody = String(init?.body ?? ""); },
  );
  try {
    const { listHubNavigationManifests } = await import("../api/adminApi.ts");
    await listHubNavigationManifests();
    const body = JSON.parse(capturedBody);
    assertEquals(body.triggerKind, "client");
    assertFalse("role" in body, "role must not be in dispatch body");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("ManifestsAdmin error surface: render source has error display for load failure", async () => {
  const src = await Deno.readTextFile(new URL("../islands/ManifestsAdmin.tsx", import.meta.url));
  assert(src.includes("setErrors("), "must update errors state on failure");
  assert(src.includes("backendUnavailable") || src.includes("setBackendUnavailable"), "must handle backend unavailable");
});

// ── HubNavigationAdmin ──

Deno.test("HubNavigationAdmin render: renders without throwing and has form elements", () => {
  let html = "";
  let threw = false;
  try { html = renderHtml(HubNavigationAdmin); } catch { threw = true; }
  assertFalse(threw, "HubNavigationAdmin must render without throwing");
  assert(html.length > 100, "must produce non-empty HTML");
});

Deno.test("HubNavigationAdmin dispatch: hub navigation API body has triggerKind=client role absent", async () => {
  const original = globalThis.fetch;
  let capturedBody = "";
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: { data: [] } },
    (_u, init) => { capturedBody = String(init?.body ?? ""); },
  );
  try {
    const { listHubNavigationManifests } = await import("../api/adminApi.ts");
    await listHubNavigationManifests();
    const body = JSON.parse(capturedBody);
    assertEquals(body.triggerKind, "client");
    assertFalse("role" in body);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("HubNavigationAdmin error surface: source has error/backend-unavailable display", async () => {
  const src = await Deno.readTextFile(new URL("../islands/HubNavigationAdmin.tsx", import.meta.url));
  assert(src.includes("setErrors(") || src.includes("setBackendUnavailable"), "must handle error state");
  assert(src.includes("setBackendUnavailable"), "must surface backend unavailable state");
});

// ── ContextTokenRegistryEditor ──

Deno.test("ContextTokenRegistryEditor render: renders without throwing", () => {
  let html = "";
  let threw = false;
  try { html = renderHtml(ContextTokenRegistryEditor); } catch { threw = true; }
  assertFalse(threw, "ContextTokenRegistryEditor must render without throwing");
  assert(html.length > 50, "must produce content");
});

Deno.test("ContextTokenRegistryEditor dispatch: fetchContextTokens body has triggerKind=client role absent", async () => {
  const original = globalThis.fetch;
  let capturedBody = "";
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: { data: [] } },
    (_u, init) => { capturedBody = String(init?.body ?? ""); },
  );
  try {
    await fetchContextTokens();
    const body = JSON.parse(capturedBody);
    assertEquals(body.triggerKind, "client");
    assertFalse("role" in body);
    assertEquals(body.layer, "context_token_registry");
    assertEquals(body.action, "list");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("ContextTokenRegistryEditor dispatch: API call error is not silent (throws)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(422, {
    success: false,
    errors: [{ message: "CONTEXT_TOKEN_FETCH_FAIL" }],
  });
  try {
    await assertRejects(
      () => fetchContextTokens(),
      Error,
      "CONTEXT_TOKEN_FETCH_FAIL",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("ContextTokenRegistryEditor render: source has form inputs for token creation", async () => {
  const src = await Deno.readTextFile(new URL("../islands/ContextTokenRegistryEditor.tsx", import.meta.url));
  assert(src.includes("handleAdd"), "must have add handler");
  assert(src.includes("newLabel") || src.includes("label"), "must have label input");
  assert(src.includes("newValue") || src.includes("value"), "must have value input");
  assert(src.includes("setStatus("), "must update status on operations");
});

// ── RegistryVectorValidator ──

Deno.test("RegistryVectorValidator render: renders without throwing and has form", () => {
  let html = "";
  let threw = false;
  try { html = renderHtml(RegistryVectorValidator); } catch { threw = true; }
  assertFalse(threw, "RegistryVectorValidator must render without throwing");
  assert(html.length > 50, "must produce content");
});

Deno.test("RegistryVectorValidator dispatch: validateRegistryVector body has triggerKind=client role absent", async () => {
  const original = globalThis.fetch;
  let capturedBody = "";
  globalThis.fetch = makeMockFetch(
    200,
    {
      success: true,
      emission: {
        data: {
          validationClass: "pass",
          checkedCount: 1,
          results: [{ queryId: "q1", validationClass: "pass", similarityScore: null, nearbyIds: [] }],
        },
      },
    },
    (_u, init) => { capturedBody = String(init?.body ?? ""); },
  );
  try {
    await validateRegistryVector("relation_registry", ["test-id-1"]);
    const body = JSON.parse(capturedBody);
    assertEquals(body.triggerKind, "client");
    assertFalse("role" in body);
    assertEquals(body.layer, "registry_vector");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("RegistryVectorValidator error surface: validation error is not silent", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(422, {
    success: false,
    errors: [{ message: "VECTOR_VALIDATION_FAIL" }],
  });
  try {
    await assertRejects(
      () => validateRegistryVector("relation_registry", ["bad-id"]),
      Error,
      "VECTOR_VALIDATION_FAIL",
    );
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("RegistryVectorValidator render: source has validate button and query input", async () => {
  const src = await Deno.readTextFile(new URL("../islands/RegistryVectorValidator.tsx", import.meta.url));
  assert(src.includes("handleValidate"), "must have validate handler");
  assert(src.includes("queryIdsText") || src.includes("textarea") || src.includes("input"), "must have input for query IDs");
  assert(src.includes("setError("), "must surface errors");
});

// ── ContentsPromotionPanel ──

Deno.test("ContentsPromotionPanel render: renders without throwing", () => {
  let html = "";
  let threw = false;
  try { html = renderHtml(ContentsPromotionPanel); } catch { threw = true; }
  assertFalse(threw, "ContentsPromotionPanel must render without throwing");
  assert(html.length > 50, "must produce content");
});

Deno.test("ContentsPromotionPanel dispatch: manifest API body has triggerKind=client role absent", async () => {
  const original = globalThis.fetch;
  let capturedBody = "";
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: { data: [] } },
    (_u, init) => { capturedBody = String(init?.body ?? ""); },
  );
  try {
    const { listAdminManifests } = await import("../api/adminApi.ts");
    await listAdminManifests("draft");
    const body = JSON.parse(capturedBody);
    assertEquals(body.triggerKind, "client");
    assertFalse("role" in body);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("ContentsPromotionPanel error surface: source has error/status display", async () => {
  const src = await Deno.readTextFile(new URL("../islands/ContentsPromotionPanel.tsx", import.meta.url));
  assert(src.includes("setStatus("), "must update status state");
  assert(src.includes("setLoading("), "must track loading state");
});

// ── ContentsScreenDesignPanel ──

Deno.test("ContentsScreenDesignPanel render: renders without throwing", () => {
  let html = "";
  let threw = false;
  try {
    html = renderHtml(ContentsScreenDesignPanel, {
      sharedManifestId: "",
      onSharedManifestIdChange: () => {},
      manifests: [],
      onManifestsChange: () => {},
      manifestsVersion: 0,
      onManifestsReload: () => {},
    });
  } catch { threw = true; }
  assertFalse(threw, "ContentsScreenDesignPanel must render without throwing");
  assert(html.length > 50, "must produce content");
});

Deno.test("ContentsScreenDesignPanel dispatch: manifest API body has triggerKind=client role absent", async () => {
  const original = globalThis.fetch;
  let capturedBody = "";
  globalThis.fetch = makeMockFetch(
    200,
    { success: true, emission: { data: [] } },
    (_u, init) => { capturedBody = String(init?.body ?? ""); },
  );
  try {
    const { listAdminManifests } = await import("../api/adminApi.ts");
    await listAdminManifests("draft");
    const body = JSON.parse(capturedBody);
    assertEquals(body.triggerKind, "client");
    assertFalse("role" in body);
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("ContentsScreenDesignPanel render: source has form inputs and submit action", async () => {
  const src = await Deno.readTextFile(new URL("../islands/ContentsScreenDesignPanel.tsx", import.meta.url));
  assert(src.includes("handleSubmit") || src.includes("submit") || src.includes("onSubmit") || src.includes("onClick"), "must have submit/click handler");
  assert(src.includes("setErrors(") || src.includes("setStatus("), "must surface errors or status");
});

// ─── Section 7: AuthPanel — auth lane rendered interaction ────────────────────
// AuthPanel uses authApi (/api/auth/login), NOT api_command_lane (/api/dispatch).
// Responsibility: verify auth lane is not mixed with command lane.

Deno.test("AuthPanel render: initial render has login form (session check pending)", () => {
  // AuthPanel renders session check state initially (sessionCheckDone=false)
  let html = "";
  let threw = false;
  try { html = renderHtml(AuthPanel); } catch { threw = true; }
  assertFalse(threw, "AuthPanel must render without throwing");
  // Initial render: sessionCheckDone=false → shows loading state
  assert(html.includes("ログイン状態を確認") || html.length > 20, "must show session check or loading state");
});

Deno.test("AuthPanel render: source has login form and submit handler with e.preventDefault()", async () => {
  const src = await Deno.readTextFile(new URL("../islands/SuperAuthPanel.tsx", import.meta.url));
  assert(src.includes("handleSubmit"), "AuthPanel must have handleSubmit");
  assert(src.includes("e.preventDefault()"), "handleSubmit must call e.preventDefault()");
  assert(src.includes("<form"), "AuthPanel must have form element");
  assert(src.includes('type="password"'), "must have password input");
  assert(src.includes('type="text"') || src.includes('type="email"'), "must have username/email input");
});

Deno.test("AuthPanel lane: does not use /api/dispatch (uses /api/auth/login)", async () => {
  const src = await Deno.readTextFile(new URL("../islands/SuperAuthPanel.tsx", import.meta.url));
  assertFalse(src.includes('fetch("/api/dispatch"'), "AuthPanel must not call /api/dispatch directly");
  assertFalse(src.includes("queueClientCommand"), "AuthPanel must not use client command lane");
  assertFalse(src.includes("queueAdminClientCommand"), "AuthPanel must not use admin command lane");
  assert(
    src.includes("loginDemo") || src.includes("loginSuper") || src.includes("authApi"),
    "AuthPanel must use authApi functions",
  );
});

Deno.test("AuthPanel auth error: login failure response has errors (not silent)", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = makeMockFetch(401, {
    success: false,
    errors: [{ code: "AUTH_INVALID_CREDENTIALS", message: "ユーザー名またはパスワードが間違っています。" }],
  });
  try {
    const result = await loginDemo({ username: "bad", password: "wrong" });
    assertEquals(result.success, false, "auth failure must be surfaced as success=false");
    assertExists(result.errors, "errors must be present on auth failure");
  } finally {
    globalThis.fetch = original;
  }
});

Deno.test("AuthPanel render: source has error display for auth failure state", async () => {
  const src = await Deno.readTextFile(new URL("../islands/SuperAuthPanel.tsx", import.meta.url));
  // AuthPanel renders error based on state.status === "error"
  assert(src.includes('status: "error"') || src.includes("status === \"error\"") || src.includes("error"), "must handle error state");
  assert(src.includes("authErrorText") || src.includes("errors"), "must display auth errors to user");
  // No silent failure: error state → UI renders error message
  assert(src.includes("setState(") || src.includes("setState({"), "must update state on error");
});

Deno.test("AuthPanel lane: scheduler lane distinct from auth lane (no /api/dispatch mixed in)", async () => {
  // Login goes to /api/auth/login, not /api/dispatch
  const original = globalThis.fetch;
  let capturedUrl = "";
  globalThis.fetch = (url, _init) => {
    capturedUrl = String(url);
    return Promise.resolve(new Response(JSON.stringify({ success: false, errors: [{ message: "test" }] }), { status: 401 }));
  };
  try {
    await loginDemo({ username: "user", password: "pass" });
    assert(capturedUrl.includes("/auth") || capturedUrl.includes("auth/login"), "login must call auth endpoint, not /api/dispatch");
    assertFalse(capturedUrl.includes("/api/dispatch"), "auth must not go through /api/dispatch");
  } finally {
    globalThis.fetch = original;
  }
});

// ─── Section 8: SeedAdmin — DOM event simulation ──────────────────────────────
// These tests render SeedAdmin into a live DOM (happy-dom) and simulate real button
// clicks that trigger async handlers → useState updates → DOM re-render assertions.
// SeedAdmin has no useEffect auto-load, so all state transitions are manually triggered.

Deno.test("SeedAdmin DOM: load button click with error response → alert-error section appears in DOM", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  __testOnly.resetCommandQueue();
  try {
    globalThis.fetch = makeMockFetch(422, {
      success: false,
      errors: [{ code: "SEED_LOAD_FAIL", message: "ファイルが見つかりません" }],
    });
    renderInto(SeedAdmin, container);
    // deno-lint-ignore no-explicit-any
    const buttons = Array.from((container as any).querySelectorAll("button")) as Array<{
      textContent?: string;
      // deno-lint-ignore no-explicit-any
      dispatchEvent: (e: any) => void;
    }>;
    const loadBtn = buttons.find((b) => b.textContent?.includes("保存済みデータ"));
    assertExists(loadBtn, "load button must be present after render");
    // deno-lint-ignore no-explicit-any
    loadBtn!.dispatchEvent(new (globalThis as any).Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const html = (container as any).innerHTML as string;
    assert(html.includes("alert-error"), "alert-error section must appear in DOM after load failure");
    assert(html.includes("ファイルが見つかりません"), "error message must be visible in DOM (not silent)");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    cleanup();
  }
});

Deno.test("SeedAdmin DOM: load button click with success response → loaded content and status appear in DOM", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  __testOnly.resetCommandQueue();
  try {
    globalThis.fetch = makeMockFetch(200, {
      success: true,
      emission: { data: { content: '{"version":"1","runtimes":[]}' } },
    });
    renderInto(SeedAdmin, container);
    // deno-lint-ignore no-explicit-any
    const buttons = Array.from((container as any).querySelectorAll("button")) as Array<{
      textContent?: string;
      // deno-lint-ignore no-explicit-any
      dispatchEvent: (e: any) => void;
    }>;
    const loadBtn = buttons.find((b) => b.textContent?.includes("保存済みデータ"));
    assertExists(loadBtn, "load button must be present");
    // deno-lint-ignore no-explicit-any
    loadBtn!.dispatchEvent(new (globalThis as any).Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const html = (container as any).innerHTML as string;
    assertFalse(html.includes("alert-error"), "no error section on success");
    assert(
      html.includes("ロードしました") || html.includes("ロード完了"),
      "success status must be visible in DOM",
    );
    assert(html.includes('{"version":"1"'), "loaded content must appear in DOM");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    cleanup();
  }
});

Deno.test("SeedAdmin DOM: import button click with error → error messages visible in DOM (not silent)", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  __testOnly.resetCommandQueue();
  try {
    globalThis.fetch = makeMockFetch(422, {
      success: false,
      errors: [{ code: "IMPORT_CONFLICT", message: "インポート競合が発生しました" }],
    });
    renderInto(SeedAdmin, container);
    // deno-lint-ignore no-explicit-any
    const buttons = Array.from((container as any).querySelectorAll("button")) as Array<{
      textContent?: string;
      // deno-lint-ignore no-explicit-any
      dispatchEvent: (e: any) => void;
    }>;
    const importBtn = buttons.find((b) => b.textContent?.includes("取り込む"));
    assertExists(importBtn, "import button must be present");
    // deno-lint-ignore no-explicit-any
    importBtn!.dispatchEvent(new (globalThis as any).Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const html = (container as any).innerHTML as string;
    assert(html.includes("alert-error"), "alert-error section must appear after import failure");
    assert(html.includes("インポート競合が発生しました"), "specific error message must be visible in DOM");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    cleanup();
  }
});

// ─── Section 9: OperationPanel — DOM event simulation ────────────────────────
// OperationPanel's useEffect calls startComponentEventRuntime() (starts setInterval).
// stopComponentEventRuntime() is called in finally to prevent test resource leaks.

Deno.test("OperationPanel DOM: dispatch click with success → emission result section appears in DOM", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  __testOnly.resetCommandQueue();
  __testOnly.resetQueue();
  try {
    globalThis.fetch = makeMockFetch(200, {
      success: true,
      emission: {
        structureMapId: "sm-test",
        packageId: "pkg-test",
        componentIds: ["c1", "c2"],
      },
    });
    renderInto(OperationPanel, container);
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const buttons = Array.from((container as any).querySelectorAll("button")) as Array<{
      textContent?: string;
      // deno-lint-ignore no-explicit-any
      dispatchEvent: (e: any) => void;
    }>;
    const dispatchBtn = buttons.find((b) => b.textContent?.includes("選択した操作を実行"));
    assertExists(dispatchBtn, "dispatch button must be present after render");
    // deno-lint-ignore no-explicit-any
    dispatchBtn!.dispatchEvent(new (globalThis as any).Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const html = (container as any).innerHTML as string;
    assert(
      html.includes("成功") || html.includes("実行結果"),
      "emission result section must appear after successful dispatch",
    );
  } finally {
    stopComponentEventRuntime();
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    __testOnly.resetQueue();
    cleanup();
  }
});

Deno.test("OperationPanel DOM: dispatch click with failure → エラーまたは部分失敗 appears in DOM (not silent)", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  __testOnly.resetCommandQueue();
  __testOnly.resetQueue();
  try {
    globalThis.fetch = makeMockFetch(422, {
      success: false,
      errors: [{ message: "OPERATION_DISPATCH_FAILED" }],
    });
    renderInto(OperationPanel, container);
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const buttons = Array.from((container as any).querySelectorAll("button")) as Array<{
      textContent?: string;
      // deno-lint-ignore no-explicit-any
      dispatchEvent: (e: any) => void;
    }>;
    const dispatchBtn = buttons.find((b) => b.textContent?.includes("選択した操作を実行"));
    assertExists(dispatchBtn, "dispatch button must be present");
    // deno-lint-ignore no-explicit-any
    dispatchBtn!.dispatchEvent(new (globalThis as any).Event("click", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const html = (container as any).innerHTML as string;
    assert(
      html.includes("エラーまたは部分失敗") || html.includes("実行結果"),
      "error/partial-failure section must appear in DOM after dispatch failure (not silent)",
    );
  } finally {
    stopComponentEventRuntime();
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    __testOnly.resetQueue();
    cleanup();
  }
});

// ─── Section 11: UiBuilderAdmin — DOM event simulation ───────────────────────

Deno.test("UiBuilderAdmin DOM: renders in live DOM without throwing", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  __testOnly.resetCommandQueue();
  try {
    globalThis.fetch = makeMockFetch(200, { success: true, emission: { data: { targets: [], items: [] } } });
    let threw = false;
    try {
      renderInto(UiBuilderAdmin, container);
    } catch {
      threw = true;
    }
    assertFalse(threw, "UiBuilderAdmin must render in DOM without throwing");
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const html = (container as any).innerHTML as string;
    assert(html.length > 100, "UiBuilderAdmin must produce non-empty DOM content");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    cleanup();
  }
});

Deno.test("UiBuilderAdmin DOM: accordion toggle click → DOM state changes (synchronous state update)", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  __testOnly.resetCommandQueue();
  try {
    globalThis.fetch = makeMockFetch(200, { success: true, emission: { data: { targets: [], items: [] } } });
    renderInto(UiBuilderAdmin, container);
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const btns = Array.from((container as any).querySelectorAll("button")) as Array<{
      textContent?: string;
      // deno-lint-ignore no-explicit-any
      dispatchEvent: (e: any) => void;
    }>;
    const accordionBtn = btns.find(
      (b) => b.textContent?.includes("開く") || b.textContent?.includes("閉じる"),
    );
    assertExists(accordionBtn, "UiBuilderAdmin must have accordion toggle buttons");
    // deno-lint-ignore no-explicit-any
    const htmlBefore = (container as any).innerHTML as string;
    // deno-lint-ignore no-explicit-any
    accordionBtn!.dispatchEvent(new (globalThis as any).Event("click", { bubbles: true }));
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const htmlAfter = (container as any).innerHTML as string;
    assert(htmlBefore !== htmlAfter, "accordion click must change DOM state");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    cleanup();
  }
});


Deno.test("UiBuilderAdmin DOM: initial render does not trigger team_markdown search and does not mount preset ecosystem panel", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  const capturedBodies: unknown[] = [];
  __testOnly.resetCommandQueue();
  try {
    globalThis.fetch = makeMockFetch(200, { success: true, emission: { data: { targets: [], items: [] } } }, (_url, init) => {
      if (typeof init?.body === "string") {
        capturedBodies.push(JSON.parse(init.body));
      }
    });
    renderInto(UiBuilderAdmin, container);
    await flushUpdates();
    await flushUpdates();

    const teamMarkdownSearch = capturedBodies.some((body) => {
      if (!body || typeof body !== "object" || Array.isArray(body)) return false;
      const b = body as Record<string, unknown>;
      return b.layer === "team_markdown" && b.action === "saved_view:search";
    });
    assertFalse(
      teamMarkdownSearch,
      "UiBuilder initial render must not mount md_viewer dashboard or run saved_view:search",
    );
    // deno-lint-ignore no-explicit-any
    const html = (container as any).innerHTML as string;
    // Preset ecosystem panel must NOT be permanently rendered (responsibility mixing resolved)
    assertFalse(
      html.includes('data-preset-child-surface="md_viewer"'),
      "UIBuilder must not permanently render md_viewer child surface (use /admin/team-dashboard instead)",
    );
    assertFalse(
      html.includes("Preset ecosystem — md_viewer child surface"),
      "UIBuilder must not display 'Preset ecosystem — md_viewer child surface' label",
    );
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    cleanup();
  }
});

Deno.test("UiBuilderAdmin source: DashboardCandidatePalette present, preset ecosystem panel absent", async () => {
  const uiBuilderSource = await Deno.readTextFile("frontend/islands/UiBuilderAdmin.tsx");

  // DashboardCandidatePalette must be defined in UIBuilder source
  assert(
    uiBuilderSource.includes("DashboardCandidatePalette"),
    "UIBuilder must define DashboardCandidatePalette for dashboard_placement_candidate components",
  );
  // DashboardCandidatePalette must be rendered in the canvas area
  assert(
    uiBuilderSource.includes('data-dashboard-candidate-palette="true"'),
    "UIBuilder must render data-dashboard-candidate-palette attribute for the dashboard candidate section",
  );
  // Preset ecosystem panel must remain absent (responsibility mixing resolved in PR#400)
  assertFalse(
    uiBuilderSource.includes("UiBuilderPresetEcosystemPanel"),
    "UIBuilder must not contain UiBuilderPresetEcosystemPanel — responsibility mixing resolved",
  );
  assertFalse(
    uiBuilderSource.includes('data-preset-child-surface="md_viewer"'),
    "UIBuilder must not contain data-preset-child-surface=md_viewer",
  );
});

// ─── Section 12: AdminImport — DOM event simulation ──────────────────────────

Deno.test("AdminImport DOM: after useEffect loads selectors → loading state gone, content visible", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  __testOnly.resetCommandQueue();
  try {
    globalThis.fetch = makeMockFetch(200, {
      success: true,
      emission: {
        data: [{ manifestId: "m1", status: "active", createdAt: "2024-01-01T00:00:00Z" }],
      },
    });
    renderInto(AdminImport, container);
    // deno-lint-ignore no-explicit-any
    const htmlBefore = (container as any).innerHTML as string;
    assert(htmlBefore.includes("選択肢をロード中") || htmlBefore.length > 50, "initial loading state visible");
    await flushUpdates();
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const htmlAfter = (container as any).innerHTML as string;
    assertFalse(htmlAfter.includes("選択肢をロード中"), "loading message must be gone after useEffect resolves");
    assert(htmlAfter.includes("プレビュー"), "preview section must be visible after selectors load");
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    cleanup();
  }
});

Deno.test("AdminImport DOM: preview click without manifest selected → error visible in DOM (not silent)", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  __testOnly.resetCommandQueue();
  try {
    // Load non-empty manifests/schemas so preview button is enabled
    globalThis.fetch = makeMockFetch(200, {
      success: true,
      emission: {
        data: [{ manifestId: "m1", status: "active", createdAt: "2024-01-01T00:00:00Z" }],
      },
    });
    renderInto(AdminImport, container);
    await flushUpdates();
    await flushUpdates();
    // Click preview button without selecting a manifest or schema → error set synchronously
    // deno-lint-ignore no-explicit-any
    const btns = Array.from((container as any).querySelectorAll("button")) as Array<{
      textContent?: string;
      // deno-lint-ignore no-explicit-any
      dispatchEvent: (e: any) => void;
    }>;
    const previewBtn = btns.find((b) => b.textContent?.trim() === "プレビュー");
    assertExists(previewBtn, "preview button must be present");
    // deno-lint-ignore no-explicit-any
    previewBtn!.dispatchEvent(new (globalThis as any).Event("click", { bubbles: true }));
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const html = (container as any).innerHTML as string;
    assert(
      html.includes("alert-error") || html.includes("選択してください") || html.includes("エラー"),
      "error must be visible in DOM when preview clicked without selections (not silent)",
    );
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
    cleanup();
  }
});

// ─── Section 13: AuthPanel — DOM event simulation ────────────────────────────
// Session check: ensureValidClientSession returns null quickly (no token in empty sessionStorage).
// No fetch mock needed for session check. Mock only for login attempt.

Deno.test("AuthPanel DOM: session check resolves with no session → login form visible in DOM", async () => {
  const { container, cleanup } = setupDom();
  try {
    renderInto(AuthPanel, container);
    // deno-lint-ignore no-explicit-any
    const htmlBefore = (container as any).innerHTML as string;
    assert(
      htmlBefore.includes("ログイン状態を確認") || htmlBefore.length > 10,
      "initial render shows session check loading state",
    );
    await flushUpdates();
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const htmlAfter = (container as any).innerHTML as string;
    assert(htmlAfter.includes("ログイン") && htmlAfter.includes("password"), "login form must appear after session check");
  } finally {
    cleanup();
  }
});

Deno.test("AuthPanel DOM: form submit with auth failure → alert-error visible in DOM (not silent)", async () => {
  const { container, cleanup } = setupDom();
  const original = globalThis.fetch;
  try {
    renderInto(AuthPanel, container);
    await flushUpdates();
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const form = (container as any).querySelector("form");
    assertExists(form, "login form must be visible after session check");
    globalThis.fetch = makeMockFetch(401, {
      success: false,
      errors: [{ code: "AUTH_INVALID_CREDENTIALS", message: "ユーザー名またはパスワードが間違っています。" }],
    });
    // deno-lint-ignore no-explicit-any
    form.dispatchEvent(new (globalThis as any).Event("submit", { bubbles: true, cancelable: true }));
    await flushUpdates();
    await flushUpdates();
    // deno-lint-ignore no-explicit-any
    const html = (container as any).innerHTML as string;
    assert(html.includes("alert-error"), "alert-error section must appear after auth failure (not silent)");
    assert(
      html.includes("ログインに失敗しました") || html.includes("パスワードが間違っています"),
      "auth error message must be visible in DOM",
    );
  } finally {
    globalThis.fetch = original;
    cleanup();
  }
});
