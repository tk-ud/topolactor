import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { aggregateTriggerTargetOptions, approvalPolicyAllowedValues, canonicalTriggerKinds, comparisonOperatorAllowedValues, materializationPayloadMapAllowedSources, previewAggregateTriggerDefinition, triggerSourceDetailKinds } from "../lib/aggregateTriggerAuthoring.ts";

Deno.test("aggregate trigger authoring exposes SSOT vocabulary without policy judgment", () => {
  assertEquals(canonicalTriggerKinds, ["cron", "hook", "client"]);
  assertEquals(triggerSourceDetailKinds.includes("scheduler_event" as never), false);
  assertEquals(materializationPayloadMapAllowedSources.includes("function_input_event"), true);
  assertEquals(materializationPayloadMapAllowedSources.includes("raw_sql" as never), false);
  assertEquals(approvalPolicyAllowedValues.includes("auto_materialize_when_threshold_passes"), true);
  assertEquals(comparisonOperatorAllowedValues.includes("!="), true);
  const targets = aggregateTriggerTargetOptions([{ targetSource: "step2_logical_entity_definition", targetId: "entity", label: "Entity" }], [{ targetSource: "step2_5_relation_definition", targetId: "relation", label: "Relation" }]);
  assertEquals(targets.map((t) => t.targetId), ["entity", "relation"]);
  const preview = previewAggregateTriggerDefinition({ canonicalTriggerKind: "client", triggerSourceDetailKind: "client_operation_event", aggregateTargetBinding: targets[0], materializationTargetBinding: targets[1] });
  assertEquals(preview.runtimeDestination, "aggregate_trigger_runtime");
  assertEquals(preview.frontendRole, "structured_selector_and_preview_only");
});
