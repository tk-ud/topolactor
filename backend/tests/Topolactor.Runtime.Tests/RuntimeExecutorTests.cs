using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Guard;
using Topolactor.Mapper;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class RuntimeExecutorTests
{
    internal static TargetDispatchOverride CreateTargetDispatchOverride()
    {
        var contextRoutePolicyRepository = new StubValidPolicyTopologyRepository();
        var contextRouteRepository = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topologyVectorRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, contextRouteRepository);

        var adminRuntime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            contextRouteRepository,
            new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, contextRoutePolicyRepository, topologyVectorRuntime),
            new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double")),
            new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double"));

        return new TargetDispatchOverride(NullLogger<TargetDispatchOverride>.Instance, adminRuntime);
    }

    internal static RuntimeExecutor CreateExecutor(
        TopologyRepository? topologyRepositoryOverride = null,
        ManifestRepository? manifestRepository = null)
    {
        var topologyRepository = topologyRepositoryOverride ?? new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var contextRoutePolicyRepository = new StubValidPolicyTopologyRepository();
        var contextRouteRepository = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");

        var resolver = new ContextRouteRecommendationResolver(
            NullLogger<ContextRouteRecommendationResolver>.Instance,
            contextRouteRepository,
            new ContextVectorBuilder(),
            new ContextNeighborSearch(),
            contextRoutePolicyRepository,
            new SystemOperationCiRuntime(NullLogger<SystemOperationCiRuntime>.Instance, contextRouteRepository));
        var abstractFunctionExecutor = new AbstractFunctionExecutor(
            new RecommendationManifestRepository(),
            new IAbstractFunctionPrimitiveAdapter[]
            {
                new RecommendationAttentionPrimitiveAdapter(
                    NullLogger<RecommendationAttentionPrimitiveAdapter>.Instance, resolver)
            });

        return new RuntimeExecutor(
            logger: NullLogger<RuntimeExecutor>.Instance,
            operationVectorResolver: new OperationVectorResolver(),
            attractorResolver: new AttractorResolver(topologyRepository),
            structureMapResolver: new StructureMapResolver(topologyRepository),
            packageResolver: new PackageResolver(topologyRepository),
            schemaResolver: new SchemaResolver(topologyRepository),
            emissionBuilder: new EmissionBuilder(),
            semanticMapper: new SemanticMapper(),
            diffLogRepository: new DiffLogRepository(NullLogger<DiffLogRepository>.Instance),
            sqlAttentionLogsRepository: new SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "test-double"),
            runtimeGuard: new RuntimeGuard(),
            abstractFunctionExecutor: abstractFunctionExecutor,
            manifestRepository: manifestRepository);
    }

    /// <summary>
    /// Creates a ManifestDispatcher with a standard handler dict:
    ///   topology_transform_runtime → executor built from topologyRepository
    /// Extra handlers are merged in (and can override the default).
    /// </summary>
    internal static ManifestDispatcher CreateDispatcher(
        TopologyRepository topologyRepository,
        ManifestRepository? manifestRepository = null,
        IReadOnlyDictionary<string, IDispatchableRuntime>? extraHandlers = null)
    {
        var executor = CreateExecutor(topologyRepository, manifestRepository);
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = executor,
        };
        if (extraHandlers is not null)
            foreach (var (k, v) in extraHandlers) handlers[k] = v;

        return new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            CreateTargetDispatchOverride(),
            manifestRepository);
    }

    [Fact]
    public async Task ExecuteAsync_InMemoryDefaultRoute_ReturnsSuccessfulEmission()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        var vector = new OperationVectorResolver().Resolve(request);
        Assert.Equal("default:entity:search", vector.AttractorKey);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Equal("00000000-0000-0000-0000-000000000004", response.Emission!.StructureMapId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), response.Emission.PackageId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), response.Emission.SchemaId);
        Assert.Contains("00000000-0000-0000-0000-000000000003", response.Emission.ComponentIds ?? []);
        Assert.Empty(response.Errors);
        Assert.NotNull(response.Emission.ContextRouteRecommendation);
        Assert.Equal(RecommendationStatus.InsufficientHistory, response.Emission.ContextRouteRecommendation!.Status);
    }

    [Fact]
    public async Task ExecuteAsync_AttractorResolverThrows_ReturnsAttractorResolveFailed()
    {
        // ATTRACTOR_RESOLVE_FAILED fires when AttractorResolver throws an unexpected exception
        // (not the route-missing InvalidOperationException, which returns Success=true with jump events).
        var executor = CreateExecutor(new AttractorFailingTopologyRepository());
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "ATTRACTOR_RESOLVE_FAILED");
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredFields_ReturnsValidationErrorsAndNoEmission()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto("Search", null, "entity", null, null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "MISSING_TARGET");
        Assert.Contains(response.Errors, e => e.Code == "MISSING_ACTION");
    }

    [Fact]
    public async Task ExecuteAsync_BrokenAttractor_ReturnsCanonicalRouteMissingJumpFallback()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto(
            "Search",
            "missing",
            "entity",
            "Search",
            null,
            null,
            new Dictionary<string, string>
            {
                ["pastHubAddress"] = "10",
                ["currentHubAddress"] = "12",
                ["pastTopologyAddress"] = "30",
                ["currentTopologyAddress"] = "34"
            });

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Empty(response.Errors);
        Assert.NotNull(response.Emission!.JumpEvents);
        Assert.Collection(
            response.Emission.JumpEvents!,
            e =>
            {
                Assert.Equal("hub", e.Scope);
                Assert.Equal(12, e.FromAddress);
                Assert.Equal(0, e.ToAddress);
                Assert.Equal(0, e.PlannedAddress);
                Assert.Equal("route_missing", e.Reason);
            },
            e =>
            {
                Assert.Equal("topology", e.Scope);
                Assert.Equal(34, e.FromAddress);
                Assert.Equal(0, e.ToAddress);
                Assert.Equal(0, e.PlannedAddress);
                Assert.Equal("route_missing", e.Reason);
            });
    }

    [Fact]
    public async Task ExecuteAsync_BrokenAttractor_MissingPastAddress_UsesZeroPastAddress()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto(
            "Search",
            "missing",
            "entity",
            "Search",
            null,
            null,
            new Dictionary<string, string>
            {
                ["currentHubAddress"] = "12",
                ["currentTopologyAddress"] = "34"
            });

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.NotNull(response.Emission!.JumpEvents);
        Assert.Collection(
            response.Emission.JumpEvents!,
            e =>
            {
                Assert.Equal("hub", e.Scope);
                Assert.Equal(12, e.FromAddress);
                Assert.Equal(0, e.ToAddress);
                Assert.Equal(0, e.PlannedAddress);
                Assert.Equal("route_missing", e.Reason);
            },
            e =>
            {
                Assert.Equal("topology", e.Scope);
                Assert.Equal(34, e.FromAddress);
                Assert.Equal(0, e.ToAddress);
                Assert.Equal(0, e.PlannedAddress);
                Assert.Equal("route_missing", e.Reason);
            });
    }

    [Fact]
    public async Task ExecuteAsync_UserActionJumpContext_EmitsOnlyOnExplicitUserAction()
    {
        var executor = CreateExecutor();
        var withUserAction = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null,
            new Dictionary<string, string>
            {
                ["jumpReason"] = "user_action",
                ["jumpScope"] = "hub",
                ["pastHubAddress"] = "7",
                ["currentHubAddress"] = "8",
                ["plannedHubAddress"] = "9"
            });

        var resWithUserAction = await executor.ExecuteAsync(withUserAction);
        Assert.True(resWithUserAction.Success);
        Assert.Single(resWithUserAction.Emission!.JumpEvents!);
        Assert.Equal("user_action", resWithUserAction.Emission.JumpEvents![0].Reason);
        Assert.Equal("hub", resWithUserAction.Emission.JumpEvents[0].Scope);
        Assert.Equal(8, resWithUserAction.Emission.JumpEvents[0].FromAddress);
        Assert.Equal(9, resWithUserAction.Emission.JumpEvents[0].ToAddress);
        Assert.Equal(9, resWithUserAction.Emission.JumpEvents[0].PlannedAddress);

        var recommendationOnly = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null,
            new Dictionary<string, string>
            {
                ["currentHubAddress"] = "5",
                ["currentTopologyAddress"] = "6"
            });
        var resRecommendationOnly = await executor.ExecuteAsync(recommendationOnly);
        Assert.True(resRecommendationOnly.Success);
        Assert.True(resRecommendationOnly.Emission!.JumpEvents is null || resRecommendationOnly.Emission.JumpEvents.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_UserActionJumpContext_MissingPlannedHubAddress_EmitsJumpWithZeroPlannedAddress()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null,
            new Dictionary<string, string>
            {
                ["jumpReason"] = "user_action",
                ["jumpScope"] = "hub",
                ["pastHubAddress"] = "7",
                ["currentHubAddress"] = "8"
            });

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Single(response.Emission!.JumpEvents!);
        Assert.Equal("hub", response.Emission.JumpEvents![0].Scope);
        Assert.Equal(8, response.Emission.JumpEvents![0].FromAddress);
        Assert.Equal(0, response.Emission.JumpEvents![0].ToAddress);
        Assert.Equal(0, response.Emission.JumpEvents![0].PlannedAddress);
    }

    [Fact]
    public async Task ExecuteAsync_UserActionJumpContext_NonNumericAddress_FallsBackToZeroAndEmitsJump()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null,
            new Dictionary<string, string>
            {
                ["jumpReason"] = "user_action",
                ["jumpScope"] = "hub",
                ["pastHubAddress"] = "x",
                ["currentHubAddress"] = "8",
                ["plannedHubAddress"] = "9"
            });

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Single(response.Emission!.JumpEvents!);
        Assert.Equal("hub", response.Emission.JumpEvents![0].Scope);
        Assert.Equal(8, response.Emission.JumpEvents![0].FromAddress);
        Assert.Equal(9, response.Emission.JumpEvents![0].ToAddress);
        Assert.Equal(9, response.Emission.JumpEvents![0].PlannedAddress);
    }

    [Fact]
    public async Task ExecuteAsync_UserActionJumpContext_InvalidScope_DoesNotEmitJump()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null,
            new Dictionary<string, string>
            {
                ["jumpReason"] = "user_action",
                ["jumpScope"] = "invalid",
                ["pastHubAddress"] = "7",
                ["currentHubAddress"] = "8",
                ["plannedHubAddress"] = "9"
            });

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.True(response.Emission!.JumpEvents is null || response.Emission.JumpEvents.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_UserActionJumpContext_ValidTopologyScope_EmitsSingleJump()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null,
            new Dictionary<string, string>
            {
                ["jumpReason"] = "user_action",
                ["jumpScope"] = "topology",
                ["pastTopologyAddress"] = "11",
                ["currentTopologyAddress"] = "12",
                ["plannedTopologyAddress"] = "13"
            });

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Single(response.Emission!.JumpEvents!);
        Assert.Equal("topology", response.Emission.JumpEvents![0].Scope);
        Assert.Equal("user_action", response.Emission.JumpEvents[0].Reason);
        Assert.Equal(12, response.Emission.JumpEvents[0].FromAddress);
        Assert.Equal(13, response.Emission.JumpEvents[0].ToAddress);
        Assert.Equal(13, response.Emission.JumpEvents[0].PlannedAddress);
    }

    [Fact]
    public async Task ExecuteAsync_UserActionJumpContext_MissingPastTopologyAddress_EmitsJumpWithZeroPastAddress()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null,
            new Dictionary<string, string>
            {
                ["jumpReason"] = "user_action",
                ["jumpScope"] = "topology",
                ["currentTopologyAddress"] = "12",
                ["plannedTopologyAddress"] = "13"
            });

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Single(response.Emission!.JumpEvents!);
        Assert.Equal("topology", response.Emission.JumpEvents![0].Scope);
        Assert.Equal("user_action", response.Emission.JumpEvents[0].Reason);
        Assert.Equal(12, response.Emission.JumpEvents[0].FromAddress);
        Assert.Equal(13, response.Emission.JumpEvents[0].ToAddress);
        Assert.Equal(13, response.Emission.JumpEvents[0].PlannedAddress);
    }

    [Fact]
    public async Task ExecuteAsync_UserActionJumpContext_JumpFromJumpToOnly_DoesNotEmitJump()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null,
            new Dictionary<string, string>
            {
                ["jumpReason"] = "user_action",
                ["jumpScope"] = "hub",
                ["jumpFrom"] = "7",
                ["jumpTo"] = "9"
            });

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Single(response.Emission!.JumpEvents!);
        Assert.Equal("hub", response.Emission.JumpEvents![0].Scope);
        Assert.Equal(0, response.Emission.JumpEvents[0].FromAddress);
        Assert.Equal(0, response.Emission.JumpEvents[0].ToAddress);
        Assert.Equal(0, response.Emission.JumpEvents[0].PlannedAddress);
    }

    [Fact]
    public async Task ExecuteAsync_UserActionJumpContext_NonUserActionReason_DoesNotEmitJump()
    {
        var executor = CreateExecutor();
        var request = new EndpointRequestDto(
            "Search", "default", "entity", "Search", null, null,
            new Dictionary<string, string>
            {
                ["jumpReason"] = "recommendation",
                ["jumpScope"] = "hub",
                ["pastHubAddress"] = "7",
                ["currentHubAddress"] = "8",
                ["plannedHubAddress"] = "9"
            });

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.True(response.Emission!.JumpEvents is null || response.Emission.JumpEvents.Count == 0);
    }

    [Fact]
    public async Task TopologyRepository_NonDefaultLookups_ReturnNull()
    {
        var repository = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");

        var structureMap = await repository.LoadStructureMapAsync("missing:entity:search");
        var package = await repository.LoadPackageAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var schema = await repository.LoadSchemaAsync(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var functionParameter = await repository.LoadFunctionParameterAsync("context_route_recommendation_resolve", "default_policy");

        Assert.Null(structureMap);
        Assert.Null(package);
        Assert.Null(schema);
        Assert.Null(functionParameter);
    }


    [Fact]
    public async Task ExecuteAsync_DefaultEntitySearch_RequestIdentityProducesAttractorKeyAndEmissionIdentity()
    {
        // Verifies pipeline identity chain from docs/design/pipeline-continuity-ssot.yaml
        // api_command_lane.required_identity:
        // request{target, layer, action} → vector.AttractorKey → emission{structureMapId, packageId, schemaId, componentIds}.
        var executor = CreateExecutor();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var vector = new OperationVectorResolver().Resolve(request);
        Assert.Equal("default:entity:search", vector.AttractorKey); // required_identity: attractor_key

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.NotNull(response.Emission!.StructureMapId);  // required_identity: structure_map_id
        Assert.NotNull(response.Emission.PackageId);        // required_identity: package_id
        Assert.NotNull(response.Emission.SchemaId);         // required_identity: schema_id
        Assert.NotNull(response.Emission.ComponentIds);     // required_identity: component_ids
        Assert.NotEmpty(response.Emission.ComponentIds!);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("logical_delete")]
    [InlineData("restore")]
    [InlineData("physical_delete")]
    [InlineData("delete")]
    public void ShouldAppendLogsDiff_MutationActions_ReturnTrue(string action)
    {
        var method = typeof(RuntimeExecutor).GetMethod("ShouldAppendLogsDiff", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var result = (bool)method.Invoke(null, [action])!;
        Assert.True(result);
    }

    [Theory]
    [InlineData("select")]
    [InlineData("read")]
    [InlineData("list")]
    [InlineData("search")]
    public void ShouldAppendLogsDiff_ReadActions_ReturnFalse(string action)
    {
        var method = typeof(RuntimeExecutor).GetMethod("ShouldAppendLogsDiff", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var result = (bool)method.Invoke(null, [action])!;
        Assert.False(result);
    }

    [Fact]
    public void ResolveContextValue_UsesEnvFallbackWhenContextMissing()
    {
        var prevSource = Environment.GetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID");
        var prevWindow = Environment.GetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW");
        Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", "env-src");
        Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", "env-window");
        try
        {
            var method = typeof(RuntimeExecutor).GetMethod("ResolveContextValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var source = (string)method.Invoke(null, [null, "sql_attention_source_set_id", "SQL_ATTENTION_SOURCE_SET_ID"])!;
            var basis = (string)method.Invoke(null, [null, "sql_attention_basis_window", "SQL_ATTENTION_BASIS_WINDOW"])!;
            Assert.Equal("env-src", source);
            Assert.Equal("env-window", basis);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", prevSource);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", prevWindow);
        }
    }

    // --- Layout identity pipeline tests ---

    [Fact]
    public void EmissionBuilder_LayoutId_IsPreservedFromWorkingShape()
    {
        // Verifies layout identity pipeline: RuntimeWorkingShape.LayoutId -> Emission.LayoutId.
        var builder = new EmissionBuilder();
        var layoutId = Guid.NewGuid().ToString();
        var shape = new RuntimeWorkingShape(
            Vector: null,
            StructureMapId: "00000000-0000-0000-0000-000000000004",
            PackageId: TopologyRepository.DefaultPackageId,
            SchemaId: TopologyRepository.DefaultSchemaId,
            ComponentIds: [TopologyRepository.DefaultComponentId],
            PackageDef: null,
            SchemaDef: null,
            ResolvedData: null,
            Errors: null,
            LayoutId: layoutId
        );

        var emission = builder.Build(shape);

        Assert.Equal(layoutId, emission.LayoutId);
    }

    [Fact]
    public void EmissionBuilder_LayoutId_IsNullWhenNotConfigured()
    {
        // Verifies that null layout_id in structure_map produces null Emission.LayoutId.
        var builder = new EmissionBuilder();
        var shape = new RuntimeWorkingShape(
            Vector: null,
            StructureMapId: "00000000-0000-0000-0000-000000000004",
            PackageId: TopologyRepository.DefaultPackageId,
            SchemaId: TopologyRepository.DefaultSchemaId,
            ComponentIds: [TopologyRepository.DefaultComponentId],
            PackageDef: null,
            SchemaDef: null,
            ResolvedData: null,
            Errors: null,
            LayoutId: null
        );

        var emission = builder.Build(shape);

        Assert.Null(emission.LayoutId);
    }

    [Fact]
    public async Task StructureMapResolver_LayoutId_IsForwardedFromRecord()
    {
        // Verifies StructureMapRecord.LayoutId -> RuntimeWorkingShape.LayoutId.
        var layoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var repo = new StubTopologyRepositoryWithLayout(layoutId);
        var resolver = new StructureMapResolver(repo);
        var attractor = new AttractorResult(
            AttractorKey: TopologyRepository.DefaultAttractorKey,
            StructureMapId: TopologyRepository.DefaultStructureMapId,
            PackageId: TopologyRepository.DefaultPackageId,
            SchemaId: TopologyRepository.DefaultSchemaId
        );

        var shape = await resolver.Resolve(attractor);

        Assert.Equal(layoutId.ToString(), shape.LayoutId);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultRoute_LayoutIdIsNullWhenNotConfigured()
    {
        // Default in-memory TopologyRepository has no layout_id configured.
        // Emission.LayoutId must be null — not an error.
        var executor = CreateExecutor();
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Null(response.Emission!.LayoutId);
        Assert.Null(response.Emission.LayoutNodes);
    }

    [Fact]
    public async Task StructureMapResolver_LayoutId_WithNoNodes_ReturnsLayoutNodesNotFoundError()
    {
        // When layout_id is set but ui_topology_tensor has no rows, expect LAYOUT_NODES_NOT_FOUND.
        // StubTopologyRepositoryWithLayout inherits base LoadLayoutNodesAsync which returns empty.
        var layoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var repo = new StubTopologyRepositoryWithLayout(layoutId);
        var resolver = new StructureMapResolver(repo);
        var attractor = new AttractorResult(
            AttractorKey: TopologyRepository.DefaultAttractorKey,
            StructureMapId: TopologyRepository.DefaultStructureMapId,
            PackageId: TopologyRepository.DefaultPackageId,
            SchemaId: TopologyRepository.DefaultSchemaId
        );

        var shape = await resolver.Resolve(attractor);

        Assert.Equal(layoutId.ToString(), shape.LayoutId);
        Assert.Null(shape.LayoutNodes);
        Assert.NotNull(shape.Errors);
        Assert.Contains(shape.Errors!, e => e.Code == "LAYOUT_NODES_NOT_FOUND");
    }

    [Fact]
    public async Task StructureMapResolver_LayoutId_WithNodes_ReturnsOrderedLayoutNodes()
    {
        // When layout_id is set and tensor rows exist, LayoutNodes are built positionally.
        // StubTopologyRepositoryWithLayoutAndNodes returns 2 tensor rows and 2 component_ids.
        var layoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var repo = new StubTopologyRepositoryWithLayoutAndNodes(layoutId);
        var resolver = new StructureMapResolver(repo);
        var attractor = new AttractorResult(
            AttractorKey: TopologyRepository.DefaultAttractorKey,
            StructureMapId: TopologyRepository.DefaultStructureMapId,
            PackageId: TopologyRepository.DefaultPackageId,
            SchemaId: TopologyRepository.DefaultSchemaId
        );

        var shape = await resolver.Resolve(attractor);

        Assert.Equal(layoutId.ToString(), shape.LayoutId);
        Assert.Null(shape.Errors);
        Assert.NotNull(shape.LayoutNodes);
        Assert.Equal(2, shape.LayoutNodes!.Count);

        // Verify positional assignment: tensor[0] → componentIds[0]
        Assert.Equal("slot_b", shape.LayoutNodes[0].SlotKey);
        Assert.Equal(0, shape.LayoutNodes[0].OrderIndex);
        Assert.Equal("00000000-0000-0000-0000-000000000099", shape.LayoutNodes[0].ComponentId);

        Assert.Equal("slot_a", shape.LayoutNodes[1].SlotKey);
        Assert.Equal(1, shape.LayoutNodes[1].OrderIndex);
        Assert.Equal(TopologyRepository.DefaultComponentId, shape.LayoutNodes[1].ComponentId);
    }

    [Fact]
    public async Task StructureMapResolver_MalformedCalcBindingsJson_ReturnsCalcBindingsJsonInvalidError()
    {
        // Verifies explicit-failure policy: malformed calculationBindings JSON in layout_patch_json
        // must produce CALC_BINDINGS_JSON_INVALID in Emission.Errors — not silent omit.
        var layoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
        var repo = new StubTopologyRepositoryWithMalformedCalcBindings(layoutId);
        var resolver = new StructureMapResolver(repo);
        var attractor = new AttractorResult(
            AttractorKey: TopologyRepository.DefaultAttractorKey,
            StructureMapId: TopologyRepository.DefaultStructureMapId,
            PackageId: TopologyRepository.DefaultPackageId,
            SchemaId: TopologyRepository.DefaultSchemaId
        );

        var shape = await resolver.Resolve(attractor);

        Assert.NotNull(shape.Errors);
        Assert.Contains(shape.Errors!, e => e.Code == "CALC_BINDINGS_JSON_INVALID");
        Assert.Null(shape.CalculationBindings);
    }

    [Fact]
    public async Task StructureMapResolver_AbsentCalcBindingsJson_IsNoOp()
    {
        // Absent calculationBindings (null from repository) must not produce any error.
        var layoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
        var repo = new StubTopologyRepositoryWithLayout(layoutId);
        var resolver = new StructureMapResolver(repo);
        var attractor = new AttractorResult(
            AttractorKey: TopologyRepository.DefaultAttractorKey,
            StructureMapId: TopologyRepository.DefaultStructureMapId,
            PackageId: TopologyRepository.DefaultPackageId,
            SchemaId: TopologyRepository.DefaultSchemaId
        );

        var shape = await resolver.Resolve(attractor);

        // LAYOUT_NODES_NOT_FOUND is expected (base stub returns no nodes) — but no calc binding error.
        Assert.NotNull(shape.Errors);
        Assert.DoesNotContain(shape.Errors!, e => e.Code == "CALC_BINDINGS_JSON_INVALID");
        Assert.Null(shape.CalculationBindings);
    }

    [Fact]
    public async Task StructureMapResolver_ValidCalcBindingsJson_ForwardedToEmission()
    {
        // Valid calculationBindings array is forwarded raw to RuntimeWorkingShape.CalculationBindings
        // without any server-side evaluation. EmissionBuilder then carries it to Emission.
        var layoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");
        var repo = new StubTopologyRepositoryWithValidCalcBindings(layoutId);
        var builder = new EmissionBuilder();
        var resolver = new StructureMapResolver(repo);
        var attractor = new AttractorResult(
            AttractorKey: TopologyRepository.DefaultAttractorKey,
            StructureMapId: TopologyRepository.DefaultStructureMapId,
            PackageId: TopologyRepository.DefaultPackageId,
            SchemaId: TopologyRepository.DefaultSchemaId
        );

        var shape = await resolver.Resolve(attractor);
        var emission = builder.Build(shape);

        Assert.NotNull(shape.CalculationBindings);
        Assert.NotNull(emission.CalculationBindings);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, emission.CalculationBindings!.Value.ValueKind);
        Assert.Equal(1, emission.CalculationBindings!.Value.GetArrayLength());
        // Verify backend did NOT evaluate the binding (no dispatch, no calculated value in emission.Data)
        Assert.Null(emission.Data);
        // Verify no calc-binding error emitted for valid JSON
        Assert.DoesNotContain(emission.Errors, e => e.Code == "CALC_BINDINGS_JSON_INVALID");
    }

    [Fact]
    public void EmissionBuilder_LayoutNodes_IsPreservedFromWorkingShape()
    {
        // Verifies LayoutNodes pipeline: RuntimeWorkingShape.LayoutNodes → Emission.LayoutNodes.
        var builder = new EmissionBuilder();
        var layoutId = Guid.NewGuid().ToString();
        var layoutNodes = new List<LayoutNode>
        {
            new LayoutNode(
                NodeId: "node-slot-b",
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: null,
                ComponentId: "00000000-0000-0000-0000-000000000099",
                ParentNodeId: null,
                SlotKey: "slot_b",
                OrderIndex: 0,
                X: 0, Y: 0, Width: 0, Height: 0),
            new LayoutNode(
                NodeId: "node-slot-a",
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: null,
                ComponentId: TopologyRepository.DefaultComponentId,
                ParentNodeId: null,
                SlotKey: "slot_a",
                OrderIndex: 1,
                X: 0, Y: 0, Width: 0, Height: 0),
        };
        var shape = new RuntimeWorkingShape(
            Vector: null,
            StructureMapId: "00000000-0000-0000-0000-000000000004",
            PackageId: TopologyRepository.DefaultPackageId,
            SchemaId: TopologyRepository.DefaultSchemaId,
            ComponentIds: [TopologyRepository.DefaultComponentId],
            PackageDef: null,
            SchemaDef: null,
            ResolvedData: null,
            Errors: null,
            LayoutId: layoutId,
            LayoutNodes: layoutNodes
        );

        var emission = builder.Build(shape);

        Assert.Equal(layoutId, emission.LayoutId);
        Assert.NotNull(emission.LayoutNodes);
        Assert.Equal(2, emission.LayoutNodes!.Count);
        Assert.Equal("slot_b", emission.LayoutNodes[0].SlotKey);
        Assert.Equal(0, emission.LayoutNodes[0].OrderIndex);
        Assert.Equal("00000000-0000-0000-0000-000000000099", emission.LayoutNodes[0].ComponentId);
    }

    [Fact]
    public async Task ExecuteAsync_WithLayoutNodes_EmissionHasLayoutNodesInSlotOrder()
    {
        // Full executor pipeline with tensor rows: verifies that real tensor rows from
        // StubTopologyRepositoryWithLayoutAndNodes produce Emission.LayoutNodes ordered by
        // OrderIndex, with slot_b (order=0) before slot_a (order=1).
        // component_ids=[secondary, default]; tensor[0]=slot_b→secondary, tensor[1]=slot_a→default.
        var layoutId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var repo = new StubTopologyRepositoryWithLayoutAndNodes(layoutId);
        var executor = CreateExecutor(repo);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var response = await executor.ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Equal(layoutId.ToString(), response.Emission!.LayoutId);
        Assert.NotNull(response.Emission.LayoutNodes);
        Assert.Equal(2, response.Emission.LayoutNodes!.Count);
        // slot_b has orderIndex=0 → must be first
        Assert.Equal("slot_b", response.Emission.LayoutNodes[0].SlotKey);
        Assert.Equal(0, response.Emission.LayoutNodes[0].OrderIndex);
        Assert.Equal("00000000-0000-0000-0000-000000000099", response.Emission.LayoutNodes[0].ComponentId);
        // slot_a has orderIndex=1 → must be second
        Assert.Equal("slot_a", response.Emission.LayoutNodes[1].SlotKey);
        Assert.Equal(1, response.Emission.LayoutNodes[1].OrderIndex);
        Assert.Equal(TopologyRepository.DefaultComponentId, response.Emission.LayoutNodes[1].ComponentId);
    }

}

/// <summary>
/// Test stub: TopologyRepository that returns the default structure map with a bound LayoutId.
/// LoadLayoutNodesAsync returns empty (inherited base) — models LAYOUT_NODES_NOT_FOUND case.
/// Used to verify StructureMapRecord.LayoutId → RuntimeWorkingShape.LayoutId pipeline
/// and the explicit LAYOUT_NODES_NOT_FOUND error path.
/// </summary>
internal class StubTopologyRepositoryWithLayout(Guid layoutId)
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")
{
    private readonly Guid _layoutId = layoutId;

    public override Task<StructureMapRecord?> LoadStructureMapAsync(string key, CancellationToken ct = default)
    {
        if (key == DefaultAttractorKey || key == DefaultStructureMapId)
        {
            return Task.FromResult<StructureMapRecord?>(new StructureMapRecord(
                StructureMapId: DefaultStructureMapId,
                AttractorKey:   DefaultAttractorKey,
                PackageId:      DefaultPackageId,
                SchemaId:       DefaultSchemaId,
                ComponentIds:   [DefaultComponentId],
                StatePolicyJson: null,
                LayoutId: _layoutId
            ));
        }
        return Task.FromResult<StructureMapRecord?>(null);
    }
}

/// <summary>
/// Test stub: TopologyRepository that returns the default structure map with a bound LayoutId
/// AND returns 2 tensor rows from LoadLayoutNodesAsync.
/// Used to verify the full LayoutNodes positional assignment pipeline.
/// Tensor rows: slot_b (order=0) → componentIds[0] = "00000000-0000-0000-0000-000000000099"
///              slot_a (order=1) → componentIds[1] = DefaultComponentId
/// </summary>
internal class StubTopologyRepositoryWithLayoutAndNodes(Guid layoutId)
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")
{
    private readonly Guid _layoutId = layoutId;
    private const string SecondaryComponentId = "00000000-0000-0000-0000-000000000099";

    public override Task<StructureMapRecord?> LoadStructureMapAsync(string key, CancellationToken ct = default)
    {
        if (key == DefaultAttractorKey || key == DefaultStructureMapId)
        {
            return Task.FromResult<StructureMapRecord?>(new StructureMapRecord(
                StructureMapId: DefaultStructureMapId,
                AttractorKey:   DefaultAttractorKey,
                PackageId:      DefaultPackageId,
                SchemaId:       DefaultSchemaId,
                ComponentIds:   [SecondaryComponentId, DefaultComponentId],
                StatePolicyJson: null,
                LayoutId: _layoutId
            ));
        }
        return Task.FromResult<StructureMapRecord?>(null);
    }

    public override Task<IReadOnlyList<LayoutNodeRecord>> LoadLayoutNodesAsync(
        Guid layoutId, CancellationToken ct = default)
    {
        IReadOnlyList<LayoutNodeRecord> rows =
        [
            new LayoutNodeRecord(
                NodeId: "node-slot-b",
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: null,
                ComponentId: SecondaryComponentId.ToString(),
                ParentNodeId: null,
                SlotKey: "slot_b",
                OrderIndex: 0,
                X: 0, Y: 0, Width: 0, Height: 0,
                LayoutClassRefs: null),
            new LayoutNodeRecord(
                NodeId: "node-slot-a",
                NodeKind: "catalog_component",
                HtmlTag: null,
                ComponentKey: null,
                ComponentId: DefaultComponentId.ToString(),
                ParentNodeId: null,
                SlotKey: "slot_a",
                OrderIndex: 1,
                X: 0, Y: 0, Width: 0, Height: 0,
                LayoutClassRefs: null),
        ];
        return Task.FromResult(rows);
    }
}

/// <summary>
/// Stub: returns default structure map with layoutId + valid nodes + malformed calculationBindings JSON.
/// Used to verify CALC_BINDINGS_JSON_INVALID explicit-failure behavior.
/// </summary>
internal class StubTopologyRepositoryWithMalformedCalcBindings(Guid layoutId)
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")
{
    private readonly Guid _layoutId = layoutId;

    public override Task<StructureMapRecord?> LoadStructureMapAsync(string key, CancellationToken ct = default)
    {
        if (key == DefaultAttractorKey || key == DefaultStructureMapId)
        {
            return Task.FromResult<StructureMapRecord?>(new StructureMapRecord(
                StructureMapId: DefaultStructureMapId,
                AttractorKey:   DefaultAttractorKey,
                PackageId:      DefaultPackageId,
                SchemaId:       DefaultSchemaId,
                ComponentIds:   [DefaultComponentId],
                StatePolicyJson: null,
                LayoutId: _layoutId
            ));
        }
        return Task.FromResult<StructureMapRecord?>(null);
    }

    public override Task<string?> LoadLayoutCalcBindingsJsonAsync(Guid layoutId, CancellationToken ct = default)
        => Task.FromResult<string?>("[{not valid json]]]");
}

/// <summary>
/// Stub: returns default structure map with layoutId + valid nodes + a valid single-element calculationBindings array.
/// Used to verify valid calculationBindings are forwarded raw without backend evaluation.
/// </summary>
internal class StubTopologyRepositoryWithValidCalcBindings(Guid layoutId)
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")
{
    private readonly Guid _layoutId = layoutId;
    private const string ValidCalcBindingsJson =
        """[{"calculationId":"test-calc","variables":{"a":{"kind":"literal","value":5}},"operation":{"op":"multiply","a":"a","b":"a"},"targetNodeId":"n","targetProp":"value"}]""";

    public override Task<StructureMapRecord?> LoadStructureMapAsync(string key, CancellationToken ct = default)
    {
        if (key == DefaultAttractorKey || key == DefaultStructureMapId)
        {
            return Task.FromResult<StructureMapRecord?>(new StructureMapRecord(
                StructureMapId: DefaultStructureMapId,
                AttractorKey:   DefaultAttractorKey,
                PackageId:      DefaultPackageId,
                SchemaId:       DefaultSchemaId,
                ComponentIds:   [DefaultComponentId],
                StatePolicyJson: null,
                LayoutId: _layoutId
            ));
        }
        return Task.FromResult<StructureMapRecord?>(null);
    }

    public override Task<string?> LoadLayoutCalcBindingsJsonAsync(Guid layoutId, CancellationToken ct = default)
        => Task.FromResult<string?>(ValidCalcBindingsJson);
}

public class SchedulerDispatcherChainTests
{
    [Fact]
    public async Task SchedulerDispatcherChain_DefaultEntitySearch_PassesThroughToExecutor()
    {
        // Verifies wiring: RuntimeTimelineScheduler → ManifestDispatcher → RuntimeExecutor.
        // Scheduler and dispatcher are pass-through skeletons; emission must be identical.
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(cts.Token);

        try
        {
            var response = await scheduler.AlignAndDispatchAsync(request, cts.Token);

            Assert.True(response.Success);
            Assert.NotNull(response.Emission);
            Assert.Equal(TopologyRepository.DefaultStructureMapId, response.Emission!.StructureMapId);
            Assert.Equal(TopologyRepository.DefaultPackageId, response.Emission.PackageId);
            Assert.Equal(TopologyRepository.DefaultSchemaId, response.Emission.SchemaId);
            Assert.Contains(TopologyRepository.DefaultComponentId, response.Emission.ComponentIds ?? []);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SchedulerDispatcherChain_RouteMissing_PropagatesFallbackJumpEvent()
    {
        // Route missing must propagate through the full chain as canonical fallback jump events.
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
        var request = new EndpointRequestDto("Search", "missing", "entity", "Search", null, null, null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(cts.Token);

        try
        {
            var response = await scheduler.AlignAndDispatchAsync(request, cts.Token);

            Assert.True(response.Success);
            Assert.NotNull(response.Emission);
            Assert.Empty(response.Errors);
            Assert.NotNull(response.Emission!.JumpEvents);
            Assert.Equal(2, response.Emission.JumpEvents!.Count);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    // --- Queue overflow boundary tests (Gap-14) ---

    [Fact]
    public void EnqueueCronTrigger_QueueNotFull_ReturnsTrue()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 4);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var accepted = scheduler.EnqueueCronTrigger(request);

        Assert.True(accepted);
    }

    [Fact]
    public void EnqueueCronTrigger_QueueFull_ReturnsFalse()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        // capacity=1: fill then attempt to overflow (no consumer running)
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 1);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var first = scheduler.EnqueueCronTrigger(request);
        var second = scheduler.EnqueueCronTrigger(request);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void EnqueueHookTrigger_QueueNotFull_ReturnsTrue()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 4);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var accepted = scheduler.EnqueueHookTrigger(request);

        Assert.True(accepted);
    }

    [Fact]
    public void EnqueueHookTrigger_QueueFull_ReturnsFalse()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 1);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);

        var first = scheduler.EnqueueHookTrigger(request);
        var second = scheduler.EnqueueHookTrigger(request);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task AlignAndDispatchAsync_ClientTrigger_QueueFull_ReturnsSchedulerQueueFullError()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher, queueCapacity: 1);
        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        // Do not start the consumer — queue fills immediately with the first item.
        var cronRequest = request with { TriggerKind = "cron" };
        scheduler.EnqueueCronTrigger(cronRequest);

        var response = await scheduler.AlignAndDispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "SCHEDULER_QUEUE_FULL");
    }

    [Fact]
    public async Task AlignAndDispatchAsync_ClientTriggerCanceled_ReturnsClientCanceledError()
    {
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo);
        var scheduler = new RuntimeTimelineScheduler(NullLogger<RuntimeTimelineScheduler>.Instance, dispatcher);
        using var serviceCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await scheduler.StartAsync(serviceCts.Token);

        try
        {
            using var clientCts = new CancellationTokenSource();
            clientCts.Cancel();

            var response = await scheduler.AlignAndDispatchAsync(
                new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null),
                clientCts.Token);

            Assert.False(response.Success);
            Assert.Contains(response.Errors, e => e.Code == "CLIENT_TRIGGER_CANCELED");
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }
}

/// <summary>
/// Tests that verify Gap-1 completion conditions:
/// - Gap-1a: target_layer_action_destination_selection_is_moved_to_manifest_dispatcher.
/// - Gap-1b: runtime_destination drives actual handler selection (not validation-only).
///
/// When ManifestRepository is configured, ManifestDispatcher resolves destination
/// from the manifest and dispatches to the registered IDispatchableRuntime handler.
/// TargetDispatchOverride is not consulted.
/// </summary>
public class ManifestDispatcherManifestDrivenTests
{
    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_ManifestFound_RoutesToRuntimeExecutor()
    {
        // Gap-1a: manifest-driven path is used when manifest repo is configured.
        var manifestId = Guid.NewGuid();
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology: BuildTopology("topology_transform_runtime"),
                Status: "active"));
        var topologyRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(topologyRepo, manifestRepo);

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_ManifestNotFound_ReturnsManifestNotFound()
    {
        var manifestRepo = new StubManifestRepository(manifest: null);
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "demo", "entity", "list", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_AmbiguousManifest_ReturnsManifestAmbiguous()
    {
        var manifestRepo = new AmbiguousStubManifestRepository();
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_AMBIGUOUS");
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_DefaultEntitySearch_ManifestFound_RoutesToRuntimeExecutor()
    {
        var manifestId = Guid.NewGuid();
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology: BuildTopology("topology_transform_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_UnknownRuntimeDestination_ReturnsRuntimeDestinationUnknown()
    {
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildTopology("unknown_runtime_xyz"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "RUNTIME_DESTINATION_UNKNOWN");
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_AxesMismatch_ReturnsManifestNotFound()
    {
        // AxesFilteredStubManifestRepository returns manifest only for default/entity/Search.
        // A request with non-matching axes (admin/seed_runtime/save) must return MANIFEST_NOT_FOUND.
        var manifestRepo = new AxesFilteredStubManifestRepository(
            matchTarget: "default",
            matchLayer: "entity",
            matchAction: "Search",
            manifest: new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildTopology("topology_transform_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);

        var request = new EndpointRequestDto("Search", "admin", "seed_runtime", "save", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.Emission);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    /// <summary>
    /// Gap-1b verification: runtime_destination selects the registered handler.
    /// FakeDispatchableRuntime is test-only (not in production DI).
    /// Sentinel in Emission.Data proves the correct handler was invoked, not just that
    /// the request succeeded via the default topology pipeline.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_AdminRuntime_SelectsRegisteredHandler_WithSentinel()
    {
        var sentinel = JsonSerializer.SerializeToElement(new { handledBy = "admin_runtime", action = "save" });
        var fakeAdminHandler = new FakeDispatchableRuntime(sentinel);
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildTopology("admin_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime> { ["admin_runtime"] = fakeAdminHandler });

        var request = new EndpointRequestDto("X", "admin", "seed_runtime", "save", null, null, null, TriggerKind: "client", Role: "admin");
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.True(fakeAdminHandler.WasCalled,
            "admin_runtime handler must be called when manifest runtime_destination=admin_runtime.");
        Assert.Equal("admin_runtime",
            response.Emission!.Data!.Value.GetProperty("handledBy").GetString());
        Assert.False(response.Errors.Any());
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_CiAttentionRefreshFragments_RoutesToAdminRuntime()
    {
        var sentinel = JsonSerializer.SerializeToElement(new { handledBy = "admin_runtime", layer = "ci_attention", action = "refresh_fragments" });
        var fakeAdminHandler = new FakeDispatchableRuntime(sentinel);
        var manifestRepo = new AxesFilteredStubManifestRepository(
            matchTarget: "admin",
            matchLayer: "ci_attention",
            matchAction: "refresh_fragments",
            manifest: new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildTopology("admin_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime> { ["admin_runtime"] = fakeAdminHandler });

        var request = new EndpointRequestDto("X", "admin", "ci_attention", "refresh_fragments", null, null, null, TriggerKind: "client", Role: "admin");
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.True(fakeAdminHandler.WasCalled);
        Assert.Equal("admin_runtime", response.Emission!.Data!.Value.GetProperty("handledBy").GetString());
        Assert.Equal("ci_attention", response.Emission.Data!.Value.GetProperty("layer").GetString());
        Assert.Equal("refresh_fragments", response.Emission.Data!.Value.GetProperty("action").GetString());
        Assert.False(response.Errors.Any());
    }

    [Fact]
    public async Task DispatchAsync_ManifestRepositoryConfigured_TopologyTransformRuntime_DoesNotCallAdminHandler()
    {
        var fakeAdminHandler = new FakeDispatchableRuntime(null);
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: BuildTopology("topology_transform_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime> { ["admin_runtime"] = fakeAdminHandler });

        var request = new EndpointRequestDto("Search", "default", "entity", "Search", null, null, null);
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.False(fakeAdminHandler.WasCalled,
            "admin_runtime handler must NOT be called when manifest runtime_destination=topology_transform_runtime.");
    }

    [Fact]
    public async Task DispatchAsync_DbNotifyFromClientTrigger_IsRejected()
    {
        var manifestId = Guid.NewGuid();
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology: BuildDbNotifyProjectionTopology(sourceRuntimeDestination: "topology_transform_runtime", projectionRuntimeDestination: "sse_projection_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime> { ["sse_projection_runtime"] = new FakeDispatchableRuntime(null) });

        var payload = JsonSerializer.SerializeToElement(new { manifest_id = manifestId.ToString() });
        var request = new EndpointRequestDto("X", "db_notify", "projection", "broadcast", null, payload, null, "client");
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "DB_NOTIFY_TRIGGER_KIND_INVALID");
    }

    [Fact]
    public async Task DispatchAsync_DbNotifyHook_PreservesManifestIdentityToHandler()
    {
        var manifestId = Guid.NewGuid();
        var fakeSseHandler = new FakeDispatchableRuntime(null);
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology: BuildDbNotifyProjectionTopology(sourceRuntimeDestination: "topology_transform_runtime", projectionRuntimeDestination: "sse_projection_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime> { ["sse_projection_runtime"] = fakeSseHandler });

        var payload = JsonSerializer.SerializeToElement(new { manifest_id = manifestId.ToString() });
        var request = new EndpointRequestDto("DbNotifyProjection", "db_notify", "projection", "broadcast", null, payload, null, "hook");
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.True(fakeSseHandler.WasCalled);
        Assert.Equal(manifestId, fakeSseHandler.LastManifestId);
    }

    [Fact]
    public async Task DispatchAsync_DbNotifyHook_DoesNotRerouteToSourceRuntimeMapping()
    {
        var manifestId = Guid.NewGuid();
        var fakeSseHandler = new FakeDispatchableRuntime(null);
        var fakeSourceHandler = new FakeDispatchableRuntime(null);
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology: BuildDbNotifyProjectionTopology(sourceRuntimeDestination: "topology_transform_runtime", projectionRuntimeDestination: "sse_projection_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime>
            {
                ["sse_projection_runtime"] = fakeSseHandler,
                ["topology_transform_runtime"] = fakeSourceHandler
            });

        var payload = JsonSerializer.SerializeToElement(new { manifest_id = manifestId.ToString() });
        var request = new EndpointRequestDto("DbNotifyProjection", "db_notify", "projection", "broadcast", null, payload, null, "hook");
        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.True(fakeSseHandler.WasCalled);
        Assert.False(fakeSourceHandler.WasCalled);
    }

    [Fact]
    public async Task DispatchAsync_DbNotifyHook_WithoutProjectionMapping_ReturnsExplicitError()
    {
        var manifestId = Guid.NewGuid();
        var manifestRepo = new StubManifestRepository(
            new ManifestRecord(
                manifestId,
                RelationRegistryId: null,
                Topology: BuildTopology("topology_transform_runtime"),
                Status: "active"));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            manifestRepo);

        var payload = JsonSerializer.SerializeToElement(new { manifest_id = manifestId.ToString() });
        var request = new EndpointRequestDto("DbNotifyProjection", "db_notify", "projection", "broadcast", null, payload, null, "hook");
        var response = await dispatcher.DispatchAsync(request);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "DB_NOTIFY_PROJECTION_MAPPING_MISSING");
    }

    [Fact]
    public async Task DispatchAsync_ManifestRoleAxis_UsesRequestRoleForResolution()
    {
        var roleRepo = new RoleFilteredManifestRepository("admin", new ManifestRecord(
            Guid.NewGuid(),
            RelationRegistryId: null,
            Topology: BuildTopology("admin_runtime"),
            Status: "active"));
        var fakeAdminHandler = new FakeDispatchableRuntime(JsonSerializer.SerializeToElement(new { handledBy = "admin_runtime" }));
        var dispatcher = RuntimeExecutorTests.CreateDispatcher(
            new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double"),
            roleRepo,
            extraHandlers: new Dictionary<string, IDispatchableRuntime> { ["admin_runtime"] = fakeAdminHandler });

        var request = new EndpointRequestDto("X", "admin", "seed_runtime", "save", null, null, null, Role: "admin");
        var response = await dispatcher.DispatchAsync(request);
        Assert.True(response.Success);
        Assert.True(fakeAdminHandler.WasCalled);

        var fail = await dispatcher.DispatchAsync(request with { Role = "viewer" });
        Assert.False(fail.Success);
        Assert.Contains(fail.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
    }

    private static IReadOnlyList<System.Text.Json.JsonElement> BuildTopology(string runtimeDestination)
    {
        var entry = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            type = "runtime_mapping",
            runtime_destination = runtimeDestination
        });
        return [entry];
    }

    private static IReadOnlyList<System.Text.Json.JsonElement> BuildDbNotifyProjectionTopology(
        string sourceRuntimeDestination,
        string projectionRuntimeDestination)
    {
        var runtimeMapping = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            type = "runtime_mapping",
            runtime_destination = sourceRuntimeDestination
        });
        var projectionMapping = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            type = "db_notify_projection_mapping",
            runtime_destination = projectionRuntimeDestination
        });
        return [runtimeMapping, projectionMapping];
    }
}

/// <summary>
/// Test-only stub handler. Records whether it was called and returns sentinel data.
/// Must NOT appear in production DI (Program.cs).
/// Per test_runtime_fixture_policy in docs/design/runtime-orchestration-ssot.yaml.
/// </summary>
internal sealed class FakeDispatchableRuntime : IDispatchableRuntime
{
    private readonly JsonElement? _sentinelData;
    public bool WasCalled { get; private set; }
    public EndpointRequestDto? LastRequest { get; private set; }
    public Guid? LastManifestId { get; private set; }

    public FakeDispatchableRuntime(JsonElement? sentinelData) => _sentinelData = sentinelData;

    public Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default)
    {
        WasCalled = true;
        LastRequest = request;
        LastManifestId = manifestId;
        var emission = new Emission(
            StructureMapId: null,
            PackageId: null,
            SchemaId: null,
            ComponentIds: [],
            Data: _sentinelData,
            Errors: [],
            ContextRouteRecommendation: null);
        return Task.FromResult(new EndpointResponseDto(Success: true, Emission: emission, Errors: []));
    }
}

internal sealed class StubManifestRepository : ManifestRepository
{
    private readonly ManifestRecord? _manifest;

    public StubManifestRepository(ManifestRecord? manifest)
        : base(NullLogger<ManifestRepository>.Instance) =>
        _manifest = manifest;

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action,
        CancellationToken ct = default) =>
        Task.FromResult(_manifest);

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult(_manifest?.ManifestId == manifestId ? _manifest : null);

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(string? statusFilter, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.EmptyList(statusFilter, ct);

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NullDetail(manifestId, ct);

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.ZeroConflicts(role, target, layer, action, excludeManifestId, ct);

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
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.ZeroPromotionConflicts(manifestKey, versionLabel, excludeManifestId, ct);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();
}

internal sealed class AmbiguousStubManifestRepository : ManifestRepository
{
    public AmbiguousStubManifestRepository() : base(NullLogger<ManifestRepository>.Instance) { }

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action,
        CancellationToken ct = default) =>
        throw new InvalidOperationException("MANIFEST_AMBIGUOUS: multiple active manifests match axes.");

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestRecord?>(null);

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(string? statusFilter, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.EmptyList(statusFilter, ct);

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NullDetail(manifestId, ct);

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.ZeroConflicts(role, target, layer, action, excludeManifestId, ct);

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
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.ZeroPromotionConflicts(manifestKey, versionLabel, excludeManifestId, ct);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();
}

internal sealed class AxesFilteredStubManifestRepository : ManifestRepository
{
    private readonly string? _matchTarget;
    private readonly string? _matchLayer;
    private readonly string? _matchAction;
    private readonly ManifestRecord _manifest;

    public AxesFilteredStubManifestRepository(string? matchTarget, string? matchLayer, string? matchAction, ManifestRecord manifest)
        : base(NullLogger<ManifestRepository>.Instance)
    {
        _matchTarget = matchTarget;
        _matchLayer = matchLayer;
        _matchAction = matchAction;
        _manifest = manifest;
    }

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action,
        CancellationToken ct = default)
    {
        if (target == _matchTarget && layer == _matchLayer && action == _matchAction)
            return Task.FromResult<ManifestRecord?>(_manifest);
        return Task.FromResult<ManifestRecord?>(null);
    }

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestRecord?>(null);

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(string? statusFilter, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.EmptyList(statusFilter, ct);

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NullDetail(manifestId, ct);

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.ZeroConflicts(role, target, layer, action, excludeManifestId, ct);

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
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.ZeroPromotionConflicts(manifestKey, versionLabel, excludeManifestId, ct);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();
}



internal sealed class RoleFilteredManifestRepository(string expectedRole, ManifestRecord manifest)
        : ManifestRepository(NullLogger<ManifestRepository>.Instance)
    {
        public override Task<ManifestRecord?> ResolveActiveManifestAsync(string? role, string? target, string? layer, string? action, CancellationToken ct = default)
            => Task.FromResult(role == expectedRole ? manifest : null);

        public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default)
            => Task.FromResult<ManifestRecord?>(manifest.ManifestId == manifestId ? manifest : null);

        public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(string? statusFilter, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.EmptyList(statusFilter, ct);

        public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NullDetail(manifestId, ct);

        public override Task<int> CountActiveAxisConflictsAsync(
            string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.ZeroConflicts(role, target, layer, action, excludeManifestId, ct);

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
            Guid manifestId, System.Text.Json.JsonElement promotionEntry, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NotImplementedDraft();

        public override Task<int> CountActivePromotionKeyConflictsAsync(
            string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.ZeroPromotionConflicts(manifestKey, versionLabel, excludeManifestId, ct);

        public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
            Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
            ManifestRepositoryStubDefaults.NotImplementedMerge();
    }

internal sealed class AttractorFailingTopologyRepository
    : TopologyRepository
{
    public AttractorFailingTopologyRepository()
        : base(NullLogger<TopologyRepository>.Instance, "test-double") { }

    public override Task<StructureMapRecord?> LoadStructureMapAsync(string key, CancellationToken ct = default)
        => throw new InvalidOperationException("Cannot resolve attractor: simulated infrastructure failure.");
}

internal sealed class RecommendationManifestRepository : IAbstractFunctionManifestRepository
{
    public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) =>
        Task.FromResult<AbstractFunctionManifest?>(functionKey == "context_route.recommendation_resolve"
            ? RecommendationAttentionAbstractFunctionTests.CreateSeedManifest()
            : null);
}
