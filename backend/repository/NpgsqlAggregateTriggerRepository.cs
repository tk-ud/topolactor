using Npgsql;
using Topolactor.Schema;

namespace Topolactor.Repository;

public class NpgsqlAggregateTriggerRepository(string connectionString) : AggregateTriggerRepository
{
    public const string AppendSql = """
        INSERT INTO runtime_orchestration.aggregate_trigger_event_evidence (trigger_definition_id, source_kind, source_id, correlation_id, event_fingerprint, event_id, canonical_trigger_kind, trigger_source_detail_kind, event_payload, actor, source)
        VALUES (@trigger_definition_id, @source_kind, @source_id, @correlation_id, @event_fingerprint, @event_id, @canonical_trigger_kind, @trigger_source_detail_kind, @event_payload::jsonb, @actor, @source)
        ON CONFLICT (source_kind, source_id, correlation_id, event_fingerprint) DO NOTHING;
        """;
    public const string UpsertSql = """
        INSERT INTO runtime_orchestration.aggregate_trigger_current (trigger_definition_id, conflict_key_hash, conflict_key, counters, updated_at)
        VALUES (@trigger_definition_id, @conflict_key_hash, @conflict_key, @delta::jsonb, now())
        ON CONFLICT (trigger_definition_id, conflict_key_hash) DO UPDATE
        SET counters = runtime_orchestration.jsonb_numeric_add(aggregate_trigger_current.counters, EXCLUDED.counters), updated_at = now()
        RETURNING counters, updated_at;
        """;
    public const string MaterializeSql = """
        INSERT INTO runtime_orchestration.aggregate_trigger_materialization_evidence (trigger_definition_id, conflict_key_hash, conflict_key, event_id, materialization_target_source, materialization_target_id, payload_fingerprint, materialization_payload_map_json)
        VALUES (@trigger_definition_id, @conflict_key_hash, @conflict_key, @event_id, @materialization_target_source, @materialization_target_id, @payload_fingerprint, @materialization_payload_map_json::jsonb)
        ON CONFLICT (trigger_definition_id, materialization_target_source, materialization_target_id, conflict_key_hash, payload_fingerprint) DO NOTHING
        RETURNING materialization_id;
        """;

    public async Task<AggregateTriggerAppendResult> AppendEventEvidenceAsync(AggregateTriggerEventEvidence evidence, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(AppendSql, conn);
        cmd.Parameters.AddWithValue("trigger_definition_id", evidence.TriggerDefinitionId);
        cmd.Parameters.AddWithValue("source_kind", evidence.TriggerSourceDetailKind);
        cmd.Parameters.AddWithValue("source_id", evidence.Source ?? "unknown_source");
        cmd.Parameters.AddWithValue("correlation_id", evidence.EventId);
        cmd.Parameters.AddWithValue("event_fingerprint", evidence.EventId);
        cmd.Parameters.AddWithValue("event_id", evidence.EventId);
        cmd.Parameters.AddWithValue("canonical_trigger_kind", evidence.CanonicalTriggerKind);
        cmd.Parameters.AddWithValue("trigger_source_detail_kind", evidence.TriggerSourceDetailKind);
        cmd.Parameters.AddWithValue("event_payload", evidence.EventPayload.GetRawText());
        cmd.Parameters.AddWithValue("actor", (object?)evidence.Actor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("source", (object?)evidence.Source ?? DBNull.Value);
        return new AggregateTriggerAppendResult(await cmd.ExecuteNonQueryAsync(ct) > 0);
    }

    public async Task<AggregateTriggerCurrentRow> AtomicUpsertCurrentAsync(Guid triggerDefinitionId, string conflictKey, IReadOnlyDictionary<string, decimal> deltaMap, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(UpsertSql, conn);
        cmd.Parameters.AddWithValue("trigger_definition_id", triggerDefinitionId);
        cmd.Parameters.AddWithValue("conflict_key_hash", conflictKey);
        cmd.Parameters.AddWithValue("conflict_key", conflictKey);
        cmd.Parameters.AddWithValue("delta", System.Text.Json.JsonSerializer.Serialize(deltaMap));
        await using var reader = await cmd.ExecuteReaderAsync(ct); await reader.ReadAsync(ct);
        var counters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(reader.GetString(0)) ?? [];
        return new AggregateTriggerCurrentRow(triggerDefinitionId, conflictKey, counters, reader.GetFieldValue<DateTimeOffset>(1));
    }

    public async Task<AggregateTriggerMaterializationResult> TryMaterializeAsync(AggregateTriggerDefinition definition, AggregateTriggerCurrentRow currentRow, string eventId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(MaterializeSql, conn);
        cmd.Parameters.AddWithValue("trigger_definition_id", definition.TriggerDefinitionId);
        cmd.Parameters.AddWithValue("conflict_key_hash", currentRow.ConflictKey);
        cmd.Parameters.AddWithValue("conflict_key", currentRow.ConflictKey);
        cmd.Parameters.AddWithValue("event_id", eventId);
        cmd.Parameters.AddWithValue("materialization_target_source", definition.MaterializationTargetBinding.TargetSource);
        cmd.Parameters.AddWithValue("materialization_target_id", definition.MaterializationTargetBinding.TargetId);
        cmd.Parameters.AddWithValue("payload_fingerprint", currentRow.ConflictKey);
        cmd.Parameters.AddWithValue("materialization_payload_map_json", System.Text.Json.JsonSerializer.Serialize(definition.MaterializationPayloadMap));
        var id = await cmd.ExecuteScalarAsync(ct);
        return id is Guid g ? new(true, g) : new(false, Guid.Empty);
    }
}
