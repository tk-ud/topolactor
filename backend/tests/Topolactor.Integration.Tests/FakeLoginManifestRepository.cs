using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Integration.Tests;

internal sealed class FakeLoginManifestRepository : ManifestRepository
{
    private static readonly ManifestRecord LoginManifest = new(
        AuthManifestIds.UserLoginManifestId,
        null,
        [
            System.Text.Json.JsonDocument.Parse(
                """{"type":"auth_action_binding","action":"login","runtime_destination":"auth_runtime","realm":"user","audience":"user_app"}""").RootElement,
            System.Text.Json.JsonDocument.Parse(
                """{"type":"ui_projection_mapping","surface":"login_form","fields":["username","password"]}""").RootElement,
        ],
        "active");

    public FakeLoginManifestRepository() : base(NullLogger<ManifestRepository>.Instance) { }

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult(manifestId == LoginManifest.ManifestId ? LoginManifest : null);

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action, CancellationToken ct = default) =>
        Task.FromResult<ManifestRecord?>(null);

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(string? statusFilter, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestDetailRecord?>(null);

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
        Guid? relationRegistryId, IReadOnlyList<System.Text.Json.JsonElement> topology, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
        Guid manifestId, Guid? relationRegistryId, IReadOnlyList<System.Text.Json.JsonElement> topology, CancellationToken ct = default) =>
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
        Guid manifestId, System.Text.Json.JsonElement promotionEntry, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, System.Text.Json.JsonElement entryBody, CancellationToken ct = default) =>
        Task.FromResult<(ManifestDetailRecord?, ValidationError?)>((null, new ValidationError("STUB", "stub")));
}
