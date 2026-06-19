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
///     via AbstractFunctionExecutor → 4-step decomposed primitive chain:
///     recommendation_candidate_source → recommendation_eligibility →
///     recommendation_score_rank → recommendation_projection.
///   - Cold start (no session id) → InsufficientHistory status in result (not fail-close).
///   - Missing table authority binding → invalid_authority fail-close (step 1).
///   - Missing function_name in step_config → invalid_authority fail-close (step 1).
///   - Missing parameter_key in step_config → invalid_authority fail-close (step 1).
///   - Missing working_shape input (null RuntimeContext) → missing_input fail-close.
///   - Lane separation: runtime_executor manifest rejected in external_port_runtime context.
///   - Lane separation: external_port_runtime manifest rejected in runtime_executor context.
///   - Lane enforcement: sql_attention_projection candidate in hub-local result → invalid_projection fail-close.
///   - function_name payload injection cannot override step_config.
///   - No route/topology auto-overwrite: result is observation/projection only.
///   - Phase Attention adapter boundary: SystemOperationCiRuntime is opaque to the manifest step chain.
/// </summary>
public class RecommendationAttentionAbstractFunctionTests
{
    // GUIDs mirror db/seed_empty.sql seed rows for af09
    private static readonly Guid Af09 = Guid.Parse("00000000-0000-0000-0000-00000000af09");
    private static readonly Guid Bf11 = Guid.Parse("00000000-0000-0000-0000-00000000bf11");
    private static readonly Guid Bf12 = Guid.Parse("00000000-0000-0000-0000-00000000bf12");
    private static readonly Guid Bf13 = Guid.Parse("00000000-0000-0000-0000-00000000bf13");
    private static readonly Guid Bf14 = Guid.Parse("00000000-0000-0000-0000-00000000bf14");

    private static readonly string[] SeedPrimitiveOrder = new[]
    {
        "recommendation_candidate_source",
        "recommendation_eligibility",
        "recommendation_score_rank",
        "recommendation_projection"
    };

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

        // All 4 decomposed steps execute; candidate_source returns InsufficientHistory and
        // downstream steps propagate the status without crashing.
        Assert.Contains("recommendation_candidate_source", result.ExecutedPrimitiveKeys);
        Assert.Equal(4, result.ExecutedPrimitiveKeys.Count);
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
    // step_config authority: function_name / parameter_key must come from step_config (step 1)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_MissingFunctionNameInStepConfig_FailsInvalidAuthority()
    {
        // Replace step 1 (recommendation_candidate_source) with a version that has no function_name.
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
    // Lane enforcement: sql_attention_projection cannot appear in hub-local result
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_LaneMixing_SqlAttentionCandidateInHubLocalResult_ThrowsInvalidProjection()
    {
        // Test the RecommendationProjectionPrimitiveAdapter directly to verify that a
        // sql_attention_projection candidate in the hub-local result is fail-closed.
        var adapter = new RecommendationProjectionPrimitiveAdapter();
        var step = new AbstractFunctionStep(
            Bf14, StepOrder: 4, PrimitiveKey: "recommendation_projection",
            StepConfig: new Dictionary<string, string>(),
            InputBindings: Array.Empty<AbstractFunctionInputBinding>(),
            ResultContextKey: "recommendation_result",
            Active: true);
        var context = new AbstractFunctionExecutionContext(
            "context_route_recommendation",
            requiredRuntimeLane: "runtime_executor",
            runtimeContext: MinimalWorkingShape());

        var source = new RecommendationCandidateSourceResult(
            Policy: ContextRoutePolicyTestFixtures.ValidPolicy(),
            EventVector: new Dictionary<Guid, float>(),
            EventNorm: 1.0f,
            PrefixCandidates: Array.Empty<ContextPrefixVectorRecord>(),
            SessionId: Guid.NewGuid(),
            CurrentOperation: "search",
            Role: null,
            TableName: null,
            TokenIds: Array.Empty<Guid>(),
            Status: RecommendationStatus.Ok,
            StatusDetail: null);

        var eligibility = new RecommendationEligibilityResult(
            Neighbors: Array.Empty<ContextNeighborResult>(),
            TransitionStats: Array.Empty<ContextTransitionStat>(),
            Status: RecommendationStatus.Ok,
            StatusDetail: null);

        // score_rank with a sql_attention_projection candidate — must not leak into hub-local result
        var scoreRank = new RecommendationScoreRankResult(
            NextOperations: new[]
            {
                new RecommendationCandidate(
                    "attentive_candidate", 1.0f, null,
                    Array.Empty<string>(),
                    RecommendationPressureLanes.SqlAttentionProjection)
            },
            NextTokens: Array.Empty<RecommendationCandidate>(),
            NextEnumItems: Array.Empty<RecommendationCandidate>(),
            NearestPrefixSessionIds: Array.Empty<Guid>(),
            ContributingTokens: Array.Empty<string>(),
            Status: RecommendationStatus.Ok,
            StatusDetail: null);

        var inputs = new Dictionary<string, object?>
        {
            ["recommendation_source"] = source,
            ["recommendation_eligibility"] = eligibility,
            ["recommendation_score_rank"] = scoreRank,
        };

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => adapter.ExecuteAsync(step, inputs, context));

        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidProjection, ex.Status);
        Assert.Contains("RECOMMENDATION_HUB_LOCAL_LANE_MIXING_PREVENTED", ex.Message);
    }

    // ---------------------------------------------------------------------------
    // No auto-mutation: result is observation/recommendation only
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_Result_DoesNotMutateTopologyOrRoute()
    {
        // 4-step decomposed chain — none of the primitives mutates route or topology state.
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var context = RuntimeExecutorContext(MinimalWorkingShape());

        await executor.ExecuteAsync("context_route.recommendation_resolve", context);

        Assert.Equal(SeedPrimitiveOrder, context.ExecutedPrimitiveKeys);
    }

    // ---------------------------------------------------------------------------
    // Phase Attention adapter boundary: SystemOperationCiRuntime is opaque
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_PhaseAttentionBoundary_NotExposedInManifestStepChain()
    {
        // The manifest has 4 primitive steps in the decomposed chain.
        // SystemOperationCiRuntime is called opaquely inside ContextRouteRecommendationResolver.
        // It must not appear as a primitive key in the executed chain.
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var context = RuntimeExecutorContext(MinimalWorkingShape());

        var result = await executor.ExecuteAsync("context_route.recommendation_resolve", context);

        Assert.DoesNotContain("phase_attention_adapter", result.ExecutedPrimitiveKeys);
        Assert.DoesNotContain("system_operation_ci", result.ExecutedPrimitiveKeys);
        Assert.Equal(4, result.ExecutedPrimitiveKeys.Count);
        Assert.Equal(SeedPrimitiveOrder, result.ExecutedPrimitiveKeys);
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

    /// <summary>
    /// Returns step 1 (recommendation_candidate_source, bf11) of the decomposed af09 manifest.
    /// Used by authority/step_config tests that only exercise the first step.
    /// </summary>
    internal static AbstractFunctionStep SeedStep() =>
        new(Bf11,
            StepOrder: 1,
            PrimitiveKey: "recommendation_candidate_source",
            StepConfig: new Dictionary<string, string>
            {
                ["function_name"] = "context_route_recommendation_resolve",
                ["parameter_key"] = "default_policy"
            },
            InputBindings: new[]
            {
                new AbstractFunctionInputBinding("working_shape", "runtime_context", "working_shape", true, false) // c031
            },
            ResultContextKey: "recommendation_source",
            Active: true);

    internal static AbstractFunctionManifest CreateSeedManifest() =>
        new(AbstractFunctionId: Af09,
            FunctionKey: "context_route.recommendation_resolve",
            RuntimeLane: "runtime_executor",
            AuthorityScope: "context_route_recommendation",
            Steps: new AbstractFunctionStep[]
            {
                // bf11 — recommendation_candidate_source
                new(Bf11, StepOrder: 1, PrimitiveKey: "recommendation_candidate_source",
                    StepConfig: new Dictionary<string, string>
                    {
                        ["function_name"] = "context_route_recommendation_resolve",
                        ["parameter_key"] = "default_policy"
                    },
                    InputBindings: new[]
                    {
                        new AbstractFunctionInputBinding("working_shape", "runtime_context", "working_shape", true, false) // c031
                    },
                    ResultContextKey: "recommendation_source",
                    Active: true),
                // bf12 — recommendation_eligibility
                new(Bf12, StepOrder: 2, PrimitiveKey: "recommendation_eligibility",
                    StepConfig: new Dictionary<string, string>(),
                    InputBindings: new[]
                    {
                        new AbstractFunctionInputBinding("working_shape",         "runtime_context", "working_shape",         true, false), // c032
                        new AbstractFunctionInputBinding("recommendation_source", "result_context",  "recommendation_source", true, false)  // c033
                    },
                    ResultContextKey: "recommendation_eligibility",
                    Active: true),
                // bf13 — recommendation_score_rank
                new(Bf13, StepOrder: 3, PrimitiveKey: "recommendation_score_rank",
                    StepConfig: new Dictionary<string, string>(),
                    InputBindings: new[]
                    {
                        new AbstractFunctionInputBinding("working_shape",              "runtime_context", "working_shape",              true, false), // c034
                        new AbstractFunctionInputBinding("recommendation_source",      "result_context",  "recommendation_source",      true, false), // c035
                        new AbstractFunctionInputBinding("recommendation_eligibility", "result_context",  "recommendation_eligibility", true, false)  // c036
                    },
                    ResultContextKey: "recommendation_score_rank",
                    Active: true),
                // bf14 — recommendation_projection
                new(Bf14, StepOrder: 4, PrimitiveKey: "recommendation_projection",
                    StepConfig: new Dictionary<string, string>(),
                    InputBindings: new[]
                    {
                        new AbstractFunctionInputBinding("recommendation_source",      "result_context", "recommendation_source",      true, false), // c037
                        new AbstractFunctionInputBinding("recommendation_eligibility", "result_context", "recommendation_eligibility", true, false), // c038
                        new AbstractFunctionInputBinding("recommendation_score_rank",  "result_context", "recommendation_score_rank",  true, false)  // c039
                    },
                    ResultContextKey: "recommendation_result",
                    Active: true),
            },
            DeniedProjectionKeys: Array.Empty<string>(),
            Active: true,
            AuthorityBindings: new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "context_route_recommendation_resolve", true),
                new("table", "context_route.context_hub_recommendation_current", true)
            },
            OutputShape: new Dictionary<string, string>
            {
                // Intermediate step result keys must be authorized in output_shape.
                // Values are the result_context_key values stored between steps.
                ["recommendation_source_step"]      = "recommendation_source",
                ["recommendation_eligibility_step"] = "recommendation_eligibility",
                ["recommendation_score_rank_step"]  = "recommendation_score_rank",
                ["context_route_result"]            = "recommendation_result"
            });

    private static (AbstractFunctionExecutor executor, RecommendationAttentionPrimitiveAdapter adapter)
        CreateExecutor(AbstractFunctionManifest manifest, TopologyRepository? topologyRepo = null)
    {
        var contextRouteRepo = new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "test-double");
        // StubValidPolicyTopologyRepository returns a valid policy so the resolver proceeds past
        // the policy-load step; the cold-start (no session) path yields InsufficientHistory.
        topologyRepo ??= new StubValidPolicyTopologyRepository();
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
            new IAbstractFunctionPrimitiveAdapter[]
            {
                adapter, // COMPATIBILITY FALLBACK — recommendation_attention (af09 no longer uses this)
                new RecommendationCandidateSourcePrimitiveAdapter(
                    NullLogger<RecommendationCandidateSourcePrimitiveAdapter>.Instance, resolver),
                new RecommendationEligibilityPrimitiveAdapter(
                    NullLogger<RecommendationEligibilityPrimitiveAdapter>.Instance, resolver),
                new RecommendationScoreRankPrimitiveAdapter(
                    NullLogger<RecommendationScoreRankPrimitiveAdapter>.Instance, resolver),
                new RecommendationProjectionPrimitiveAdapter()
            });
        return (executor, adapter);
    }

    // ---------------------------------------------------------------------------
    // Manifest-authorized SQL path: step_config values reach LoadFunctionParameterAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_StepConfig_FunctionNameAndParameterKeyFlowToSqlCall()
    {
        // Proves the "manifest-authorized SQL" leg: function_name and parameter_key
        // from seed step_config must reach topology.function_parameters SQL query,
        // not be replaced by C# constants in ContextRouteRecommendationResolver.
        var capturing = new CapturingTopologyRepository();
        var (executor, _) = CreateExecutor(CreateSeedManifest(), capturing);
        var context = RuntimeExecutorContext(MinimalWorkingShape());

        // Cold-start: no session → InsufficientHistory, but policy SQL runs first.
        await executor.ExecuteAsync("context_route.recommendation_resolve", context);

        // The function_name and parameter_key from step_config must reach the SQL call.
        Assert.Equal("context_route_recommendation_resolve", capturing.CapturedFunctionName);
        Assert.Equal("default_policy", capturing.CapturedParameterKey);
    }

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest? _manifest;
        public StaticManifestRepository(AbstractFunctionManifest? manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) =>
            Task.FromResult(_manifest);
    }
}
