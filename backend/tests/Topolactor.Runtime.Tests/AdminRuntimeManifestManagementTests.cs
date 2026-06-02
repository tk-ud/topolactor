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

    [Fact]
    public async Task AssignScreenDataShape_PersistsStructuredFields()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, ValidTopology("admin", "tgt", "screen_list", "Read", "topology_transform_runtime"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = manifestId.ToString(),
            tableRef = "my_table",
            searchKeyColumns = new[] { "col_a", "col_b" },
            aggregationKey = "col_a",
            displayColumns = new[] { "col_a", "col_b", "col_c" },
            columns = new[] { new { name = "col_a", dataType = "text", nullable = true } },
            relationIntents = new[] { new { joinTableRef = "other_table", localKey = "id", remoteKey = "ref_id" } },
            initialDataRows = new[] { new Dictionary<string, string> { ["col_a"] = "v1" } },
            screenOperationKind = "list",
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        var rawJson = data.Value.GetProperty("topologyRawJson").GetString() ?? "[]";
        var entries = JsonSerializer.Deserialize<JsonElement[]>(rawJson)!;
        var shapeEntry = entries.First(e =>
            e.TryGetProperty("type", out var t) && t.GetString() == "screen_data_shape");

        Assert.Equal("my_table", shapeEntry.GetProperty("tableRef").GetString());
        var searchKeys = shapeEntry.GetProperty("searchKeyColumns").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Contains("col_a", searchKeys);
        Assert.Contains("col_b", searchKeys);
        Assert.Equal("col_a", shapeEntry.GetProperty("aggregationKey").GetString());
        var displayCols = shapeEntry.GetProperty("displayColumns").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Contains("col_a", displayCols);
        var relationIntents = shapeEntry.GetProperty("relationIntents").EnumerateArray().ToList();
        Assert.Single(relationIntents);
        Assert.Equal("other_table", relationIntents[0].GetProperty("joinTableRef").GetString());
        var initialRows = shapeEntry.GetProperty("initialDataRows").EnumerateArray().ToList();
        Assert.Single(initialRows);
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

/// <summary>
/// Tests for ManifestCanonicalProjection.ExtractTableRef and static helpers.
/// topology.physical_tables mismatch explicit failure is covered in integration tests (requires DB).
/// This class covers the static helpers that can be unit-tested without a DB.
/// </summary>
public class ManifestCanonicalProjectionUnitTests
{
    [Fact]
    public void ExtractTableRef_ReturnsTableRef_WhenPresent()
    {
        var entry = JsonSerializer.SerializeToElement(new
        {
            type = "screen_data_shape",
            tableRef = "my_table",
        });
        Assert.Equal("my_table", ManifestCanonicalProjection.ExtractTableRef(entry));
    }

    [Fact]
    public void ExtractTableRef_FallsBackToDbTableName_WhenTableRefAbsent()
    {
        var entry = JsonSerializer.SerializeToElement(new
        {
            type = "screen_data_shape",
            dbTableName = "legacy_table",
        });
        Assert.Equal("legacy_table", ManifestCanonicalProjection.ExtractTableRef(entry));
    }

    [Fact]
    public void ExtractTableRef_ReturnsNull_WhenBothAbsent()
    {
        var entry = JsonSerializer.SerializeToElement(new { type = "screen_data_shape" });
        Assert.Null(ManifestCanonicalProjection.ExtractTableRef(entry));
    }

    [Fact]
    public void ExtractScreenOperationKind_ReturnsKind_WhenPresent()
    {
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new { type = "screen_data_shape", screenOperationKind = "list" }),
        };
        Assert.Equal("list", ManifestCanonicalProjection.ExtractScreenOperationKind(topology));
    }

    [Fact]
    public void ExtractScreenOperationKind_ReturnsNull_WhenAbsent()
    {
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new { type = "screen_data_shape" }),
        };
        Assert.Null(ManifestCanonicalProjection.ExtractScreenOperationKind(topology));
    }

    /// <summary>
    /// Regression: TryProjectWiringAsync is now documented to return WIRING_TABLE_REF_NOT_FOUND
    /// when tableRef is present but not in topology.physical_tables. This contract is verified
    /// in live DB integration tests. Unit-level: verify that ExtractTableRef correctly identifies
    /// the field that triggers the check.
    /// </summary>
    [Fact]
    public void WiringTableRefMismatch_Contract_ExtractTableRefIdentifiesCheckTarget()
    {
        var shapeWithRef = JsonSerializer.SerializeToElement(new
        {
            type = "screen_data_shape",
            tableRef = "unregistered_table",
        });
        var tableRef = ManifestCanonicalProjection.ExtractTableRef(shapeWithRef);
        // When tableRef is non-null/non-empty, TryProjectWiringAsync will query topology.physical_tables.
        // If not found, it returns WIRING_TABLE_REF_NOT_FOUND (explicit failure, not silent skip).
        Assert.Equal("unregistered_table", tableRef);
        Assert.False(string.IsNullOrWhiteSpace(tableRef),
            "Non-empty tableRef triggers physical_tables lookup — mismatch must fail explicitly");
    }
}
