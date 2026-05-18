using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql implementation of TopologyRepository.
/// Replaces in-memory skeleton reads with real SQL queries against the topology store.
///
/// Canonical tables:
///   structure_maps, package_registry, schema_registry, function_parameters
///
/// Wiring: inject NpgsqlTopologyRepository wherever TopologyRepository is required in
/// production DI registration. Tests continue to use the in-memory base class directly.
/// </summary>
public class NpgsqlTopologyRepository : TopologyRepository
{
    private readonly ILogger<NpgsqlTopologyRepository> _npgsqlLogger;

    public NpgsqlTopologyRepository(
        ILogger<NpgsqlTopologyRepository> logger,
        string connectionString)
        : base(NullLogger<TopologyRepository>.Instance, connectionString)
    {
        _npgsqlLogger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads a structure map by attractor_key or structure_map_id (UUID string).
    /// Returns null when not found — caller must treat as broken reference.
    /// SQL: SELECT ... FROM structure_maps WHERE (attractor_key = @key OR structure_map_id::text = @key) AND active = true LIMIT 1
    /// </summary>
    public override async Task<StructureMapRecord?> LoadStructureMapAsync(
        string key, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT structure_map_id::text, attractor_key, package_id, schema_id, " +
            "       component_ids, state_policy::text " +
            "FROM structure_maps " +
            "WHERE (attractor_key = @key OR structure_map_id::text = @key) " +
            "  AND active = true " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("key", key);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlTopologyRepository.LoadStructureMapAsync: no record for key='{Key}'.", key);
            return null;
        }

        var componentIdsRaw = reader.GetValue(4) as Guid[];
        var componentIds = componentIdsRaw?.Select(g => g.ToString()).ToList()
            ?? new List<string>();

        return new StructureMapRecord(
            StructureMapId: reader.GetString(0),
            AttractorKey:   reader.GetString(1),
            PackageId:      reader.GetGuid(2),
            SchemaId:       reader.GetGuid(3),
            ComponentIds:   componentIds,
            StatePolicyJson: reader.IsDBNull(5) ? null : reader.GetString(5)
        );
    }

    /// <summary>
    /// Loads a package record from package_registry by package_id.
    /// Returns null when not found — caller must treat as broken reference.
    /// </summary>
    public override async Task<PackageRecord?> LoadPackageAsync(
        Guid packageId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT package_id, name, package_def::text " +
            "FROM package_registry " +
            "WHERE package_id = @id AND active = true " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("id", packageId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlTopologyRepository.LoadPackageAsync: no record for packageId='{Id}'.", packageId);
            return null;
        }

        return new PackageRecord(
            PackageId:     reader.GetGuid(0),
            PackageName:   reader.GetString(1),
            Version:       null,
            RawDefinition: reader.IsDBNull(2) ? null : reader.GetString(2)
        );
    }

    /// <summary>
    /// Loads a schema record from schema_registry by schema_id.
    /// Returns null when not found — caller must treat as broken reference.
    /// </summary>
    public override async Task<SchemaRecord?> LoadSchemaAsync(
        Guid schemaId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT schema_id, name, schema_def::text " +
            "FROM schema_registry " +
            "WHERE schema_id = @id AND active = true " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("id", schemaId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlTopologyRepository.LoadSchemaAsync: no record for schemaId='{Id}'.", schemaId);
            return null;
        }

        return new SchemaRecord(
            SchemaId:      reader.GetGuid(0),
            SchemaName:    reader.GetString(1),
            Version:       null,
            RawDefinition: reader.IsDBNull(2) ? null : reader.GetString(2)
        );
    }

    /// <summary>
    /// Loads a function_parameter value from the topology store.
    /// Returns null when no active row is found — caller must treat as policy-missing.
    /// SQL: SELECT parameter_value FROM function_parameters
    ///   WHERE function_name = @fn AND parameter_key = @key AND active = true LIMIT 1
    /// </summary>
    public override async Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT parameter_value::text FROM function_parameters " +
            "WHERE function_name = @fn AND parameter_key = @key AND active = true " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("fn", functionName);
        cmd.Parameters.AddWithValue("key", parameterKey);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
        {
            _npgsqlLogger.LogDebug(
                "NpgsqlTopologyRepository.LoadFunctionParameterAsync: no parameter for '{Fn}/{Key}'.",
                functionName, parameterKey);
            return null;
        }

        return (string)result;
    }

    public override async Task<IReadOnlyList<DemoEntityProjection>> LoadDemoEntityListAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT entity_id, COALESCE(entity_jsonb->>'label','Untitled'), COALESCE(entity_jsonb->>'state','unknown') FROM entities WHERE hub_id='00000000-0000-0000-0000-000000000010' ORDER BY created_at";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<DemoEntityProjection>();
        while (await reader.ReadAsync(ct)) list.Add(new DemoEntityProjection(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        return list;
    }

    public override async Task<DemoEntityProjection?> LoadDemoEntityDetailAsync(Guid entityId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT entity_id, COALESCE(entity_jsonb->>'label','Untitled'), COALESCE(entity_jsonb->>'state','unknown') FROM entities WHERE entity_id=@id LIMIT 1";
        cmd.Parameters.AddWithValue("id", entityId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new DemoEntityProjection(r.GetGuid(0), r.GetString(1), r.GetString(2));
    }

    public override async Task<DemoTransitionResult> ApplyDemoTransitionAsync(Guid entityId, string action, string? title, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            if (action == "create")
            {
                var activeStateIdCmd = conn.CreateCommand(); activeStateIdCmd.Transaction = tx;
                activeStateIdCmd.CommandText = "SELECT state_id FROM state_registry WHERE name='active' LIMIT 1";
                var activeStateIdObj = await activeStateIdCmd.ExecuteScalarAsync(ct);
                if (activeStateIdObj is null)
                {
                    await tx.RollbackAsync(ct);
                    return new(false, "STATE_POLICY_NOT_FOUND", "state_registry.active is missing");
                }
                var cmd = conn.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO entities(entity_id,hub_id,entity_jsonb,relation_ids,state_id) VALUES(@id,'00000000-0000-0000-0000-000000000010',jsonb_build_object('label',@title,'state','active','hub_id','00000000-0000-0000-0000-000000000010'),ARRAY['00000000-0000-0000-0000-000000000011']::uuid[],(SELECT state_id FROM state_registry WHERE name='active' LIMIT 1))";
                cmd.Parameters.AddWithValue("id", entityId); cmd.Parameters.AddWithValue("title", title ?? "Untitled");
                await cmd.ExecuteNonQueryAsync(ct);
                var hist = conn.CreateCommand(); hist.Transaction = tx;
                hist.CommandText = "INSERT INTO demo_state_transitions(entity_id,action,before_state,after_state,diff_json,event_json) VALUES(@id,'create',NULL,'active',jsonb_build_object('created',true,'title',@title,'state',jsonb_build_object('before',NULL,'after','active')),jsonb_build_object('action','create','entity_id',@id::text,'title',@title,'after_state','active'))";
                hist.Parameters.AddWithValue("id", entityId);
                hist.Parameters.AddWithValue("title", title ?? "Untitled");
                await hist.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct); return new(true, null, null);
            }
            var read = conn.CreateCommand(); read.Transaction = tx;
            read.CommandText = "SELECT COALESCE(entity_jsonb->>'state','unknown') FROM entities WHERE entity_id=@id FOR UPDATE";
            read.Parameters.AddWithValue("id", entityId);
            var current = (string?)await read.ExecuteScalarAsync(ct);
            if (current is null) { await tx.RollbackAsync(ct); return new(false, "NOT_FOUND", "entity not found"); }
            var next = action == "advance" && current == "active" ? "operating" : action == "advance" && current == "operating" ? "archived" : null;
            if (next is null) { await tx.RollbackAsync(ct); return new(false, "INVALID_TRANSITION", "invalid transition"); }
            var stateIdCmd = conn.CreateCommand(); stateIdCmd.Transaction = tx;
            stateIdCmd.CommandText = "SELECT state_id FROM state_registry WHERE name=@name LIMIT 1";
            stateIdCmd.Parameters.AddWithValue("name", next);
            var nextStateIdObj = await stateIdCmd.ExecuteScalarAsync(ct);
            if (nextStateIdObj is null) { await tx.RollbackAsync(ct); return new(false, "STATE_POLICY_NOT_FOUND", $"state_registry.{next} is missing"); }
            var up = conn.CreateCommand(); up.Transaction = tx;
            up.CommandText = "UPDATE entities SET entity_jsonb=jsonb_set(entity_jsonb,'{state}',to_jsonb(@next::text),true), state_id=@stateId, updated_at=now() WHERE entity_id=@id";
            up.Parameters.AddWithValue("id", entityId); up.Parameters.AddWithValue("next", next);
            up.Parameters.AddWithValue("stateId", (Guid)nextStateIdObj);
            await up.ExecuteNonQueryAsync(ct);
            var hist = conn.CreateCommand(); hist.Transaction = tx;
            hist.CommandText = "INSERT INTO demo_state_transitions(entity_id,action,before_state,after_state,diff_json,event_json) VALUES(@id,@action,@before,@after,jsonb_build_object('state',jsonb_build_object('before',@before,'after',@after),'state_id',jsonb_build_object('after',@stateId::text)),jsonb_build_object('action',@action,'entity_id',@id::text,'before_state',@before,'after_state',@after))";
            hist.Parameters.AddWithValue("id", entityId); hist.Parameters.AddWithValue("action", action); hist.Parameters.AddWithValue("before", current); hist.Parameters.AddWithValue("after", next);
            hist.Parameters.AddWithValue("stateId", (Guid)nextStateIdObj);
            await hist.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct); return new(true, null, null);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505") { await tx.RollbackAsync(ct); return new(false, "PERSISTENCE_CONFLICT", ex.MessageText); }
    }

    public override async Task<IReadOnlyList<object>> LoadDemoTransitionHistoryAsync(Guid entityId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT action,before_state,after_state,diff_json::text,event_json::text,created_at FROM demo_state_transitions WHERE entity_id=@id ORDER BY created_at DESC LIMIT 20";
        cmd.Parameters.AddWithValue("id", entityId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<object>();
        while (await r.ReadAsync(ct))
        {
            rows.Add(new
            {
                action = r.GetString(0),
                beforeState = r.IsDBNull(1) ? null : r.GetString(1),
                afterState = r.IsDBNull(2) ? null : r.GetString(2),
                diff = r.GetString(3),
                @event = r.GetString(4),
                createdAt = r.GetDateTime(5)
            });
        }
        return rows;
    }
}
