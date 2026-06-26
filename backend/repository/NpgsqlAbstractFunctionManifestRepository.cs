using System.Text.Json;
using Npgsql;
using Topolactor.Runtime;

namespace Topolactor.Repository;

public sealed class NpgsqlAbstractFunctionManifestRepository : IAbstractFunctionManifestRepository
{
    private readonly string _connectionString;

    public NpgsqlAbstractFunctionManifestRepository(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        Guid manifestId;
        string runtimeLane;
        string authorityScope;
        bool active;
        IReadOnlyList<string> deniedProjectionKeys;
        IReadOnlyDictionary<string, string> outputShape;

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT abstract_function_id, runtime_lane, authority_scope, projection_deny_keys, output_shape, active
                FROM topology.abstract_function_manifests
                WHERE function_key = @functionKey
                """;
            cmd.Parameters.AddWithValue("functionKey", functionKey);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            manifestId = reader.GetGuid(reader.GetOrdinal("abstract_function_id"));
            runtimeLane = reader.GetString(reader.GetOrdinal("runtime_lane"));
            authorityScope = reader.GetString(reader.GetOrdinal("authority_scope"));
            deniedProjectionKeys = reader.GetFieldValue<string[]>(reader.GetOrdinal("projection_deny_keys"));
            outputShape = ParseJsonObject(reader.GetString(reader.GetOrdinal("output_shape")));
            active = reader.GetBoolean(reader.GetOrdinal("active"));
        }

        var authorityBindings = await LoadAuthorityBindingsAsync(conn, manifestId, ct);
        var bindingsByStep = await LoadBindingsAsync(conn, manifestId, ct);
        var steps = new List<AbstractFunctionStep>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT abstract_function_step_id, step_order, primitive_key, step_config, result_context_key, active, is_compensation_step, external_context_key
                FROM topology.abstract_function_steps
                WHERE abstract_function_id = @manifestId
                ORDER BY step_order ASC
                """;
            cmd.Parameters.AddWithValue("manifestId", manifestId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var stepId = reader.GetGuid(reader.GetOrdinal("abstract_function_step_id"));
                steps.Add(new AbstractFunctionStep(
                    stepId,
                    reader.GetInt32(reader.GetOrdinal("step_order")),
                    reader.GetString(reader.GetOrdinal("primitive_key")),
                    ParseJsonObject(reader.GetString(reader.GetOrdinal("step_config"))),
                    bindingsByStep.TryGetValue(stepId, out var bindings) ? bindings : Array.Empty<AbstractFunctionInputBinding>(),
                    reader.IsDBNull(reader.GetOrdinal("result_context_key")) ? null : reader.GetString(reader.GetOrdinal("result_context_key")),
                    reader.GetBoolean(reader.GetOrdinal("active")),
                    reader.GetBoolean(reader.GetOrdinal("is_compensation_step")),
                    reader.IsDBNull(reader.GetOrdinal("external_context_key")) ? null : reader.GetString(reader.GetOrdinal("external_context_key"))));
            }
        }

        return new AbstractFunctionManifest(manifestId, functionKey, runtimeLane, authorityScope, steps, deniedProjectionKeys, active, authorityBindings, outputShape);
    }

    private static async Task<IReadOnlyList<AbstractFunctionAuthorityBinding>> LoadAuthorityBindingsAsync(
        NpgsqlConnection conn,
        Guid manifestId,
        CancellationToken ct)
    {
        var result = new List<AbstractFunctionAuthorityBinding>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT authority_kind, authority_ref, active
            FROM topology.abstract_function_authority_bindings
            WHERE abstract_function_id = @manifestId
            ORDER BY authority_kind ASC, authority_ref ASC
            """;
        cmd.Parameters.AddWithValue("manifestId", manifestId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new AbstractFunctionAuthorityBinding(
                reader.GetString(reader.GetOrdinal("authority_kind")),
                reader.GetString(reader.GetOrdinal("authority_ref")),
                reader.GetBoolean(reader.GetOrdinal("active"))));
        }
        return result;
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<AbstractFunctionInputBinding>>> LoadBindingsAsync(
        NpgsqlConnection conn,
        Guid manifestId,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, List<AbstractFunctionInputBinding>>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.abstract_function_step_id, b.input_key, b.binding_source, b.binding_path, b.required, b.secret
            FROM topology.abstract_function_input_bindings b
            JOIN topology.abstract_function_steps s
              ON s.abstract_function_step_id = b.abstract_function_step_id
            WHERE s.abstract_function_id = @manifestId
              AND b.active = true
            ORDER BY s.step_order ASC, b.input_key ASC
            """;
        cmd.Parameters.AddWithValue("manifestId", manifestId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var stepId = reader.GetGuid(reader.GetOrdinal("abstract_function_step_id"));
            if (!result.TryGetValue(stepId, out var bindings))
            {
                bindings = new List<AbstractFunctionInputBinding>();
                result[stepId] = bindings;
            }

            bindings.Add(new AbstractFunctionInputBinding(
                reader.GetString(reader.GetOrdinal("input_key")),
                reader.GetString(reader.GetOrdinal("binding_source")),
                reader.GetString(reader.GetOrdinal("binding_path")),
                reader.GetBoolean(reader.GetOrdinal("required")),
                reader.GetBoolean(reader.GetOrdinal("secret"))));
        }

        return result.ToDictionary(static kvp => kvp.Key, static kvp => (IReadOnlyList<AbstractFunctionInputBinding>)kvp.Value);
    }

    private static IReadOnlyDictionary<string, string> ParseJsonObject(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("ABSTRACT_FUNCTION_STEP_CONFIG_INVALID");

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText();
        }
        return result;
    }
}
