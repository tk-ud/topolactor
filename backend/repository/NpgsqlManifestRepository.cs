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
    public override async Task<IReadOnlySet<string>> ListActivePhysicalTableRefsAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT table_ref FROM topology.physical_tables WHERE active = true";

        var refs = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!reader.IsDBNull(0))
            {
                var tableRef = reader.GetString(0).Trim();
                if (!string.IsNullOrWhiteSpace(tableRef))
                    refs.Add(tableRef);
            }
        }

        return refs;
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
        await using var tx = await conn.BeginTransactionAsync(ct);

        ManifestDetailRecord created;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
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
                await tx.RollbackAsync(ct);
                return (null, new ValidationError("MANIFEST_CREATE_FAILED", "Failed to create draft manifest."));
            }

            created = ReadDetailRecord(reader);
        }

        var projectionError = await ManifestCanonicalProjection.ProjectOnAuthoringDraftAsync(conn, created, ct, tx);
        if (projectionError is not null)
        {
            await tx.RollbackAsync(ct);
            return (null, projectionError);
        }

        await tx.CommitAsync(ct);
        return (created, null);
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
        await using var tx = await conn.BeginTransactionAsync(ct);

        ManifestDetailRecord updated;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
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
                await tx.RollbackAsync(ct);
                return (null, new ValidationError("MANIFEST_UPDATE_FAILED", $"Failed to update draft manifest {manifestId}."));
            }

            updated = ReadDetailRecord(reader);
        }

        var projectionError = await ManifestCanonicalProjection.ProjectOnAuthoringDraftAsync(conn, updated, ct, tx);
        if (projectionError is not null)
        {
            await tx.RollbackAsync(ct);
            return (null, projectionError);
        }

        await tx.CommitAsync(ct);
        return (updated, null);
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

        var projectionError = await ManifestCanonicalProjection.ProjectOnPromoteAsync(conn, promoted, ct, tx);
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

    /// <inheritdoc/>
    public override async Task<CloneSourceEvidence?> LoadCloneSourceEvidenceAsync(
        Guid sourceActiveManifestId,
        CancellationToken ct = default)
    {
        var detail = await LoadDetailByIdAsync(sourceActiveManifestId, ct);
        if (detail is null) return null;
        if (!string.Equals(detail.Status, "active", StringComparison.OrdinalIgnoreCase)) return null;
        return BuildSourceEvidence(detail);
    }

    /// <inheritdoc/>
    public override async Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateCloneReplacementDraftFromActiveAsync(
        Guid sourceActiveManifestId,
        CancellationToken ct = default)
    {
        var source = await LoadDetailByIdAsync(sourceActiveManifestId, ct);
        if (source is null)
            return (null, new ValidationError("CLONE_SOURCE_NOT_FOUND", $"Source manifest {sourceActiveManifestId} was not found."));
        if (!string.Equals(source.Status, "active", StringComparison.OrdinalIgnoreCase))
            return (null, new ValidationError("CLONE_SOURCE_NOT_ACTIVE", $"Source manifest {sourceActiveManifestId} is status={source.Status}; clone source must be active."));

        var evidence = BuildSourceEvidence(source);
        var cloneEntry = CloneDraftMetadata.BuildEntry(
            CloneDraftMetadata.OriginManualCloneReplacement,
            CloneDraftMetadata.CloneModeReplacement,
            sourceActiveManifestId,
            evidence);
        var topology = ManifestCanonicalProjection.MergeTopologyEntry(
            source.Topology, CloneDraftMetadata.EntryType, cloneEntry);
        topology = ManifestListEligibility.EnsureContentsTypeOnTopology(
            topology, ManifestContentsTypeVocabulary.Contents);

        return await CreateDraftAsync(source.RelationRegistryId, topology, ct);
    }

    /// <inheritdoc/>
    public override async Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateCloneNewTopologyDraftFromActiveAsync(
        Guid sourceActiveManifestId,
        string newTopologySystemName,
        string? userFacingLabel,
        CancellationToken ct = default)
    {
        var source = await LoadDetailByIdAsync(sourceActiveManifestId, ct);
        if (source is null)
            return (null, new ValidationError("CLONE_SOURCE_NOT_FOUND", $"Source manifest {sourceActiveManifestId} was not found."));
        if (!string.Equals(source.Status, "active", StringComparison.OrdinalIgnoreCase))
            return (null, new ValidationError("CLONE_SOURCE_NOT_ACTIVE", $"Source manifest {sourceActiveManifestId} is status={source.Status}; clone source must be active."));

        var evidence = BuildSourceEvidence(source);
        var renamed = CloneDraftMetadata.WithNewTopologyIdentity(source.Topology, newTopologySystemName, userFacingLabel);
        var cloneEntry = CloneDraftMetadata.BuildEntry(
            CloneDraftMetadata.OriginManualCloneNewTopology,
            CloneDraftMetadata.CloneModeNewTopology,
            sourceActiveManifestId,
            evidence);
        var topology = ManifestCanonicalProjection.MergeTopologyEntry(
            renamed, CloneDraftMetadata.EntryType, cloneEntry);
        topology = ManifestListEligibility.EnsureContentsTypeOnTopology(
            topology, ManifestContentsTypeVocabulary.Contents);

        return await CreateDraftAsync(source.RelationRegistryId, topology, ct);
    }

    /// <inheritdoc/>
    public override Task<int> CountActiveIdentityConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        CountActiveAxisConflictsAsync(role, target, layer, action, excludeManifestId, ct);

    /// <inheritdoc/>
    public override async Task<CloneReplacementMergeResult> MergeCloneReplacementDraftToActiveAsync(
        Guid draftManifestId,
        IReadOnlySet<string> allowedRuntimeDestinations,
        string? actor,
        CancellationToken ct = default)
    {
        // Deterministic lock ordering to avoid deadlock: lock the WORKING DRAFT row first,
        // then the MERGE-TARGET ACTIVE (source) row. Draft rows and active rows are disjoint by
        // status, so no concurrent merge can request these two locks in the opposite order.
        // Every decision below — status, clone_metadata authority, source evidence, stale check,
        // active identity conflict, validation, diff/log evidence, active UPDATE, draft DELETE —
        // is made from the locked draft snapshot read INSIDE this transaction, never from an
        // out-of-transaction read. A concurrent draft mutation therefore cannot slip a stale
        // snapshot past the guard: it blocks on the draft lock until this merge commits (then
        // finds the draft deleted) or this merge sees the committed change and fails close.
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // 1) Lock + read the working draft row inside the transaction.
        ManifestDetailRecord? draft;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "SELECT manifest_id, relation_registry_id, topology, status, created_at, updated_at " +
                "FROM manifest WHERE manifest_id = @id FOR UPDATE";
            cmd.Parameters.AddWithValue("id", draftManifestId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            draft = await reader.ReadAsync(ct) ? ReadDetailRecord(reader) : null;
        }

        if (draft is null)
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, "MANIFEST_NOT_FOUND", $"Draft manifest {draftManifestId} was not found.");
        }
        if (!string.Equals(draft.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, "MANIFEST_NOT_DRAFT", $"Manifest {draftManifestId} is status={draft.Status}; only a draft can be merged.");
        }

        var meta = CloneDraftMetadata.Extract(draft.Topology);
        if (meta is null)
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, "CLONE_METADATA_MISSING", "Draft has no clone_metadata; not a replacement clone draft.");
        }
        // Source evidence / lineage / SQL Attention candidate alone never grant replacement authority.
        if (!string.Equals(meta.DraftOrigin, CloneDraftMetadata.OriginManualCloneReplacement, StringComparison.Ordinal) ||
            !string.Equals(meta.CloneMode, CloneDraftMetadata.CloneModeReplacement, StringComparison.Ordinal))
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, "REPLACEMENT_AUTHORITY_DENIED",
                $"Replacement merge requires draft_origin=manual_clone_replacement and clone_mode=replacement (got {meta.DraftOrigin}/{meta.CloneMode}).");
        }
        if (meta.SourceActiveManifestId is null || meta.SourceEvidence is null)
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, "MISSING_SOURCE_EVIDENCE", "Replacement clone draft is missing source_active_manifest_id / source evidence.");
        }

        var sourceId = meta.SourceActiveManifestId.Value;
        var draftSummary = ManifestTopologyValidator.ExtractSummary(draft.Topology);

        // 2) Lock + read the merge-target active (source) row.
        ManifestDetailRecord? source;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "SELECT manifest_id, relation_registry_id, topology, status, created_at, updated_at " +
                "FROM manifest WHERE manifest_id = @id FOR UPDATE";
            cmd.Parameters.AddWithValue("id", sourceId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            source = await reader.ReadAsync(ct) ? ReadDetailRecord(reader) : null;
        }

        if (source is null)
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, "CLONE_SOURCE_NOT_FOUND", $"Merge target {sourceId} was not found.");
        }
        if (!string.Equals(source.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, "STALE_SOURCE_ACTIVE", $"Merge target {sourceId} is status={source.Status}; source active is stale.");
        }
        // UUID integrity: clone source must equal the merge-target active row.
        if (source.ManifestId != sourceId)
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, "SOURCE_IDENTITY_MISMATCH", "Clone source UUID does not match the merge-target active row.");
        }
        // Stale check: source active topology must be unchanged since clone capture.
        var liveHash = CloneDraftMetadata.ComputeTopologyHash(source.Topology);
        if (!string.Equals(liveHash, meta.SourceEvidence.SourceTopologyHash, StringComparison.Ordinal))
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, "STALE_SOURCE_ACTIVE", "Source active changed since the clone was created; re-clone before merging.");
        }

        // Active identity conflict: no other active row may share the merge-target axes.
        if (draftSummary.DispatcherMapping is { } axes)
        {
            var conflict = await CountActiveIdentityConflictsTxAsync(conn, tx, axes.Role, axes.Target, axes.Layer, axes.Action, sourceId, ct);
            if (conflict > 0)
            {
                await tx.RollbackAsync(ct);
                return MergeFail(draftManifestId, "ACTIVE_IDENTITY_CONFLICT", $"Another active manifest already holds the merge-target axes ({conflict} conflict(s)).");
            }
        }

        // The merged production shape excludes the draft's clone_metadata bookkeeping entry.
        var mergedTopology = StripCloneMetadata(draft.Topology);
        var validation = ManifestTopologyValidator.Validate(mergedTopology, allowedRuntimeDestinations);
        if (validation.IsBlocking)
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, validation.Errors[0].Code, validation.Errors[0].Message);
        }

        // Diff/log evidence gate — a no-op merge has no evidence and fails close.
        var (diffJson, changeCount) = CloneDraftMetadata.ComputeTopologyDiff(source.Topology, mergedTopology);
        if (changeCount == 0)
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, "REPLACEMENT_MERGE_NO_DIFF", "Replacement draft is identical to the active source; nothing to merge.");
        }

        // Diff/log evidence is preserved in the existing edit-log surface (not the draft row).
        await AppendMergeEditLogAsync(conn, tx, source, draft, diffJson, actor, ct);

        // existing active row UPDATE in place.
        ManifestDetailRecord mergedActive;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "UPDATE manifest SET relation_registry_id = @rel, topology = @topology, updated_at = now() " +
                "WHERE manifest_id = @id AND status = 'active' " +
                "RETURNING manifest_id, relation_registry_id, topology, status, created_at, updated_at";
            cmd.Parameters.AddWithValue("id", sourceId);
            cmd.Parameters.AddWithValue("rel", (object?)draft.RelationRegistryId ?? DBNull.Value);
            AddTopologyArrayParameter(cmd, "topology", mergedTopology);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                await tx.RollbackAsync(ct);
                return MergeFail(draftManifestId, "REPLACEMENT_MERGE_FAILED", "Failed to update the active manifest during merge.");
            }
            mergedActive = ReadDetailRecord(reader);
        }

        var projectionError = await ManifestCanonicalProjection.ProjectOnPromoteAsync(conn, mergedActive, ct, tx);
        if (projectionError is not null)
        {
            await tx.RollbackAsync(ct);
            return MergeFail(draftManifestId, projectionError.Code, projectionError.Message);
        }

        // working draft row DELETE after a successful merge (no promotion, no draft-row audit retention).
        await using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM manifest WHERE manifest_id = @id AND status = 'draft'";
            del.Parameters.AddWithValue("id", draftManifestId);
            await del.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new CloneReplacementMergeResult(true, sourceId, draftManifestId, diffJson, changeCount, null);
    }

    private static CloneReplacementMergeResult MergeFail(Guid draftId, string code, string message) =>
        new(false, null, draftId, null, 0, new ValidationError(code, message));

    private static IReadOnlyList<JsonElement> StripCloneMetadata(IReadOnlyList<JsonElement> topology)
    {
        var list = new List<JsonElement>(topology.Count);
        foreach (var entry in topology)
        {
            if (entry.ValueKind == JsonValueKind.Object &&
                entry.TryGetProperty("type", out var typeEl) &&
                typeEl.ValueKind == JsonValueKind.String &&
                string.Equals(typeEl.GetString(), CloneDraftMetadata.EntryType, StringComparison.Ordinal))
            {
                continue;
            }
            list.Add(entry);
        }
        return list;
    }

    private static async Task<int> CountActiveIdentityConflictsTxAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx,
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT manifest_id, topology FROM manifest WHERE status = 'active'";
        var count = 0;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var manifestId = reader.GetGuid(0);
            if (excludeManifestId.HasValue && manifestId == excludeManifestId.Value) continue;
            var topology = ParseTopologyArray(reader.IsDBNull(1) ? null : reader.GetFieldValue<string[]>(1));
            if (MatchesAxes(topology, role, target, layer, action)) count++;
        }
        return count;
    }

    private static async Task AppendMergeEditLogAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx,
        ManifestDetailRecord source, ManifestDetailRecord draft, string diffJson, string? actor, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO topology.topology_edit_log " +
            "(target_table, target_id, operation, before_json, after_json, diff_json, actor) " +
            "VALUES (@tt, @tid, @op, @before::jsonb, @after::jsonb, @diff::jsonb, @actor)";
        cmd.Parameters.AddWithValue("tt", "manifest:replacement_clone_merge");
        cmd.Parameters.AddWithValue("tid", source.ManifestId.ToString());
        cmd.Parameters.AddWithValue("op", "merge_clone_replacement_draft_to_active");
        cmd.Parameters.AddWithValue("before", JsonSerializer.Serialize(source.Topology));
        cmd.Parameters.AddWithValue("after", JsonSerializer.Serialize(StripCloneMetadata(draft.Topology)));
        cmd.Parameters.AddWithValue("diff", diffJson);
        cmd.Parameters.AddWithValue("actor", (object?)actor ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
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

    // MatchesAxes moved to the shared DispatcherMappingAxisAuthority (round 17 hardening) so
    // ManifestDispatcher's target_ref admin_runtime authorization reuses identical semantics
    // instead of a parallel duplicate implementation.
    private static bool MatchesAxes(
        IReadOnlyList<JsonElement> topology,
        string? role,
        string? target,
        string? layer,
        string? action) =>
        DispatcherMappingAxisAuthority.MatchesAxes(topology, role, target, layer, action);
}
