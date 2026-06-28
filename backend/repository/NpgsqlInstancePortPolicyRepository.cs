using System.Text.Json;
using Npgsql;
using Topolactor.Runtime;

namespace Topolactor.Repository;

public sealed class NpgsqlInstancePortPolicyRepository : IInstancePortPolicyRepository
{
    private readonly string _connectionString;
    private readonly IExternalCredentialVaultRepository _credentialVaultRepository;

    public NpgsqlInstancePortPolicyRepository(string connectionString, IExternalCredentialVaultRepository credentialVaultRepository)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _credentialVaultRepository = credentialVaultRepository ?? throw new ArgumentNullException(nameof(credentialVaultRepository));
    }

    public async Task<InstancePortRecord?> ResolveInstancePortRecordAsync(string portKind, Guid instancePortId, CancellationToken ct = default)
    {
        var table = portKind switch { "db_instance_port" => "topology.db_instance_port", "runtime_instance_port" => "topology.runtime_instance_port", _ => throw new InvalidOperationException("INSTANCE_PORT_TARGET_REF_INVALID") };
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT instance_port_id, port_kind, instance_authority_key, provider_kind, required_by_bundle, reference_key, active FROM {table} WHERE instance_port_id = @id AND active = true";
        cmd.Parameters.AddWithValue("id", instancePortId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new InstancePortRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetBoolean(6));
    }

    public async Task<ExternalCredentialVaultRecord?> ResolveInstanceCredentialReferenceAsync(string referenceKey, CancellationToken ct = default)
        => await _credentialVaultRepository.LoadByReferenceKeyAsync(referenceKey, ct);

    public async Task<InstanceConnectionPolicy?> LoadConnectionPolicyAsync(string instanceAuthorityKey, string credentialReferenceKey, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT policy_id, instance_authority_key, credential_reference_key, connection_timeout_ms, statement_timeout_ms, max_result_bytes, allowed_schemas, allowed_function_names, result_sanitize_policy_key, active
            FROM topology.instance_connection_policy
            WHERE instance_authority_key = @key AND credential_reference_key = @ref AND active = true
            """;
        cmd.Parameters.AddWithValue("key", instanceAuthorityKey);
        cmd.Parameters.AddWithValue("ref", credentialReferenceKey);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new InstanceConnectionPolicy(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetFieldValue<string[]>(6).ToHashSet(StringComparer.Ordinal), r.GetFieldValue<string[]>(7).ToHashSet(StringComparer.Ordinal), r.GetString(8), r.GetBoolean(9));
    }

    public async Task<InstanceOperationAuthorityBinding?> LoadOperationAuthorityBindingAsync(string instanceAuthorityKey, string operationBindingKey, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT binding_id, operation_binding_key, instance_authority_key, function_key, function_schema, function_name, abstract_function_key, output_shape, secret_deny, active
            FROM topology.instance_operation_authority_binding
            WHERE instance_authority_key = @key AND operation_binding_key = @op AND active = true
            """;
        cmd.Parameters.AddWithValue("key", instanceAuthorityKey);
        cmd.Parameters.AddWithValue("op", operationBindingKey);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new InstanceOperationAuthorityBinding(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), ParseJson(r.GetFieldValue<string>(7)), r.GetBoolean(8), r.GetBoolean(9));
    }

    private static IReadOnlyDictionary<string, string> ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? string.Empty : p.Value.GetRawText(), StringComparer.Ordinal);
    }
}
