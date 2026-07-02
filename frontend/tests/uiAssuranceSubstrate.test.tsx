import {
  assert,
  assertEquals,
  assertFalse,
} from "https://deno.land/std@0.224.0/assert/mod.ts";
import { h } from "preact";
import { renderToString } from "preact-render-to-string";
import AggregateTriggerAuthoringPanel from "../components/AggregateTriggerAuthoringPanel.tsx";
import { buildAssignPayloadForStep } from "../lib/contentsAssign.ts";
import { emptyManifestScreenDesign } from "../lib/manifestScreenDesign.ts";
import {
  type AggregateTriggerDefinitionPayload,
  aggregateTriggerTargetOptions,
  buildAggregateTriggerDefinition,
  canonicalTriggerKinds,
  materializationPayloadMapAllowedSources,
  type StepTarget,
  triggerSourceDetailKinds,
} from "../lib/aggregateTriggerAuthoring.ts";

const SQL_FORBIDDEN = [
  "raw_sql",
  "select ",
  " where ",
  " case ",
  "scheduler_event",
  "arbitrary_table",
];

type UiAssuranceCase = {
  proofId: string;
  routePath: string;
  islandPath: string;
  connectedComponentPath: string;
  componentMarker: string;
  step2Targets: StepTarget[];
  step25Targets: StepTarget[];
  buildPayload: () => AggregateTriggerDefinitionPayload[];
};

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
  assert(
    "trigger_definition_id" in payload,
    "backend schema field trigger_definition_id must exist",
  );
  assert(
    "trigger_source" in payload,
    "backend schema field trigger_source must exist",
  );
  assert(
    "canonical_trigger_kind" in payload.trigger_source,
    "backend schema field canonical_trigger_kind must exist",
  );
  assert(
    "trigger_source_detail_kind" in payload.trigger_source,
    "backend schema field trigger_source_detail_kind must exist",
  );
  assert(
    "processing_function_scope" in payload,
    "backend schema field processing_function_scope must exist",
  );
  assert(
    "execution_scope" in payload,
    "backend schema field execution_scope must exist",
  );
  assert(
    "transaction_boundary" in payload,
    "backend schema field transaction_boundary must exist",
  );
  assert(
    "aggregate_target_binding" in payload,
    "backend schema field aggregate_target_binding must exist",
  );
  assert(
    "target_source" in payload.aggregate_target_binding,
    "backend schema field target_source must exist",
  );
  assert(
    "target_id" in payload.aggregate_target_binding,
    "backend schema field target_id must exist",
  );
  assert(
    "threshold_policy" in payload,
    "backend schema field threshold_policy must exist",
  );
  assert(
    "materialization_target_binding" in payload,
    "backend schema field materialization_target_binding must exist",
  );
  assert(
    "materialization_payload_map" in payload,
    "backend schema field materialization_payload_map must exist",
  );
  assert(
    "approval_policy" in payload,
    "backend schema field approval_policy must exist",
  );
}

const aggregateTriggerUiAssurance: UiAssuranceCase = {
  proofId: "aggregate-trigger-step3-ui-assurance",
  routePath: "frontend/routes/admin/contents.tsx",
  islandPath: "frontend/islands/ContentsScreenDesignPanel.tsx",
  connectedComponentPath:
    "frontend/components/AggregateTriggerAuthoringPanel.tsx",
  componentMarker:
    'data-ui-assurance-component="aggregate-trigger-step3-authoring"',
  step2Targets: [{
    targetSource: "step2_logical_entity_definition",
    targetId: "orders",
    label: "Step2 logical entity: orders",
  }],
  step25Targets: [{
    targetSource: "step2_5_relation_definition",
    targetId: "orders->customers",
    label: "Step2.5 relation: orders → customers",
  }],
  buildPayload: () => [buildAggregateTriggerDefinition({
    triggerDefinitionId: "00000000-0000-0000-0000-000000000001",
    canonicalTriggerKind: "client",
    triggerSourceDetailKind: "client_operation_event",
    aggregateTargetBinding: {
      targetSource: "step2_logical_entity_definition",
      targetId: "orders",
      label: "orders",
    },
    materializationTargetBinding: {
      targetSource: "step2_5_relation_definition",
      targetId: "orders->customers",
      label: "orders → customers",
    },
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
  })],
};

Deno.test("UI assurance substrate: route, island, and component are connected for admin contents Step3", async () => {
  const route = await Deno.readTextFile(aggregateTriggerUiAssurance.routePath);
  const island = await Deno.readTextFile(
    aggregateTriggerUiAssurance.islandPath,
  );
  const component = await Deno.readTextFile(
    aggregateTriggerUiAssurance.connectedComponentPath,
  );

  assert(
    route.includes("ContentsAdmin"),
    "admin contents route must mount the workflow island tree",
  );
  assert(
    island.includes("AggregateTriggerAuthoringPanel"),
    "workflow island must import/use aggregate trigger UI, not helper-only proof",
  );
  assert(
    component.includes(aggregateTriggerUiAssurance.componentMarker),
    "component must expose a stable UI assurance marker",
  );
});

Deno.test("UI assurance substrate: Step3 rendered UI exposes selectors, preview, and excludes forbidden vocabulary", () => {
  const html = renderToString(h(AggregateTriggerAuthoringPanel, {
    step2LogicalEntityDefinitions: aggregateTriggerUiAssurance.step2Targets,
    step25RelationDefinitions: aggregateTriggerUiAssurance.step25Targets,
  }));
  assert(
    html.includes("aggregate-trigger-step3-authoring"),
    "Step3 aggregate trigger UI marker must render",
  );
  assert(
    html.includes("aggregate canonical trigger kind"),
    "canonical trigger selector must render",
  );
  assert(
    html.includes("aggregate target binding"),
    "aggregate target selector must render",
  );
  assert(
    html.includes("Step2.5 relation"),
    "Step2.5 saved relation target must render",
  );
  assert(
    html.includes("structured payload preview"),
    "structured preview must render",
  );
  assertFalse(
    html.includes("scheduler_event"),
    "scheduler_event must not be selectable",
  );
  assertFalse(
    html.toLowerCase().includes("raw_sql"),
    "raw SQL source must not be selectable",
  );
  assert(
    html.includes("backend runtime authority"),
    "frontend must disclose backend runtime authority for final judgment",
  );
});

Deno.test("UI assurance substrate: Step2 and Step2.5 saved targets are the only Step3 target options", () => {
  const options = aggregateTriggerTargetOptions(
    aggregateTriggerUiAssurance.step2Targets,
    aggregateTriggerUiAssurance.step25Targets,
  );
  assertEquals(options.map((o) => o.targetId), ["orders", "orders->customers"]);
  const payload = aggregateTriggerUiAssurance.buildPayload()[0];
  assertEquals(payload.aggregate_target_binding, {
    target_source: "step2_logical_entity_definition",
    target_id: "orders",
  });
  assertEquals(payload.materialization_target_binding, {
    target_source: "step2_5_relation_definition",
    target_id: "orders->customers",
  });
});

Deno.test("UI assurance substrate: workflow UI payload shape matches backend AggregateTriggerContracts names", () => {
  const payload = aggregateTriggerUiAssurance.buildPayload()[0];
  assertBackendAggregateTriggerShape(payload);
  assertEquals(canonicalTriggerKinds, ["cron", "hook", "client"]);
  assertFalse(triggerSourceDetailKinds.includes("scheduler_event" as never));
  assert(
    materializationPayloadMapAllowedSources.includes(
      payload.materialization_payload_map[0].source as never,
    ),
  );
  assertNoForbiddenPayloadVocabulary(payload);
});

Deno.test("admin workflow E2E substrate: Step3 assign payload carries UI-authored aggregate trigger definitions", () => {
  const design = emptyManifestScreenDesign();
  design.logicalTables = [{
    tableName: "orders",
    columns: [{ name: "id", dataType: "text", nullable: false }],
  }];
  const aggregateTriggerDefinitions = aggregateTriggerUiAssurance
    .buildPayload();
  const payload = buildAssignPayloadForStep(3, "manifest-1", design, null, {
    aggregateTriggerDefinitions,
  });
  assertEquals(payload.aggregateTriggerDefinitions?.length, 1);
  assertBackendAggregateTriggerShape(
    payload.aggregateTriggerDefinitions
      ?.[0] as AggregateTriggerDefinitionPayload,
  );
  assertNoForbiddenPayloadVocabulary(payload.aggregateTriggerDefinitions);
});
