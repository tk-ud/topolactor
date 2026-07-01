using Topolactor.Repository;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AggregateTriggerRepositorySqlContractTests
{
    [Fact]
    public void NpgsqlRepository_UsesSchemaQualifiedParameterizedFixedTemplates()
    {
        Assert.Contains("runtime_orchestration.aggregate_trigger_event_log", NpgsqlAggregateTriggerRepository.AppendSql);
        Assert.Contains("ON CONFLICT (definition_id, event_id) DO NOTHING", NpgsqlAggregateTriggerRepository.AppendSql);
        Assert.Contains("@definition_id", NpgsqlAggregateTriggerRepository.AppendSql);
        Assert.Contains("runtime_orchestration.aggregate_trigger_current", NpgsqlAggregateTriggerRepository.UpsertSql);
        Assert.Contains("ON CONFLICT (definition_id, conflict_key) DO UPDATE", NpgsqlAggregateTriggerRepository.UpsertSql);
        Assert.Contains("runtime_orchestration.aggregate_trigger_materialization_log", NpgsqlAggregateTriggerRepository.MaterializeSql);
        Assert.Contains("ON CONFLICT (definition_id, conflict_key, materialization_target_kind, materialization_target_id) DO NOTHING", NpgsqlAggregateTriggerRepository.MaterializeSql);
        Assert.DoesNotContain("{", NpgsqlAggregateTriggerRepository.AppendSql);
    }
}
