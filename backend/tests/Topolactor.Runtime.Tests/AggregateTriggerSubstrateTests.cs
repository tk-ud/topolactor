using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AggregateTriggerSubstrateTests
{
    private static readonly Guid DefId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static AggregateTriggerDefinition Definition(string trigger = "client", string detail = "client_operation_event", string payloadSource = "function_input_event") => new(
        DefId,
        new(trigger, detail),
        new("aggregate_current_atomic_upsert", "operation_definition", "event_schema", [detail], "materialization_policy"),
        "aggregate_key_partition", "event_append_aggregate_upsert_and_materialization",
        new("step2_logical_entity_definition", "defined_entity"), ["entity_id"], new Dictionary<string, decimal> { ["selected"] = 1, ["total"] = 1 },
        new(2, "selected", "total", ">=", 0.5m), new("step2_5_relation_definition", "defined_relation"),
        [new("name", payloadSource, "display_name")], "auto_materialize_when_threshold_passes", "append_evidence");

    [Fact]
    public void Validator_EnforcesCanonicalVocabularyTargetsAndPayloadMap()
    {
        var ok = AggregateTriggerDefinitionValidator.Validate(Definition(), new HashSet<string>{"defined_entity"}, new HashSet<string>{"defined_relation"});
        Assert.Empty(ok);
        var bad = Definition(trigger: "scheduler_event", detail: "scheduler_event", payloadSource: "raw_sql");
        bad = bad with { ConflictKeyFields = ["id where 1=1"], MaterializationTargetBinding = new("step2_logical_entity_definition", "unknown") };
        var errors = AggregateTriggerDefinitionValidator.Validate(bad, new HashSet<string>{"defined_entity"}, new HashSet<string>{"defined_relation"});
        Assert.Contains(errors, e => e.Code == "AGGREGATE_TRIGGER_KIND_INVALID");
        Assert.Contains(errors, e => e.Code == "AGGREGATE_TRIGGER_SOURCE_DETAIL_KIND_INVALID");
        Assert.Contains(errors, e => e.Code == "AGGREGATE_PAYLOAD_SOURCE_INVALID");
        Assert.Contains(errors, e => e.Code == "AGGREGATE_RAW_SQL_PROHIBITED");
        Assert.Contains(errors, e => e.Code == "AGGREGATE_MATERIALIZATION_TARGET_INVALID");
    }

    [Fact]
    public async Task Repository_IsIdempotentAtomicAndGuardsDuplicateMaterialization()
    {
        var repo = new InMemoryAggregateTriggerRepository();
        var payload = JsonSerializer.SerializeToElement(new { entity_id = "a" });
        Assert.True((await repo.AppendEventEvidenceAsync(new(DefId, "event-1", "client", "client_operation_event", payload, "actor", "ui"))).Appended);
        Assert.False((await repo.AppendEventEvidenceAsync(new(DefId, "event-1", "client", "client_operation_event", payload, "actor", "ui"))).Appended);
        var first = await repo.AtomicUpsertCurrentAsync(DefId, "a", new Dictionary<string, decimal>{{"selected",1},{"total",1}});
        var second = await repo.AtomicUpsertCurrentAsync(DefId, "a", new Dictionary<string, decimal>{{"selected",1},{"total",1}});
        Assert.Equal(2, second.Counters["selected"]);
        Assert.False(AggregateTriggerConditionEvaluator.Evaluate(Definition().ThresholdPolicy, first));
        Assert.True(AggregateTriggerConditionEvaluator.Evaluate(Definition().ThresholdPolicy, second));
        var notEqualPolicy = Definition().ThresholdPolicy with { ComparisonOperator = "!=", TargetRatio = 0.25m };
        Assert.True(AggregateTriggerConditionEvaluator.Evaluate(notEqualPolicy, second));
        var m1 = await repo.TryMaterializeAsync(Definition(), second, "event-2");
        var m2 = await repo.TryMaterializeAsync(Definition(), second, "event-3");
        Assert.True(m1.Created);
        Assert.False(m2.Created);
        Assert.Equal(m1.MaterializationId, m2.MaterializationId);
    }

    [Fact]
    public async Task Runtime_ExecutesOnlyWhenStepTargetsAreDeclaredAndRejectsFrontendPolicyJudgment()
    {
        var runtime = new AggregateTriggerRuntime(new InMemoryAggregateTriggerRepository());
        var r1 = await runtime.ExecuteAsync(BuildRequest("event-1"), null);
        Assert.True(r1.Success);
        Assert.Contains("threshold_not_satisfied", r1.Emission!.Data!.Value.GetRawText());
        var r2 = await runtime.ExecuteAsync(BuildRequest("event-2"), null);
        Assert.True(r2.Success);
        Assert.Contains("materialized", r2.Emission!.Data!.Value.GetRawText());
        var dup = await runtime.ExecuteAsync(BuildRequest("event-2"), null);
        Assert.Contains("duplicate_event_evidence", dup.Emission!.Data!.Value.GetRawText());
        var invalidSource = await runtime.ExecuteAsync(BuildRequest("event-3", Definition(payloadSource:"arbitrary_json_path_not_declared_by_schema")), null);
        Assert.False(invalidSource.Success);
        Assert.Contains(invalidSource.Errors, e => e.Code == "AGGREGATE_PAYLOAD_SOURCE_INVALID");
        var invalidTarget = await runtime.ExecuteAsync(BuildRequest("event-4", declaredStep2: [], declaredStep25: []), null);
        Assert.False(invalidTarget.Success);
        Assert.Contains(invalidTarget.Errors, e => e.Code == "AGGREGATE_TARGET_INVALID");
        Assert.Contains(invalidTarget.Errors, e => e.Code == "AGGREGATE_MATERIALIZATION_TARGET_INVALID");
    }

    private static EndpointRequestDto BuildRequest(string eventId, AggregateTriggerDefinition? definition = null, IReadOnlyList<string>? declaredStep2 = null, IReadOnlyList<string>? declaredStep25 = null)
    {
        var payload = JsonSerializer.SerializeToElement(new AggregateTriggerRuntimeRequest(definition ?? Definition(), eventId, "a", JsonSerializer.SerializeToElement(new { entity_id = "a" }), declaredStep2 ?? ["defined_entity"], declaredStep25 ?? ["defined_relation"], "actor", "ui", false));
        return new("AggregateTrigger", "aggregate_trigger", "runtime", "execute", null, payload, null, "client", "admin");
    }
}
