using Topolactor.Repository;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AggregateTriggerRepositorySqlContractTests
{
    [Fact]
    public void NpgsqlRepository_UsesSchemaQualifiedParameterizedFixedTemplates()
    {
        Assert.Contains("runtime_orchestration.aggregate_trigger_event_evidence", NpgsqlAggregateTriggerRepository.AppendSql);
        Assert.Contains("ON CONFLICT (source_kind, source_id, correlation_id, event_fingerprint) DO NOTHING", NpgsqlAggregateTriggerRepository.AppendSql);
        Assert.Contains("@trigger_definition_id", NpgsqlAggregateTriggerRepository.AppendSql);
        Assert.Contains("runtime_orchestration.aggregate_trigger_current", NpgsqlAggregateTriggerRepository.UpsertSql);
        Assert.Contains("ON CONFLICT (trigger_definition_id, conflict_key_hash) DO UPDATE", NpgsqlAggregateTriggerRepository.UpsertSql);
        Assert.Contains("runtime_orchestration.aggregate_trigger_materialization_evidence", NpgsqlAggregateTriggerRepository.MaterializeSql);
        Assert.Contains("ON CONFLICT (trigger_definition_id, materialization_target_source, materialization_target_id, conflict_key_hash, payload_fingerprint) DO NOTHING", NpgsqlAggregateTriggerRepository.MaterializeSql);
        Assert.DoesNotContain("aggregate_trigger_event_log", NpgsqlAggregateTriggerRepository.AppendSql);
        Assert.DoesNotContain("{", NpgsqlAggregateTriggerRepository.AppendSql);
    }
}
