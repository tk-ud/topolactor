import {
  assert,
  assertEquals,
  assertFalse,
} from "https://deno.land/std@0.224.0/assert/mod.ts";
import { h, options, render } from "preact";
import { renderToString } from "preact-render-to-string";
import ContentsAdminRoute from "../routes/admin/contents.tsx";
import UiBuilderAdminRoute from "../routes/admin/ui-builder.tsx";
import ManifestsAdminRoute from "../routes/admin/manifests.tsx";
import ContentsAdmin from "../islands/ContentsAdmin.tsx";
import ContentsScreenDesignPanel from "../islands/ContentsScreenDesignPanel.tsx";
import UiBuilderAdmin from "../islands/UiBuilderAdmin.tsx";
import ManifestsAdmin from "../islands/ManifestsAdmin.tsx";
import AggregateTriggerAuthoringPanel from "../components/AggregateTriggerAuthoringPanel.tsx";
import { buildAssignPayloadForStep } from "../lib/contentsAssign.ts";
import { emptyManifestScreenDesign } from "../lib/manifestScreenDesign.ts";
import { buildVisualLayoutPatchJson } from "../runtime/visualLayoutUtils.ts";
import { encodeManifestPackageTargetRef } from "../lib/packageWiringPicker.ts";
import {
  __testOnly,
  queueAdminClientCommand,
} from "../runtime/frontendScheduler.ts";
import { flushUpdates, setupDom } from "./test-dom-setup.ts";
import {
  type AggregateTriggerDefinitionPayload,
  aggregateTriggerTargetOptions,
  buildAggregateTriggerDefinition,
  canonicalTriggerKinds,
  materializationPayloadMapAllowedSources,
  type StepTarget,
  triggerSourceDetailKinds,
  aggregateTriggerTargetFromKey,
} from "../lib/aggregateTriggerAuthoring.ts";

// deno-lint-ignore no-explicit-any
(options as any).requestAnimationFrame = (cb: () => void): number => {
  setTimeout(cb, 0);
  return 0;
};

const SQL_FORBIDDEN = [
  "raw_sql",
  "select ",
  " where ",
  " case ",
  "scheduler_event",
  "arbitrary_table",
  "undefined-target",
  "undefined-manifest",
  "undefined-binding",
];

type UiAssuranceCase = {
  routePath: "/admin/contents" | "/admin/ui-builder" | "/admin/manifests";
  routeComponent: () => h.JSX.Element;
  bodyComponent: () => h.JSX.Element;
  bodyMarkers: string[];
  boundaryMarkers: string[];
};

const adminWorkflowCases: UiAssuranceCase[] = [
  {
    routePath: "/admin/contents",
    routeComponent: ContentsAdminRoute,
    bodyComponent: ContentsAdmin,
    bodyMarkers: [
      "topolactor — 管理",
      "step 1",
      "step 2",
      "step 3",
      "画面づくり",
    ],
    boundaryMarkers: ["ContentsScreenDesignPanel", "ContentsPromotionPanel"],
  },
  {
    routePath: "/admin/ui-builder",
    routeComponent: UiBuilderAdminRoute,
    bodyComponent: UiBuilderAdmin,
    bodyMarkers: ["topolactor", "canvas", "パッケージ", "部品", "配線"],
    boundaryMarkers: [
      "queueAdminClientCommand",
      "buildVisualLayoutPatchJson",
      "encodeManifestPackageTargetRef",
    ],
  },
  {
    routePath: "/admin/manifests",
    routeComponent: ManifestsAdminRoute,
    bodyComponent: ManifestsAdmin,
    bodyMarkers: ["topolactor — 管理", "作成済みページ", "ページ", "ナビ"],
    boundaryMarkers: ["listHubNavigationManifests", "HubNavigationAdmin"],
  },
];

function assertNoForbiddenPayloadVocabulary(payload: unknown) {
  const serialized = JSON.stringify(payload).toLowerCase();
  for (const term of SQL_FORBIDDEN) {
    assertFalse(
      serialized.includes(term),
      `payload must not include forbidden vocabulary: ${term}`,
    );
  }
}

function assertBackendAggregateTriggerShape(
  payload: AggregateTriggerDefinitionPayload,
) {
  assert("trigger_definition_id" in payload);
  assert("trigger_source" in payload);
  assert("canonical_trigger_kind" in payload.trigger_source);
  assert("trigger_source_detail_kind" in payload.trigger_source);
  assert("processing_function_scope" in payload);
  assert("execution_scope" in payload);
  assert("transaction_boundary" in payload);
  assert("aggregate_target_binding" in payload);
  assert("target_source" in payload.aggregate_target_binding);
  assert("target_id" in payload.aggregate_target_binding);
  assert("threshold_policy" in payload);
  assert("materialization_target_binding" in payload);
  assert("materialization_payload_map" in payload);
  assert("approval_policy" in payload);
}


function dispatchInputValue(element: Element, value: string) {
  (element as HTMLInputElement).value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
  element.dispatchEvent(new Event("change", { bubbles: true }));
}

function dispatchSelectValue(element: Element, value: string) {
  (element as HTMLSelectElement).value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
  element.dispatchEvent(new Event("change", { bubbles: true }));
}

function buttonByText(container: Element, text: string): HTMLButtonElement {
  const buttons = Array.from(container.querySelectorAll("button"));
  const button = (buttons.find((b) => b.textContent?.trim() === text) ??
    buttons.find((b) => b.textContent?.includes(text))) as
      | HTMLButtonElement
      | undefined;
  assert(button, `button not found: ${text}`);
  return button;
}

async function clickAndFlush(button: HTMLButtonElement) {
  button.click();
  await flushUpdates();
  await flushUpdates();
}

async function confirmOpenDialog(container: Element) {
  const confirm = buttonByText(container, "実行する");
  await clickAndFlush(confirm);
}

type CapturedDispatch = Record<string, unknown> & {
  layer?: string;
  action?: string;
  payload?: Record<string, unknown>;
};

function installAdminDispatchMock(captured: CapturedDispatch[], manifestId: string) {
  let topologyRawJson = "[]";
  const original = globalThis.fetch;
  globalThis.fetch = (_url, init) => {
    const body = JSON.parse(String((init as { body?: BodyInit })?.body ?? "{}"));
    captured.push(body);
    const layer = body.layer;
    const action = body.action;
    const payload = body.payload ?? {};
    if (layer === "manifest" && action === "assign_screen_data_shape") {
      topologyRawJson = JSON.stringify([{ type: "screen_data_shape", ...payload }]);
    }
    const detail = {
      manifestId,
      status: "draft",
      relationRegistryId: null,
      summary: { entryTypes: ["screen_data_shape"] },
      topologyRawJson,
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z",
    };
    const data = layer === "manifest" && action === "list"
      ? [{ manifestId, status: "draft", relationRegistryId: null, role: null, target: null, layer: null, action: null, runtimeDestination: null, createdAt: detail.createdAt, updatedAt: detail.updatedAt }]
      : layer === "manifest" && action === "get"
      ? detail
      : layer === "manifest" && action === "list_relationship_remote_targets"
      ? []
      : layer === "enum_dictionary" && action === "get_group"
      ? { groupId: payload.groupId, groupName: "demo", items: [] }
      : layer === "enum_dictionary" && action === "list_groups"
      ? []
      : layer === "admin_csv_json_import" &&
          (action === "list_manifests" || action === "list_schemas")
      ? []
      : layer === "ui_component_bucket" && action === "list"
      ? []
      : layer === "ui_topology" && action === "promoted_palette"
      ? []
      : layer === "hub_navigation" && action === "list_manifests"
      ? []
      : detail;
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, emission: { data } }), {
        status: 200,
      }),
    );
  };
  return () => {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  };
}

const step2Targets: StepTarget[] = [
  {
    targetSource: "step2_logical_entity_definition",
    targetId: "orders",
    label: "Step2 logical entity: orders",
  },
  {
    targetSource: "step2_logical_entity_definition",
    targetId: "invoices",
    label: "Step2 logical entity: invoices",
  },
];
const step25Targets: StepTarget[] = [{
  targetSource: "step2_5_relation_definition",
  targetId: "orders->customers",
  label: "Step2.5 relation: orders → customers",
}];

function buildUiSelectedAggregatePayload(): AggregateTriggerDefinitionPayload[] {
  return [buildAggregateTriggerDefinition({
    triggerDefinitionId: "00000000-0000-0000-0000-000000000001",
    canonicalTriggerKind: "hook",
    triggerSourceDetailKind: "hook_event",
    aggregateTargetBinding: step2Targets[1],
    materializationTargetBinding: step25Targets[0],
    functionId: "aggregate_trigger_authoring_function",
    operationDefinitionId: "contents_step3_operation",
    acceptedEventSchemaRef: "contents.step3.aggregate_trigger.event.v1",
    materializationPolicyRef: "backend_runtime_authority_required",
    conflictKeyFields: ["operation_definition_id"],
    deltaMap: { event_count: 1 },
    thresholdPolicy: {
      minimum_trial_count: 1,
      ratio_numerator_field: "event_count",
      ratio_denominator_field: "event_count",
      comparison_operator: ">=",
      target_ratio: 1,
    },
    materializationPayloadMap: [{
      target_field: "operation_definition_id",
      source: "function_input_event",
      source_field: "operation_definition_id",
    }],
  })];
}

Deno.test("admin workflow UI assurance substrate: route wrappers and workflow bodies render for all main admin routes", async () => {
  for (const routeCase of adminWorkflowCases) {
    const routeHtml = renderToString(h(routeCase.routeComponent, {}));
    assert(
      routeHtml.includes("ログイン状態を確認しています"),
      `${routeCase.routePath}: route wrapper must render the auth-gated route shell`,
    );

    const bodyHtml = renderToString(h(routeCase.bodyComponent, {}));
    for (const marker of routeCase.bodyMarkers) {
      assert(
        bodyHtml.toLowerCase().includes(marker.toLowerCase()),
        `${routeCase.routePath}: workflow body must render marker ${marker}`,
      );
    }
  }
});

Deno.test("admin workflow UI assurance substrate: route body operations dispatch through client-trigger UI paths", async () => {
  const manifestId = "00000000-0000-0000-0000-000000000542";
  const captured: CapturedDispatch[] = [];
  const restoreFetch = installAdminDispatchMock(captured, manifestId);

  const uiEnv = setupDom();
  try {
    render(h(UiBuilderAdmin, {}), uiEnv.container);
    // UiBuilderAdmin reactively loads its package/layout-candidate lists on mount
    // (no manual "reload list" button in this workflow); the reactive load itself
    // must still be a client-trigger admin dispatch.
    await flushUpdates();
    await flushUpdates();
    await flushUpdates();
    assert(
      captured.some((body) =>
        body.layer === "ui_topology" &&
        body.action === "list_packages" &&
        body.triggerKind === "client" &&
        !("role" in body)
      ),
      "ui-builder UI operation must dispatch through the client-trigger admin lane",
    );
  } finally {
    render(null, uiEnv.container);
    uiEnv.cleanup();
  }

  const manifestEnv = setupDom();
  try {
    render(h(ManifestsAdmin, {}), manifestEnv.container);
    await flushUpdates();
    await flushUpdates();
    await clickAndFlush(buttonByText(manifestEnv.container, "再読み込み"));
    assert(
      captured.some((body) =>
        body.layer === "hub_navigation" &&
        body.action === "list_manifests" &&
        body.triggerKind === "client" &&
        !("role" in body)
      ),
      "manifests UI operation must dispatch through the client-trigger hub navigation lane",
    );
  } finally {
    render(null, manifestEnv.container);
    manifestEnv.cleanup();
    restoreFetch();
  }
});

Deno.test("contents workflow substrate: Step2 → Step2.5 → Step3 targets are constrained and assigned through Step3 payload", () => {
  const options = aggregateTriggerTargetOptions(step2Targets, step25Targets);
  assertEquals(options.map((o) => o.targetId), [
    "orders",
    "invoices",
    "orders->customers",
  ]);

  const design = emptyManifestScreenDesign();
  design.logicalTables = [
    {
      tableName: "orders",
      columns: [{ name: "id", dataType: "text", nullable: false }],
    },
    {
      tableName: "invoices",
      columns: [{ name: "id", dataType: "text", nullable: false }],
    },
  ];
  design.relationIntents = [{
    localTableRef: "orders",
    joinTableRef: "customers",
    localKey: "customer_id",
    remoteKey: "id",
  }];
  const aggregateTriggerDefinitions = buildUiSelectedAggregatePayload();
  const payload = buildAssignPayloadForStep(3, "manifest-1", design, null, {
    aggregateTriggerDefinitions,
  });

  assertEquals(
    payload.aggregateTriggerDefinitions?.[0].aggregate_target_binding.target_id,
    "invoices",
  );
  assertEquals(
    payload.aggregateTriggerDefinitions?.[0].materialization_target_binding
      .target_id,
    "orders->customers",
  );
  assertBackendAggregateTriggerShape(
    payload.aggregateTriggerDefinitions
      ?.[0] as AggregateTriggerDefinitionPayload,
  );
  assertNoForbiddenPayloadVocabulary(payload.aggregateTriggerDefinitions);
});

Deno.test("contents workflow UI substrate: Step2 → Step2.5 → Step3 save path emits UI-derived aggregate trigger payload", async () => {
  const manifestId = "00000000-0000-0000-0000-000000000542";
  const captured: CapturedDispatch[] = [];
  const restoreFetch = installAdminDispatchMock(captured, manifestId);
  const { container, cleanup } = setupDom();
  try {
    render(
      h(ContentsScreenDesignPanel, {
        sharedManifestId: manifestId,
        onSharedManifestIdChange: () => {},
        manifests: [{
          manifestId,
          status: "draft",
          relationRegistryId: null,
          role: null,
          target: null,
          layer: null,
          action: null,
          runtimeDestination: null,
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z",
        }],
        onManifestsChange: () => {},
        manifestsVersion: 1,
        onManifestsReload: () => {},
      }),
      container,
    );
    await flushUpdates();
    await flushUpdates();
    await flushUpdates();
    await flushUpdates();

    // Step 1: topologySystemName is required and drives the primary logical
    // table's auto-derived name (topologySystemNameToPhysicalTable) — rec-a's
    // primary table row has no manual name input, only Step 2+ tables do.
    const topologySystemNameInput = container.querySelector(
      'input[placeholder="例: customer-management"]',
    ) as HTMLInputElement;
    dispatchInputValue(topologySystemNameInput, "orders");
    await flushUpdates();

    await clickAndFlush(buttonByText(container, "Step 2"));
    await flushUpdates();
    await flushUpdates();
    const tableNameInputs = () =>
      Array.from(container.querySelectorAll('input[placeholder="例: customer-contacts"]'));
    const columnNameInputs = () =>
      Array.from(container.querySelectorAll('input[placeholder="項目名"]'));
    dispatchInputValue(columnNameInputs()[0], "id");
    await flushUpdates();
    await clickAndFlush(buttonByText(container, "テーブルを追加"));
    dispatchInputValue(tableNameInputs()[0], "customers");
    await flushUpdates();
    dispatchInputValue(columnNameInputs()[1], "id");
    await flushUpdates();
    await clickAndFlush(buttonByText(container, "Step 2 を保存"));
    await confirmOpenDialog(container);

    assert(
      captured.some((body) =>
        body.layer === "manifest" &&
        body.action === "assign_screen_data_shape" &&
        Array.isArray(body.payload?.logicalTables) &&
        (body.payload?.logicalTables as Array<{ tableName?: string }>).some((t) =>
          t.tableName === "orders"
        )
      ),
      "Step2 UI save must send logicalTables through assign_screen_data_shape",
    );

    await clickAndFlush(buttonByText(container, "関連付けを追加"));
    const relationSelects = Array.from(container.querySelectorAll("select"));
    dispatchSelectValue(relationSelects[1], "orders");
    await flushUpdates();
    dispatchSelectValue(relationSelects[2], "id");
    await flushUpdates();
    dispatchSelectValue(relationSelects[4], "customers");
    await flushUpdates();
    const relationSelectsAfterRemote = Array.from(container.querySelectorAll("select"));
    dispatchSelectValue(relationSelectsAfterRemote[5], "id");
    await flushUpdates();
    await clickAndFlush(buttonByText(container, "Step 2.5 を保存"));
    await confirmOpenDialog(container);

    assert(
      captured.some((body) =>
        body.layer === "manifest" &&
        body.action === "assign_screen_data_shape" &&
        Array.isArray(body.payload?.relationIntents) &&
        (body.payload?.relationIntents as Array<{ localTableRef?: string; joinTableRef?: string }>).some((r) =>
          r.localTableRef === "orders" && r.joinTableRef === "customers"
        )
      ),
      "Step2.5 UI save must send relationIntents through assign_screen_data_shape",
    );

    const aggregateSelect = container.querySelector(
      'select[aria-label="aggregate target binding"]',
    );
    const materializationSelect = container.querySelector(
      'select[aria-label="aggregate materialization target binding"]',
    );
    assert(aggregateSelect);
    assert(materializationSelect);
    const aggregateOptions = Array.from(aggregateSelect.querySelectorAll("option"))
      .map((o) => (o as HTMLOptionElement).value);
    assertEquals(aggregateOptions, [
      "step2_logical_entity_definition:orders",
      "step2_logical_entity_definition:customers",
      "step2_5_relation_definition:orders->customers",
    ]);
    assertFalse(aggregateOptions.includes("step2_logical_entity_definition:undefined-target"));
    dispatchSelectValue(aggregateSelect, "step2_logical_entity_definition:customers");
    await flushUpdates();
    dispatchSelectValue(materializationSelect, "step2_5_relation_definition:orders->customers");
    await flushUpdates();
    await clickAndFlush(buttonByText(container, "Step 3 を保存"));
    await confirmOpenDialog(container);

    const step3Assign = captured.filter((body) =>
      body.layer === "manifest" && body.action === "assign_screen_data_shape"
    ).at(-1);
    const definitions = step3Assign?.payload
      ?.aggregateTriggerDefinitions as AggregateTriggerDefinitionPayload[] | undefined;
    assert(definitions?.[0], "Step3 save must include UI-derived aggregateTriggerDefinitions");
    assertEquals(definitions[0].aggregate_target_binding.target_id, "customers");
    assertEquals(
      definitions[0].materialization_target_binding.target_id,
      "orders->customers",
    );
    assertBackendAggregateTriggerShape(definitions[0]);
    assertNoForbiddenPayloadVocabulary(step3Assign?.payload);
  } finally {
    render(null, container);
    cleanup();
    restoreFetch();
  }
});

Deno.test("contents aggregate trigger Step3 UI: select changes drive preview and assign payload", async () => {
  const seen: AggregateTriggerDefinitionPayload[][] = [];
  const { container, cleanup } = setupDom();
  try {
    render(
      h(AggregateTriggerAuthoringPanel, {
        step2LogicalEntityDefinitions: step2Targets,
        step25RelationDefinitions: step25Targets,
        onPayloadChange: (payload) => seen.push(payload),
      }),
      container,
    );
    await flushUpdates();

    const selects = Array.from(container.querySelectorAll("select"));
    assertEquals(selects.length, 4);
    (selects[0] as HTMLSelectElement).value = "hook";
    selects[0].dispatchEvent(new Event("input", { bubbles: true }));
    selects[0].dispatchEvent(new Event("change", { bubbles: true }));
    (selects[1] as HTMLSelectElement).value = "hook_event";
    selects[1].dispatchEvent(new Event("input", { bubbles: true }));
    selects[1].dispatchEvent(new Event("change", { bubbles: true }));
    (selects[2] as HTMLSelectElement).value =
      "step2_logical_entity_definition:invoices";
    selects[2].dispatchEvent(new Event("input", { bubbles: true }));
    selects[2].dispatchEvent(new Event("change", { bubbles: true }));
    (selects[3] as HTMLSelectElement).value =
      "step2_5_relation_definition:orders->customers";
    selects[3].dispatchEvent(new Event("input", { bubbles: true }));
    selects[3].dispatchEvent(new Event("change", { bubbles: true }));
    await flushUpdates();
    await flushUpdates();

    const latest = seen.at(-1)?.[0];
    assert(latest, "UI selection must emit an aggregate trigger payload");
    assertEquals(latest.trigger_source.canonical_trigger_kind, "hook");
    assertEquals(
      latest.trigger_source.trigger_source_detail_kind,
      "hook_event",
    );
    assertEquals(latest.aggregate_target_binding.target_id, "invoices");
    assertEquals(
      latest.materialization_target_binding.target_id,
      "orders->customers",
    );
    assert(container.textContent?.includes('"target_id": "invoices"'));

  } finally {
    render(null, container);
    cleanup();
  }
});

Deno.test("admin workflow payload substrate: ui-builder and manifests payloads stay in frontend projection/client-trigger boundaries", async () => {
  const layoutJson = buildVisualLayoutPatchJson([{
    nodeId: "node-1",
    componentKey: "button.primitive",
    isDraftOnly: false,
    slotKey: "main",
    orderIndex: 0,
    parentNodeId: null,
    gridCol: 1,
    gridRow: 1,
    x: 10,
    y: 20,
    width: 120,
    height: 40,
  }]);
  const layoutPayload = JSON.parse(layoutJson);
  assertEquals(layoutPayload.nodes[0].nodeId, "node-1");
  assertFalse("runtime_destination" in layoutPayload.nodes[0]);
  assertNoForbiddenPayloadVocabulary(layoutPayload);

  const targetRef = encodeManifestPackageTargetRef(
    "00000000-0000-0000-0000-000000000010",
    "screen.searchConditions[0]",
  );
  assertEquals(
    targetRef,
    "manifest:00000000-0000-0000-0000-000000000010:screen.searchConditions[0]",
  );
  assertNoForbiddenPayloadVocabulary({ targetRef });

  const captured: Record<string, unknown>[] = [];
  const original = globalThis.fetch;
  globalThis.fetch = (_url, init) => {
    captured.push(
      JSON.parse(String((init as { body?: BodyInit })?.body ?? "{}")),
    );
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, emission: null }), {
        status: 200,
      }),
    );
  };
  try {
    await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "hub_navigation",
      action: "list_manifests",
    });
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
  assertEquals(captured[0].triggerKind, "client");
  assertEquals(captured[0].layer, "hub_navigation");
  assertFalse("role" in captured[0]);
  assertNoForbiddenPayloadVocabulary(captured[0]);
});

Deno.test("admin workflow invalid-input substrate: undefined targets and forbidden vocabulary fail closed", async () => {
  const html = renderToString(h(AggregateTriggerAuthoringPanel, {
    step2LogicalEntityDefinitions: [],
    step25RelationDefinitions: [],
  }));
  assert(html.includes("payload は作成されません"));
  assertFalse(html.includes("scheduler_event"));
  assertFalse(html.toLowerCase().includes("raw_sql"));

  assertEquals(canonicalTriggerKinds, ["cron", "hook", "client"]);
  assertFalse(triggerSourceDetailKinds.includes("scheduler_event" as never));
  assertFalse(
    materializationPayloadMapAllowedSources.includes("raw_sql" as never),
  );
  assertEquals(
    aggregateTriggerTargetFromKey(
      step2Targets,
      "step2_logical_entity_definition:undefined-target",
    ),
    null,
  );

  const captured: Record<string, unknown>[] = [];
  const original = globalThis.fetch;
  globalThis.fetch = (_url, init) => {
    captured.push(
      JSON.parse(String((init as { body?: BodyInit })?.body ?? "{}")),
    );
    return Promise.resolve(
      new Response(JSON.stringify({ success: true, emission: null }), {
        status: 200,
      }),
    );
  };
  try {
    await queueAdminClientCommand({
      operationType: "admin",
      target: "admin",
      layer: "hub_navigation",
      action: "list_manifests",
      ...({ role: "admin" } as { role: string }),
    });
  } finally {
    globalThis.fetch = original;
    __testOnly.resetCommandQueue();
  }
  assertFalse(
    "role" in captured[0],
    "runtime role vocabulary must be stripped from UI command payload",
  );
});
