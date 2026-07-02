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
    bodyMarkers: ["topolactor", "canvas", "パッケージ", "component", "binding"],
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

Deno.test("admin workflow UI assurance substrate: connected components/helpers are used by actual route body modules", async () => {
  for (const routeCase of adminWorkflowCases) {
    const sourcePath = routeCase.routePath === "/admin/contents"
      ? "../islands/ContentsAdmin.tsx"
      : routeCase.routePath === "/admin/ui-builder"
      ? "../islands/UiBuilderAdmin.tsx"
      : "../islands/ManifestsAdmin.tsx";
    const source = await Deno.readTextFile(
      new URL(sourcePath, import.meta.url),
    );
    for (const marker of routeCase.boundaryMarkers) {
      assert(
        source.includes(marker),
        `${routeCase.routePath}: body module must connect ${marker}`,
      );
    }
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
