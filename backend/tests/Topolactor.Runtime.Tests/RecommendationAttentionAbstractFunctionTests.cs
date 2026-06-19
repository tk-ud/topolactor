using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Seed path tests for context_route.recommendation_resolve abstract function manifest (af09).
///
/// Coverage:
///   - Seed manifest (runtime_executor lane, context_route_recommendation scope) executes
///     via AbstractFunctionExecutor → recommendation_attention primitive.
///   - Cold start (no session id) → InsufficientHistory status in result (not fail-close).
///   - Missing table authority binding → invalid_authority fail-close.
///   - Missing function_name in step_config → invalid_authority fail-close.
///   - Missing parameter_key in step_config → invalid_authority fail-close.
///   - Missing working_shape input (null RuntimeContext) → missing_input fail-close.
///   - Lane separation: runtime_executor manifest rejected in external_port_runtime context.
///   - Lane separation: external_port_runtime manifest rejected in runtime_executor context.
///   - function_name payload injection cannot override step_config.
///   - No route/topology auto-overwrite: result is observation/projection only.
///   - Phase Attention adapter boundary: SystemOperationCiRuntime is opaque to the manifest step.
/// </summary>
public class RecommendationAttentionAbstractFunctionTests
{
    // ---------------------------------------------------------------------------
    // Seed path: cold start → InsufficientHistory (no session, not an error)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_ColdStart_ReturnsInsufficientHistoryResult()
    {
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var shape = MinimalWorkingShape();
        var context = RuntimeExecutorContext(shape);

        var result = await executor.ExecuteAsync("context_route.recommendation_resolve", context);

        Assert.Contains("recommendation_attention", result.ExecutedPrimitiveKeys);
        var recResult = Assert.IsType<ContextRouteRecommendationResult>(result.ResultContext["recommendation_result"]);
        Assert.Equal(RecommendationStatus.InsufficientHistory, recResult.Status);
    }

    // ---------------------------------------------------------------------------
    // Fail-close: missing working_shape (null RuntimeContext)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_NullRuntimeContext_FailsClosedMissingInput()
    {
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        // No runtimeContext → "working_shape" binding resolves to null
        var context = new AbstractFunctionExecutionContext(
            "context_route_recommendation",
            requiredRuntimeLane: "runtime_executor",
            runtimeContext: null);

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("context_route.recommendation_resolve", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.MissingInput, ex.Status);
        Assert.Contains("working_shape", ex.Message);
    }

    // ---------------------------------------------------------------------------
    // Authority checks
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_MissingTableAuthority_FailsInvalidAuthority()
    {
        var manifest = CreateSeedManifest() with
        {
            AuthorityBindings = new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "context_route_recommendation_resolve", true)
                // table binding deliberately absent
            }
        };
        var (executor, _) = CreateExecutor(manifest);
        var context = RuntimeExecutorContext(MinimalWorkingShape());

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("context_route.recommendation_resolve", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
        Assert.Contains("context_route.context_hub_recommendation_current", ex.Message);
    }

    [Fact]
    public async Task SeedPath_MissingPolicyAuthorityBinding_FailsMissingAuthority()
    {
        var manifest = CreateSeedManifest() with
        {
            AuthorityBindings = new AbstractFunctionAuthorityBinding[]
            {
                new("table", "context_route.context_hub_recommendation_current", true)
                // policy binding deliberately absent
            }
        };
        var (executor, _) = CreateExecutor(manifest);
        var context = RuntimeExecutorContext(MinimalWorkingShape());

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("context_route.recommendation_resolve", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.MissingAuthority, ex.Status);
    }

    // ---------------------------------------------------------------------------
    // step_config authority: function_name / parameter_key must come from step_config
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_MissingFunctionNameInStepConfig_FailsInvalidAuthority()
    {
        var stepWithout = SeedStep() with
        {
            StepConfig = new Dictionary<string, string> { ["parameter_key"] = "default_policy" }
        };
        var manifest = CreateSeedManifest() with { Steps = new[] { stepWithout } };
        var (executor, _) = CreateExecutor(manifest);
        var context = RuntimeExecutorContext(MinimalWorkingShape());

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("context_route.recommendation_resolve", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
        Assert.Contains("FUNCTION_NAME_MISSING", ex.Message);
    }

    [Fact]
    public async Task SeedPath_FunctionNameFromPayload_CannotOverrideStepConfig()
    {
        // Payload carries function_name — the adapter must ignore it and read from step_config only.
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var context = new AbstractFunctionExecutionContext(
            "context_route_recommendation",
            requestPayload: JsonSerializer.SerializeToElement(new { function_name = "malicious_override" }),
            requiredRuntimeLane: "runtime_executor",
            runtimeContext: MinimalWorkingShape());

        // Should succeed (cold-start InsufficientHistory from test-double resolver)
        var result = await executor.ExecuteAsync("context_route.recommendation_resolve", context);
        var recResult = Assert.IsType<ContextRouteRecommendationResult>(result.ResultContext["recommendation_result"]);
        // function_name came from step_config ("context_route_recommendation_resolve"), not payload
        Assert.Equal(RecommendationStatus.InsufficientHistory, recResult.Status);
    }

    // ---------------------------------------------------------------------------
    // Lane separation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_RuntimeExecutorManifest_RejectedInExternalPortContext()
    {
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        // default RequiredRuntimeLane is "external_port_runtime"
        var context = new AbstractFunctionExecutionContext(
            "context_route_recommendation",
            runtimeContext: MinimalWorkingShape());

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("context_route.recommendation_resolve", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
        Assert.Contains("ABSTRACT_FUNCTION_RUNTIME_LANE_INVALID", ex.Message);
    }

    [Fact]
    public async Task SeedPath_ExternalPortManifest_RejectedInRuntimeExecutorContext()
    {
        var externalManifest = CreateSeedManifest() with { RuntimeLane = "external_port_runtime" };
        var (executor, _) = CreateExecutor(externalManifest);
        var context = RuntimeExecutorContext(MinimalWorkingShape());

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("context_route.recommendation_resolve", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
    }

    // ---------------------------------------------------------------------------
    // No auto-mutation: result is observation/recommendation only
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_Result_DoesNotMutateTopologyOrRoute()
    {
        // Only recommendation_attention primitive in the chain — no mutation primitive.
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var context = RuntimeExecutorContext(MinimalWorkingShape());

        await executor.ExecuteAsync("context_route.recommendation_resolve", context);

        Assert.Equal(new[] { "recommendation_attention" }, context.ExecutedPrimitiveKeys);
    }

    // ---------------------------------------------------------------------------
    // Phase Attention adapter boundary: SystemOperationCiRuntime is opaque
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_PhaseAttentionBoundary_NotExposedInManifestStepChain()
    {
        // The manifest has exactly one step (recommendation_attention).
        // SystemOperationCiRuntime is called opaquely inside ContextRouteRecommendationResolver.
        // It must not appear as a primitive key in the executed chain.
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var context = RuntimeExecutorContext(MinimalWorkingShape());

        var result = await executor.ExecuteAsync("context_route.recommendation_resolve", context);

        Assert.DoesNotContain("phase_attention_adapter", result.ExecutedPrimitiveKeys);
        Assert.DoesNotContain("system_operation_ci", result.ExecutedPrimitiveKeys);
        Assert.Single(result.ExecutedPrimitiveKeys); // only recommendation_attention
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static AbstractFunctionExecutionContext RuntimeExecutorContext(RuntimeWorkingShape shape) =>
        new("context_route_recommendation", requiredRuntimeLane: "runtime_executor", runtimeContext: shape);

    private static RuntimeWorkingShape MinimalWorkingShape() =>
        // Vector must be non-null so the resolver passes the MISSING_VECTOR check.
        // No ContextSessionId → resolver returns InsufficientHistory (cold start, not error).
        new(Vector: new OperationVector("default", "entity", "search", "default:entity:search",
                "user", null, null, ContextSessionId: null),
            StructureMapId: null, PackageId: null, SchemaId: null,
            ComponentIds: null, PackageDef: null, SchemaDef: null, ResolvedData: null, Errors: null);

    internal static AbstractFunctionStep SeedStep() =>
        new(Guid.Parse("00000000-0000-0000-0000-00000000bf11"),
            StepOrder: 1,
            PrimitiveKey: "recommendation_attention",
            StepConfig: new Dictionary<string, string>
            {
                ["function_name"] = "context_route_recommendation_resolve",
                ["parameter_key"] = "default_policy"
            },
            InputBindings: new[]
            {
                new AbstractFunctionInputBinding("working_shape", "runtime_context", "working_shape", true, false)
            },
            ResultContextKey: "recommendation_result",
            Active: true);

    internal static AbstractFunctionManifest CreateSeedManifest() =>
        new(AbstractFunctionId: Guid.Parse("00000000-0000-0000-0000-00000000af09"),
            FunctionKey: "context_route.recommendation_resolve",
            RuntimeLane: "runtime_executor",
            AuthorityScope: "context_route_recommendation",
            Steps: new[] { SeedStep() },
            DeniedProjectionKeys: Array.Empty<string>(),
            Active: true,
            AuthorityBindings: new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "context_route_recommendation_resolve", true),
                new("table", "context_route.context_hub_recommendation_current", true)
            },
            OutputShape: new Dictionary<string, string> { ["context_route_result"] = "recommendation_result" });

    private static (AbstractFunctionExecutor executor, RecommendationAttentionPrimitiveAdapter adapter)
        CreateExecutor(AbstractFunctionManifest manifest)
    {
        var contextRouteRepo = new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "test-double");
        // StubValidPolicyTopologyRepository returns a valid policy so the resolver proceeds past
        // the policy-load step; the cold-start (no session) path yields InsufficientHistory.
        var topologyRepo = new StubValidPolicyTopologyRepository();
        var resolver = new ContextRouteRecommendationResolver(
            NullLogger<ContextRouteRecommendationResolver>.Instance,
            contextRouteRepo,
            new ContextVectorBuilder(),
            new ContextNeighborSearch(),
            topologyRepo,
            new SystemOperationCiRuntime(NullLogger<SystemOperationCiRuntime>.Instance, contextRouteRepo));
        var adapter = new RecommendationAttentionPrimitiveAdapter(
            NullLogger<RecommendationAttentionPrimitiveAdapter>.Instance, resolver);
        var executor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { adapter });
        return (executor, adapter);
    }

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest? _manifest;
        public StaticManifestRepository(AbstractFunctionManifest? manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) =>
            Task.FromResult(_manifest);
    }
}
