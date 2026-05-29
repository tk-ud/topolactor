using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// In-memory manifest repository for admin manifest management unit tests.
/// </summary>
internal sealed class InMemoryManifestAdminRepository : ManifestRepository
{
    private readonly List<ManifestDetailRecord> _manifests = [];

    public InMemoryManifestAdminRepository() : base(NullLogger<ManifestRepository>.Instance) { }

    public void Seed(ManifestDetailRecord record) => _manifests.Add(record);

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
}
