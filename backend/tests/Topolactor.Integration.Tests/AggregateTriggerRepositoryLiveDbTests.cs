using System.Text.Json;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for the aggregate trigger substrate repository boundary.
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set, matching the repository
/// live-DB test convention. Requires db/runtime_orchestration_tables.sql applied to the
/// target database.
///
/// Verifies: idempotent event evidence append, atomic aggregate current upsert, the aggregate
/// upsert + materialization evidence append committing in one backend transaction
/// (aggregate_trigger_substrate_contract.transaction_boundary.atomicity_rule), the duplicate
/// materialization guard, and canonical structured definition persistence round-trip.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class AggregateTriggerRepositoryLiveDbTests
{
    [Fact]
    public async Task AppendEventEvidenceAsync_IsIdempotentByFingerprint()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var repo = new NpgsqlAggregateTriggerRepository(cs);
        var payload = JsonSerializer.SerializeToElement(new { entity_id = "a" });
        var evidence = new AggregateTriggerEventEvidence(defId, "event-1", "client", "client_operation_event", payload, "actor", "live-db-test");

        try
        {
            var first = await repo.AppendEventEvidenceAsync(evidence);
            var second = await repo.AppendEventEvidenceAsync(evidence);
            Assert.True(first.Appended);
            Assert.False(second.Appended);

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM runtime_orchestration.aggregate_trigger_event_evidence WHERE trigger_definition_id = @id";
            cmd.Parameters.AddWithValue("id", defId);
            Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await CleanupAsync(cs, defId);
        }
    }

    [Fact]
    public async Task AtomicUpsertAndMaterializeAsync_CommitsUpsertAndMaterializationInSameTransaction()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var repo = new NpgsqlAggregateTriggerRepository(cs);
        var definition = BuildDefinition(defId);

        try
        {
            // First event: below minimum_trial_count(2) -> threshold not satisfied, no materialization row.
            var r1 = await repo.AtomicUpsertAndMaterializeAsync(definition, "conflict-a", "event-1",
                row => Evaluate(definition, row) ? AggregateTriggerMaterializationDecision.Materialize : AggregateTriggerMaterializationDecision.ThresholdNotSatisfied);
            Assert.Equal(AggregateTriggerMaterializationDecision.ThresholdNotSatisfied, r1.Decision);
            Assert.Null(r1.Materialization);

            // Second event: threshold satisfied -> upsert + materialization append in one transaction.
            var r2 = await repo.AtomicUpsertAndMaterializeAsync(definition, "conflict-a", "event-2",
                row => Evaluate(definition, row) ? AggregateTriggerMaterializationDecision.Materialize : AggregateTriggerMaterializationDecision.ThresholdNotSatisfied);
            Assert.Equal(AggregateTriggerMaterializationDecision.Materialize, r2.Decision);
            Assert.NotNull(r2.Materialization);
            Assert.True(r2.Materialization!.Created);

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();

            // Both rows exist.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM runtime_orchestration.aggregate_trigger_current WHERE trigger_definition_id = @id AND conflict_key = 'conflict-a'";
                cmd.Parameters.AddWithValue("id", defId);
                Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
            }
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM runtime_orchestration.aggregate_trigger_materialization_evidence WHERE trigger_definition_id = @id AND conflict_key = 'conflict-a'";
                cmd.Parameters.AddWithValue("id", defId);
                Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
            }

            // xmin equality is the direct proof the two INSERT/UPDATE statements committed under the
            // same backend transaction id: PostgreSQL stamps every row with the transaction id (xmin)
            // of the writer that last modified it.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT
                        (SELECT xmin::text FROM runtime_orchestration.aggregate_trigger_current WHERE trigger_definition_id = @id AND conflict_key = 'conflict-a'),
                        (SELECT xmin::text FROM runtime_orchestration.aggregate_trigger_materialization_evidence WHERE trigger_definition_id = @id AND conflict_key = 'conflict-a')
                    """;
                cmd.Parameters.AddWithValue("id", defId);
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                var currentXmin = reader.GetString(0);
                var materializationXmin = reader.GetString(1);
                Assert.Equal(currentXmin, materializationXmin);
            }
        }
        finally
        {
            await CleanupAsync(cs, defId);
        }
    }

    [Fact]
    public async Task AtomicUpsertAndMaterializeAsync_GuardsDuplicateMaterializationAcrossCalls()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var repo = new NpgsqlAggregateTriggerRepository(cs);
        var definition = BuildDefinition(defId);

        try
        {
            await repo.AtomicUpsertAndMaterializeAsync(definition, "conflict-b", "event-1", _ => AggregateTriggerMaterializationDecision.ThresholdNotSatisfied);
            var first = await repo.AtomicUpsertAndMaterializeAsync(definition, "conflict-b", "event-2", _ => AggregateTriggerMaterializationDecision.Materialize);
            var second = await repo.AtomicUpsertAndMaterializeAsync(definition, "conflict-b", "event-3", _ => AggregateTriggerMaterializationDecision.Materialize);

            Assert.True(first.Materialization!.Created);
            Assert.False(second.Materialization!.Created);
            Assert.Equal(first.Materialization.MaterializationId, second.Materialization.MaterializationId);

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM runtime_orchestration.aggregate_trigger_materialization_evidence WHERE trigger_definition_id = @id AND conflict_key = 'conflict-b'";
            cmd.Parameters.AddWithValue("id", defId);
            Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await CleanupAsync(cs, defId);
        }
    }

    [Fact]
    public async Task SaveDefinitionAsync_AndLoadDefinitionAsync_RoundTripCanonicalPersistedFields()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var defId = Guid.NewGuid();
        var repo = new NpgsqlAggregateTriggerRepository(cs);
        var definition = BuildDefinition(defId);

        try
        {
            await repo.SaveDefinitionAsync(definition);
            var loaded = await repo.LoadDefinitionAsync(defId);

            Assert.NotNull(loaded);
            Assert.Equal(definition.ExecutionScope, loaded!.ExecutionScope);
            Assert.Equal(definition.TransactionBoundary, loaded.TransactionBoundary);
            Assert.Equal(definition.AggregateTargetBinding.TargetId, loaded.AggregateTargetBinding.TargetId);
            Assert.Equal(definition.MaterializationTargetBinding.TargetId, loaded.MaterializationTargetBinding.TargetId);
            Assert.Equal(definition.ApprovalPolicy, loaded.ApprovalPolicy);
            Assert.Equal(definition.ThresholdPolicy.MinimumTrialCount, loaded.ThresholdPolicy.MinimumTrialCount);
            Assert.Equal(definition.ThresholdPolicy.TargetRatio, loaded.ThresholdPolicy.TargetRatio);

            // Re-save (admin re-authoring the same trigger_definition_id) upserts rather than duplicating.
            await repo.SaveDefinitionAsync(definition with { ApprovalPolicy = "require_backend_approval_before_materialization" });
            var reloaded = await repo.LoadDefinitionAsync(defId);
            Assert.Equal("require_backend_approval_before_materialization", reloaded!.ApprovalPolicy);

            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM runtime_orchestration.aggregate_trigger_definitions WHERE trigger_definition_id = @id";
            cmd.Parameters.AddWithValue("id", defId);
            Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await CleanupAsync(cs, defId);
        }
    }

    internal static AggregateTriggerDefinition BuildDefinition(Guid defId) => new(
        defId,
        new AggregateTriggerSource("client", "client_operation_event"),
        new AggregateTriggerProcessingFunctionScope("live_db_test_function", "live_db_test_operation", "live_db_test.event.v1", ["client_operation_event"], "backend_runtime_authority_required"),
        "aggregate_key_partition",
        "event_append_aggregate_upsert_and_materialization",
        new AggregateTriggerTargetBinding("step2_logical_entity_definition", "live_db_test_entity"),
        ["entity_id"],
        new Dictionary<string, decimal> { ["selected"] = 1, ["total"] = 1 },
        new AggregateTriggerThresholdPolicy(2, "selected", "total", ">=", 0.5m),
        new AggregateTriggerTargetBinding("step2_5_relation_definition", "live_db_test_relation"),
        [new AggregateTriggerPayloadMapEntry("name", "function_input_event", "display_name")],
        "auto_materialize_when_threshold_passes",
        "append_evidence");

    internal static bool Evaluate(AggregateTriggerDefinition definition, AggregateTriggerCurrentRow row) =>
        Topolactor.Runtime.AggregateTriggerConditionEvaluator.Evaluate(definition.ThresholdPolicy, row);

    internal static async Task CleanupAsync(string cs, Guid triggerDefinitionId)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM runtime_orchestration.aggregate_trigger_materialization_evidence WHERE trigger_definition_id = @id;
            DELETE FROM runtime_orchestration.aggregate_trigger_current WHERE trigger_definition_id = @id;
            DELETE FROM runtime_orchestration.aggregate_trigger_event_evidence WHERE trigger_definition_id = @id;
            DELETE FROM runtime_orchestration.aggregate_trigger_definitions WHERE trigger_definition_id = @id;
            """;
        cmd.Parameters.AddWithValue("id", triggerDefinitionId);
        await cmd.ExecuteNonQueryAsync();
    }

    internal static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }
        return cs;
    }
}
