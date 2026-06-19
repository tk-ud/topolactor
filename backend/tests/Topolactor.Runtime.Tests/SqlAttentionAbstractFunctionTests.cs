using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Seed path tests for sql_attention.list_projection abstract function manifest (af08).
///
/// Coverage:
///   - Seed manifest (admin_runtime lane, admin_sql_attention scope) executes via AbstractFunctionExecutor.
///   - sql_attention primitive returns SqlAttentionTopologyProjectionResult via result context key "projection_result".
///   - MissingPolicy — topology.function_parameters row absent → explicit MissingPolicy status in result (not fail-close).
///   - NoEvidence — logs.attention has no evidence rows → explicit NoEvidence status in result.
///   - Missing source_set_id (null payload) → missing_input fail-close.
///   - Empty/whitespace source_set_id → missing_input fail-close from primitive.
///   - Lane separation — admin_runtime manifest rejected when context uses default external_port_runtime lane.
///   - Lane separation — external_port_runtime manifest rejected when context uses admin_runtime lane.
///   - Authority check — missing logs.attention table binding → invalid_authority fail-close.
///   - Authority check — missing policy binding → missing_authority fail-close.
///   - No route/topology auto-mutation: primitive result is evidence/projection only.
///   - fail-close vocabulary: missing_input, invalid_authority, missing_authority are explicit statuses.
/// </summary>
public class SqlAttentionAbstractFunctionTests
{
    // ---------------------------------------------------------------------------
    // Seed path: successful execution produces SqlAttentionTopologyProjectionResult
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_ColdStart_ReturnsMissingPolicyResult()
    {
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var context = AdminContext(JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" }));

        var result = await executor.ExecuteAsync("sql_attention.list_projection", context);

        Assert.Contains("sql_attention", result.ExecutedPrimitiveKeys);
        var projectionResult = Assert.IsType<SqlAttentionTopologyProjectionResult>(result.ResultContext["projection_result"]);
        Assert.Equal(TopologyProjectionStatus.MissingPolicy, projectionResult.Status);
        Assert.NotNull(projectionResult.StatusDetail);
        Assert.Empty(projectionResult.Candidates);
    }

    [Fact]
    public async Task SeedPath_NoEvidence_ReturnsNoEvidenceResult()
    {
        var (executor, _) = CreateExecutor(CreateSeedManifest(), policyJson: ValidPolicyJson);

        var context = AdminContext(JsonSerializer.SerializeToElement(new { sourceSetId = "empty_set" }));

        var result = await executor.ExecuteAsync("sql_attention.list_projection", context);

        var projectionResult = Assert.IsType<SqlAttentionTopologyProjectionResult>(result.ResultContext["projection_result"]);
        Assert.Equal(TopologyProjectionStatus.NoEvidence, projectionResult.Status);
        Assert.Empty(projectionResult.Candidates);
    }

    // ---------------------------------------------------------------------------
    // Fail-close: missing / empty source_set_id
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_NullPayload_FailsClosedMissingInput()
    {
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var context = AdminContext(requestPayload: null);

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("sql_attention.list_projection", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.MissingInput, ex.Status);
    }

    [Fact]
    public async Task SeedPath_EmptySourceSetId_FailsClosedMissingInput()
    {
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var context = AdminContext(JsonSerializer.SerializeToElement(new { sourceSetId = "   " }));

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("sql_attention.list_projection", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.MissingInput, ex.Status);
    }

    // ---------------------------------------------------------------------------
    // Lane separation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_AdminRuntimeManifest_RejectedInExternalPortContext()
    {
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        // Default RequiredRuntimeLane is "external_port_runtime"
        var context = new AbstractFunctionExecutionContext("admin_sql_attention",
            JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" }));

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("sql_attention.list_projection", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
        Assert.Contains("ABSTRACT_FUNCTION_RUNTIME_LANE_INVALID", ex.Message);
    }

    [Fact]
    public async Task SeedPath_ExternalPortManifest_RejectedInAdminRuntimeContext()
    {
        var externalManifest = CreateSeedManifest() with { RuntimeLane = "external_port_runtime" };
        var (executor, _) = CreateExecutor(externalManifest);
        var context = AdminContext(JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" }));

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("sql_attention.list_projection", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
    }

    // ---------------------------------------------------------------------------
    // Authority checks
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_MissingLogsAttentionTableAuthority_FailsInvalidAuthority()
    {
        var manifest = CreateSeedManifest() with
        {
            AuthorityBindings = new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "admin_sql_attention_projection", true)
                // logs.attention table binding deliberately absent
            }
        };
        var (executor, _) = CreateExecutor(manifest);
        var context = AdminContext(JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" }));

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("sql_attention.list_projection", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
        Assert.Contains("logs.attention", ex.Message);
    }

    [Fact]
    public async Task SeedPath_MissingPolicyAuthorityBinding_FailsMissingAuthority()
    {
        var manifest = CreateSeedManifest() with
        {
            AuthorityBindings = new AbstractFunctionAuthorityBinding[]
            {
                new("table", "logs.attention", true)
                // policy binding deliberately absent
            }
        };
        var (executor, _) = CreateExecutor(manifest);
        var context = AdminContext(JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" }));

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("sql_attention.list_projection", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.MissingAuthority, ex.Status);
    }

    // ---------------------------------------------------------------------------
    // step_config authority: function_name / parameter_key must come from step_config
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_MissingFunctionNameInStepConfig_FailsInvalidAuthority()
    {
        var stepWithoutFunctionName = SeedStep() with
        {
            StepConfig = new Dictionary<string, string> { ["parameter_key"] = "default_policy" }
        };
        var manifest = CreateSeedManifest() with { Steps = new[] { stepWithoutFunctionName } };
        var (executor, _) = CreateExecutor(manifest);
        var context = AdminContext(JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" }));

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("sql_attention.list_projection", context));

        Assert.Equal(AbstractFunctionFailCloseStatus.InvalidAuthority, ex.Status);
        Assert.Contains("FUNCTION_NAME_MISSING", ex.Message);
    }

    [Fact]
    public async Task SeedPath_FunctionNameFromPayload_CannotOverrideStepConfig()
    {
        // Payload has a function_name key — it must not affect step_config resolution.
        // The adapter reads only step.StepConfig["function_name"], not payload.
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var context = AdminContext(JsonSerializer.SerializeToElement(new
        {
            sourceSetId = "test_set",
            function_name = "malicious_override"  // must be ignored
        }));

        // Should succeed via cold start (MissingPolicy from test-double TopologyRepository)
        var result = await executor.ExecuteAsync("sql_attention.list_projection", context);
        var projectionResult = Assert.IsType<SqlAttentionTopologyProjectionResult>(result.ResultContext["projection_result"]);
        // function_name from step_config = "sql_attention_topology_projection"; policy lookup uses that, not payload
        Assert.Equal(TopologyProjectionStatus.MissingPolicy, projectionResult.Status);
    }

    // ---------------------------------------------------------------------------
    // No auto-mutation: result is observation/projection only
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SeedPath_ProjectionResult_DoesNotMutateTopologyOrRoute()
    {
        var (executor, _) = CreateExecutor(CreateSeedManifest());
        var context = AdminContext(JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" }));

        await executor.ExecuteAsync("sql_attention.list_projection", context);

        // Only sql_attention primitive executed — no mutation primitive in chain
        Assert.Equal(new[] { "sql_attention" }, context.ExecutedPrimitiveKeys);
    }

    // ---------------------------------------------------------------------------
    // fail-close vocabulary is explicit across all status paths
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("missing_input")]
    [InlineData("invalid_authority")]
    [InlineData("missing_authority")]
    public async Task FailCloseVocabulary_AllStatusesAreExplicit(string expectedStatus)
    {
        AbstractFunctionManifest manifest;
        JsonElement? payload;

        switch (expectedStatus)
        {
            case "missing_input":
                manifest = CreateSeedManifest();
                payload = null;
                break;
            case "invalid_authority":
                manifest = CreateSeedManifest() with
                {
                    AuthorityBindings = new AbstractFunctionAuthorityBinding[]
                    {
                        new("policy", "admin_sql_attention_projection", true)
                        // logs.attention absent → invalid_authority
                    }
                };
                payload = JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" });
                break;
            default: // missing_authority
                manifest = CreateSeedManifest() with
                {
                    AuthorityBindings = Array.Empty<AbstractFunctionAuthorityBinding>()
                };
                payload = JsonSerializer.SerializeToElement(new { sourceSetId = "test_set" });
                break;
        }

        var (executor, _) = CreateExecutor(manifest);
        var context = AdminContext(payload);

        var ex = await Assert.ThrowsAsync<AbstractFunctionFailCloseException>(
            () => executor.ExecuteAsync("sql_attention.list_projection", context));

        Assert.Equal(expectedStatus, ex.Status);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private const string ValidPolicyJson = """{"top_k": 10, "min_neighbor_score": 0.85, "recent_window_days": 30}""";

    private static AbstractFunctionExecutionContext AdminContext(JsonElement? requestPayload = null) =>
        new("admin_sql_attention", requestPayload, requiredRuntimeLane: "admin_runtime");

    private static AbstractFunctionStep SeedStep() =>
        new(Guid.Parse("00000000-0000-0000-0000-00000000bf10"),
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

    private static AbstractFunctionManifest CreateSeedManifest() =>
        new(AbstractFunctionId: Guid.Parse("00000000-0000-0000-0000-00000000af08"),
            FunctionKey: "sql_attention.list_projection",
            RuntimeLane: "admin_runtime",
            AuthorityScope: "admin_sql_attention",
            Steps: new[] { SeedStep() },
            DeniedProjectionKeys: Array.Empty<string>(),
            Active: true,
            AuthorityBindings: new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "admin_sql_attention_projection", true),
                new("table", "logs.attention", true)
            },
            OutputShape: new Dictionary<string, string> { ["sql_attention_result"] = "projection_result" });

    private static (AbstractFunctionExecutor executor, SqlAttentionProjectionPrimitiveAdapter adapter)
        CreateExecutor(AbstractFunctionManifest manifest, string? policyJson = null)
    {
        var logsRepo = new SqlAttentionLogsRepository(
            NullLogger<SqlAttentionLogsRepository>.Instance, "test-double");
        var topologyRepo = policyJson is null
            ? new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double")
            : new StubTopologyRepositoryWithPolicy(policyJson);
        var adapter = new SqlAttentionProjectionPrimitiveAdapter(
            NullLogger<SqlAttentionProjectionPrimitiveAdapter>.Instance,
            logsRepo,
            topologyRepo);
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

    private sealed class StubTopologyRepositoryWithPolicy : TopologyRepository
    {
        private readonly string _policyJson;
        public StubTopologyRepositoryWithPolicy(string policyJson)
            : base(NullLogger<TopologyRepository>.Instance, "test-double") =>
            _policyJson = policyJson;
        public override Task<string?> LoadFunctionParameterAsync(string functionName, string parameterKey, CancellationToken ct = default) =>
            Task.FromResult<string?>(_policyJson);
    }
}
