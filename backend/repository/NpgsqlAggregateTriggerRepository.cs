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
    public const string SaveDefinitionSql = """
        INSERT INTO runtime_orchestration.aggregate_trigger_definitions (
            trigger_definition_id, processing_function_id, operation_definition_id, accepted_event_schema_ref, allowed_source_kinds_json,
            materialization_policy_ref, trigger_source_kind, trigger_source_detail_kind,
            execution_scope, transaction_boundary, aggregate_target_binding_json, conflict_key_fields_json, delta_map_json,
            threshold_policy_json, materialization_target_binding_json, materialization_payload_map_json, approval_policy, active, updated_at)
        VALUES (
            @trigger_definition_id, @processing_function_id, @operation_definition_id, @accepted_event_schema_ref, @allowed_source_kinds_json::jsonb,
            @materialization_policy_ref, @trigger_source_kind, @trigger_source_detail_kind,
            @execution_scope, @transaction_boundary, @aggregate_target_binding_json::jsonb, @conflict_key_fields_json::jsonb, @delta_map_json::jsonb,
            @threshold_policy_json::jsonb, @materialization_target_binding_json::jsonb, @materialization_payload_map_json::jsonb, @approval_policy, TRUE, now())
        ON CONFLICT (trigger_definition_id) DO UPDATE SET
            processing_function_id = EXCLUDED.processing_function_id,
            operation_definition_id = EXCLUDED.operation_definition_id,
            accepted_event_schema_ref = EXCLUDED.accepted_event_schema_ref,
            allowed_source_kinds_json = EXCLUDED.allowed_source_kinds_json,
            materialization_policy_ref = EXCLUDED.materialization_policy_ref,
            trigger_source_kind = EXCLUDED.trigger_source_kind,
            trigger_source_detail_kind = EXCLUDED.trigger_source_detail_kind,
            execution_scope = EXCLUDED.execution_scope,
            transaction_boundary = EXCLUDED.transaction_boundary,
            aggregate_target_binding_json = EXCLUDED.aggregate_target_binding_json,
            conflict_key_fields_json = EXCLUDED.conflict_key_fields_json,
            delta_map_json = EXCLUDED.delta_map_json,
            threshold_policy_json = EXCLUDED.threshold_policy_json,
            materialization_target_binding_json = EXCLUDED.materialization_target_binding_json,
            materialization_payload_map_json = EXCLUDED.materialization_payload_map_json,
            approval_policy = EXCLUDED.approval_policy,
            active = TRUE,
            updated_at = now();
        """;
    public const string LookupExistingMaterializationSql = """
        SELECT materialization_id FROM runtime_orchestration.aggregate_trigger_materialization_evidence
        WHERE trigger_definition_id = @trigger_definition_id
          AND materialization_target_source = @materialization_target_source
          AND materialization_target_id = @materialization_target_id
          AND conflict_key_hash = @conflict_key_hash
          AND payload_fingerprint = @payload_fingerprint;
        """;
    public const string LoadDefinitionSql = """
        SELECT trigger_definition_id, processing_function_id, operation_definition_id, accepted_event_schema_ref, allowed_source_kinds_json,
               materialization_policy_ref, trigger_source_kind, trigger_source_detail_kind,
               execution_scope, transaction_boundary, aggregate_target_binding_json, conflict_key_fields_json, delta_map_json,
               threshold_policy_json, materialization_target_binding_json, materialization_payload_map_json, approval_policy
        FROM runtime_orchestration.aggregate_trigger_definitions
        WHERE trigger_definition_id = @trigger_definition_id AND active;
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
        return await UpsertCurrentAsync(conn, null, triggerDefinitionId, conflictKey, deltaMap, ct);
    }

    public async Task<AggregateTriggerMaterializationResult> TryMaterializeAsync(AggregateTriggerDefinition definition, AggregateTriggerCurrentRow currentRow, string eventId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        return await MaterializeAsync(conn, null, definition, currentRow.ConflictKey, eventId, ct);
    }

    public async Task<AggregateTriggerUpsertAndMaterializeResult> AtomicUpsertAndMaterializeAsync(
        AggregateTriggerDefinition definition,
        string conflictKey,
        string eventId,
        Func<AggregateTriggerCurrentRow, AggregateTriggerMaterializationDecision> decide,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var current = await UpsertCurrentAsync(conn, tx, definition.TriggerDefinitionId, conflictKey, definition.DeltaMap, ct);
        var decision = decide(current);
        if (decision != AggregateTriggerMaterializationDecision.Materialize)
        {
            await tx.CommitAsync(ct);
            return new AggregateTriggerUpsertAndMaterializeResult(current, decision, null);
        }

        // Materialization evidence append happens inside the same transaction as the aggregate
        // upsert above (aggregate_trigger_substrate_contract.transaction_boundary.atomicity_rule).
        var materialization = await MaterializeAsync(conn, tx, definition, conflictKey, eventId, ct);
        await tx.CommitAsync(ct);
        return new AggregateTriggerUpsertAndMaterializeResult(current, decision, materialization);
    }

    public async Task SaveDefinitionAsync(AggregateTriggerDefinition definition, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(SaveDefinitionSql, conn);
        cmd.Parameters.AddWithValue("trigger_definition_id", definition.TriggerDefinitionId);
        cmd.Parameters.AddWithValue("processing_function_id", definition.ProcessingFunctionScope.FunctionId);
        cmd.Parameters.AddWithValue("operation_definition_id", definition.ProcessingFunctionScope.OperationDefinitionId);
        cmd.Parameters.AddWithValue("accepted_event_schema_ref", definition.ProcessingFunctionScope.AcceptedEventSchemaRef);
        cmd.Parameters.AddWithValue("allowed_source_kinds_json", System.Text.Json.JsonSerializer.Serialize(definition.ProcessingFunctionScope.AllowedSourceKinds));
        cmd.Parameters.AddWithValue("materialization_policy_ref", definition.ProcessingFunctionScope.MaterializationPolicyRef);
        cmd.Parameters.AddWithValue("trigger_source_kind", definition.TriggerSource.CanonicalTriggerKind);
        cmd.Parameters.AddWithValue("trigger_source_detail_kind", definition.TriggerSource.TriggerSourceDetailKind);
        cmd.Parameters.AddWithValue("execution_scope", definition.ExecutionScope);
        cmd.Parameters.AddWithValue("transaction_boundary", definition.TransactionBoundary);
        cmd.Parameters.AddWithValue("aggregate_target_binding_json", System.Text.Json.JsonSerializer.Serialize(definition.AggregateTargetBinding));
        cmd.Parameters.AddWithValue("conflict_key_fields_json", System.Text.Json.JsonSerializer.Serialize(definition.ConflictKeyFields));
        cmd.Parameters.AddWithValue("delta_map_json", System.Text.Json.JsonSerializer.Serialize(definition.DeltaMap));
        cmd.Parameters.AddWithValue("threshold_policy_json", System.Text.Json.JsonSerializer.Serialize(definition.ThresholdPolicy));
        cmd.Parameters.AddWithValue("materialization_target_binding_json", System.Text.Json.JsonSerializer.Serialize(definition.MaterializationTargetBinding));
        cmd.Parameters.AddWithValue("materialization_payload_map_json", System.Text.Json.JsonSerializer.Serialize(definition.MaterializationPayloadMap));
        cmd.Parameters.AddWithValue("approval_policy", definition.ApprovalPolicy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<AggregateTriggerDefinition?> LoadDefinitionAsync(Guid triggerDefinitionId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(LoadDefinitionSql, conn);
        cmd.Parameters.AddWithValue("trigger_definition_id", triggerDefinitionId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var triggerSourceKind = reader.GetString(6);
        var triggerSourceDetailKind = reader.GetString(7);
        return new AggregateTriggerDefinition(
            reader.GetGuid(0),
            new AggregateTriggerSource(triggerSourceKind, triggerSourceDetailKind),
            new AggregateTriggerProcessingFunctionScope(
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                System.Text.Json.JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(4), jsonOptions)!,
                reader.GetString(5)),
            reader.GetString(8),
            reader.GetString(9),
            System.Text.Json.JsonSerializer.Deserialize<AggregateTriggerTargetBinding>(reader.GetString(10), jsonOptions)!,
            System.Text.Json.JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(11), jsonOptions)!,
            System.Text.Json.JsonSerializer.Deserialize<IReadOnlyDictionary<string, decimal>>(reader.GetString(12), jsonOptions)!,
            System.Text.Json.JsonSerializer.Deserialize<AggregateTriggerThresholdPolicy>(reader.GetString(13), jsonOptions)!,
            System.Text.Json.JsonSerializer.Deserialize<AggregateTriggerTargetBinding>(reader.GetString(14), jsonOptions)!,
            System.Text.Json.JsonSerializer.Deserialize<IReadOnlyList<AggregateTriggerPayloadMapEntry>>(reader.GetString(15), jsonOptions)!,
            reader.GetString(16));
    }

    private static async Task<AggregateTriggerCurrentRow> UpsertCurrentAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, Guid triggerDefinitionId, string conflictKey, IReadOnlyDictionary<string, decimal> deltaMap, CancellationToken ct)
    {
        await using var cmd = tx is null ? new NpgsqlCommand(UpsertSql, conn) : new NpgsqlCommand(UpsertSql, conn, tx);
        cmd.Parameters.AddWithValue("trigger_definition_id", triggerDefinitionId);
        cmd.Parameters.AddWithValue("conflict_key_hash", conflictKey);
        cmd.Parameters.AddWithValue("conflict_key", conflictKey);
        cmd.Parameters.AddWithValue("delta", System.Text.Json.JsonSerializer.Serialize(deltaMap));
        await using var reader = await cmd.ExecuteReaderAsync(ct); await reader.ReadAsync(ct);
        var counters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(reader.GetString(0)) ?? [];
        return new AggregateTriggerCurrentRow(triggerDefinitionId, conflictKey, counters, reader.GetFieldValue<DateTimeOffset>(1));
    }

    private static async Task<AggregateTriggerMaterializationResult> MaterializeAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, AggregateTriggerDefinition definition, string conflictKey, string eventId, CancellationToken ct)
    {
        await using var cmd = tx is null ? new NpgsqlCommand(MaterializeSql, conn) : new NpgsqlCommand(MaterializeSql, conn, tx);
        cmd.Parameters.AddWithValue("trigger_definition_id", definition.TriggerDefinitionId);
        cmd.Parameters.AddWithValue("conflict_key_hash", conflictKey);
        cmd.Parameters.AddWithValue("conflict_key", conflictKey);
        cmd.Parameters.AddWithValue("event_id", eventId);
        cmd.Parameters.AddWithValue("materialization_target_source", definition.MaterializationTargetBinding.TargetSource);
        cmd.Parameters.AddWithValue("materialization_target_id", definition.MaterializationTargetBinding.TargetId);
        cmd.Parameters.AddWithValue("payload_fingerprint", conflictKey);
        cmd.Parameters.AddWithValue("materialization_payload_map_json", System.Text.Json.JsonSerializer.Serialize(definition.MaterializationPayloadMap));
        var id = await cmd.ExecuteScalarAsync(ct);
        if (id is Guid insertedId) return new(true, insertedId);

        // ON CONFLICT DO NOTHING does not RETURNING the pre-existing row; look it up explicitly
        // (same connection/transaction) so callers get the stable materialization_id for the
        // already-materialized duplicate guard outcome.
        await using var lookupCmd = tx is null ? new NpgsqlCommand(LookupExistingMaterializationSql, conn) : new NpgsqlCommand(LookupExistingMaterializationSql, conn, tx);
        lookupCmd.Parameters.AddWithValue("trigger_definition_id", definition.TriggerDefinitionId);
        lookupCmd.Parameters.AddWithValue("materialization_target_source", definition.MaterializationTargetBinding.TargetSource);
        lookupCmd.Parameters.AddWithValue("materialization_target_id", definition.MaterializationTargetBinding.TargetId);
        lookupCmd.Parameters.AddWithValue("conflict_key_hash", conflictKey);
        lookupCmd.Parameters.AddWithValue("payload_fingerprint", conflictKey);
        var existingId = await lookupCmd.ExecuteScalarAsync(ct);
        return new(false, existingId is Guid g ? g : Guid.Empty);
    }
}
