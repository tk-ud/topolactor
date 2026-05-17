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
}
