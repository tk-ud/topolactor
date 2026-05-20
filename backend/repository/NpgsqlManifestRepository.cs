using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Topolactor.Repository;

/// <summary>
/// Npgsql implementation of ManifestRepository.
///
/// Resolves active manifests from the manifest table by matching dispatcher_mapping
/// topology entries against the request axes (role, target, layer, action).
///
/// DB contract (manifest_tables.sql):
///   manifest (status='active') -> topology JSONB[]
///   topology entry shape for dispatcher_mapping:
///     { "type": "dispatcher_mapping", "role": "...", "target": "...", "layer": "...", "action": "..." }
///
/// No silent fallback: not found -> null; multiple matches -> InvalidOperationException.
/// </summary>
public class NpgsqlManifestRepository : ManifestRepository
{
    private readonly string _connectionString;

    public NpgsqlManifestRepository(
        ILogger<NpgsqlManifestRepository> logger,
        string connectionString)
        : base(logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <inheritdoc/>
    public override async Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role,
        string? target,
        string? layer,
        string? action,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Load all active manifests and filter by dispatcher_mapping axes in application code.
        // When manifest table is small this is acceptable; add DB-side JSONB query when needed.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT manifest_id, relation_registry_id, topology, status " +
            "FROM manifest " +
            "WHERE status = 'active'";

        var candidates = new List<ManifestRecord>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var manifestId = reader.GetGuid(0);
            var relationRegistryId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
            var topologyRaw = reader.IsDBNull(2) ? null : reader.GetFieldValue<string[]>(2);
            var status = reader.GetString(3);

            var topology = ParseTopologyArray(topologyRaw);

            if (MatchesAxes(topology, role, target, layer, action))
            {
                candidates.Add(new ManifestRecord(manifestId, relationRegistryId, topology, status));
            }
        }

        if (candidates.Count == 0)
            return null;

        if (candidates.Count > 1)
        {
            throw new InvalidOperationException(
                $"MANIFEST_AMBIGUOUS: multiple active manifests match axes " +
                $"role={role} target={target} layer={layer} action={action}. " +
                $"Ambiguity is prohibited; deactivate duplicate manifests.");
        }

        return candidates[0];
    }

    /// <inheritdoc/>
    public override async Task<ManifestRecord?> LoadByIdAsync(
        Guid manifestId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT manifest_id, relation_registry_id, topology, status " +
            "FROM manifest " +
            "WHERE manifest_id = @id " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("id", manifestId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var id = reader.GetGuid(0);
        var relId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
        var topologyRaw = reader.IsDBNull(2) ? null : reader.GetFieldValue<string[]>(2);
        var status = reader.GetString(3);

        return new ManifestRecord(id, relId, ParseTopologyArray(topologyRaw), status);
    }

    private static IReadOnlyList<JsonElement> ParseTopologyArray(string[]? raw)
    {
        if (raw is null || raw.Length == 0)
            return Array.Empty<JsonElement>();

        var result = new List<JsonElement>(raw.Length);
        foreach (var item in raw)
        {
            try
            {
                result.Add(JsonDocument.Parse(item).RootElement.Clone());
            }
            catch (JsonException)
            {
                // Malformed JSONB entry — skip silently? No: log and include as null-ish.
                // Include as string JsonElement to preserve for debugging.
                result.Add(JsonSerializer.SerializeToElement(item));
            }
        }
        return result;
    }

    /// <summary>
    /// Returns true when the topology array contains a dispatcher_mapping entry
    /// matching all provided axes. Null axes are treated as wildcards (match any).
    /// </summary>
    private static bool MatchesAxes(
        IReadOnlyList<JsonElement> topology,
        string? role,
        string? target,
        string? layer,
        string? action)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            if (!entry.TryGetProperty("type", out var typeEl) ||
                !string.Equals(typeEl.GetString(), "dispatcher_mapping", StringComparison.Ordinal))
                continue;

            if (!AxisMatches(entry, "role", role)) continue;
            if (!AxisMatches(entry, "target", target)) continue;
            if (!AxisMatches(entry, "layer", layer)) continue;
            if (!AxisMatches(entry, "action", action)) continue;

            return true;
        }
        return false;
    }

    private static bool AxisMatches(JsonElement entry, string propName, string? value)
    {
        if (value is null) return true;
        if (!entry.TryGetProperty(propName, out var prop)) return false;
        return string.Equals(prop.GetString(), value, StringComparison.OrdinalIgnoreCase);
    }
}
