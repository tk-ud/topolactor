using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

public class NpgsqlAggregateTriggerRepository(string connectionString) : AggregateTriggerRepository
{
    public const string AppendSql = """
        INSERT INTO runtime_orchestration.aggregate_trigger_event_log (definition_id, event_id, trigger_kind, source_detail_kind, event_payload, actor, source)
        VALUES (@definition_id, @event_id, @trigger_kind, @source_detail_kind, @event_payload::jsonb, @actor, @source)
        ON CONFLICT (definition_id, event_id) DO NOTHING;
        """;
    public const string UpsertSql = """
        INSERT INTO runtime_orchestration.aggregate_trigger_current (definition_id, conflict_key, counters, updated_at)
        VALUES (@definition_id, @conflict_key, @delta::jsonb, now())
        ON CONFLICT (definition_id, conflict_key) DO UPDATE
        SET counters = runtime_orchestration.jsonb_numeric_add(aggregate_trigger_current.counters, EXCLUDED.counters), updated_at = now()
        RETURNING counters, updated_at;
        """;
    public const string MaterializeSql = """
        INSERT INTO runtime_orchestration.aggregate_trigger_materialization_log (definition_id, conflict_key, event_id, materialization_target_kind, materialization_target_id, payload_map)
        VALUES (@definition_id, @conflict_key, @event_id, @target_kind, @target_id, @payload_map::jsonb)
        ON CONFLICT (definition_id, conflict_key, materialization_target_kind, materialization_target_id) DO NOTHING
        RETURNING materialization_id;
        """;

    public async Task<AggregateTriggerAppendResult> AppendEventEvidenceAsync(AggregateTriggerEventEvidence evidence, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(AppendSql, conn);
        cmd.Parameters.AddWithValue("definition_id", evidence.DefinitionId); cmd.Parameters.AddWithValue("event_id", evidence.EventId);
        cmd.Parameters.AddWithValue("trigger_kind", evidence.TriggerKind); cmd.Parameters.AddWithValue("source_detail_kind", evidence.SourceDetailKind);
        cmd.Parameters.AddWithValue("event_payload", evidence.EventPayload.GetRawText()); cmd.Parameters.AddWithValue("actor", (object?)evidence.Actor ?? DBNull.Value); cmd.Parameters.AddWithValue("source", (object?)evidence.Source ?? DBNull.Value);
        return new AggregateTriggerAppendResult(await cmd.ExecuteNonQueryAsync(ct) > 0);
    }

    public async Task<AggregateTriggerCurrentRow> AtomicUpsertCurrentAsync(Guid definitionId, string conflictKey, IReadOnlyDictionary<string, decimal> deltaMap, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(UpsertSql, conn);
        cmd.Parameters.AddWithValue("definition_id", definitionId); cmd.Parameters.AddWithValue("conflict_key", conflictKey);
        cmd.Parameters.AddWithValue("delta", System.Text.Json.JsonSerializer.Serialize(deltaMap));
        await using var reader = await cmd.ExecuteReaderAsync(ct); await reader.ReadAsync(ct);
        var counters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(reader.GetString(0)) ?? [];
        return new AggregateTriggerCurrentRow(definitionId, conflictKey, counters, reader.GetFieldValue<DateTimeOffset>(1));
    }

    public async Task<AggregateTriggerMaterializationResult> TryMaterializeAsync(AggregateTriggerDefinition definition, AggregateTriggerCurrentRow currentRow, string eventId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(MaterializeSql, conn);
        cmd.Parameters.AddWithValue("definition_id", definition.DefinitionId); cmd.Parameters.AddWithValue("conflict_key", currentRow.ConflictKey); cmd.Parameters.AddWithValue("event_id", eventId);
        cmd.Parameters.AddWithValue("target_kind", definition.MaterializationTargetBinding.TargetKind); cmd.Parameters.AddWithValue("target_id", definition.MaterializationTargetBinding.TargetId);
        cmd.Parameters.AddWithValue("payload_map", System.Text.Json.JsonSerializer.Serialize(definition.MaterializationPayloadMap));
        var id = await cmd.ExecuteScalarAsync(ct);
        return id is Guid g ? new(true, g) : new(false, Guid.Empty);
    }
}
