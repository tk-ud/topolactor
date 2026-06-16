using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimePackageWiringTests
{
    [Fact]
    public async Task ExecuteDataAsync_GetPackageWiring_ReturnsWiringDto()
    {
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var wiring = new AdminPackageWiringDto(
            wiringId.ToString(),
            "admin:ui-builder:button:wiring",
            "button",
            "route",
            null);
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(wiring));

        var vector = new OperationVector(
            "admin", "ui_topology", "get_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new { packageId = packageId.ToString() }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal(wiringId.ToString(), data!.Value.GetProperty("wiringId").GetString());
        Assert.Equal("route", data.Value.GetProperty("targetSurface").GetString());
    }

    [Fact]
    public async Task ExecuteDataAsync_GetPackageWiring_WhenMissing_ReturnsNotFound()
    {
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(null));

        var vector = new OperationVector(
            "admin", "ui_topology", "get_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new { packageId = Guid.NewGuid().ToString() }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.Equal("PACKAGE_WIRING_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_UpdatePackageWiring_ReturnsOkWithRefreshedWiring()
    {
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var initial = new AdminPackageWiringDto(
            wiringId.ToString(),
            "admin:ui-builder:button:wiring",
            "button",
            "route",
            null);
        var refreshed = initial with { WiringKind = "evt", TargetSurface = "ui", TargetRef = "manifest:demo" };
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(initial, refreshed));

        var vector = new OperationVector(
            "admin", "ui_topology", "update_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                packageId = packageId.ToString(),
                wiringId = wiringId.ToString(),
                wiringKind = "evt",
                targetSurface = "ui",
                targetRef = "manifest:demo",
            }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("evt", data.Value.GetProperty("wiring").GetProperty("wiringKind").GetString());
        Assert.Equal("ui", data.Value.GetProperty("wiring").GetProperty("targetSurface").GetString());
    }

    [Fact]
    public async Task ExecuteDataAsync_UpdatePackageWiring_MapsRepositoryError()
    {
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(
            new AdminPackageWiringDto(Guid.NewGuid().ToString(), "k", "button", "route", null),
            updateError: new ValidationError("PACKAGE_WIRING_NOT_FOUND", "not linked")));

        var vector = new OperationVector(
            "admin", "ui_topology", "update_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                packageId = Guid.NewGuid().ToString(),
                wiringId = Guid.NewGuid().ToString(),
                wiringKind = "button",
                targetSurface = "route",
            }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PACKAGE_WIRING_NOT_FOUND", error!.Code);
    }


    [Fact]
    public async Task ExecuteDataAsync_ListExternalPortAuthoringCandidates_ReturnsDbDerivedCandidateShape()
    {
        var candidate = new ExternalPortAuthoringCandidateDto(
            PortId: Guid.NewGuid().ToString(),
            PortKind: "hook_port",
            ProviderKind: "generic_provider",
            CredentialKind: "external",
            ReferenceKey: "credential.ref",
            RequiredByBundle: "consumer.bundle",
            ConsumerBundleBinding: null,
            UrlOrEnvReference: null,
            HookPath: "/hooks/generic",
            RouteKey: "generic.route",
            TargetRef: "external-port:hook_port:port-id:generic.route");
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(null, candidates: new[] { candidate }));

        var vector = new OperationVector(
            "admin", "ui_topology", "list_external_port_authoring_candidates", null, "admin",
            null,
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        var first = data!.Value.GetProperty("candidates")[0];
        Assert.Equal("hook_port", first.GetProperty("portKind").GetString());
        Assert.Equal("generic_provider", first.GetProperty("providerKind").GetString());
        Assert.Equal("external", first.GetProperty("credentialKind").GetString());
        Assert.Equal("consumer.bundle", first.GetProperty("requiredByBundle").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("consumerBundleBinding").ValueKind);
        Assert.Equal("external-port:hook_port:port-id:generic.route", first.GetProperty("targetRef").GetString());
        Assert.False(first.TryGetProperty("secret", out _));
        Assert.False(first.TryGetProperty("password", out _));
    }

    // ─── Manifest surface wiring key validation ───────────────────────────────

    [Fact]
    public async Task ExecuteDataAsync_UpdatePackageWiring_ManifestSurface_MissingWiringKey_ReturnsError()
    {
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var wiring = new AdminPackageWiringDto(
            wiringId.ToString(), "wk", "navigation", "manifest", null);
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(wiring));

        // targetRef = manifest:<uuid> without wiringKey — must be rejected
        var vector = new OperationVector(
            "admin", "ui_topology", "update_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                packageId = packageId.ToString(),
                wiringId = wiringId.ToString(),
                wiringKind = "navigation",
                targetSurface = "manifest",
                targetRef = $"manifest:{packageId}",
            }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.Equal("MANIFEST_WIRING_KEY_MISSING", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_UpdatePackageWiring_ManifestSurface_EmptyWiringKeyInRef_ReturnsError()
    {
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var wiring = new AdminPackageWiringDto(
            wiringId.ToString(), "wk", "navigation", "manifest", null);
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(wiring));

        // targetRef = manifest:<uuid>: with empty key after colon — must be rejected
        var vector = new OperationVector(
            "admin", "ui_topology", "update_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                packageId = packageId.ToString(),
                wiringId = wiringId.ToString(),
                wiringKind = "navigation",
                targetSurface = "manifest",
                targetRef = $"manifest:{packageId}:",
            }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.Equal("MANIFEST_WIRING_KEY_MISSING", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_UpdatePackageWiring_ManifestSurface_ManifestNotFound_ReturnsError()
    {
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var nonExistentManifestId = Guid.NewGuid();
        var wiring = new AdminPackageWiringDto(
            wiringId.ToString(), "wk", "navigation", "manifest", null);
        var repo = new InMemoryManifestAdminRepository(); // no manifests seeded
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(wiring), repo);

        var vector = new OperationVector(
            "admin", "ui_topology", "update_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                packageId = packageId.ToString(),
                wiringId = wiringId.ToString(),
                wiringKind = "navigation",
                targetSurface = "manifest",
                targetRef = $"manifest:{nonExistentManifestId}:sc.name.eq",
            }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.Equal("MANIFEST_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_UpdatePackageWiring_ManifestSurface_NoScreenDataShape_ReturnsError()
    {
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new { type = "dispatcher_mapping", role = "admin" }),
        };
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(new ManifestDetailRecord(manifestId, null, topology, "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var wiring = new AdminPackageWiringDto(
            wiringId.ToString(), "wk", "navigation", "manifest", null);
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(wiring), repo);

        var vector = new OperationVector(
            "admin", "ui_topology", "update_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                packageId = packageId.ToString(),
                wiringId = wiringId.ToString(),
                wiringKind = "navigation",
                targetSurface = "manifest",
                targetRef = $"manifest:{manifestId}:sc.name.eq",
            }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.Equal("SCREEN_DATA_SHAPE_UNRESOLVED", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_UpdatePackageWiring_ManifestSurface_NoScreenReadQueryWiring_ReturnsError()
    {
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        // screen_data_shape present but no screenReadQueryWiring
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new { type = "screen_data_shape", tableRef = "users" }),
        };
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(new ManifestDetailRecord(manifestId, null, topology, "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var wiring = new AdminPackageWiringDto(
            wiringId.ToString(), "wk", "navigation", "manifest", null);
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(wiring), repo);

        var vector = new OperationVector(
            "admin", "ui_topology", "update_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                packageId = packageId.ToString(),
                wiringId = wiringId.ToString(),
                wiringKind = "navigation",
                targetSurface = "manifest",
                targetRef = $"manifest:{manifestId}:sc.name.eq",
            }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.Equal("SCREEN_DATA_SHAPE_UNRESOLVED", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_UpdatePackageWiring_ManifestSurface_WiringKeyNotInCandidates_ReturnsError()
    {
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new
            {
                type = "screen_data_shape",
                screenReadQueryWiring = new
                {
                    searchConditions = new
                    {
                        bindings = new[] { new { wiringKey = "sc.name.eq", column = "name", @operator = "=" } },
                    },
                },
            }),
        };
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(new ManifestDetailRecord(manifestId, null, topology, "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var wiring = new AdminPackageWiringDto(
            wiringId.ToString(), "wk", "navigation", "manifest", null);
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(wiring), repo);

        var vector = new OperationVector(
            "admin", "ui_topology", "update_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                packageId = packageId.ToString(),
                wiringId = wiringId.ToString(),
                wiringKind = "navigation",
                targetSurface = "manifest",
                targetRef = $"manifest:{manifestId}:unknown.key",
            }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.Equal("MANIFEST_WIRING_KEY_UNRESOLVED", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_UpdatePackageWiring_ManifestSurface_ValidWiringKey_Succeeds()
    {
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        // Seed a manifest with a matching wiringKey candidate
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new
            {
                type = "screen_data_shape",
                screenReadQueryWiring = new
                {
                    searchConditions = new
                    {
                        bindings = new[] { new { wiringKey = "sc.name.eq", column = "name", @operator = "=" } },
                    },
                },
            }),
        };
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(new ManifestDetailRecord(manifestId, null, topology, "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var initial = new AdminPackageWiringDto(
            wiringId.ToString(), "wk", "navigation", "manifest", null);
        var refreshed = initial with
        {
            TargetSurface = "manifest",
            TargetRef = $"manifest:{manifestId}:sc.name.eq",
        };
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(initial, refreshed), repo);

        var vector = new OperationVector(
            "admin", "ui_topology", "update_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                packageId = packageId.ToString(),
                wiringId = wiringId.ToString(),
                wiringKind = "navigation",
                targetSurface = "manifest",
                targetRef = $"manifest:{manifestId}:sc.name.eq",
            }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task ExecuteDataAsync_UpdatePackageWiring_RouteSurface_NoWiringKeyRequired()
    {
        var packageId = Guid.NewGuid();
        var wiringId = Guid.NewGuid();
        var wiring = new AdminPackageWiringDto(
            wiringId.ToString(), "wk", "navigation", "route", "route:customer-management");
        var refreshed = wiring with { TargetRef = "route:demo" };
        var runtime = CreateRuntime(new WiringStubUiTopologyRepository(wiring, refreshed));

        var vector = new OperationVector(
            "admin", "ui_topology", "update_package_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                packageId = packageId.ToString(),
                wiringId = wiringId.ToString(),
                wiringKind = "navigation",
                targetSurface = "route",
                targetRef = "route:demo",
            }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
    }

    // ─── list_screen_read_query_wiring ────────────────────────────────────────

    [Fact]
    public async Task ExecuteDataAsync_ListScreenReadQueryWiring_WithCandidates_ReturnsCandidates()
    {
        var manifestId = Guid.NewGuid();
        var screenDataShape = new
        {
            type = "screen_data_shape",
            screenReadQueryWiring = new
            {
                searchConditions = new
                {
                    bindings = new[]
                    {
                        new { wiringKey = "sc.name.eq", column = "name", @operator = "=" },
                    },
                },
                displayColumns = new
                {
                    bindings = new[]
                    {
                        new { wiringKey = "dc.salary", column = "salary" },
                    },
                },
            },
        };
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(screenDataShape),
        };
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, topology, "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var runtime = CreateRuntime(repo: repo);

        var vector = new OperationVector(
            "admin", "manifest", "list_screen_read_query_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        var candidates = data!.Value.GetProperty("candidates");
        Assert.Equal(JsonValueKind.Array, candidates.ValueKind);
        Assert.True(candidates.GetArrayLength() >= 2);
        Assert.Contains(
            candidates.EnumerateArray(),
            c => c.GetProperty("wiringKey").GetString() == "sc.name.eq");
        Assert.Contains(
            candidates.EnumerateArray(),
            c => c.GetProperty("wiringKey").GetString() == "dc.salary");
    }

    [Fact]
    public async Task ExecuteDataAsync_ListScreenReadQueryWiring_ManifestNotFound_ReturnsError()
    {
        var repo = new InMemoryManifestAdminRepository();
        var runtime = CreateRuntime(repo: repo);

        var vector = new OperationVector(
            "admin", "manifest", "list_screen_read_query_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new { manifestId = Guid.NewGuid().ToString() }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.Equal("MANIFEST_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_ListScreenReadQueryWiring_NoScreenDataShape_ReturnsEmptyCandidates()
    {
        var manifestId = Guid.NewGuid();
        // Topology without screen_data_shape entry
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new { type = "dispatcher_mapping", role = "admin" }),
        };
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, topology, "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var runtime = CreateRuntime(repo: repo);

        var vector = new OperationVector(
            "admin", "manifest", "list_screen_read_query_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        var candidates = data!.Value.GetProperty("candidates");
        Assert.Equal(0, candidates.GetArrayLength());
    }

    [Fact]
    public async Task ExecuteDataAsync_ListScreenReadQueryWiring_ScreenDataShapeWithoutWiring_ReturnsEmptyCandidates()
    {
        var manifestId = Guid.NewGuid();
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new { type = "screen_data_shape", tableRef = "users" }),
        };
        var repo = new InMemoryManifestAdminRepository();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, topology, "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var runtime = CreateRuntime(repo: repo);

        var vector = new OperationVector(
            "admin", "manifest", "list_screen_read_query_wiring", null, "admin",
            JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal(0, data!.Value.GetProperty("candidates").GetArrayLength());
    }

    private static AdminRuntime CreateRuntime(
        UiTopologyRepository? uiRepo = null,
        InMemoryManifestAdminRepository? repo = null)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        uiRepo ??= new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo, null,
            null, null, null, null, repo);
    }

    private sealed class WiringStubUiTopologyRepository : UiTopologyRepository
    {
        private AdminPackageWiringDto? _wiring;
        private readonly AdminPackageWiringDto? _afterUpdate;
        private readonly ValidationError? _updateError;
        private readonly IReadOnlyList<ExternalPortAuthoringCandidateDto> _candidates;

        public WiringStubUiTopologyRepository(
            AdminPackageWiringDto? wiring,
            AdminPackageWiringDto? afterUpdate = null,
            ValidationError? updateError = null,
            IReadOnlyList<ExternalPortAuthoringCandidateDto>? candidates = null)
            : base(NullLogger<UiTopologyRepository>.Instance, "test-double")
        {
            _wiring = wiring;
            _afterUpdate = afterUpdate ?? wiring;
            _updateError = updateError;
            _candidates = candidates ?? Array.Empty<ExternalPortAuthoringCandidateDto>();
        }

        public override Task<IReadOnlyList<ExternalPortAuthoringCandidateDto>> ListExternalPortAuthoringCandidatesAsync(CancellationToken ct = default)
            => Task.FromResult(_candidates);

        public override Task<AdminPackageWiringDto?> GetPackageWiringAsync(Guid packageId, CancellationToken ct = default)
            => Task.FromResult(_wiring);

        public override Task<ValidationError?> UpdatePackageWiringAsync(
            Guid packageId,
            Guid wiringId,
            string wiringKind,
            string targetSurface,
            string? targetRef,
            CancellationToken ct = default)
        {
            if (_updateError is not null) return Task.FromResult<ValidationError?>(_updateError);
            _wiring = _afterUpdate;
            return Task.FromResult<ValidationError?>(null);
        }
    }
}
