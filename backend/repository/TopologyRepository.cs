using Npgsql;
using Microsoft.Extensions.Logging;

namespace Topolactor.Repository;

/// <summary>
/// Repository for loading stored topology data: structure maps, packages, schemas,
/// and runtime function parameters.
///
/// In-memory skeleton: the default dummy topology path (attractor key "default:entity:search")
/// returns seeded structure/package/schema records without a real DB connection.
/// Function parameters are not seeded in production repository code; missing policy
/// returns null so callers can surface explicit policy-missing errors.
/// </summary>
public class TopologyRepository
{
    // Deterministic IDs matching db/seed_empty.sql so the in-memory skeleton
    // and the DB seed reference the same topology node IDs.
    public static readonly Guid DefaultPackageId  = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultSchemaId   = new("00000000-0000-0000-0000-000000000002");
    public const string DefaultComponentId        = "00000000-0000-0000-0000-000000000003";
    public const string DefaultStructureMapId     = "00000000-0000-0000-0000-000000000004";
    public const string DefaultAttractorKey       = "default:entity:search";

    private static readonly StructureMapRecord DefaultStructureMap = new(
        StructureMapId: DefaultStructureMapId,
        AttractorKey:   DefaultAttractorKey,
        PackageId:      DefaultPackageId,
        SchemaId:       DefaultSchemaId,
        ComponentIds:   [DefaultComponentId]
    );

    private static readonly PackageRecord DefaultPackage = new(
        PackageId:     DefaultPackageId,
        PackageName:   "default_package",
        Version:       null,
        RawDefinition: "{}"
    );

    private static readonly SchemaRecord DefaultSchema = new(
        SchemaId:      DefaultSchemaId,
        SchemaName:    "default_schema",
        Version:       null,
        RawDefinition: """{"fields":[{"key":"label","type":"text","label":"Label"}]}"""
    );

    private readonly ILogger<TopologyRepository> _logger;
    private readonly string _connectionString;

    public TopologyRepository(ILogger<TopologyRepository> logger, string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Loads a structure map record by attractor key or structure map ID.
    /// Returns the default dummy record for the "default:entity:search" path.
    /// Returns null for all other keys (broken reference — caller must error).
    /// </summary>
    public Task<StructureMapRecord?> LoadStructureMapAsync(string key, CancellationToken ct = default)
    {
        if (key == DefaultAttractorKey || key == DefaultStructureMapId)
        {
            _logger.LogDebug("TopologyRepository.LoadStructureMapAsync: returning default in-memory record for key='{Key}'.", key);
            return Task.FromResult<StructureMapRecord?>(DefaultStructureMap);
        }

        _logger.LogDebug("TopologyRepository.LoadStructureMapAsync: no record found for key='{Key}'.", key);
        return Task.FromResult<StructureMapRecord?>(null);
    }

    /// <summary>
    /// Loads a package record by its ID.
    /// Returns the default dummy package for the default package ID.
    /// Returns null for all other IDs (broken reference — caller must error).
    /// </summary>
    public Task<PackageRecord?> LoadPackageAsync(Guid packageId, CancellationToken ct = default)
    {
        if (packageId == DefaultPackageId)
        {
            _logger.LogDebug("TopologyRepository.LoadPackageAsync: returning default in-memory record for packageId='{PackageId}'.", packageId);
            return Task.FromResult<PackageRecord?>(DefaultPackage);
        }

        _logger.LogDebug("TopologyRepository.LoadPackageAsync: no record found for packageId='{PackageId}'.", packageId);
        return Task.FromResult<PackageRecord?>(null);
    }

    /// <summary>
    /// Loads a schema record by its ID.
    /// Returns the default dummy schema for the default schema ID.
    /// Returns null for all other IDs (broken reference — caller must error).
    /// </summary>
    public Task<SchemaRecord?> LoadSchemaAsync(Guid schemaId, CancellationToken ct = default)
    {
        if (schemaId == DefaultSchemaId)
        {
            _logger.LogDebug("TopologyRepository.LoadSchemaAsync: returning default in-memory record for schemaId='{SchemaId}'.", schemaId);
            return Task.FromResult<SchemaRecord?>(DefaultSchema);
        }

        _logger.LogDebug("TopologyRepository.LoadSchemaAsync: no record found for schemaId='{SchemaId}'.", schemaId);
        return Task.FromResult<SchemaRecord?>(null);
    }

    /// <summary>
    /// Loads a function_parameters row by (function_name, parameter_key) and returns
    /// the parameter_value as a raw JSON string, or null if no active row is found.
    ///
    /// In-memory skeleton: returns null for all function parameters. Production
    /// policy values must come from stored topology data, not repository constants.
    ///
    /// Real DB implementation: SELECT parameter_value FROM function_parameters
    ///   WHERE function_name = @fn AND parameter_key = @key AND active = true
    ///   LIMIT 1
    /// Returns null when no active row exists — caller must treat null as policy-missing.
    /// </summary>
    public virtual async Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"""
            SELECT parameter_value::text
            FROM function_parameters
            WHERE function_name = @functionName
              AND parameter_key = @parameterKey
              AND active = true
            LIMIT 1;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("functionName", functionName);
        cmd.Parameters.AddWithValue("parameterKey", parameterKey);

        var scalar = await cmd.ExecuteScalarAsync(ct);
        if (scalar is null or DBNull)
        {
            _logger.LogDebug(
                "TopologyRepository.LoadFunctionParameterAsync: no active parameter found for '{FunctionName}/{ParameterKey}'.",
                functionName,
                parameterKey);
            return null;
        }

        return Convert.ToString(scalar);
    }
}

/// <summary>
/// Stored structure map data loaded from topology storage.
/// Maps an attractor key to its associated package, schema, and component definitions.
/// </summary>
public record StructureMapRecord(
    string StructureMapId,
    string AttractorKey,
    Guid PackageId,
    Guid SchemaId,
    IReadOnlyList<string> ComponentIds
);

/// <summary>
/// Stored package definition loaded from topology storage.
/// </summary>
public record PackageRecord(
    Guid PackageId,
    string PackageName,
    string? Version,
    string? RawDefinition
);

/// <summary>
/// Stored schema definition loaded from topology storage.
/// </summary>
public record SchemaRecord(
    Guid SchemaId,
    string SchemaName,
    string? Version,
    string? RawDefinition
);