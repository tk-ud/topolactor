import { assertEquals, assertExists, assertFalse } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  __testOnly,
  emitComponentOperationEvent,
  queueAdminClientCommand,
  queueClientCommand,
} from "../runtime/frontendScheduler.ts";

// ─── UI Handler Behavior — api_command_lane + event_log_lane coverage ─────────
// Purpose: verify UI handler dispatch operations and component event emissions
// are intact after pipeline routing changes.
//
// Lane model (SSOT: docs/design/pipeline-continuity-ssot.yaml):
//   api_command_lane              → queueAdminClientCommand / queueClientCommand → /api/dispatch
//   frontend_component_event_log_lane → emitComponentOperationEvent → /api/component-events/append
//
// Note: Preact components are not rendered; tests exercise the dispatch layer directly.
// Handler existence is verified via static source analysis (requires --allow-read).

// ─── Section 1: Handler Inventory Guard ──────────────────────────────────────
// Verifies that interactive islands declare event handlers.
// If an island loses all handlers it will be caught here.

const INTERACTIVE_ISLANDS: string[] = [
  "SeedAdmin.tsx",
  "OperationPanel.tsx",
  "AdminImport.tsx",
  "SuperAuthPanel.tsx",
  "UserDemoStepper.tsx",
  "UiBuilderAdmin.tsx",
  "ContentsPromotionPanel.tsx",
  "ContentsScreenDesignPanel.tsx",
  "ContextTokenRegistryEditor.tsx",
  "RegistryVectorValidator.tsx",
  "ManifestsAdmin.tsx",
  "HubNavigationAdmin.tsx",
];

// Pattern covers: event props, named async handler functions, and arrow handler assignments
const HANDLER_PATTERN =
  /\b(onClick|onSubmit|onChange|onInput|onSelect|handleSubmit|handleLoad|handleSave|handleValidate|handlePreview|handleImport|handleApply|runScenario|handleDispatch|handleFileChange|async function handle[A-Z])/;

Deno.test("handler inventory: all interactive islands declare at least one event handler", async () => {
  const missing: string[] = [];
  for (const name of INTERACTIVE_ISLANDS) {
    const content = await Deno.readTextFile(new URL(`../islands/${name}`, import.meta.url));
    if (!HANDLER_PATTERN.test(content)) missing.push(name);
  }
  assertEquals(
    missing,
    [],
    `islands missing event handlers: ${missing.join(", ")}`,
  );
});

// ─── Section 2: SeedAdmin dispatch command shapes (api_command_lane) ──────────
// SeedAdmin.dispatchSeedOp(layer, action) → queueAdminClientCommand → /api/dispatch
// Verifies layer=seed_runtime, triggerKind=client, role absent for each action.

const SEED_ACTIONS = ["load", "save", "validate", "preview", "import"] as const;

for (const seedAction of SEED_ACTIONS) {
  Deno.test(
    `SeedAdmin: action=${seedAction} dispatches layer=seed_runtime triggerKind=client via api_command_lane`,
    async () => {
      __testOnly.resetCommandQueue();
      let capturedBody: Record<string, unknown> = {};
      const originalFetch = globalThis.fetch;
      globalThis.fetch = async (_input: unknown, init?: RequestInit) => {
        capturedBody = JSON.parse(init!.body as string);
        return new Response(
          JSON.stringify({ success: true, emission: null }),
          { status: 200 },
        );
      };
      try {
        await queueAdminClientCommand({
          operationType: "admin",
          target: "admin",
          layer: "seed_runtime",
          action: seedAction,
        });
        assertEquals(
          capturedBody.operationType,
          "admin",
          `action=${seedAction}: operationType must be "admin"`,
        );
        assertEquals(
          capturedBody.layer,
          "seed_runtime",
          `action=${seedAction}: layer must be "seed_runtime"`,
        );
        assertEquals(
          capturedBody.action,
          seedAction,
          `action=${seedAction}: action field must match`,
        );
        assertEquals(
          capturedBody.triggerKind,
          "client",
          `action=${seedAction}: triggerKind must be "client" (api_command_lane SSOT)`,
        );
        assertFalse(
          "role" in capturedBody,
          `action=${seedAction}: role must not be in dispatch body`,
        );
      } finally {
        globalThis.fetch = originalFetch;
        __testOnly.resetCommandQueue();
      }
    },
  );
}

// ─── Section 3: UiBuilderAdmin dispatch command shapes ────────────────────────
// UiBuilderAdmin.dispatchAdminOp(layer, action) → queueAdminClientCommand
// Spot-checks key layers used by UiBuilderAdmin handlers.

const UI_BUILDER_DISPATCH_CASES: Array<{ layer: string; action: string }> = [
  { layer: "ui_component_bucket", action: "list" },
  { layer: "ui_topology", action: "layout_candidates" },
  { layer: "package_generator", action: "generate" },
  { layer: "system_ci", action: "list_targets" },
];

for (const { layer, action } of UI_BUILDER_DISPATCH_CASES) {
  Deno.test(
    `UiBuilderAdmin: layer=${layer} action=${action} dispatches with triggerKind=client via api_command_lane`,
    async () => {
      __testOnly.resetCommandQueue();
      let capturedBody: Record<string, unknown> = {};
      const originalFetch = globalThis.fetch;
      globalThis.fetch = async (_input: unknown, init?: RequestInit) => {
        capturedBody = JSON.parse(init!.body as string);
        return new Response(
          JSON.stringify({ success: true, emission: null }),
          { status: 200 },
        );
      };
      try {
        await queueAdminClientCommand({
          operationType: "admin",
          target: "admin",
          layer,
          action,
        });
        assertEquals(capturedBody.operationType, "admin");
        assertEquals(capturedBody.layer, layer);
        assertEquals(capturedBody.action, action);
        assertEquals(capturedBody.triggerKind, "client");
        assertFalse("role" in capturedBody);
      } finally {
        globalThis.fetch = originalFetch;
        __testOnly.resetCommandQueue();
      }
    },
  );
}

// ─── Section 4: OperationPanel component event SSOT identity ─────────────────
// Verifies the required identity fields for frontend_component_event_log_lane.
// Fields: componentId, packageId, layoutId, eventType, payload, actorOrSource,
//         occurredAt (auto-stamped), idempotencyKey (auto-generated).

Deno.test(
  "OperationPanel: selectPreset emits event with SSOT required identity fields (preset-select)",
  () => {
    __testOnly.resetQueue();
    const result = emitComponentOperationEvent({
      componentId: "operation-panel:preset-select",
      packageId: "runtime-operation-panel",
      layoutId: "runtime-operation-layout",
      eventType: "select",
      actorOrSource: "OperationPanel",
      payload: { presetId: "demo-search" },
    });
    assertEquals(result.ok, true);
    const [queued] = __testOnly.getQueueSnapshot();
    assertEquals(queued.componentId, "operation-panel:preset-select");
    assertEquals(queued.packageId, "runtime-operation-panel");
    assertEquals(queued.layoutId, "runtime-operation-layout");
    assertEquals(queued.eventType, "select");
    assertEquals(queued.actorOrSource, "OperationPanel");
    assertExists(queued.idempotencyKey, "idempotencyKey must be auto-generated");
    assertExists(queued.occurredAt, "occurredAt must be auto-stamped");
    assertEquals(typeof queued.payload.presetId, "string");
    __testOnly.resetQueue();
  },
);

Deno.test(
  "OperationPanel: handleDispatch emits event with SSOT identity (dispatch-form submit)",
  () => {
    __testOnly.resetQueue();
    const result = emitComponentOperationEvent({
      componentId: "operation-panel:dispatch-form",
      packageId: "runtime-operation-panel",
      layoutId: "runtime-operation-layout",
      eventType: "submit",
      actorOrSource: "OperationPanel",
      payload: {
        presetId: "demo-search",
        operationType: "Search",
        target: "default",
        layer: "entity",
        action: "Search",
        hasContext: false,
      },
    });
    assertEquals(result.ok, true);
    const [queued] = __testOnly.getQueueSnapshot();
    assertEquals(queued.componentId, "operation-panel:dispatch-form");
    assertEquals(queued.eventType, "submit");
    assertEquals(queued.actorOrSource, "OperationPanel");
    assertExists(queued.idempotencyKey);
    assertExists(queued.occurredAt);
    assertEquals(queued.payload.operationType, "Search");
    assertEquals(queued.payload.hasContext, false);
    __testOnly.resetQueue();
  },
);

// ─── Section 5: Lane distinction — static source analysis ────────────────────
// Verifies that each island uses the correct lane and does not bypass the scheduler.

Deno.test(
  "lane: SeedAdmin uses api_command_lane (queueAdminClientCommand only, not event_log_lane)",
  async () => {
    const content = await Deno.readTextFile(
      new URL("../islands/SeedAdmin.tsx", import.meta.url),
    );
    assertEquals(
      content.includes("queueAdminClientCommand"),
      true,
      "SeedAdmin must use queueAdminClientCommand",
    );
    assertFalse(
      content.includes("emitComponentOperationEvent"),
      "SeedAdmin must not emit component events (event_log_lane)",
    );
    assertFalse(
      content.includes('fetch("/api/dispatch"'),
      "SeedAdmin must not call /api/dispatch directly",
    );
  },
);

Deno.test(
  "lane: OperationPanel uses both api_command_lane (queueClientCommand) and event_log_lane (emitComponentOperationEvent)",
  async () => {
    const content = await Deno.readTextFile(
      new URL("../islands/OperationPanel.tsx", import.meta.url),
    );
    assertEquals(
      content.includes("queueClientCommand"),
      true,
      "OperationPanel must use queueClientCommand for command dispatch",
    );
    assertEquals(
      content.includes("emitComponentOperationEvent"),
      true,
      "OperationPanel must emit component events",
    );
    assertFalse(
      content.includes("queueAdminClientCommand"),
      "OperationPanel must not use admin command lane",
    );
    assertFalse(
      content.includes('fetch("/api/dispatch"'),
      "OperationPanel must not call /api/dispatch directly",
    );
  },
);

Deno.test(
  "lane: UserDemoStepper uses api_command_lane (queueClientCommand only)",
  async () => {
    const content = await Deno.readTextFile(
      new URL("../islands/UserDemoStepper.tsx", import.meta.url),
    );
    assertEquals(
      content.includes("queueClientCommand"),
      true,
      "UserDemoStepper must use queueClientCommand",
    );
    assertFalse(
      content.includes("queueAdminClientCommand"),
      "UserDemoStepper must not use admin lane",
    );
    assertFalse(
      content.includes("emitComponentOperationEvent"),
      "UserDemoStepper must not emit component events",
    );
  },
);

Deno.test(
  "lane: UiBuilderAdmin uses api_command_lane (queueAdminClientCommand only)",
  async () => {
    const content = await Deno.readTextFile(
      new URL("../islands/UiBuilderAdmin.tsx", import.meta.url),
    );
    assertEquals(
      content.includes("queueAdminClientCommand"),
      true,
      "UiBuilderAdmin must use queueAdminClientCommand",
    );
    assertFalse(
      content.includes("emitComponentOperationEvent"),
      "UiBuilderAdmin must not emit component events",
    );
    assertFalse(
      content.includes('fetch("/api/dispatch"'),
      "UiBuilderAdmin must not call /api/dispatch directly",
    );
  },
);

Deno.test(
  "lane: AdminImport routes through adminApi (not direct scheduler or fetch bypass)",
  async () => {
    const content = await Deno.readTextFile(
      new URL("../islands/AdminImport.tsx", import.meta.url),
    );
    assertFalse(
      content.includes("queueAdminClientCommand"),
      "AdminImport must use adminApi functions, not direct queueAdminClientCommand",
    );
    assertFalse(
      content.includes('fetch("/api/dispatch"'),
      "AdminImport must not call /api/dispatch directly",
    );
    // adminApi functions (which internally use queueAdminClientCommand) must be imported
    assertEquals(
      content.includes("from \"../api/adminApi.ts\""),
      true,
      "AdminImport must import from adminApi",
    );
  },
);

Deno.test(
  "lane: AuthPanel does not use scheduler lanes (uses authApi only)",
  async () => {
    const content = await Deno.readTextFile(
      new URL("../islands/SuperAuthPanel.tsx", import.meta.url),
    );
    assertFalse(
      content.includes("queueClientCommand"),
      "AuthPanel must not use queueClientCommand",
    );
    assertFalse(
      content.includes("queueAdminClientCommand"),
      "AuthPanel must not use queueAdminClientCommand",
    );
    assertFalse(
      content.includes('fetch("/api/dispatch"'),
      "AuthPanel must not call /api/dispatch directly",
    );
  },
);

// ─── Section 6: Error surface — no silent failures ────────────────────────────
// Verifies that network/dispatch errors resolve as { success: false } (not silently dropped).
// SeedAdmin handlers check body.success / body.errors and set status — this verifies
// the result carries the error rather than swallowing it.

Deno.test(
  "error surface: SeedAdmin dispatch network error resolves as success=false with message (not silent)",
  async () => {
    __testOnly.resetCommandQueue();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = async () => {
      throw new Error("SEED_NETWORK_FAIL");
    };
    try {
      const result = await queueAdminClientCommand({
        operationType: "admin",
        target: "admin",
        layer: "seed_runtime",
        action: "load",
      });
      assertEquals(
        result.success,
        false,
        "network error must resolve as success=false, not silent",
      );
      assertExists(result.errors, "errors array must be present");
      assertEquals(
        result.errors?.[0]?.message,
        "SEED_NETWORK_FAIL",
        "error message must be preserved in result",
      );
    } finally {
      globalThis.fetch = originalFetch;
      __testOnly.resetCommandQueue();
    }
  },
);

Deno.test(
  "error surface: OperationPanel dispatch network error resolves as success=false (not silent)",
  async () => {
    __testOnly.resetCommandQueue();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = async () => {
      throw new Error("OP_PANEL_NETWORK_FAIL");
    };
    try {
      const result = await queueClientCommand({
        operationType: "Search",
        target: "default",
        layer: "entity",
        action: "Search",
      });
      assertEquals(
        result.success,
        false,
        "network error must resolve as success=false, not silent",
      );
      assertEquals(
        result.errors?.[0]?.message,
        "OP_PANEL_NETWORK_FAIL",
        "error message must be preserved",
      );
    } finally {
      globalThis.fetch = originalFetch;
      __testOnly.resetCommandQueue();
    }
  },
);

Deno.test(
  "error surface: UiBuilderAdmin dispatch error resolves as success=false (not silent)",
  async () => {
    __testOnly.resetCommandQueue();
    const originalFetch = globalThis.fetch;
    globalThis.fetch = async () => {
      throw new Error("UI_BUILDER_FAIL");
    };
    try {
      const result = await queueAdminClientCommand({
        operationType: "admin",
        target: "admin",
        layer: "ui_component_bucket",
        action: "list",
      });
      assertEquals(result.success, false);
      assertEquals(result.errors?.[0]?.message, "UI_BUILDER_FAIL");
    } finally {
      globalThis.fetch = originalFetch;
      __testOnly.resetCommandQueue();
    }
  },
);

// ─── Section 7: Component event SSOT — all required identity fields present ───
// Verifies the frontend_component_event_log_lane required_identity contract.
// Fields: componentId, packageId, layoutId, eventType, payload, actorOrSource,
//         occurredAt (auto), idempotencyKey (auto).

Deno.test(
  "component event SSOT: all required identity fields are preserved in queued event",
  () => {
    __testOnly.resetQueue();
    const result = emitComponentOperationEvent({
      componentId: "ssot-test-component",
      packageId: "ssot-test-package",
      layoutId: "ssot-test-layout",
      eventType: "click",
      actorOrSource: "SsotTestActor",
      payload: { action: "test-click", value: 42 },
    });
    assertEquals(result.ok, true);
    const [queued] = __testOnly.getQueueSnapshot();
    // Required identity fields
    assertEquals(queued.componentId, "ssot-test-component", "componentId must be preserved");
    assertEquals(queued.packageId, "ssot-test-package", "packageId must be preserved");
    assertEquals(queued.layoutId, "ssot-test-layout", "layoutId must be preserved");
    assertEquals(queued.eventType, "click", "eventType must be preserved");
    assertEquals(queued.actorOrSource, "SsotTestActor", "actorOrSource must be preserved");
    assertEquals(queued.payload.action, "test-click", "payload must be preserved");
    // Auto-generated identity fields
    assertExists(queued.occurredAt, "occurredAt must be auto-stamped");
    assertExists(queued.idempotencyKey, "idempotencyKey must be auto-generated");
    assertEquals(
      typeof queued.idempotencyKey,
      "string",
      "idempotencyKey must be a string",
    );
    assertEquals(
      typeof queued.occurredAt,
      "string",
      "occurredAt must be an ISO string",
    );
    __testOnly.resetQueue();
  },
);

Deno.test(
  "component event SSOT: missing componentId is an explicit error (not silent)",
  () => {
    const result = emitComponentOperationEvent({
      componentId: "",
      eventType: "click",
      actorOrSource: "TestActor",
      payload: {},
    });
    assertEquals(
      result.ok,
      false,
      "missing componentId must return explicit error, not silently accept",
    );
  },
);

// ─── Section 8: UserDemoStepper runScenario dispatch shape ────────────────────
// Verifies that runScenario sends through queueClientCommand with triggerKind=client.

Deno.test(
  "UserDemoStepper: runScenario dispatches through api_command_lane with triggerKind=client",
  async () => {
    __testOnly.resetCommandQueue();
    let capturedBody: Record<string, unknown> = {};
    const originalFetch = globalThis.fetch;
    globalThis.fetch = async (_input: unknown, init?: RequestInit) => {
      capturedBody = JSON.parse(init!.body as string);
      return new Response(
        JSON.stringify({ success: true, emission: { componentIds: ["c1"] } }),
        { status: 200 },
      );
    };
    try {
      // UserDemoStepper.runScenario → queueClientCommand(preset.operation, token, context)
      await queueClientCommand({ operationType: "Search", target: "demo", layer: "entity", action: "Search" });
      assertEquals(capturedBody.triggerKind, "client");
      assertFalse("role" in capturedBody);
    } finally {
      globalThis.fetch = originalFetch;
      __testOnly.resetCommandQueue();
    }
  },
);
