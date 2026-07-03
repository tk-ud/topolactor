using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Manifest resolution fixture for aggregate_trigger_runtime dispatch proof. Resolves
/// role="admin" target="aggregate_trigger" layer="runtime" action="execute" to the
/// aggregate_trigger_runtime destination, matching the dispatcher_mapping / runtime_mapping
/// axes an admin-authored manifest would carry in production.
/// </summary>
internal sealed class FakeAggregateTriggerManifestRepository : ManifestRepository
{
    private static readonly Guid ManifestId = Guid.Parse("00000000-0000-0000-0000-0000000a6161");
    private static readonly ManifestRecord Manifest = new(
        ManifestId,
        null,
        [
            JsonSerializer.SerializeToElement(new { type = "dispatcher_mapping", role = "admin", target = "aggregate_trigger", layer = "runtime", action = "execute" }),
            JsonSerializer.SerializeToElement(new { type = "runtime_mapping", runtime_destination = "aggregate_trigger_runtime" }),
        ],
        "active");

    public FakeAggregateTriggerManifestRepository() : base(NullLogger<ManifestRepository>.Instance) { }

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action, CancellationToken ct = default) =>
        Task.FromResult(
            string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(target, "aggregate_trigger", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(layer, "runtime", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action, "execute", StringComparison.OrdinalIgnoreCase)
                ? Manifest
                : null);

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult(manifestId == ManifestId ? Manifest : null);

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(string? statusFilter, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestDetailRecord?>(null);

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

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));
}
