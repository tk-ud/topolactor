using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
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
            logicalTables = new[]
            {
                new
                {
                    tableName = "my_table",
                    columns = new[] { new { name = "id", dataType = "text", nullable = false } },
                },
                new
                {
                    tableName = "other_table",
                    columns = new[] { new { name = "ref_id", dataType = "text", nullable = false } },
                },
            },
            relationIntents = new[] { new { joinTableRef = "other_table", localTableRef = "my_table", localKey = "id", remoteKey = "ref_id" } },
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

    [Fact]
    public async Task AssignScreenDataShape_Persists_SearchConditions_HavingConditions_DisplayColumnMode()
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
            searchConditions = new object[]
            {
                new { column = "col_a", @operator = "=", value = "test", logicalConnector = "and" },
                new { column = "col_b", @operator = "between", value = "1", valueTo = "10" },
                new { column = "col_c", @operator = "in", values = new[] { "x", "y" } },
                new { column = "col_d", @operator = "is null" },
            },
            havingConditions = new object[]
            {
                new { column = "salary", function = "sum", @operator = ">", value = "1000" },
            },
            displayColumnMode = "none",
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null), default);

        Assert.Null(error);
        var rawJson = data!.Value.GetProperty("topologyRawJson").GetString() ?? "[]";
        var shapeEntry = JsonSerializer.Deserialize<JsonElement[]>(rawJson)!
            .First(e => e.TryGetProperty("type", out var t) && t.GetString() == "screen_data_shape");

        var searchConds = shapeEntry.GetProperty("searchConditions").EnumerateArray().ToList();
        Assert.Equal(4, searchConds.Count);
        Assert.Equal("col_a", searchConds[0].GetProperty("column").GetString());
        Assert.Equal("=", searchConds[0].GetProperty("operator").GetString());
        Assert.Equal("test", searchConds[0].GetProperty("value").GetString());
        Assert.Equal("between", searchConds[1].GetProperty("operator").GetString());
        Assert.Equal("10", searchConds[1].GetProperty("valueTo").GetString());
        Assert.Equal("in", searchConds[2].GetProperty("operator").GetString());
        Assert.Equal(2, searchConds[2].GetProperty("values").GetArrayLength());
        Assert.Equal("is null", searchConds[3].GetProperty("operator").GetString());

        var havingConds = shapeEntry.GetProperty("havingConditions").EnumerateArray().ToList();
        Assert.Single(havingConds);
        Assert.Equal("salary", havingConds[0].GetProperty("column").GetString());
        Assert.Equal("sum", havingConds[0].GetProperty("function").GetString());
        Assert.Equal(">", havingConds[0].GetProperty("operator").GetString());
        Assert.Equal("1000", havingConds[0].GetProperty("value").GetString());

        Assert.Equal("none", shapeEntry.GetProperty("displayColumnMode").GetString());
    }

    private static IReadOnlyList<JsonElement> ValidTopology(
        string role, string target, string layer, string action, string runtimeDestination) =>
        ManifestTopologyValidator.BuildTopology(role, target, layer, action, runtimeDestination, null);

    [Fact]
    public async Task AssignScreenDataShape_Rejects_Unresolved_Active_Remote_Table()
    {
        var repo = new InMemoryManifestAdminRepository();
        var draftId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            draftId, null, DraftTopologyWithLogicalTables("my_table", "id"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        repo.Seed(new ManifestDetailRecord(
            activeId, null, DraftTopologyWithLogicalTables("remote_table", "ref_id"), "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = draftId.ToString(),
            relationIntents = new[]
            {
                new
                {
                    localTableRef = "my_table",
                    joinTableRef = "missing_on_remote",
                    localKey = "id",
                    remoteKey = "ref_id",
                    remoteManifestId = activeId.ToString(),
                },
            },
        });

        var (_, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null),
            default);

        Assert.NotNull(error);
        Assert.Equal("RELATION_REMOTE_TABLE_UNRESOLVED", error!.Code);
    }

    [Fact]
    public async Task AssignScreenDataShape_Persists_Active_Remote_Manifest_Reference()
    {
        var repo = new InMemoryManifestAdminRepository();
        var draftId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            draftId, null, DraftTopologyWithLogicalTables("my_table", "id"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        repo.Seed(new ManifestDetailRecord(
            activeId, null, DraftTopologyWithLogicalTables("remote_table", "ref_id"), "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = draftId.ToString(),
            relationIntents = new[]
            {
                new
                {
                    localTableRef = "my_table",
                    joinTableRef = "remote_table",
                    localKey = "id",
                    remoteKey = "ref_id",
                    remoteManifestId = activeId.ToString(),
                },
            },
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null),
            default);

        Assert.Null(error);
        var rawJson = data!.Value.GetProperty("topologyRawJson").GetString() ?? "[]";
        var shapeEntry = JsonSerializer.Deserialize<JsonElement[]>(rawJson)!
            .First(e => e.TryGetProperty("type", out var t) && t.GetString() == "screen_data_shape");
        var rel = shapeEntry.GetProperty("relationIntents")[0];
        Assert.Equal(activeId.ToString(), rel.GetProperty("remoteManifestId").GetString());
    }

    [Fact]
    public async Task ListRelationshipRemoteTargets_Returns_Active_Manifests_With_Tables()
    {
        var repo = new InMemoryManifestAdminRepository();
        var draftId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            draftId, null, DraftTopologyWithLogicalTables("t1", "c1"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        repo.Seed(new ManifestDetailRecord(
            activeId, null, DraftTopologyWithLogicalTables("remote_t", "rk"), "active",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new { excludeManifestId = draftId.ToString() });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "list_relationship_remote_targets", null, "admin", payload, null),
            default);

        Assert.Null(error);
        var items = data!.Value.EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal(activeId.ToString(), items[0].GetProperty("manifestId").GetString());
        Assert.Equal("remote_t", items[0].GetProperty("logicalTables")[0].GetProperty("tableName").GetString());
    }

    private static IReadOnlyList<JsonElement> DraftTopologyWithLogicalTables(
        string tableName, string columnName)
    {
        var list = ValidTopology("admin", "tgt", "screen_list", "Read", "topology_transform_runtime").ToList();
        var shape = JsonSerializer.SerializeToElement(new
        {
            type = "screen_data_shape",
            logicalTables = new[]
            {
                new
                {
                    tableName,
                    columns = new[] { new { name = columnName, dataType = "text", nullable = true } },
                },
            },
        });
        list.Add(shape);
        return list;
    }

    [Fact]
    public async Task AssignScreenDataShape_Persists_EnumGroupId_On_Column()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, ValidTopology("admin", "tgt", "screen_list", "Read", "topology_transform_runtime"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var enumRepo = InMemoryEnumDictionaryRepository.WithDemoSeed();
        var runtime = CreateRuntime(repo, enumRepo);
        var groupId = InMemoryEnumDictionaryRepository.DemoGroupId.ToString();
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = manifestId.ToString(),
            logicalTables = new[]
            {
                new
                {
                    tableName = "my_table",
                    columns = new[]
                    {
                        new { name = "status", dataType = "text", nullable = true, enumGroupId = groupId },
                    },
                },
            },
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null), default);

        Assert.Null(error);
        var rawJson = data!.Value.GetProperty("topologyRawJson").GetString() ?? "[]";
        var shapeEntry = JsonSerializer.Deserialize<JsonElement[]>(rawJson)!
            .First(e => e.TryGetProperty("type", out var t) && t.GetString() == "screen_data_shape");
        var col = shapeEntry.GetProperty("logicalTables")[0].GetProperty("columns")[0];
        Assert.Equal(groupId, col.GetProperty("enumGroupId").GetString());
    }

    [Fact]
    public async Task AssignScreenDataShape_Rejects_Unknown_EnumGroupId()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, ValidTopology("admin", "tgt", "screen", "Read", "topology_transform_runtime"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo, InMemoryEnumDictionaryRepository.WithDemoSeed());
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = manifestId.ToString(),
            logicalTables = new[]
            {
                new
                {
                    tableName = "t",
                    columns = new[]
                    {
                        new
                        {
                            name = "status",
                            dataType = "text",
                            nullable = true,
                            enumGroupId = Guid.NewGuid().ToString(),
                        },
                    },
                },
            },
        });

        var (_, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null), default);

        Assert.NotNull(error);
        Assert.Equal("ENUM_GROUP_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task AssignScreenDataShape_Rejects_Empty_EnumGroupItems()
    {
        var emptyGroupId = Guid.Parse("33333333-3333-3333-3333-333333333301");
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, ValidTopology("admin", "tgt", "screen", "Read", "topology_transform_runtime"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo, InMemoryEnumDictionaryRepository.WithEmptyGroup(emptyGroupId));
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = manifestId.ToString(),
            logicalTables = new[]
            {
                new
                {
                    tableName = "t",
                    columns = new[]
                    {
                        new
                        {
                            name = "status",
                            dataType = "text",
                            nullable = true,
                            enumGroupId = emptyGroupId.ToString(),
                        },
                    },
                },
            },
        });

        var (_, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null), default);

        Assert.NotNull(error);
        Assert.Equal("ENUM_GROUP_ITEMS_EMPTY", error!.Code);
    }

    [Fact]
    public async Task EnumDictionary_ListGroups_Returns_Seeded_Group()
    {
        var enumRepo = InMemoryEnumDictionaryRepository.WithDemoSeed();
        var runtime = CreateRuntime(new InMemoryManifestAdminRepository(), enumRepo);
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "enum_dictionary", "list_groups", null, "admin", null, null), default);

        Assert.Null(error);
        Assert.Equal(2, data!.Value.GetArrayLength());
        var groupIds = data.Value.EnumerateArray()
            .Select(e => e.GetProperty("groupId").GetString())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(InMemoryEnumDictionaryRepository.DemoGroupId.ToString(), groupIds);
        Assert.Contains(InMemoryEnumDictionaryRepository.UserStatusGroupId.ToString(), groupIds);
    }

    private static AdminRuntime CreateRuntime(
        InMemoryManifestAdminRepository manifestRepo,
        EnumDictionaryRepository? enumRepo = null)
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
            manifestRepo,
            null,
            topoRepo,
            enumRepo);
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
    /// Regression: a tableRef mismatch fails before canonical projection writes, even when the
    /// projection helper is called independently of NpgsqlManifestRepository's transaction.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task ProjectOnPromote_TableRefMismatch_LeavesNoPartialCanonicalWrite()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required for manifest projection live DB regression " +
                    "(TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 enforces DB presence).");
            // No DB connection available — explicit local skip. Set TOPOLACTOR_TEST_DB_CONNECTION
            // to execute the canonical no-partial-write assertion against PostgreSQL.
            return;
        }

        var hubId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var missingTableRef = $"missing_projection_table_{Guid.NewGuid():N}";
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new { type = "hub_grouping", hubId, manifestKey = $"test-{manifestId:N}" }),
            JsonSerializer.SerializeToElement(new { type = "screen_data_shape", tableRef = missingTableRef }),
        };
        var detail = new ManifestDetailRecord(
            manifestId, null, topology, "active", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        try
        {
            await using (var seedHub = new NpgsqlCommand(
                "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", conn))
            {
                seedHub.Parameters.AddWithValue("id", hubId);
                await seedHub.ExecuteNonQueryAsync();
            }

            var error = await ManifestCanonicalProjection.ProjectOnPromoteAsync(conn, detail, default);

            Assert.NotNull(error);
            Assert.Equal("WIRING_TABLE_REF_NOT_FOUND", error!.Code);
            Assert.Equal(0, await CountAsync(
                conn,
                "SELECT count(*) FROM hubs.topology_manifests WHERE topology_manifest_id = @id",
                manifestId));
            Assert.Equal(0, await CountAsync(
                conn,
                "SELECT count(*) FROM topology.wiring_physical_to_package WHERE package_id = @id",
                manifestId));
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand(
                "DELETE FROM topology.wiring_physical_to_package WHERE package_id = @manifest; " +
                "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @manifest; " +
                "DELETE FROM hubs.hub WHERE hub_id = @hub",
                conn);
            cleanup.Parameters.AddWithValue("manifest", manifestId);
            cleanup.Parameters.AddWithValue("hub", hubId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task<long> CountAsync(NpgsqlConnection conn, string sql, Guid id)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

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
