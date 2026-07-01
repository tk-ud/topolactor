import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { aggregateTriggerKinds, aggregateTriggerTargetOptions, materializationPayloadSourceKinds, previewAggregateTriggerDefinition, sourceDetailKinds } from "../lib/aggregateTriggerAuthoring.ts";

Deno.test("aggregate trigger authoring exposes structured selector without policy judgment", () => {
  assertEquals(aggregateTriggerKinds, ["cron", "hook", "client"]);
  assertEquals(sourceDetailKinds.includes("scheduler_event" as never), false);
  assertEquals(materializationPayloadSourceKinds.includes("function_input_event"), true);
  assertEquals(materializationPayloadSourceKinds.includes("raw_sql" as never), false);
  const targets = aggregateTriggerTargetOptions([{ kind: "step2_entity", id: "entity", label: "Entity" }], [{ kind: "step2_5_relation", id: "relation", label: "Relation" }]);
  assertEquals(targets.map((t) => t.id), ["entity", "relation"]);
  const preview = previewAggregateTriggerDefinition({ triggerKind: "client", sourceDetailKind: "ui_operation", aggregateTarget: targets[0], materializationTarget: targets[1] });
  assertEquals(preview.runtimeDestination, "aggregate_trigger_runtime");
  assertEquals(preview.frontendJudgment, "authoring_preview_only_no_threshold_materialization_or_approval_decision");
});
