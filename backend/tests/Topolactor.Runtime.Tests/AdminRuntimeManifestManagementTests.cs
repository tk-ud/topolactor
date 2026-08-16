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
    public async Task Validate_DuplicateDispatcherMappingEntries_ReturnsBlockingError()
    {
        // Round 19: a topology with 2+ dispatcher_mapping entries must be rejected outright at
        // save/validate time -- without this, the LAST entry silently wins (no duplicate guard in
        // ManifestTopologyValidator.Validate's foreach), discarding the first entry's declared
        // axes/role with no signal to the author, and leaving dispatch-time authorization
        // (DispatcherMappingAxisAuthority.FindDeclaredRole etc.) dependent on JSONB array order for
        // any (layer, action) the duplicates happen to share.
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new
            {
                type = "dispatcher_mapping", role = "admin", target = "admin", layer = "enum_dictionary", action = "create_group",
            }),
            JsonSerializer.SerializeToElement(new
            {
                type = "dispatcher_mapping", role = "user", target = "admin", layer = "enum_dictionary", action = "create_group",
            }),
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
            i => i.GetProperty("code").GetString() == "MANIFEST_TOPOLOGY_DUPLICATE_DISPATCHER_MAPPING");
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
            topologySystemName = "my-table",
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
    public async Task AssignScreenDataShape_Validates_AggregateTriggerDefinitions_Before_Persisting()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, DraftTopologyWithLogicalTables("orders", "id"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = manifestId.ToString(),
            topologySystemName = "orders",
            logicalTables = new[]
            {
                new
                {
                    tableName = "orders",
                    columns = new[] { new { name = "id", dataType = "text", nullable = false } },
                },
            },
            aggregateTriggerDefinitions = new[]
            {
                ValidAggregateTriggerDefinition("missing_target", "orders"),
            },
        });

        var (_, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null),
            default);

        Assert.NotNull(error);
        Assert.Equal("AGGREGATE_TARGET_INVALID", error!.Code);
    }

    [Fact]
    public async Task AssignScreenDataShape_Persists_ValidatorAccepted_AggregateTriggerDefinitions()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, DraftTopologyWithLogicalTables("orders", "id"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var aggregateTriggerRepo = new InMemoryAggregateTriggerRepository();
        var runtime = CreateRuntime(repo, aggregateTriggerRepo: aggregateTriggerRepo);
        var definitionId = Guid.NewGuid().ToString();
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = manifestId.ToString(),
            topologySystemName = "orders",
            logicalTables = new[]
            {
                new
                {
                    tableName = "orders",
                    columns = new[] { new { name = "id", dataType = "text", nullable = false } },
                },
            },
            aggregateTriggerDefinitions = new[]
            {
                ValidAggregateTriggerDefinition("orders", "orders", definitionId),
            },
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null),
            default);

        Assert.Null(error);
        var entries = JsonSerializer.Deserialize<JsonElement[]>(
            data!.Value.GetProperty("topologyRawJson").GetString() ?? "[]")!;
        var shapeEntry = entries.First(e =>
            e.TryGetProperty("type", out var t) && t.GetString() == "screen_data_shape");
        var definitions = shapeEntry.GetProperty("aggregateTriggerDefinitions").EnumerateArray().ToList();
        Assert.Single(definitions);
        Assert.Equal("orders", definitions[0].GetProperty("aggregate_target_binding").GetProperty("target_id").GetString());

        // Canonical execution-authority persistence route (runtime_orchestration.aggregate_trigger_definitions),
        // in addition to the screen_data_shape authoring-draft projection asserted above.
        var persisted = await aggregateTriggerRepo.LoadDefinitionAsync(Guid.Parse(definitionId));
        Assert.NotNull(persisted);
        Assert.Equal("orders", persisted!.AggregateTargetBinding.TargetId);
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
            topologySystemName = "test-screen",
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

        Assert.True(shapeEntry.TryGetProperty("screenReadQueryWiring", out var wiring));
        Assert.Equal(JsonValueKind.Object, wiring.ValueKind);
        Assert.True(wiring.TryGetProperty("searchConditions", out var searchWiring));
        Assert.True(searchWiring.TryGetProperty("bindings", out var bindings));
        Assert.Equal(4, bindings.GetArrayLength());
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
            topologySystemName = "my-table",
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

        var enumRepo = InMemoryEnumDictionaryRepository.WithFixtureSeed();
        var runtime = CreateRuntime(repo, enumRepo);
        var groupId = InMemoryEnumDictionaryRepository.FixtureGroupId.ToString();
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = manifestId.ToString(),
            topologySystemName = "my-table",
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

        var runtime = CreateRuntime(repo, InMemoryEnumDictionaryRepository.WithFixtureSeed());
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
    public async Task AssignScreenDataShape_Persists_TopologySystemName()
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
            topologySystemName = "customer-management",
            userFacingTopologyLabel = "顧客管理",
        });

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        var rawJson = data.Value.GetProperty("topologyRawJson").GetString() ?? "[]";
        var entries = System.Text.Json.JsonSerializer.Deserialize<JsonElement[]>(rawJson)!;
        var shapeEntry = entries.First(e =>
            e.TryGetProperty("type", out var t) && t.GetString() == "screen_data_shape");
        Assert.Equal("customer-management", shapeEntry.GetProperty("topologySystemName").GetString());
        Assert.Equal("顧客管理", shapeEntry.GetProperty("userFacingTopologyLabel").GetString());
    }

    [Fact]
    public async Task AssignScreenDataShape_Rejects_Invalid_TopologySystemName()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, ValidTopology("admin", "tgt", "screen_list", "Read", "topology_transform_runtime"), "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        foreach (var invalid in new[] { "顧客管理", "-foo", "foo-", "foo--bar", "Foo Bar", "FOO" })
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                manifestId = manifestId.ToString(),
                topologySystemName = invalid,
            });
            var (_, error) = await runtime.ExecuteDataAsync(
                new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null), default);
            Assert.NotNull(error);
            Assert.Equal("INVALID_TOPOLOGY_SYSTEM_NAME", error!.Code);
        }
    }

    [Fact]
    public async Task AssignScreenDataShape_Rejects_Missing_TopologySystemName()
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
            // topologySystemName omitted
        });
        var (_, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null), default);
        Assert.NotNull(error);
        Assert.Equal("TOPOLOGY_SYSTEM_NAME_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task AssignScreenDataShape_Rejects_Changed_TopologySystemName()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        // Seed a manifest that already has topologySystemName set via a prior topology entry.
        var topologyWithSysName = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new
            {
                type = "dispatcher_mapping",
                role = "admin", target = "customer-management", layer = "screen_list", action = "Read",
                runtime_destination = "topology_transform_runtime",
            }),
            JsonSerializer.SerializeToElement(new
            {
                type = ManifestCanonicalProjection.ScreenDataShapeEntryType,
                topologySystemName = "customer-management",
            }),
        };
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, topologyWithSysName, "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = manifestId.ToString(),
            topologySystemName = "different-name",
        });
        var (_, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null), default);
        Assert.NotNull(error);
        Assert.Equal("TOPOLOGY_SYSTEM_NAME_IMMUTABLE", error!.Code);
    }

    [Fact]
    public async Task AssignScreenDataShape_Allows_Reassign_With_Same_TopologySystemName()
    {
        var repo = new InMemoryManifestAdminRepository();
        var manifestId = Guid.NewGuid();
        var topologyWithSysName = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new
            {
                type = "dispatcher_mapping",
                role = "admin", target = "customer-management", layer = "screen_list", action = "Read",
                runtime_destination = "topology_transform_runtime",
            }),
            JsonSerializer.SerializeToElement(new
            {
                type = ManifestCanonicalProjection.ScreenDataShapeEntryType,
                topologySystemName = "customer-management",
            }),
        };
        repo.Seed(new ManifestDetailRecord(
            manifestId, null, topologyWithSysName, "draft",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            manifestId = manifestId.ToString(),
            topologySystemName = "customer-management",
            screenOperationKind = "list",
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "assign_screen_data_shape", null, "admin", payload, null), default);
        Assert.Null(error);
        Assert.True(data.HasValue);
    }

    [Fact]
    public async Task EnumDictionary_ListGroups_Returns_Seeded_Group()
    {
        var enumRepo = InMemoryEnumDictionaryRepository.WithFixtureSeed();
        var runtime = CreateRuntime(new InMemoryManifestAdminRepository(), enumRepo);
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "enum_dictionary", "list_groups", null, "admin", null, null), default);

        Assert.Null(error);
        var groups = data!.Value.GetProperty("groups");
        Assert.Equal(2, groups.GetArrayLength());
        var groupIds = groups.EnumerateArray()
            .Select(e => e.GetProperty("groupId").GetString())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(InMemoryEnumDictionaryRepository.FixtureGroupId.ToString(), groupIds);
        Assert.Contains(InMemoryEnumDictionaryRepository.UserStatusGroupId.ToString(), groupIds);
    }

    private static object ValidAggregateTriggerDefinition(string aggregateTargetId, string materializationTargetId, string? definitionId = null) => new
    {
        trigger_definition_id = Guid.Parse(definitionId ?? Guid.NewGuid().ToString()),
        trigger_source = new
        {
            canonical_trigger_kind = "client",
            trigger_source_detail_kind = "client_operation_event",
        },
        processing_function_scope = new
        {
            function_id = "aggregate_trigger_authoring_function",
            operation_definition_id = "contents_step3_operation",
            accepted_event_schema_ref = "contents.step3.aggregate_trigger.event.v1",
            allowed_source_kinds = new[] { "function_input_event" },
            materialization_policy_ref = "backend_runtime_authority_required",
        },
        execution_scope = "single_event",
        transaction_boundary = "event_append_only",
        aggregate_target_binding = new
        {
            target_source = "step2_logical_entity_definition",
            target_id = aggregateTargetId,
        },
        conflict_key_fields = new[] { "operation_definition_id" },
        delta_map = new Dictionary<string, decimal> { ["event_count"] = 1m },
        threshold_policy = new
        {
            minimum_trial_count = 1,
            ratio_numerator_field = "event_count",
            ratio_denominator_field = "event_count",
            comparison_operator = ">=",
            target_ratio = 1m,
        },
        materialization_target_binding = new
        {
            target_source = "step2_logical_entity_definition",
            target_id = materializationTargetId,
        },
        materialization_payload_map = new[]
        {
            new
            {
                target_field = "operation_definition_id",
                source = "function_input_event",
                source_field = "operation_definition_id",
            },
        },
        approval_policy = "auto_materialize_when_threshold_passes",
        evidence_policy = "structured_authoring_preview_only",
    };

    private static AdminRuntime CreateRuntime(
        InMemoryManifestAdminRepository manifestRepo,
        EnumDictionaryRepository? enumRepo = null,
        AggregateTriggerRepository? aggregateTriggerRepo = null,
        IAbstractFunctionManifestRepository? abstractFunctionRepo = null)
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
            enumRepo,
            aggregateTriggerRepository: aggregateTriggerRepo,
            abstractFunctionManifestRepository: abstractFunctionRepo);
    }

    private sealed class FakeAbstractFunctionManifestCandidateRepository(
        IReadOnlyList<AbstractFunctionManifestCandidate> candidates) : IAbstractFunctionManifestRepository
    {
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) =>
            throw new NotSupportedException("not used by the candidate-listing tests");

        public Task<IReadOnlyList<AbstractFunctionManifestCandidate>> ListActiveByRuntimeLaneAsync(string runtimeLane, CancellationToken ct = default) =>
            Task.FromResult(candidates);
    }

    [Fact]
    public async Task ListAggregateTriggerProcessingFunctions_ReturnsRegistryBackedCandidates()
    {
        var repo = new InMemoryManifestAdminRepository();
        var candidates = new List<AbstractFunctionManifestCandidate>
        {
            new("registered_function", "orders", true, 2),
        };
        var runtime = CreateRuntime(repo, abstractFunctionRepo: new FakeAbstractFunctionManifestCandidateRepository(candidates));

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "list_aggregate_trigger_processing_functions", null, "admin", null, null));

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Contains("registered_function", data!.Value.GetRawText());
        Assert.Contains("orders", data.Value.GetRawText());
    }

    [Fact]
    public async Task ListAggregateTriggerProcessingFunctions_NoRepositoryWired_ReturnsEmptyListNotError()
    {
        var repo = new InMemoryManifestAdminRepository();
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "list_aggregate_trigger_processing_functions", null, "admin", null, null));

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal("[]", data!.Value.GetRawText());
    }

    [Fact]
    public async Task ManifestList_FiltersByContentsType()
    {
        var repo = new InMemoryManifestAdminRepository();
        var contentsId = Guid.NewGuid();
        var seedId = Guid.NewGuid();
        repo.Seed(MakeContentsDraft(contentsId, "customer-mgmt", logicalTables: 1));
        repo.Seed(MakeSeedActive(seedId));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            status = "draft",
            contentsType = ManifestContentsTypeVocabulary.Contents,
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "list", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal(1, data.Value.GetArrayLength());
        Assert.Equal(contentsId.ToString(), data.Value[0].GetProperty("manifestId").GetString());
    }

    [Fact]
    public async Task ManifestList_ActiveCloneSourceFilter_RequiresLogicalTablesAndPhysical()
    {
        var repo = new InMemoryManifestAdminRepository();
        repo.SeedActivePhysicalTableRef("customer_mgmt");
        var eligibleId = Guid.NewGuid();
        var ineligibleId = Guid.NewGuid();
        repo.Seed(MakeContentsActive(eligibleId, "customer-mgmt", tableRef: "customer_mgmt", logicalTables: 1));
        repo.Seed(MakeContentsActive(ineligibleId, "other-app", tableRef: "missing_table", logicalTables: 1));

        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            status = "active",
            contentsType = ManifestContentsTypeVocabulary.Contents,
            logicalTablesMin = 1,
            physical = true,
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "list", null, "admin", payload, null), default);

        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.Equal(1, data.Value.GetArrayLength());
        Assert.Equal(eligibleId.ToString(), data.Value[0].GetProperty("manifestId").GetString());
    }

    [Fact]
    public async Task CreateNewTopologyDraft_StampsContentsTypeOnShape()
    {
        var repo = new InMemoryManifestAdminRepository();
        var runtime = CreateRuntime(repo);
        var payload = JsonSerializer.SerializeToElement(new
        {
            role = "admin",
            target = "admin",
            layer = "manifest",
            action = "list",
            runtimeDestination = "admin_runtime",
            screenOperationKind = "list",
        });
        var (data, error) = await runtime.ExecuteDataAsync(
            new OperationVector("admin", "manifest", "create_new_topology_draft", null, "admin", payload, null),
            default);

        Assert.Null(error);
        var manifestId = Guid.Parse(data!.Value.GetProperty("manifestId").GetString()!);
        var detail = await repo.LoadDetailByIdAsync(manifestId, default);
        Assert.NotNull(detail);
        var shape = ScreenDataShapeTopologyReader.FindScreenDataShapeEntry(detail!.Topology);
        Assert.True(shape.HasValue);
        Assert.Equal(
            ManifestContentsTypeVocabulary.Contents,
            shape.Value.GetProperty("contentsType").GetString());
    }

    private static ManifestDetailRecord MakeContentsDraft(
        Guid id, string topologySystemName, int logicalTables = 0, string? tableRef = null)
    {
        var shape = new Dictionary<string, object?>
        {
            ["type"] = "screen_data_shape",
            ["contentsType"] = ManifestContentsTypeVocabulary.Contents,
            ["topologySystemName"] = topologySystemName,
        };
        if (logicalTables > 0)
        {
            shape["logicalTables"] = new[]
            {
                new { tableName = topologySystemName.Replace('-', '_'), columns = new[] { new { name = "id", dataType = "uuid", nullable = false } } },
            };
        }
        if (!string.IsNullOrWhiteSpace(tableRef))
            shape["tableRef"] = tableRef;

        var topology = ValidTopology("admin", "admin", "manifest", "list", "admin_runtime").ToList();
        topology.Add(JsonSerializer.SerializeToElement(shape));
        return new ManifestDetailRecord(id, null, topology, "draft", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private static ManifestDetailRecord MakeContentsActive(
        Guid id, string topologySystemName, string tableRef, int logicalTables)
    {
        var record = MakeContentsDraft(id, topologySystemName, logicalTables, tableRef);
        return record with { Status = "active" };
    }

    private static ManifestDetailRecord MakeSeedActive(Guid id)
    {
        var topology = ValidTopology("admin", "admin", "manifest", "list", "admin_runtime").ToList();
        topology.Add(JsonSerializer.SerializeToElement(new
        {
            type = "screen_data_shape",
            contentsType = ManifestContentsTypeVocabulary.RuntimeSeed,
            tableRef = "seed.projection_lane",
        }));
        return new ManifestDetailRecord(id, null, topology, "active", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
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
    /// Authoring draft projection writes topology_manifests without requiring wiring table_ref.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task ProjectOnAuthoringDraft_WritesTopologyManifest_WithoutWiringTableRef()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required for manifest projection live DB regression " +
                    "(TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 enforces DB presence).");
            return;
        }

        var hubId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var missingTableRef = $"missing_authoring_table_{Guid.NewGuid():N}";
        var topology = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(new { type = "hub_grouping", hubId, manifestKey = $"authoring-{manifestId:N}" }),
            JsonSerializer.SerializeToElement(new { type = "screen_data_shape", tableRef = missingTableRef }),
        };
        var detail = new ManifestDetailRecord(
            manifestId, null, topology, "draft", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

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

            var error = await ManifestCanonicalProjection.ProjectOnAuthoringDraftAsync(conn, detail, default);

            Assert.Null(error);
            Assert.Equal(1, await CountAsync(
                conn,
                "SELECT count(*) FROM hubs.topology_manifests WHERE topology_manifest_id = @id",
                manifestId));
            Assert.Equal(0, await CountAsync(
                conn,
                "SELECT count(*) FROM topology.wiring_physical_to_package WHERE package_id = @id",
                manifestId));

            // production_projection_connectivity_invariant realignment: ProjectOnAuthoringDraftAsync
            // must mirror detail.Status ("draft" here) onto hubs.topology_manifests.status rather
            // than hardcoding 'active' -- the phase mismatch this round's realignment resolves.
            await using var statusCmd = new NpgsqlCommand(
                "SELECT status FROM hubs.topology_manifests WHERE topology_manifest_id = @id", conn);
            statusCmd.Parameters.AddWithValue("id", manifestId);
            Assert.Equal("draft", (string?)await statusCmd.ExecuteScalarAsync());
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
