using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AggregateTriggerSubstrateTests
{
    private static readonly Guid DefId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static AggregateTriggerDefinition Definition(string trigger = "client", string source = "ui_operation", string payloadSource = "function_input_event") => new(
        DefId, trigger, source, "aggregate_current_atomic_upsert", "processing_function", "single_transaction",
        new("step2_entity", "defined_entity"), ["entity_id"], new Dictionary<string, decimal> { ["selected"] = 1, ["total"] = 1 },
        new(2, "selected", "total", ">=", 0.5m), new("step2_5_relation", "defined_relation"),
        [new("name", payloadSource, "display_name")], "none");

    [Fact]
    public void Validator_EnforcesCanonicalVocabularyTargetsAndPayloadMap()
    {
        var ok = AggregateTriggerDefinitionValidator.Validate(Definition(), new HashSet<string>{"defined_entity"}, new HashSet<string>{"defined_relation"});
        Assert.Empty(ok);
        var bad = Definition(trigger: "scheduler_event", source: "scheduler_event", payloadSource: "raw_sql");
        bad = bad with { ConflictKeyFields = ["id where 1=1"], MaterializationTargetBinding = new("step2_entity", "unknown") };
        var errors = AggregateTriggerDefinitionValidator.Validate(bad, new HashSet<string>{"defined_entity"}, new HashSet<string>{"defined_relation"});
        Assert.Contains(errors, e => e.Code == "AGGREGATE_TRIGGER_KIND_INVALID");
        Assert.Contains(errors, e => e.Code == "AGGREGATE_SOURCE_DETAIL_KIND_INVALID");
        Assert.Contains(errors, e => e.Code == "AGGREGATE_PAYLOAD_SOURCE_KIND_INVALID");
        Assert.Contains(errors, e => e.Code == "AGGREGATE_RAW_SQL_PROHIBITED");
        Assert.Contains(errors, e => e.Code == "AGGREGATE_MATERIALIZATION_TARGET_INVALID");
    }

    [Fact]
    public async Task Repository_IsIdempotentAtomicAndGuardsDuplicateMaterialization()
    {
        var repo = new InMemoryAggregateTriggerRepository();
        var payload = JsonSerializer.SerializeToElement(new { entity_id = "a" });
        Assert.True((await repo.AppendEventEvidenceAsync(new(DefId, "event-1", "client", "ui_operation", payload, "actor", "ui"))).Appended);
        Assert.False((await repo.AppendEventEvidenceAsync(new(DefId, "event-1", "client", "ui_operation", payload, "actor", "ui"))).Appended);
        var first = await repo.AtomicUpsertCurrentAsync(DefId, "a", new Dictionary<string, decimal>{{"selected",1},{"total",1}});
        var second = await repo.AtomicUpsertCurrentAsync(DefId, "a", new Dictionary<string, decimal>{{"selected",1},{"total",1}});
        Assert.Equal(2, second.Counters["selected"]);
        Assert.False(AggregateTriggerConditionEvaluator.Evaluate(Definition().ThresholdPolicy, first));
        Assert.True(AggregateTriggerConditionEvaluator.Evaluate(Definition().ThresholdPolicy, second));
        var m1 = await repo.TryMaterializeAsync(Definition(), second, "event-2");
        var m2 = await repo.TryMaterializeAsync(Definition(), second, "event-3");
        Assert.True(m1.Created);
        Assert.False(m2.Created);
        Assert.Equal(m1.MaterializationId, m2.MaterializationId);
    }

    [Fact]
    public async Task Runtime_DispatchableExecutesEventToCurrentToMaterializationAndRejectsFrontendPolicyJudgment()
    {
        var runtime = new AggregateTriggerRuntime(new InMemoryAggregateTriggerRepository());
        var req1 = BuildRequest("event-1");
        var r1 = await runtime.ExecuteAsync(req1, null);
        Assert.True(r1.Success);
        Assert.Contains("threshold_not_satisfied", r1.Emission!.Data!.Value.GetRawText());
        var r2 = await runtime.ExecuteAsync(BuildRequest("event-2"), null);
        Assert.True(r2.Success);
        Assert.Contains("materialized", r2.Emission!.Data!.Value.GetRawText());
        var dup = await runtime.ExecuteAsync(BuildRequest("event-2"), null);
        Assert.Contains("duplicate_event_evidence", dup.Emission!.Data!.Value.GetRawText());
        var invalid = await runtime.ExecuteAsync(BuildRequest("event-3", Definition(payloadSource:"arbitrary_json_path")), null);
        Assert.False(invalid.Success);
        Assert.Contains(invalid.Errors, e => e.Code == "AGGREGATE_PAYLOAD_SOURCE_KIND_INVALID");
    }

    private static EndpointRequestDto BuildRequest(string eventId, AggregateTriggerDefinition? definition = null)
    {
        var payload = JsonSerializer.SerializeToElement(new AggregateTriggerRuntimeRequest(definition ?? Definition(), eventId, "a", JsonSerializer.SerializeToElement(new { entity_id = "a" }), "actor", "ui", false));
        return new("AggregateTrigger", "aggregate_trigger", "runtime", "execute", null, payload, null, "client", "admin");
    }
}
