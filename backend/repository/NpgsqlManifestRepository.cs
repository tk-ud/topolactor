using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Topolactor.Schema;

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
        var detail = await LoadDetailByIdAsync(manifestId, ct);
        if (detail is null) return null;
        return new ManifestRecord(detail.ManifestId, detail.RelationRegistryId, detail.Topology, detail.Status);
    }

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(
        string? statusFilter,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        if (string.IsNullOrWhiteSpace(statusFilter))
        {
            cmd.CommandText =
                "SELECT manifest_id, relation_registry_id, topology, status, created_at, updated_at " +
                "FROM manifest ORDER BY updated_at DESC";
        }
        else
        {
            cmd.CommandText =
                "SELECT manifest_id, relation_registry_id, topology, status, created_at, updated_at " +
                "FROM manifest WHERE status = @status ORDER BY updated_at DESC";
            cmd.Parameters.AddWithValue("status", statusFilter.Trim().ToLowerInvariant());
        }

        var items = new List<ManifestListItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var topology = ParseTopologyArray(reader.IsDBNull(2) ? null : reader.GetFieldValue<string[]>(2));
            var summary = ManifestTopologyValidator.ExtractSummary(topology);
            items.Add(new ManifestListItem(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.GetString(3),
                summary.DispatcherMapping?.Role,
                summary.DispatcherMapping?.Target,
                summary.DispatcherMapping?.Layer,
                summary.DispatcherMapping?.Action,
                summary.RuntimeMapping?.RuntimeDestination,
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return items;
    }

    /// <inheritdoc/>
    public override async Task<ManifestDetailRecord?> LoadDetailByIdAsync(
        Guid manifestId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT manifest_id, relation_registry_id, topology, status, created_at, updated_at " +
            "FROM manifest WHERE manifest_id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("id", manifestId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new ManifestDetailRecord(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            ParseTopologyArray(reader.IsDBNull(2) ? null : reader.GetFieldValue<string[]>(2)),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    /// <inheritdoc/>
    public override async Task<int> CountActiveAxisConflictsAsync(
        string role,
        string target,
        string layer,
        string action,
        Guid? excludeManifestId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT manifest_id, topology FROM manifest WHERE status = 'active'";

        var count = 0;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var manifestId = reader.GetGuid(0);
            if (excludeManifestId.HasValue && manifestId == excludeManifestId.Value)
                continue;

            var topology = ParseTopologyArray(reader.IsDBNull(1) ? null : reader.GetFieldValue<string[]>(1));
            if (MatchesAxes(topology, role, target, layer, action))
                count++;
        }

        return count;
    }

    /// <inheritdoc/>
    public override async Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
        Guid? relationRegistryId,
        IReadOnlyList<JsonElement> topology,
        CancellationToken ct = default)
    {
        var manifestId = Guid.NewGuid();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) " +
            "VALUES (@id, @rel, @topology, 'draft') " +
            "RETURNING manifest_id, relation_registry_id, topology, status, created_at, updated_at";
        cmd.Parameters.AddWithValue("id", manifestId);
        cmd.Parameters.AddWithValue("rel", (object?)relationRegistryId ?? DBNull.Value);
        AddTopologyArrayParameter(cmd, "topology", topology);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return (null, new ValidationError("MANIFEST_CREATE_FAILED", "Failed to create draft manifest."));
        }

        return (ReadDetailRecord(reader), null);
    }

    /// <inheritdoc/>
    public override async Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
        Guid manifestId,
        Guid? relationRegistryId,
        IReadOnlyList<JsonElement> topology,
        CancellationToken ct = default)
    {
        var existing = await LoadDetailByIdAsync(manifestId, ct);
        if (existing is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));
        if (!string.Equals(existing.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return (null, new ValidationError(
                "MANIFEST_NOT_DRAFT",
                $"Manifest {manifestId} is status={existing.Status}; only draft manifests can be updated."));
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE manifest SET relation_registry_id = @rel, topology = @topology, updated_at = now() " +
            "WHERE manifest_id = @id AND status = 'draft' " +
            "RETURNING manifest_id, relation_registry_id, topology, status, created_at, updated_at";
        cmd.Parameters.AddWithValue("id", manifestId);
        cmd.Parameters.AddWithValue("rel", (object?)relationRegistryId ?? DBNull.Value);
        AddTopologyArrayParameter(cmd, "topology", topology);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return (null, new ValidationError("MANIFEST_UPDATE_FAILED", $"Failed to update draft manifest {manifestId}."));
        }

        return (ReadDetailRecord(reader), null);
    }

    /// <inheritdoc/>
    public override async Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
        Guid manifestId,
        IReadOnlySet<string> allowedRuntimeDestinations,
        CancellationToken ct = default)
    {
        var existing = await LoadDetailByIdAsync(manifestId, ct);
        if (existing is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));
        if (!string.Equals(existing.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return (null, new ValidationError(
                "MANIFEST_NOT_DRAFT",
                $"Manifest {manifestId} is status={existing.Status}; only draft manifests can be promoted."));
        }

        var summary = ManifestTopologyValidator.ExtractSummary(existing.Topology);
        var axes = summary.DispatcherMapping;
        var conflictCount = 0;
        if (axes is not null)
        {
            conflictCount = await CountActiveAxisConflictsAsync(
                axes.Role, axes.Target, axes.Layer, axes.Action, manifestId, ct);
        }

        var validation = ManifestTopologyValidator.Validate(
            existing.Topology,
            allowedRuntimeDestinations,
            checkActiveAxisConflict: true,
            activeAxisConflictCount: conflictCount);

        if (validation.IsBlocking)
        {
            var first = validation.Errors[0];
            return (null, first);
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        ManifestDetailRecord promoted;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "UPDATE manifest SET status = 'active', updated_at = now() " +
                "WHERE manifest_id = @id AND status = 'draft' " +
                "RETURNING manifest_id, relation_registry_id, topology, status, created_at, updated_at";
            cmd.Parameters.AddWithValue("id", manifestId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                await tx.RollbackAsync(ct);
                return (null, new ValidationError("MANIFEST_PROMOTE_FAILED", $"Failed to promote manifest {manifestId}."));
            }

            promoted = ReadDetailRecord(reader);
        }

        var projectionError = await ManifestCanonicalProjection.ProjectOnPromoteAsync(conn, promoted, ct);
        if (projectionError is not null)
        {
            await tx.RollbackAsync(ct);
            return (null, projectionError);
        }

        await tx.CommitAsync(ct);
        return (promoted, null);
    }

    /// <inheritdoc/>
    public override async Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
        Guid manifestId,
        CancellationToken ct = default)
    {
        var existing = await LoadDetailByIdAsync(manifestId, ct);
        if (existing is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));
        if (!string.Equals(existing.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return (null, new ValidationError(
                "MANIFEST_NOT_ACTIVE",
                $"Manifest {manifestId} is status={existing.Status}; only active manifests can be deprecated."));
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE manifest SET status = 'deprecated', updated_at = now() " +
            "WHERE manifest_id = @id AND status = 'active' " +
            "RETURNING manifest_id, relation_registry_id, topology, status, created_at, updated_at";
        cmd.Parameters.AddWithValue("id", manifestId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return (null, new ValidationError("MANIFEST_DEPRECATE_FAILED", $"Failed to deprecate manifest {manifestId}."));
        }

        return (ReadDetailRecord(reader), null);
    }

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
        string? statusFilter,
        CancellationToken ct = default)
    {
        var all = await ListManifestsAsync(statusFilter, ct);
        var items = new List<PromotionManifestListItem>();
        foreach (var m in all)
        {
            var detail = await LoadDetailByIdAsync(m.ManifestId, ct);
            if (detail is null) continue;
            var metadata = PromotionManifestValidator.ExtractMetadataDto(detail.Topology);
            if (metadata is null) continue;
            items.Add(new PromotionManifestListItem(
                detail.ManifestId,
                detail.Status,
                metadata.ManifestKey,
                metadata.VersionLabel,
                !string.IsNullOrWhiteSpace(metadata.DisclosureText),
                detail.CreatedAt,
                detail.UpdatedAt));
        }
        return items;
    }

    /// <inheritdoc/>
    public override async Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
        Guid manifestId,
        JsonElement promotionEntry,
        CancellationToken ct = default)
    {
        var existing = await LoadDetailByIdAsync(manifestId, ct);
        if (existing is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));
        if (!string.Equals(existing.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return (null, new ValidationError(
                "MANIFEST_NOT_DRAFT",
                $"Manifest {manifestId} is status={existing.Status}; only draft manifests can be updated."));
        }

        var merged = PromotionManifestValidator.MergeIntoTopology(existing.Topology, promotionEntry);
        return await UpdateDraftAsync(manifestId, existing.RelationRegistryId, merged, ct);
    }

    /// <inheritdoc/>
    public override async Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey,
        string versionLabel,
        Guid? excludeManifestId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT manifest_id, topology FROM manifest WHERE status = 'active'";

        var count = 0;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var manifestId = reader.GetGuid(0);
            if (excludeManifestId.HasValue && manifestId == excludeManifestId.Value)
                continue;

            var topology = ParseTopologyArray(reader.IsDBNull(1) ? null : reader.GetFieldValue<string[]>(1));
            var metadata = PromotionManifestValidator.ExtractMetadataDto(topology);
            if (metadata is null) continue;
            if (string.Equals(metadata.ManifestKey, manifestKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(metadata.VersionLabel, versionLabel, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    /// <inheritdoc/>
    public override async Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId,
        string entryType,
        JsonElement entryBody,
        CancellationToken ct = default)
    {
        var existing = await LoadDetailByIdAsync(manifestId, ct);
        if (existing is null)
            return (null, new ValidationError("MANIFEST_NOT_FOUND", $"Manifest {manifestId} was not found."));
        if (!string.Equals(existing.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return (null, new ValidationError(
                "MANIFEST_NOT_DRAFT",
                $"Manifest {manifestId} is status={existing.Status}; only draft manifests can be updated."));
        }

        var entry = entryBody.ValueKind == JsonValueKind.Object &&
                    entryBody.TryGetProperty("type", out var typeEl) &&
                    typeEl.ValueKind == JsonValueKind.String
            ? entryBody
            : JsonSerializer.SerializeToElement(new { type = entryType });

        var merged = ManifestCanonicalProjection.MergeTopologyEntry(existing.Topology, entryType, entry);
        return await UpdateDraftAsync(manifestId, existing.RelationRegistryId, merged, ct);
    }

    private static ManifestDetailRecord ReadDetailRecord(NpgsqlDataReader reader)
    {
        return new ManifestDetailRecord(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            ParseTopologyArray(reader.IsDBNull(2) ? null : reader.GetFieldValue<string[]>(2)),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    private static string[] SerializeTopologyArray(IReadOnlyList<JsonElement> topology) =>
        topology.Select(e => e.GetRawText()).ToArray();

    private static void AddTopologyArrayParameter(
        NpgsqlCommand cmd,
        string parameterName,
        IReadOnlyList<JsonElement> topology)
    {
        cmd.Parameters.Add(new NpgsqlParameter(parameterName, NpgsqlDbType.Array | NpgsqlDbType.Jsonb)
        {
            Value = SerializeTopologyArray(topology),
        });
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
                result.Add(JsonSerializer.SerializeToElement(item));
            }
        }
        return result;
    }

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
