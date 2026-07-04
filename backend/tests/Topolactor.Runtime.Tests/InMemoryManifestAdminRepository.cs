using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// In-memory manifest repository for admin manifest management unit tests.
/// </summary>
internal sealed record ProjectedTopologyManifest(
    Guid TopologyManifestId,
    Guid HubId,
    string ManifestKey,
    string Status);

internal sealed record InMemoryMergeEditLogEntry(
    string TargetTable,
    string? TargetId,
    string Operation,
    string? DiffJson,
    string? Actor);

internal sealed class InMemoryManifestAdminRepository : ManifestRepository
{
    private readonly List<ManifestDetailRecord> _manifests = [];
    private readonly List<ProjectedTopologyManifest> _projected = [];
    private readonly List<InMemoryMergeEditLogEntry> _mergeEditLog = [];
    private readonly HashSet<string> _activePhysicalTableRefs = new(StringComparer.Ordinal);

    public InMemoryManifestAdminRepository() : base(NullLogger<ManifestRepository>.Instance) { }

    public void SeedActivePhysicalTableRef(string tableRef) =>
        _activePhysicalTableRefs.Add(tableRef.Trim());

    public override Task<IReadOnlySet<string>> ListActivePhysicalTableRefsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<string>>(_activePhysicalTableRefs);

    public void Seed(ManifestDetailRecord record) => _manifests.Add(record);

    public IReadOnlyList<ProjectedTopologyManifest> ProjectedTopologyManifests => _projected;

    public IReadOnlyList<InMemoryMergeEditLogEntry> MergeEditLog => _mergeEditLog;

    public IReadOnlyList<ManifestDetailRecord> Manifests => _manifests;

    /// <summary>Test-only: mutate an active row's topology to simulate a source drifting after a clone.</summary>
    public Task UpdateActiveTopologyForTest(Guid manifestId, IReadOnlyList<JsonElement> topology)
    {
        var idx = _manifests.FindIndex(m => m.ManifestId == manifestId);
        if (idx >= 0)
            _manifests[idx] = _manifests[idx] with { Topology = topology.ToList(), UpdatedAt = DateTimeOffset.UtcNow };
        return Task.CompletedTask;
    }

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action, CancellationToken ct = default)
    {
        var matches = _manifests
            .Where(m => m.Status == "active")
            .Where(m => MatchesAxes(m.Topology, role, target, layer, action))
            .ToList();

        if (matches.Count == 0) return Task.FromResult<ManifestRecord?>(null);
        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"MANIFEST_AMBIGUOUS: multiple active manifests match axes role={role} target={target} layer={layer} action={action}.");
        }

        var m0 = matches[0];
        return Task.FromResult<ManifestRecord?>(new ManifestRecord(m0.ManifestId, m0.RelationRegistryId, m0.Topology, m0.Status));
    }

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default)
    {
        var detail = _manifests.FirstOrDefault(m => m.ManifestId == manifestId);
        if (detail is null) return Task.FromResult<ManifestRecord?>(null);
        return Task.FromResult<ManifestRecord?>(new ManifestRecord(detail.ManifestId, detail.RelationRegistryId, detail.Topology, detail.Status));
    }

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(string? statusFilter, CancellationToken ct = default)
    {
        var query = string.IsNullOrWhiteSpace(statusFilter)
            ? _manifests
            : _manifests.Where(m => m.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));

        var items = query.Select(m =>
        {
            var summary = ManifestTopologyValidator.ExtractSummary(m.Topology);
            return new ManifestListItem(
                m.ManifestId,
                m.RelationRegistryId,
                m.Status,
                summary.DispatcherMapping?.Role,
                summary.DispatcherMapping?.Target,
                summary.DispatcherMapping?.Layer,
                summary.DispatcherMapping?.Action,
                summary.RuntimeMapping?.RuntimeDestination,
                m.CreatedAt,
                m.UpdatedAt);
        }).ToList();

        return Task.FromResult<IReadOnlyList<ManifestListItem>>(items);
    }

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult(_manifests.FirstOrDefault(m => m.ManifestId == manifestId));

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default)
    {
        var count = _manifests.Count(m =>
            m.Status == "active" &&
            (!excludeManifestId.HasValue || m.ManifestId != excludeManifestId.Value) &&
            MatchesAxes(m.Topology, role, target, layer, action));
        return Task.FromResult(count);
    }

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
        Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var record = new ManifestDetailRecord(Guid.NewGuid(), relationRegistryId, topology.ToList(), "draft", now, now);
        _manifests.Add(record);
        return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((record, null));
    }

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
        Guid manifestId, Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default)
    {
        var idx = _manifests.FindIndex(m => m.ManifestId == manifestId);
        if (idx < 0)
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_FOUND", "not found")));
        if (_manifests[idx].Status != "draft")
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_DRAFT", "not draft")));

        var updated = _manifests[idx] with
        {
            RelationRegistryId = relationRegistryId,
            Topology = topology.ToList(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _manifests[idx] = updated;
        return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((updated, null));
    }

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
        Guid manifestId, IReadOnlySet<string> allowedRuntimeDestinations, CancellationToken ct = default)
    {
        var idx = _manifests.FindIndex(m => m.ManifestId == manifestId);
        if (idx < 0)
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_FOUND", "not found")));
        if (_manifests[idx].Status != "draft")
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_DRAFT", "not draft")));

        var detail = _manifests[idx];
        var summary = ManifestTopologyValidator.ExtractSummary(detail.Topology);
        var conflictCount = 0;
        if (summary.DispatcherMapping is not null)
        {
            conflictCount = _manifests.Count(m =>
                m.Status == "active" &&
                m.ManifestId != manifestId &&
                MatchesAxes(m.Topology,
                    summary.DispatcherMapping.Role,
                    summary.DispatcherMapping.Target,
                    summary.DispatcherMapping.Layer,
                    summary.DispatcherMapping.Action));
        }

        var validation = ManifestTopologyValidator.Validate(
            detail.Topology, allowedRuntimeDestinations, true, conflictCount);
        if (validation.IsBlocking)
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, validation.Errors[0]));

        var promoted = detail with { Status = "active", UpdatedAt = DateTimeOffset.UtcNow };
        _manifests[idx] = promoted;

        var (hubId, manifestKey) = ManifestCanonicalProjection.ExtractHubGrouping(promoted.Topology);
        manifestKey ??= PromotionManifestValidator.ExtractMetadataDto(promoted.Topology)?.ManifestKey;
        manifestKey ??= ManifestCanonicalProjection.BuildDefaultManifestKey(summary);
        _projected.Add(new ProjectedTopologyManifest(
            promoted.ManifestId,
            hubId ?? Guid.Empty,
            manifestKey,
            "active"));

        return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((promoted, null));
    }

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
        Guid manifestId, CancellationToken ct = default)
    {
        var idx = _manifests.FindIndex(m => m.ManifestId == manifestId);
        if (idx < 0)
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_FOUND", "not found")));
        if (_manifests[idx].Status != "active")
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_ACTIVE", "not active")));

        var deprecated = _manifests[idx] with { Status = "deprecated", UpdatedAt = DateTimeOffset.UtcNow };
        _manifests[idx] = deprecated;
        return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((deprecated, null));
    }

    public override Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
        string? statusFilter, CancellationToken ct = default)
    {
        var items = _manifests
            .Where(m => string.IsNullOrWhiteSpace(statusFilter) ||
                        m.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
            .Select(m =>
            {
                var metadata = PromotionManifestValidator.ExtractMetadataDto(m.Topology);
                if (metadata is null) return null;
                return new PromotionManifestListItem(
                    m.ManifestId,
                    m.Status,
                    metadata.ManifestKey,
                    metadata.VersionLabel,
                    !string.IsNullOrWhiteSpace(metadata.DisclosureText),
                    m.CreatedAt,
                    m.UpdatedAt);
            })
            .Where(i => i is not null)
            .Cast<PromotionManifestListItem>()
            .ToList();
        return Task.FromResult<IReadOnlyList<PromotionManifestListItem>>(items);
    }

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
        Guid manifestId, JsonElement promotionEntry, CancellationToken ct = default)
    {
        var idx = _manifests.FindIndex(m => m.ManifestId == manifestId);
        if (idx < 0)
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_FOUND", "not found")));
        if (_manifests[idx].Status != "draft")
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_DRAFT", "not draft")));

        var merged = PromotionManifestValidator.MergeIntoTopology(_manifests[idx].Topology, promotionEntry);
        var updated = _manifests[idx] with { Topology = merged.ToList(), UpdatedAt = DateTimeOffset.UtcNow };
        _manifests[idx] = updated;
        return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((updated, null));
    }

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId,
        string entryType,
        JsonElement entryBody,
        CancellationToken ct = default)
    {
        var idx = _manifests.FindIndex(m => m.ManifestId == manifestId);
        if (idx < 0)
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_FOUND", "not found")));
        if (_manifests[idx].Status != "draft")
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_DRAFT", "not draft")));

        var entry = entryBody.ValueKind == JsonValueKind.Object
            ? entryBody
            : JsonSerializer.SerializeToElement(new { type = entryType });
        var merged = ManifestCanonicalProjection.MergeTopologyEntry(_manifests[idx].Topology, entryType, entry);
        var updated = _manifests[idx] with { Topology = merged.ToList(), UpdatedAt = DateTimeOffset.UtcNow };
        _manifests[idx] = updated;
        return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((updated, null));
    }

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default)
    {
        var count = _manifests.Count(m =>
            m.Status == "active" &&
            (!excludeManifestId.HasValue || m.ManifestId != excludeManifestId.Value) &&
            PromotionManifestValidator.ExtractMetadataDto(m.Topology) is { } meta &&
            string.Equals(meta.ManifestKey, manifestKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(meta.VersionLabel, versionLabel, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(count);
    }

    public override Task<CloneSourceEvidence?> LoadCloneSourceEvidenceAsync(
        Guid sourceActiveManifestId, CancellationToken ct = default)
    {
        var detail = _manifests.FirstOrDefault(m => m.ManifestId == sourceActiveManifestId);
        if (detail is null || !detail.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<CloneSourceEvidence?>(null);
        return Task.FromResult<CloneSourceEvidence?>(BuildSourceEvidence(detail));
    }

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateCloneReplacementDraftFromActiveAsync(
        Guid sourceActiveManifestId, CancellationToken ct = default)
    {
        var source = _manifests.FirstOrDefault(m => m.ManifestId == sourceActiveManifestId);
        if (source is null)
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("CLONE_SOURCE_NOT_FOUND", "source not found")));
        if (!source.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("CLONE_SOURCE_NOT_ACTIVE", "source not active")));

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
        return CreateDraftAsync(source.RelationRegistryId, topology, ct);
    }

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateCloneNewTopologyDraftFromActiveAsync(
        Guid sourceActiveManifestId, string newTopologySystemName, string? userFacingLabel, CancellationToken ct = default)
    {
        var source = _manifests.FirstOrDefault(m => m.ManifestId == sourceActiveManifestId);
        if (source is null)
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("CLONE_SOURCE_NOT_FOUND", "source not found")));
        if (!source.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("CLONE_SOURCE_NOT_ACTIVE", "source not active")));

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
        return CreateDraftAsync(source.RelationRegistryId, topology, ct);
    }

    public override Task<int> CountActiveIdentityConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        CountActiveAxisConflictsAsync(role, target, layer, action, excludeManifestId, ct);

    public override Task<CloneReplacementMergeResult> MergeCloneReplacementDraftToActiveAsync(
        Guid draftManifestId, IReadOnlySet<string> allowedRuntimeDestinations, string? actor, CancellationToken ct = default)
    {
        CloneReplacementMergeResult Fail(string code, string message) =>
            new(false, null, draftManifestId, null, 0, new ValidationError(code, message));

        var draftIdx = _manifests.FindIndex(m => m.ManifestId == draftManifestId);
        if (draftIdx < 0)
            return Task.FromResult(Fail("MANIFEST_NOT_FOUND", "draft not found"));
        var draft = _manifests[draftIdx];
        if (!draft.Status.Equals("draft", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Fail("MANIFEST_NOT_DRAFT", "not draft"));

        var meta = CloneDraftMetadata.Extract(draft.Topology);
        if (meta is null)
            return Task.FromResult(Fail("CLONE_METADATA_MISSING", "no clone metadata"));
        if (meta.DraftOrigin != CloneDraftMetadata.OriginManualCloneReplacement ||
            meta.CloneMode != CloneDraftMetadata.CloneModeReplacement)
            return Task.FromResult(Fail("REPLACEMENT_AUTHORITY_DENIED", $"{meta.DraftOrigin}/{meta.CloneMode}"));
        if (meta.SourceActiveManifestId is null || meta.SourceEvidence is null)
            return Task.FromResult(Fail("MISSING_SOURCE_EVIDENCE", "no source evidence"));

        var sourceId = meta.SourceActiveManifestId.Value;
        var sourceIdx = _manifests.FindIndex(m => m.ManifestId == sourceId);
        if (sourceIdx < 0)
            return Task.FromResult(Fail("CLONE_SOURCE_NOT_FOUND", "merge target not found"));
        var source = _manifests[sourceIdx];
        if (!source.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Fail("STALE_SOURCE_ACTIVE", "merge target not active"));

        var liveHash = CloneDraftMetadata.ComputeTopologyHash(source.Topology);
        if (liveHash != meta.SourceEvidence.SourceTopologyHash)
            return Task.FromResult(Fail("STALE_SOURCE_ACTIVE", "source changed since clone"));

        var draftSummary = ManifestTopologyValidator.ExtractSummary(draft.Topology);
        if (draftSummary.DispatcherMapping is { } axes)
        {
            var conflict = _manifests.Count(m =>
                m.Status == "active" && m.ManifestId != sourceId &&
                MatchesAxes(m.Topology, axes.Role, axes.Target, axes.Layer, axes.Action));
            if (conflict > 0)
                return Task.FromResult(Fail("ACTIVE_IDENTITY_CONFLICT", $"{conflict} conflict(s)"));
        }

        var mergedTopology = StripCloneMetadata(draft.Topology);
        var validation = ManifestTopologyValidator.Validate(mergedTopology, allowedRuntimeDestinations);
        if (validation.IsBlocking)
            return Task.FromResult(Fail(validation.Errors[0].Code, validation.Errors[0].Message));

        var (diffJson, changeCount) = CloneDraftMetadata.ComputeTopologyDiff(source.Topology, mergedTopology);
        if (changeCount == 0)
            return Task.FromResult(Fail("REPLACEMENT_MERGE_NO_DIFF", "no changes"));

        _mergeEditLog.Add(new InMemoryMergeEditLogEntry(
            "manifest:replacement_clone_merge", sourceId.ToString(),
            "merge_clone_replacement_draft_to_active", diffJson, actor));

        // existing active row UPDATE in place; working draft row DELETE.
        _manifests[sourceIdx] = source with { Topology = mergedTopology, RelationRegistryId = draft.RelationRegistryId, UpdatedAt = DateTimeOffset.UtcNow };
        _manifests.RemoveAll(m => m.ManifestId == draftManifestId);

        return Task.FromResult(new CloneReplacementMergeResult(true, sourceId, draftManifestId, diffJson, changeCount, null));
    }

    private static IReadOnlyList<JsonElement> StripCloneMetadata(IReadOnlyList<JsonElement> topology)
    {
        var list = new List<JsonElement>(topology.Count);
        foreach (var entry in topology)
        {
            if (entry.ValueKind == JsonValueKind.Object &&
                entry.TryGetProperty("type", out var typeEl) &&
                typeEl.ValueKind == JsonValueKind.String &&
                string.Equals(typeEl.GetString(), CloneDraftMetadata.EntryType, StringComparison.Ordinal))
                continue;
            list.Add(entry);
        }
        return list;
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
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                !string.Equals(typeEl.GetString(), "dispatcher_mapping", StringComparison.Ordinal)) continue;
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

internal static class ManifestRepositoryStubDefaults
{
    public static Task<IReadOnlyList<ManifestListItem>> EmptyList(string? statusFilter, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

    public static Task<ManifestDetailRecord?> NullDetail(Guid manifestId, CancellationToken ct) =>
        Task.FromResult<ManifestDetailRecord?>(null);

    public static Task<int> ZeroConflicts(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct) =>
        Task.FromResult(0);

    public static Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> NotImplementedDraft() =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>(
            (null, new ValidationError("STUB", "manifest admin stub not implemented")));

    public static Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> NotImplementedLifecycle() =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>(
            (null, new ValidationError("STUB", "manifest admin stub not implemented")));

    public static Task<IReadOnlyList<PromotionManifestListItem>> EmptyPromotionList(string? statusFilter, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PromotionManifestListItem>>([]);

    public static Task<int> ZeroPromotionConflicts(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct) =>
        Task.FromResult(0);

    public static Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> NotImplementedMerge() =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>(
            (null, new ValidationError("STUB", "manifest admin stub not implemented")));
}
