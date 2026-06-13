using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies ManifestDispatcher capability_requirement gate:
/// - Manifest with required_role=admin: user token denied (AUTH_CAPABILITY_DENIED).
/// - Manifest with required_role=admin: admin token allowed.
/// - Manifest without capability_requirement: any authenticated role allowed (non-admin runtime).
/// - admin_runtime manifest without explicit capability_requirement: inferred admin requirement.
/// - target_ref path also enforces capability gate (bypasses role-axis filtering).
/// - request body role is not trusted — JWT-supplied role (request.Role from JWT claim) is used.
/// </summary>
public class ManifestCapabilityGateTests
{
    private static readonly Guid ManifestId = new("cccccccc-dddd-eeee-ffff-000000000099");

    private static ManifestDispatcher BuildDispatcher(ManifestRecord manifest, bool includeAdminRuntime = false)
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var targetOverride = RuntimeExecutorTests.CreateTargetDispatchOverride(topologyRepo);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = new StubSuccessRuntime(),
        };
        if (includeAdminRuntime)
            handlers["admin_runtime"] = new StubSuccessRuntime();
        var repo = new FixedManifestRepository(ManifestId, manifest);
        return new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            targetOverride,
            repo);
    }

    private static ManifestRecord MakeManifest(string? requiredRole)
    {
        var topology = new List<object>
        {
            new { type = "runtime_mapping", runtime_destination = "topology_transform_runtime" },
        };
        if (requiredRole is not null)
            topology.Add(new { type = "capability_requirement", required_role = requiredRole });

        return new ManifestRecord(
            ManifestId: ManifestId,
            RelationRegistryId: null,
            Topology: JsonSerializer.SerializeToElement(topology).EnumerateArray().ToArray(),
            Status: "active");
    }

    private static EndpointRequestDto MakeRequest(string role, string? targetRef = null)
    {
        JsonElement? payload = null;
        if (targetRef is not null)
        {
            payload = JsonSerializer.SerializeToElement(new { target_ref = targetRef });
        }
        return new EndpointRequestDto(
            OperationType: "Search",
            Target: "screen",
            Layer: "screen_list",
            Action: "Search",
            IdOrHubId: null,
            Payload: payload,
            Context: null,
            TriggerKind: "client",
            Role: role);
    }

    // ─── Axes path: user token → admin-required manifest → denied ────────────

    [Fact]
    public async Task DispatchAsync_AdminRequiredManifest_UserRole_ReturnsDenied()
    {
        var manifest = MakeManifest(requiredRole: "admin");
        var dispatcher = BuildDispatcher(manifest);

        var response = await dispatcher.DispatchAsync(MakeRequest(role: "user"));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
    }

    // ─── Axes path: admin token → admin-required manifest → allowed ──────────

    [Fact]
    public async Task DispatchAsync_AdminRequiredManifest_AdminRole_Succeeds()
    {
        var manifest = MakeManifest(requiredRole: "admin");
        var dispatcher = BuildDispatcher(manifest);

        var response = await dispatcher.DispatchAsync(MakeRequest(role: "admin"));

        Assert.True(response.Success);
        Assert.Empty(response.Errors);
    }

    // ─── No capability_requirement: any role allowed ──────────────────────────

    [Fact]
    public async Task DispatchAsync_NoCapabilityRequirement_UserRole_Succeeds()
    {
        var manifest = MakeManifest(requiredRole: null);
        var dispatcher = BuildDispatcher(manifest);

        var response = await dispatcher.DispatchAsync(MakeRequest(role: "user"));

        Assert.True(response.Success);
    }

    // ─── target_ref path: user token → admin-required manifest → denied ──────

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRequiredManifest_UserRole_ReturnsDenied()
    {
        var manifest = MakeManifest(requiredRole: "admin");
        var dispatcher = BuildDispatcher(manifest);
        var targetRef = $"manifest:{ManifestId}:wiring_key";

        var response = await dispatcher.DispatchAsync(MakeRequest(role: "user", targetRef: targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
    }

    // ─── target_ref path: admin token → admin-required manifest → allowed ────

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRequiredManifest_AdminRole_Succeeds()
    {
        var manifest = MakeManifest(requiredRole: "admin");
        var dispatcher = BuildDispatcher(manifest);
        var targetRef = $"manifest:{ManifestId}:wiring_key";

        var response = await dispatcher.DispatchAsync(MakeRequest(role: "admin", targetRef: targetRef));

        Assert.True(response.Success);
    }

    // ─── admin_runtime inference (no explicit capability_requirement) ─────────
    // Manifests routed to admin_runtime are protected even when they lack an explicit
    // capability_requirement entry. ManifestDispatcher infers required_role=admin from
    // runtime_mapping.runtime_destination == "admin_runtime". This closes the target_ref
    // bypass gap for existing admin manifests that don't declare capability_requirement.

    private static ManifestRecord MakeAdminRuntimeManifest() => new ManifestRecord(
        ManifestId: ManifestId,
        RelationRegistryId: null,
        Topology: JsonSerializer.SerializeToElement(new object[]
        {
            new { type = "runtime_mapping", runtime_destination = "admin_runtime" },
        }).EnumerateArray().ToArray(),
        Status: "active");

    [Fact]
    public async Task DispatchAsync_AdminRuntimeManifest_NoCapabilityRequirement_UserRole_ReturnsDenied()
    {
        var manifest = MakeAdminRuntimeManifest();
        var dispatcher = BuildDispatcher(manifest, includeAdminRuntime: true);

        var response = await dispatcher.DispatchAsync(MakeRequest(role: "user"));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
    }

    [Fact]
    public async Task DispatchAsync_AdminRuntimeManifest_NoCapabilityRequirement_AdminRole_Succeeds()
    {
        var manifest = MakeAdminRuntimeManifest();
        var dispatcher = BuildDispatcher(manifest, includeAdminRuntime: true);

        var response = await dispatcher.DispatchAsync(MakeRequest(role: "admin"));

        Assert.True(response.Success);
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_NoCapabilityRequirement_UserRole_ReturnsDenied()
    {
        var manifest = MakeAdminRuntimeManifest();
        var dispatcher = BuildDispatcher(manifest, includeAdminRuntime: true);
        var targetRef = $"manifest:{ManifestId}:wiring_key";

        var response = await dispatcher.DispatchAsync(MakeRequest(role: "user", targetRef: targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_NoCapabilityRequirement_AdminRole_Succeeds()
    {
        var manifest = MakeAdminRuntimeManifest();
        var dispatcher = BuildDispatcher(manifest, includeAdminRuntime: true);
        var targetRef = $"manifest:{ManifestId}:wiring_key";

        var response = await dispatcher.DispatchAsync(MakeRequest(role: "admin", targetRef: targetRef));

        Assert.True(response.Success);
    }
}

/// <summary>Fixed manifest repository for capability gate tests.</summary>
internal sealed class FixedManifestRepository(Guid knownId, ManifestRecord manifest)
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
