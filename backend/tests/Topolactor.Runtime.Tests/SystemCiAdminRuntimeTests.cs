using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Topolactor.Scheduler;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class SystemCiAdminRuntimeTests
{
    [Fact]
    public void DiagnosticRunner_ListTargets_ReturnsCallableTargets()
    {
        var runtime = CreateCiRuntime();

        var targets = runtime.ListTargets();

        Assert.NotEmpty(targets);
        Assert.All(targets, t => Assert.False(string.IsNullOrWhiteSpace(t.Target)));
    }

    [Fact]
    public async Task DiagnosticRunner_InspectAsync_UnknownTarget_ThrowsArgumentException()
    {
        var runtime = CreateCiRuntime();

        await Assert.ThrowsAsync<ArgumentException>(() => runtime.InspectAsync("unknown_target"));
    }

    [Fact]
    public async Task DiagnosticRunner_InspectAsync_HubAttention_ReturnsSystemCiDiagnosticResult()
    {
        var runtime = CreateCiRuntime();

        var result = await runtime.InspectAsync("hub_attention_continuity");

        Assert.Equal("hub_attention_continuity", result.InspectionTarget);
        Assert.Contains(result.OverallStatus, new[] { SystemCiStatus.Pass, SystemCiStatus.Gap, SystemCiStatus.Blocking });
    }

    [Fact]
    public async Task AdminRuntime_SystemCiListTargets_ReturnsTargetsViaExecuteDataAsync()
    {
        var expectedTargets = CreateCiRuntime().ListTargets().Select(t => t.Target).ToArray();
        var runtime = CreateAdminRuntime(new StubRunner(expectedTargets));
        var vector = new OperationVector("admin", "system_ci", "list_targets", null, "admin", null, null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        var arr = data!.Value.EnumerateArray().Select(x => x.GetProperty("Target").GetString()).ToArray();
        Assert.Equal(expectedTargets, arr);
    }

    [Fact]
    public async Task AdminRuntime_SystemCiInspect_MissingTarget_ReturnsExplicitError()
    {
        var runtime = CreateAdminRuntime(new StubRunner(CreateCiRuntime().ListTargets().Select(t => t.Target).ToArray()));
        var vector = new OperationVector("admin", "system_ci", "inspect", null, "admin", JsonSerializer.SerializeToElement(new { }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SYSTEM_CI_TARGET_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task AdminRuntime_SystemCiInspect_UnknownTarget_ReturnsExplicitError()
    {
        var runtime = CreateAdminRuntime(new StubRunner(CreateCiRuntime().ListTargets().Select(t => t.Target).ToArray()));
        var vector = new OperationVector("admin", "system_ci", "inspect", null, "admin", JsonSerializer.SerializeToElement(new { target = "unknown_target" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SYSTEM_CI_TARGET_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task AdminRuntime_SystemCiInspect_ReturnsDiagnosticResult()
    {
        var expectedTargets = CreateCiRuntime().ListTargets().Select(t => t.Target).ToArray();
        var runtime = CreateAdminRuntime(new StubRunner(expectedTargets));
        var vector = new OperationVector("admin", "system_ci", "inspect", null, "admin", JsonSerializer.SerializeToElement(new { target = expectedTargets[0] }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal(expectedTargets[0], data!.Value.GetProperty("InspectionTarget").GetString());
    }
    [Fact]
    public async Task AdminRuntime_CiAttentionRefreshFragments_WritesFragments_AndInspectRemainsReadOnly()
    {
        var repo = new SpyCiAttentionGuidanceRepository();
        var runtime = CreateAdminRuntime(new StubRunner(["hub_attention_continuity"]), repo);
        var inspectVector = new OperationVector("admin", "system_ci", "inspect", null, "admin", JsonSerializer.SerializeToElement(new { target = "hub_attention_continuity" }), null);
        _ = await runtime.ExecuteDataAsync(inspectVector);
        Assert.Equal(0, repo.UpsertCalls);

        var vector = new OperationVector("admin", "ci_attention", "refresh_fragments", null, "admin",
            JsonSerializer.SerializeToElement(new { target = "hub_attention_continuity", source_kind = "manual_check", source_id = "ci:test", target_kind = "hub", target_key = "hub_attention_continuity", source_table_or_surface = "system_ci" }), null);
        var (data, error) = await runtime.ExecuteDataAsync(vector);
        Assert.Null(error);
        Assert.True(data.HasValue);
        Assert.True(repo.UpsertCalls > 0);
    }

    private static SystemOperationCiRuntime CreateCiRuntime()
    {
        var repo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy");
        return new SystemOperationCiRuntime(NullLogger<SystemOperationCiRuntime>.Instance, repo);
    }

    private static AdminRuntime CreateAdminRuntime(ISystemCiDiagnosticRunner runner, CiAttentionGuidanceRepository? repo = null)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo, runner, null, repo, new SseEventBroadcaster());
    }
    private sealed class SpyCiAttentionGuidanceRepository : CiAttentionGuidanceRepository
    {
        public int UpsertCalls { get; private set; }
        public override Task<CiAttentionGuidanceFragmentStored> UpsertCurrentAppendHistoryAsync(CiAttentionGuidanceFragmentUpsert fragment, CancellationToken ct = default)
        {
            UpsertCalls++;
            var p = new CiAttentionGuidanceGuidanceEventPayload(Guid.NewGuid(), "missing_input", "active", "warning", fragment.TargetKind, fragment.TargetKey, "admin_ui_builder", DateTimeOffset.UtcNow);
            return Task.FromResult(new CiAttentionGuidanceFragmentStored(p.FragmentId, p));
        }
    }

    private sealed class StubRunner : ISystemCiDiagnosticRunner
    {
        private readonly IReadOnlyList<string> _targets;
        public StubRunner(IReadOnlyList<string> targets) => _targets = targets;

        public IReadOnlyList<SystemCiTargetDto> ListTargets() =>
            _targets.Select(x => new SystemCiTargetDto(x)).ToArray();

        public Task<SystemCiDiagnosticResult> InspectAsync(string target, CancellationToken ct = default)
        {
            _ = ct;
            if (!_targets.Contains(target))
                throw new ArgumentException("unknown target", nameof(target));
            return Task.FromResult(new SystemCiDiagnosticResult(
                target,
                SystemCiInspectionKind.CronContinuity,
                SystemCiStatus.Gap,
                [new SystemCiFinding("STUB_GAP", SystemCiStatus.Gap, "stub detail", Classification: SystemCiFindingClassification.NotCovered)],
                DateTimeOffset.UtcNow));
        }
    }
}
