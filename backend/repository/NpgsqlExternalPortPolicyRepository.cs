using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using Topolactor.Runtime;

namespace Topolactor.Repository;

/// <summary>
/// Production Npgsql implementation of the generic external port policy read substrate.
/// Reads only active seed/data-defined port records and policy metadata; plaintext credential
/// material is intentionally outside this projection.
/// </summary>
public sealed class NpgsqlExternalPortPolicyRepository : IExternalPortPolicyRepository
{
    private readonly ILogger<NpgsqlExternalPortPolicyRepository> _logger;
    private readonly string _connectionString;

    public NpgsqlExternalPortPolicyRepository(
        ILogger<NpgsqlExternalPortPolicyRepository> logger,
        string connectionString)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<ExternalPortRecord?> LoadPortRecordAsync(
        string requiredByBundle,
        string portKind,
        string? routeKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requiredByBundle))
            throw new ArgumentException("requiredByBundle is required.", nameof(requiredByBundle));

        var shape = GetPortTableShape(portKind);
        if (shape.PortKind == "hook_port" && string.IsNullOrWhiteSpace(routeKey))
            throw new InvalidOperationException("EXTERNAL_HOOK_PORT_ROUTE_KEY_REQUIRED");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildPortRecordSql(shape, routeKey);
        cmd.Parameters.AddWithValue("requiredByBundle", requiredByBundle);
        if (shape.PortKind == "hook_port")
            cmd.Parameters.AddWithValue("routeKey", routeKey!);

        var records = new List<ExternalPortRecord>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                records.Add(MapPortRecord(reader, shape.PortKind));
            }
        }

        if (records.Count == 0)
            return null;

        if (records.Count > 1)
            throw new InvalidOperationException("EXTERNAL_PORT_RECORD_AMBIGUOUS");

        return records[0];
    }


    public async Task<ExternalPortRecord?> LoadPortRecordByIdAsync(
        string portKind,
        Guid portId,
        string? routeKey,
        CancellationToken ct = default)
    {
        var shape = GetPortTableShape(portKind);
        if (shape.PortKind == "hook_port" && string.IsNullOrWhiteSpace(routeKey))
            throw new InvalidOperationException("EXTERNAL_HOOK_PORT_ROUTE_KEY_REQUIRED");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        var routePredicate = shape.PortKind == "hook_port" ? " AND route_key = @routeKey" : string.Empty;
        cmd.CommandText = $"""
            SELECT {shape.IdColumn} AS port_id,
                   required_by_bundle,
                   provider_kind,
                   {shape.UrlOrEnvReferenceSelect},
                   {shape.HookPathSelect},
                   {shape.HeaderKeySelect},
                   {shape.RouteKeySelect},
                   credential_kind,
                   reference_key,
                   active
            FROM {shape.TableName}
            WHERE active = true
              AND {shape.IdColumn} = @portId{routePredicate}
            """;
        cmd.Parameters.AddWithValue("portId", portId);
        if (shape.PortKind == "hook_port")
            cmd.Parameters.AddWithValue("routeKey", routeKey!);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var record = MapPortRecord(reader, shape.PortKind);
        if (await reader.ReadAsync(ct)) throw new InvalidOperationException("EXTERNAL_PORT_RECORD_AMBIGUOUS");
        return record;
    }

    public async Task<ExternalPortRecord?> LoadPortRecordByCanonicalBindingAsync(
        string manifestKey,
        string tableRef,
        string portKind,
        Guid portId,
        string? routeKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(manifestKey))
            throw new ArgumentException("manifestKey is required.", nameof(manifestKey));
        if (string.IsNullOrWhiteSpace(tableRef))
            throw new ArgumentException("tableRef is required.", nameof(tableRef));

        // Fail-close: tableRef must correspond exactly to portKind's canonical physical table.
        var expectedTableRef = portKind switch
        {
            "access_port"   => "topology.external_access_ports",
            "response_port" => "topology.external_response_ports",
            "hook_port"     => "topology.external_hook_ports",
            _ => throw new InvalidOperationException("EXTERNAL_PORT_KIND_UNSUPPORTED")
        };
        if (!string.Equals(tableRef, expectedTableRef, StringComparison.Ordinal))
            throw new InvalidOperationException("EXTERNAL_PORT_CANONICAL_BINDING_TABLE_REF_PORT_KIND_MISMATCH");

        if (portKind == "hook_port" && string.IsNullOrWhiteSpace(routeKey))
            throw new InvalidOperationException("EXTERNAL_HOOK_PORT_ROUTE_KEY_REQUIRED");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var bindingCmd = conn.CreateCommand();
        bindingCmd.CommandText = """
            SELECT 1
            FROM hubs.topology_manifests tm
            JOIN topology.physical_table_manifest_bindings pmb
              ON pmb.topology_manifest_id = tm.topology_manifest_id
             AND pmb.active = true
            JOIN topology.physical_tables pt
              ON pt.physical_table_id = pmb.physical_table_id
             AND pt.active = true
             AND pt.table_ref = @tableRef
            WHERE tm.status = 'active'
              AND tm.manifest_key = @manifestKey
            LIMIT 1
            """;
        bindingCmd.Parameters.AddWithValue("manifestKey", manifestKey);
        bindingCmd.Parameters.AddWithValue("tableRef", tableRef);

        if (await bindingCmd.ExecuteScalarAsync(ct) is null)
            throw new InvalidOperationException("EXTERNAL_PORT_CANONICAL_BINDING_NOT_FOUND");

        return await LoadPortRecordByIdAsync(portKind, portId, routeKey, ct);
    }

    public async Task<ExternalPortPolicy?> LoadPolicyAsync(ExternalPortRecord portRecord, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(portRecord);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var policies = new List<(Guid PolicyId, string PolicyKey, string PortKind, string RequiredByBundle, bool Active)>();
        await using (var policyCmd = conn.CreateCommand())
        {
            policyCmd.CommandText = """
                SELECT policy_id, policy_key, port_kind, required_by_bundle, active
                FROM topology.external_port_policies
                WHERE active = true
                  AND port_kind = @portKind
                  AND required_by_bundle = @requiredByBundle
                ORDER BY policy_key
                """;
            policyCmd.Parameters.AddWithValue("portKind", portRecord.PortKind);
            policyCmd.Parameters.AddWithValue("requiredByBundle", portRecord.RequiredByBundle);

            await using var reader = await policyCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                policies.Add((
                    reader.GetGuid(reader.GetOrdinal("policy_id")),
                    reader.GetString(reader.GetOrdinal("policy_key")),
                    reader.GetString(reader.GetOrdinal("port_kind")),
                    reader.GetString(reader.GetOrdinal("required_by_bundle")),
                    reader.GetBoolean(reader.GetOrdinal("active"))));
            }
        }

        if (policies.Count == 0)
            return null;

        if (policies.Count > 1)
            throw new InvalidOperationException("EXTERNAL_PORT_POLICY_AMBIGUOUS");

        var policy = policies[0];
        var steps = new List<ExternalPortPolicyStep>();
        await using (var stepCmd = conn.CreateCommand())
        {
            stepCmd.CommandText = """
                SELECT policy_step_id, policy_id, step_order, operation_key, CASE WHEN abstract_function_key IS NULL THEN step_config ELSE jsonb_set(step_config, '{abstract_function_key}', to_jsonb(abstract_function_key), true) END AS step_config, active
                FROM topology.external_port_policy_steps
                WHERE active = true
                  AND policy_id = @policyId
                ORDER BY step_order ASC
                """;
            stepCmd.Parameters.AddWithValue("policyId", policy.PolicyId);

            await using var reader = await stepCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var operationKey = reader.GetString(reader.GetOrdinal("operation_key"));
                if (string.IsNullOrWhiteSpace(operationKey))
                    throw new InvalidOperationException("EXTERNAL_PORT_POLICY_STEP_OPERATION_KEY_INVALID");

                steps.Add(new ExternalPortPolicyStep(
                    reader.GetGuid(reader.GetOrdinal("policy_step_id")),
                    reader.GetGuid(reader.GetOrdinal("policy_id")),
                    reader.GetInt32(reader.GetOrdinal("step_order")),
                    operationKey,
                    ParseStepConfig(reader.GetString(reader.GetOrdinal("step_config"))),
                    reader.GetBoolean(reader.GetOrdinal("active"))));
            }
        }

        return new ExternalPortPolicy(
            policy.PolicyId,
            policy.PolicyKey,
            policy.PortKind,
            policy.RequiredByBundle,
            steps,
            policy.Active);
    }

    public static IReadOnlyDictionary<string, string> ParseStepConfig(string stepConfigJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(stepConfigJson) ? "{}" : stepConfigJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("EXTERNAL_PORT_POLICY_STEP_CONFIG_INVALID");

        var config = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            config[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText();
        }

        return config;
    }

    private static PortTableShape GetPortTableShape(string portKind) => portKind switch
    {
        "access_port" => new PortTableShape(
            "access_port",
            "topology.external_access_ports",
            "access_port_id",
            "url_or_env_reference",
            "NULL::text AS hook_path",
            "NULL::text AS header_key",
            "NULL::text AS route_key"),
        "response_port" => new PortTableShape(
            "response_port",
            "topology.external_response_ports",
            "response_port_id",
            "url_or_env_reference",
            "NULL::text AS hook_path",
            "NULL::text AS header_key",
            "NULL::text AS route_key"),
        "hook_port" => new PortTableShape(
            "hook_port",
            "topology.external_hook_ports",
            "hook_port_id",
            "NULL::text AS url_or_env_reference",
            "hook_path",
            "header_key",
            "route_key"),
        _ => throw new InvalidOperationException("EXTERNAL_PORT_KIND_UNSUPPORTED")
    };

    private static string BuildPortRecordSql(PortTableShape shape, string? routeKey)
    {
        var routePredicate = shape.PortKind == "hook_port"
            ? " AND route_key = @routeKey"
            : string.Empty;

        return $"""
            SELECT {shape.IdColumn} AS port_id,
                   required_by_bundle,
                   provider_kind,
                   {shape.UrlOrEnvReferenceSelect},
                   {shape.HookPathSelect},
                   {shape.HeaderKeySelect},
                   {shape.RouteKeySelect},
                   credential_kind,
                   reference_key,
                   active
            FROM {shape.TableName}
            WHERE active = true
              AND required_by_bundle = @requiredByBundle{routePredicate}
            ORDER BY {shape.IdColumn}
            """;
    }

    private static ExternalPortRecord MapPortRecord(NpgsqlDataReader reader, string portKind) => new(
        reader.GetGuid(reader.GetOrdinal("port_id")),
        portKind,
        reader.GetString(reader.GetOrdinal("required_by_bundle")),
        reader.GetString(reader.GetOrdinal("provider_kind")),
        GetNullableString(reader, "url_or_env_reference"),
        GetNullableString(reader, "hook_path"),
        GetNullableString(reader, "header_key"),
        GetNullableString(reader, "route_key"),
        reader.GetString(reader.GetOrdinal("credential_kind")),
        GetNullableString(reader, "reference_key"),
        reader.GetBoolean(reader.GetOrdinal("active")));

    private static string? GetNullableString(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private sealed record PortTableShape(
        string PortKind,
        string TableName,
        string IdColumn,
        string UrlOrEnvReferenceSelect,
        string HookPathSelect,
        string HeaderKeySelect,
        string RouteKeySelect);
}
