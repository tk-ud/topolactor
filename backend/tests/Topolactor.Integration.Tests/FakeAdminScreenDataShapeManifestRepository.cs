using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Minimal manifest repository fixture supporting the single draft manifest round-trip
/// AdminRuntime.assign_screen_data_shape needs: seed, load, and merge a topology extension
/// entry into the draft. All other ManifestRepository surface area is out of scope for this
/// fixture and returns an explicit STUB error rather than a silent no-op.
/// </summary>
internal sealed class FakeAdminScreenDataShapeManifestRepository : ManifestRepository
{
    private ManifestDetailRecord? _draft;

    public FakeAdminScreenDataShapeManifestRepository() : base(NullLogger<ManifestRepository>.Instance) { }

    public void Seed(ManifestDetailRecord record) => _draft = record;

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult(_draft?.ManifestId == manifestId ? _draft : null);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default)
    {
        if (_draft is null || _draft.ManifestId != manifestId)
            return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("MANIFEST_NOT_FOUND", "not found")));

        var entry = entryBody.ValueKind == JsonValueKind.Object ? entryBody : JsonSerializer.SerializeToElement(new { type = entryType });
        var merged = ManifestCanonicalProjection.MergeTopologyEntry(_draft.Topology, entryType, entry);
        _draft = _draft with { Topology = merged.ToList(), UpdatedAt = DateTimeOffset.UtcNow };
        return Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((_draft, null));
    }

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action, CancellationToken ct = default) =>
        Task.FromResult<ManifestRecord?>(null);

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestRecord?>(null);

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(string? statusFilter, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
        Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
        Guid manifestId, Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
        Guid manifestId, IReadOnlySet<string> allowedRuntimeDestinations, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
        Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
        string? statusFilter, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PromotionManifestListItem>>([]);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
        Guid manifestId, JsonElement promotionEntry, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);
}
