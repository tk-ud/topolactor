using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for AdminRuntime sql_attention:list_projection dispatch.
///
/// Coverage:
///   - sql_attention:list_projection is reachable via ExecuteDataAsync.
///   - Executor not registered → explicit error SQL_ATTENTION_PROJECTION_RUNTIME_NOT_AVAILABLE.
///   - Missing sourceSetId (null payload) → fail-close from abstract function → SQL_ATTENTION_SOURCE_SET_ID_REQUIRED.
///   - Empty sourceSetId → fail-close from sql_attention primitive → SQL_ATTENTION_SOURCE_SET_ID_REQUIRED.
///   - Cold start (no policy) → emission.data status=MissingPolicy (no silent fallback).
///   - Unknown layer:action → ADMIN_OPERATION_NOT_FOUND.
/// </summary>
public class AdminRuntimeSqlAttentionProjectionTests
{
    [Fact]
    public async Task ListProjection_ExecutorNotRegistered_ReturnsExplicitError()
    {
        var runtime = BuildAdminRuntime(abstractFunctionExecutor: null);
        var vector = new OperationVector("admin", "sql_attention", "list_projection", null, "admin",
            JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SQL_ATTENTION_PROJECTION_RUNTIME_NOT_AVAILABLE", error!.Code);
    }

    [Fact]
    public async Task ListProjection_MissingPayload_ReturnsSourceSetIdRequired()
    {
        var runtime = BuildAdminRuntime(CreateAbstractFunctionExecutor());
        var vector = new OperationVector("admin", "sql_attention", "list_projection", null, "admin", null, null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SQL_ATTENTION_SOURCE_SET_ID_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task ListProjection_EmptySourceSetId_ReturnsSourceSetIdRequired()
    {
        var runtime = BuildAdminRuntime(CreateAbstractFunctionExecutor());
        var vector = new OperationVector("admin", "sql_attention", "list_projection", null, "admin",
            JsonSerializer.SerializeToElement(new { sourceSetId = "   " }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SQL_ATTENTION_SOURCE_SET_ID_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task ListProjection_ColdStart_ReturnsMissingPolicyInEmissionData()
    {
        // No policy in function_parameters (test-double TopologyRepository returns null) → MissingPolicy status.
        // Not an error at the dispatch layer; explicit status in emission.data.
        var runtime = BuildAdminRuntime(CreateAbstractFunctionExecutor());
        var vector = new OperationVector("admin", "sql_attention", "list_projection", null, "admin",
            JsonSerializer.SerializeToElement(new { sourceSetId = "test_source_set" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        // Status serialized as snake_case string via _camelCaseSnakeEnumOptions.
        var statusValue = data!.Value.GetProperty("status").GetString();
        Assert.Equal("missing_policy", statusValue);
    }

    [Fact]
    public async Task ListProjection_UnknownLayerAction_ReturnsAdminOperationNotFound()
    {
        var runtime = BuildAdminRuntime(CreateAbstractFunctionExecutor());
        var vector = new OperationVector("admin", "sql_attention", "nonexistent_action", null, "admin",
            JsonSerializer.SerializeToElement(new { sourceSetId = "test" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("ADMIN_OPERATION_NOT_FOUND", error!.Code);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static AbstractFunctionExecutor CreateAbstractFunctionExecutor()
    {
        var logsRepo = new SqlAttentionLogsRepository(
            NullLogger<SqlAttentionLogsRepository>.Instance, "test-double");
        var topologyRepo = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        var adapter = new SqlAttentionProjectionPrimitiveAdapter(
            NullLogger<SqlAttentionProjectionPrimitiveAdapter>.Instance,
            logsRepo,
            topologyRepo);
        var manifest = CreateSeedManifest();
        return new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { adapter });
    }

    private static AbstractFunctionManifest CreateSeedManifest()
    {
        var step = new AbstractFunctionStep(
            AbstractFunctionStepId: Guid.Parse("00000000-0000-0000-0000-00000000bf10"),
            StepOrder: 1,
            PrimitiveKey: "sql_attention",
            StepConfig: new Dictionary<string, string>
            {
                ["function_name"] = SqlAttentionTopologyProjectionRuntime.ProjectionFunctionName,
                ["parameter_key"] = SqlAttentionTopologyProjectionRuntime.ProjectionPolicyKey
            },
            InputBindings: new[]
            {
                new AbstractFunctionInputBinding("source_set_id", "payload", "sourceSetId", true, false)
            },
            ResultContextKey: "projection_result",
            Active: true);

        return new AbstractFunctionManifest(
            AbstractFunctionId: Guid.Parse("00000000-0000-0000-0000-00000000af08"),
            FunctionKey: "sql_attention.list_projection",
            RuntimeLane: "admin_runtime",
            AuthorityScope: "admin_sql_attention",
            Steps: new[] { step },
            DeniedProjectionKeys: Array.Empty<string>(),
            Active: true,
            AuthorityBindings: new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "admin_sql_attention_projection", true),
                new("table", "logs.attention", true)
            },
            OutputShape: new Dictionary<string, string> { ["sql_attention_result"] = "projection_result" });
    }

    private static AdminRuntime BuildAdminRuntime(AbstractFunctionExecutor? abstractFunctionExecutor)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            abstractFunctionExecutor: abstractFunctionExecutor);
    }

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest? _manifest;
        public StaticManifestRepository(AbstractFunctionManifest? manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) =>
            Task.FromResult(_manifest);
    }
}
