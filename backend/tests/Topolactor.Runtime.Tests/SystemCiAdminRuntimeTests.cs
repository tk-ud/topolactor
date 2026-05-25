using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class SystemCiAdminRuntimeTests
{
    private static readonly IReadOnlyList<string> KnownSystemCiTargets =
    [
        "hub_attention_continuity",
        "current_rebuildability",
        "registry_continuity"
    ];

    [Fact]
    public void DiagnosticRunner_ListTargets_ReturnsCallableTargets()
    {
        var runtime = CreateCiRuntime();

        var targets = runtime.ListTargets();

        Assert.Equal(KnownSystemCiTargets, targets.Select(t => t.Target).ToArray());
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
        var runtime = CreateAdminRuntime(new StubRunner());
        var vector = new OperationVector("admin", "system_ci", "list_targets", null, "admin", null, null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        var arr = data!.Value.EnumerateArray().Select(x => x.GetProperty("Target").GetString()).ToArray();
        Assert.Equal(KnownSystemCiTargets, arr);
    }

    [Fact]
    public async Task AdminRuntime_SystemCiInspect_MissingTarget_ReturnsExplicitError()
    {
        var runtime = CreateAdminRuntime(new StubRunner());
        var vector = new OperationVector("admin", "system_ci", "inspect", null, "admin", JsonSerializer.SerializeToElement(new { }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SYSTEM_CI_TARGET_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task AdminRuntime_SystemCiInspect_UnknownTarget_ReturnsExplicitError()
    {
        var runtime = CreateAdminRuntime(new StubRunner());
        var vector = new OperationVector("admin", "system_ci", "inspect", null, "admin", JsonSerializer.SerializeToElement(new { target = "unknown_target" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("SYSTEM_CI_TARGET_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task AdminRuntime_SystemCiInspect_ReturnsDiagnosticResult()
    {
        var runtime = CreateAdminRuntime(new StubRunner());
        var vector = new OperationVector("admin", "system_ci", "inspect", null, "admin", JsonSerializer.SerializeToElement(new { target = "hub_attention_continuity" }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("hub_attention_continuity", data!.Value.GetProperty("InspectionTarget").GetString());
    }

    private static SystemOperationCiRuntime CreateCiRuntime()
    {
        var repo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy");
        return new SystemOperationCiRuntime(NullLogger<SystemOperationCiRuntime>.Instance, repo);
    }

    private static AdminRuntime CreateAdminRuntime(ISystemCiDiagnosticRunner runner)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo, runner, null);
    }

    private sealed class StubRunner : ISystemCiDiagnosticRunner
    {
        public IReadOnlyList<SystemCiTargetDto> ListTargets() =>
            KnownSystemCiTargets.Select(x => new SystemCiTargetDto(x)).ToArray();

        public Task<SystemCiDiagnosticResult> InspectAsync(string target, CancellationToken ct = default)
        {
            _ = ct;
            if (!KnownSystemCiTargets.Contains(target))
                throw new ArgumentException("unknown target", nameof(target));
            return Task.FromResult(new SystemCiDiagnosticResult(target, SystemCiInspectionKind.CronContinuity, SystemCiStatus.Pass, [], DateTimeOffset.UtcNow));
        }
    }
}
