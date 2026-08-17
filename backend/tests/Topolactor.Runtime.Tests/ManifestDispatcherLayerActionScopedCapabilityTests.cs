using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Generic (non-team-dashboard-specific) proof for the two mechanisms added by the "team-dashboard
/// Normal ManifestDispatcher production reachability" round (2026-08-17):
/// 1. Layer/action-scoped capability_requirement — docs/design/auth-db-session-credential-ssot.yaml
///    manifest_capability_requirement.layer_action_scoped_override. A synthetic manifest (not
///    team-dashboard's own) proves a scoped-open entry overrides admin_runtime inference for exactly
///    one (layer, action), while an UNSCOPED action on the SAME manifest stays admin-gated.
/// 2. default_screen_read override — docs/design/runtime-orchestration-ssot.yaml
///    ui_projection_render_reachability_contract.default_screen_read_override. A synthetic manifest
///    proves the structural screen-read fallback re-dispatches to a declared real action and returns
///    its Data, instead of synthesizing an empty-Data success, and that a manifest declaring no such
///    entry keeps the old behavior exactly.
///
/// These prove the mechanisms are reusable by ANY manifest/layer/action — team-dashboard's own dd010/
/// dd020 seed rows (db/seed_empty.sql) are one adopter, proven separately by
/// TeamDashboardHubRelationUiProjectionLiveDbTests.cs.
/// </summary>
public class ManifestDispatcherLayerActionScopedCapabilityTests
{
    private static readonly Guid ManifestId = new("dddddddd-eeee-ffff-0000-000000000001");

    private static ManifestDispatcher BuildDispatcher(ManifestRecord manifest, IDispatchableRuntime adminRuntime)
    {
        var targetOverride = RuntimeExecutorTests.CreateTargetDispatchOverride();
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = adminRuntime,
        };
        var repo = new FixedScopedCapabilityManifestRepository(ManifestId, manifest);
        return new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            targetOverride,
            repo);
    }

    private static EndpointRequestDto MakeRequest(string? role, string layer, string action, string? targetRef = null)
    {
        JsonElement? payload = targetRef is not null
            ? JsonSerializer.SerializeToElement(new { target_ref = targetRef })
            : null;
        return new EndpointRequestDto(
            OperationType: action,
            Target: "screen",
            Layer: layer,
            Action: action,
            IdOrHubId: null,
            Payload: payload,
            Context: null,
            TriggerKind: "client",
            Role: role!);
    }

    // ─── layer/action-scoped capability_requirement ───────────────────────────

    // Dispatched via bare axes (no target_ref) so only the shared capability_requirement gate is
    // exercised, isolated from the SEPARATE target_ref dispatcher_mapping.role authorization gate
    // (round 18, TARGET_REF_ROLE_UNAUTHORIZED — already covered by ManifestCapabilityGateTests.cs).
    private static ManifestRecord MakeScopedCapabilityManifest() => new(
        ManifestId: ManifestId,
        RelationRegistryId: null,
        Topology: JsonSerializer.SerializeToElement(new object[]
        {
            new { type = "runtime_mapping", runtime_destination = "admin_runtime" },
            // Scoped-open: only the "get" action is exempted from admin inference.
            new { type = "capability_requirement", layer = "widget", action = "get" },
        }).EnumerateArray().ToArray(),
        Status: "active");

    [Fact]
    public async Task DispatchAsync_ScopedOpenEntry_NonAdminRole_MatchingLayerAction_Succeeds()
    {
        var manifest = MakeScopedCapabilityManifest();
        var dispatcher = BuildDispatcher(manifest, new StubSuccessRuntime());

        var response = await dispatcher.DispatchAsync(MakeRequest("user", "widget", "get"));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }

    [Fact]
    public async Task DispatchAsync_ScopedOpenEntry_DoesNotWiden_UnscopedActionOnSameManifest()
    {
        // "delete" has no scoped capability_requirement entry of its own — it must still fall through
        // to admin_runtime inference (admin-only), proving the scoped-open entry above did not widen
        // the WHOLE manifest.
        var manifest = MakeScopedCapabilityManifest();
        var dispatcher = BuildDispatcher(manifest, new StubSuccessRuntime());

        var response = await dispatcher.DispatchAsync(MakeRequest("user", "widget", "delete"));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
    }

    [Fact]
    public async Task DispatchAsync_ScopedOpenEntry_AdminRole_MatchingLayerAction_StillSucceeds()
    {
        var manifest = MakeScopedCapabilityManifest();
        var dispatcher = BuildDispatcher(manifest, new StubSuccessRuntime());

        var response = await dispatcher.DispatchAsync(MakeRequest("admin", "widget", "get"));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }

    [Fact]
    public void ResolveRequiredRole_ScopedEntryWithRequiredRole_TakesPriorityOverUnscopedExplicitEntry()
    {
        var topology = JsonSerializer.SerializeToElement(new object[]
        {
            new { type = "runtime_mapping", runtime_destination = "admin_runtime" },
            new { type = "capability_requirement", required_role = "admin" },
            new { type = "capability_requirement", layer = "widget", action = "get", required_role = "viewer" },
        }).EnumerateArray().ToArray();

        Assert.Equal("viewer", ManifestDispatcher.ResolveRequiredRole(topology, "widget", "get"));
        // A different (layer, action) on the SAME topology falls back to the unscoped explicit entry.
        Assert.Equal("admin", ManifestDispatcher.ResolveRequiredRole(topology, "widget", "delete"));
        // No layer/action supplied at all (e.g. HubNavigationResolver's own call shape) also falls
        // back to the unscoped explicit entry — scoped entries never participate without both axes.
        Assert.Equal("admin", ManifestDispatcher.ResolveRequiredRole(topology));
    }

    [Fact]
    public void ResolveRequiredRole_NoScopedEntries_BehavesExactlyAsBeforeThisAddition()
    {
        var topology = JsonSerializer.SerializeToElement(new object[]
        {
            new { type = "runtime_mapping", runtime_destination = "admin_runtime" },
        }).EnumerateArray().ToArray();

        Assert.Equal("admin", ManifestDispatcher.ResolveRequiredRole(topology, "widget", "get"));
        Assert.Equal("admin", ManifestDispatcher.ResolveRequiredRole(topology));
    }

    // ─── default_screen_read override ──────────────────────────────────────

    private sealed class FakeAdminRuntimeWithDefaultRead : IDispatchableRuntime
    {
        public Task<EndpointResponseDto> ExecuteAsync(
            EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default)
        {
            if (request.Layer == "widget" && request.Action == "get")
            {
                var data = JsonSerializer.SerializeToElement(new { value = "real-data" });
                return Task.FromResult(new EndpointResponseDto(
                    Success: true,
                    Emission: new Emission(null, null, null, [], data, []),
                    Errors: []));
            }

            return Task.FromResult(new EndpointResponseDto(
                Success: false,
                Emission: null,
                Errors: [new ValidationError("ADMIN_OPERATION_NOT_FOUND", $"no case for {request.Layer}:{request.Action}")]));
        }
    }

    private static ManifestRecord MakeDefaultScreenReadManifest(bool declareDefaultScreenRead) => new(
        ManifestId: ManifestId,
        RelationRegistryId: null,
        Topology: JsonSerializer.SerializeToElement(new object[]
        {
            new { type = "runtime_mapping", runtime_destination = "admin_runtime" },
            new { type = "ui_projection", layoutId = Guid.NewGuid().ToString() },
            declareDefaultScreenRead
                ? new { type = "dispatcher_mapping", role = "admin", target = "manifest", layer = "widget", action = "get", default_screen_read = true }
                : (object)new { type = "dispatcher_mapping", role = "admin", target = "manifest", layer = "widget", action = "get" },
            // Scoped-open on the OUTER structural axes (screen_list, Search) — the request axes
            // ProjectionShell's default entry actually sends — so a non-admin caller reaches the
            // structural fallback at all.
            new { type = "capability_requirement", layer = "screen_list", action = "Search" },
            // Scoped-open on the INNER declared default_screen_read action's own (layer, action) —
            // independently re-validated before the override re-dispatches to it.
            new { type = "capability_requirement", layer = "widget", action = "get" },
        }).EnumerateArray().ToArray(),
        Status: "active");

    [Fact]
    public async Task DispatchAsync_DefaultScreenReadDeclared_StructuralReadReturnsRealData()
    {
        var manifest = MakeDefaultScreenReadManifest(declareDefaultScreenRead: true);
        var dispatcher = BuildDispatcher(manifest, new FakeAdminRuntimeWithDefaultRead());
        var targetRef = $"manifest:{ManifestId}:projection_entry";

        var response = await dispatcher.DispatchAsync(MakeRequest("user", "screen_list", "Search", targetRef));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(response.Emission);
        Assert.NotNull(response.Emission!.Data);
        Assert.Equal("real-data", response.Emission!.Data!.Value.GetProperty("value").GetString());
    }

    [Fact]
    public async Task DispatchAsync_DefaultScreenReadNotDeclared_StructuralReadKeepsEmptyDataFallback()
    {
        var manifest = MakeDefaultScreenReadManifest(declareDefaultScreenRead: false);
        var dispatcher = BuildDispatcher(manifest, new FakeAdminRuntimeWithDefaultRead());
        var targetRef = $"manifest:{ManifestId}:projection_entry";

        var response = await dispatcher.DispatchAsync(MakeRequest("user", "screen_list", "Search", targetRef));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(response.Emission);
        Assert.Null(response.Emission!.Data);
    }

    [Fact]
    public async Task DispatchAsync_DefaultScreenReadDeclared_DeclaredActionOwnCapabilityStillEnforced()
    {
        // The declared default_screen_read action's OWN capability_requirement is independently
        // enforced — remove the scoped-open entry for (widget, get) so it falls back to admin_runtime
        // inference, and confirm a non-admin caller is denied even though the manifest declares
        // default_screen_read=true.
        var manifest = new ManifestRecord(
            ManifestId: ManifestId,
            RelationRegistryId: null,
            Topology: JsonSerializer.SerializeToElement(new object[]
            {
                new { type = "runtime_mapping", runtime_destination = "admin_runtime" },
                new { type = "ui_projection", layoutId = Guid.NewGuid().ToString() },
                new { type = "dispatcher_mapping", role = "admin", target = "manifest", layer = "widget", action = "get", default_screen_read = true },
                // Outer structural axes opened, but the declared default_screen_read action's OWN
                // (widget, get) pair has no scoped-open entry — it must still fall back to
                // admin_runtime inference (admin-only).
                new { type = "capability_requirement", layer = "screen_list", action = "Search" },
            }).EnumerateArray().ToArray(),
            Status: "active");
        var dispatcher = BuildDispatcher(manifest, new FakeAdminRuntimeWithDefaultRead());
        var targetRef = $"manifest:{ManifestId}:projection_entry";

        var response = await dispatcher.DispatchAsync(MakeRequest("user", "screen_list", "Search", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
    }
}

/// <summary>Fixed manifest repository for layer/action-scoped capability + default_screen_read tests.</summary>
internal sealed class FixedScopedCapabilityManifestRepository(Guid knownId, ManifestRecord manifest)
    : ManifestRepository(NullLogger<ManifestRepository>.Instance)
{
    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult(manifestId == knownId ? manifest : null);

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action, CancellationToken ct = default) =>
        Task.FromResult<ManifestRecord?>(manifest);

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(
        string? statusFilter, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestDetailRecord?>(null);

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
        Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
        Guid manifestId, Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
        Guid manifestId, IReadOnlySet<string> allowedRuntimeDestinations, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedLifecycle();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
        Guid manifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedLifecycle();

    public override Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
        string? statusFilter, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.EmptyPromotionList(statusFilter, ct);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
        Guid manifestId, JsonElement promotionEntry, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();
}
