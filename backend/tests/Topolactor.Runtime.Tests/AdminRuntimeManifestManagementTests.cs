using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class AdminRuntimeManifestManagementTests
{
    private static readonly HashSet<string> KnownDestinations = new(StringComparer.OrdinalIgnoreCase)
    {
        "topology_transform_runtime",
        "admin_runtime",
        "sse_projection_runtime",
    };

    [Fact]
    public async Task ManifestList_ReturnsRows()
    {
        var repo = new InMemoryManifestAdminRepository();
        var topology = ValidTopology("admin", "admin", "manifest", "list", "admin_runtime");
        repo.Seed(new ManifestDetailRecord(
            Guid.NewGuid(), null, topology, "active", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "list", null, "admin", null, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal(1, data.Value.GetArrayLength());
    }

    [Fact]
    public async Task Validate_MissingDispatcherMapping_ReturnsBlockingError()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new
            {
                type = "runtime_mapping",
                runtime_destination = "admin_runtime",
            }),
        };
        repo.Seed(new ManifestDetailRecord(manifestId, null, topology, "draft", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "validate", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.False(data.Value.GetProperty("valid").GetBoolean());
        Assert.Contains(
            data.Value.GetProperty("issues").EnumerateArray(),
            i => i.GetProperty("code").GetString() == "DISPATCHER_MAPPING_MISSING");
    }

    [Fact]
    public async Task Validate_UnknownRuntimeDestination_ReturnsBlockingError()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, ValidTopology("admin", "admin", "manifest", "validate", "unknown_runtime"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "validate", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.False(data!.Value.GetProperty("valid").GetBoolean());
        Assert.Contains(
            data.Value.GetProperty("issues").EnumerateArray(),
            i => i.GetProperty("code").GetString() == "RUNTIME_DESTINATION_UNKNOWN");
    }

    [Fact]
    public async Task Validate_ProjectionDefinitionMissingConstructorKey_ReturnsBlockingError()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        var topology = ManifestTopologyValidator.BuildTopology(
            "admin", "admin", "manifest", "validate", "admin_runtime",
            JsonSerializer.SerializeToElement(new { outputKind = "form_inputs", packageIds = Array.Empty<string>() }));
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, topology, "draft", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "validate", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.False(data!.Value.GetProperty("valid").GetBoolean());
        Assert.Contains(
            data.Value.GetProperty("issues").EnumerateArray(),
            i => i.GetProperty("code").GetString() == "PROJECTION_CONSTRUCTOR_KEY_MISSING");
    }

    [Fact]
    public async Task Promote_DraftToActive_Succeeds()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, ValidTopology("admin", "admin", "manifest", "promote", "admin_runtime"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "promote", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("active", data.Value.GetProperty("status").GetString());
        Assert.Single(repo.ProjectedTopologyManifests);
        Assert.Equal(manifestId, repo.ProjectedTopologyManifests[0].TopologyManifestId);
    }

    [Fact]
    public async Task CreateDraft_WithScreenOperationKind_DerivesAxes()
    {
        var repo = new InMemoryManifestAdminRepository();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            screenOperationKind = "search",
            role = "",
            target = "",
            layer = "",
            action = "",
            runtimeDestination = "",
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "create_draft", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal("Search", data!.Value.GetProperty("summary").GetProperty("dispatcherMapping")
            .GetProperty("action").GetString());
    }

    [Fact]
    public async Task Promote_ConflictingActiveAxes_Fails()
    {
        var repo = new InMemoryManifestAdminRepository();
        var activeId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var topology = ValidTopology("admin", "admin", "manifest", "promote", "admin_runtime");
        repo.Seed(new ManifestDetailRecord(activeId, null, topology, "active", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        repo.Seed(new ManifestDetailRecord(draftId, null, topology, "draft", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = draftId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "promote", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.False(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("MANIFEST_ACTIVE_AXES_CONFLICT", data.Value.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Deprecate_ActiveManifest_Succeeds()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, ValidTopology("admin", "admin", "manifest", "deprecate", "admin_runtime"), "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { manifestId = manifestId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "deprecate", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal("deprecated", data.Value.GetProperty("status").GetString());
    }

    private static IReadOnlyList<JsonElement> ValidTopology(
        string role, string target, string layer, string action, string runtimeDestination) =>
        ManifestTopologyValidator.BuildTopology(role, target, layer, action, runtimeDestination, null);

    private static AdminRuntime CreateRuntime(InMemoryManifestAdminRepository manifestRepo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(
            NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            null,
            null,
            null,
            null,
            null,
            manifestRepo);
    }
}
