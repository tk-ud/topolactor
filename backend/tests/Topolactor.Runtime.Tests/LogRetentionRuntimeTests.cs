using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// ---------------------------------------------------------------------------
// Stub helpers
// ---------------------------------------------------------------------------

/// <summary>
/// Stub TopologyRepository returning a given retention policy JSON.
/// All policy values must come from topology data — not from runtime code.
/// </summary>
internal sealed class StubRetentionPolicyTopologyRepository(string policyJson)
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy")
{
    public override Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
        => Task.FromResult<string?>(policyJson);
}

/// <summary>
/// Stub TopologyRepository returning null, simulating a missing policy row.
/// </summary>
internal sealed class StubMissingRetentionPolicyTopologyRepository()
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy")
{
    public override Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}

/// <summary>
/// Stub ContextRouteRepository that records which method was called and with what args.
/// Used to verify that LogRetentionRuntime dispatches to the correct method.
/// </summary>
internal sealed class SpyContextRouteRepository()
    : ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy")
{
    public int DeleteCalls { get; private set; }
    public int ArchiveCalls { get; private set; }
    public int? LastDeleteColdDays { get; private set; }
    public int? LastDeleteHotDays { get; private set; }
    public int? LastArchiveColdDays { get; private set; }
    public int? LastArchiveHotDays { get; private set; }

    public override Task<int> DeleteOldContextEventsAsync(
        int coldDays, int batchSize, int? hotDays = null, CancellationToken ct = default)
    {
        DeleteCalls++;
        LastDeleteColdDays = coldDays;
        LastDeleteHotDays  = hotDays;
        return Task.FromResult(42);
    }

    public override Task<int> ArchiveOldContextEventsAsync(
        int coldDays, int batchSize, int? hotDays = null, CancellationToken ct = default)
    {
        ArchiveCalls++;
        LastArchiveColdDays = coldDays;
        LastArchiveHotDays  = hotDays;
        return Task.FromResult(7);
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class LogRetentionRuntimeTests
{
    // -----------------------------------------------------------------------
    // Missing policy
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_MissingPolicy_ReturnsMissingPolicy()
    {
        var runtime = new LogRetentionRuntime(
            NullLogger<LogRetentionRuntime>.Instance,
            new StubMissingRetentionPolicyTopologyRepository(),
            new SpyContextRouteRepository());

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.MissingPolicy, result.Status);
        Assert.Equal(0, result.RowsAffected);
    }

    // -----------------------------------------------------------------------
    // Invalid policy (MalformedPolicy)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_MalformedJson_ReturnsMalformedPolicy()
    {
        var runtime = MakeRuntime("{not valid json}", new SpyContextRouteRepository());

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.MalformedPolicy, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ColdDaysZero_ReturnsMalformedPolicy()
    {
        var runtime = MakeRuntime(
            PolicyJson(coldDays: 0, hotDays: null),
            new SpyContextRouteRepository());

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.MalformedPolicy, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_HotDaysZero_ReturnsMalformedPolicy()
    {
        var runtime = MakeRuntime(
            PolicyJson(coldDays: 365, hotDays: 0),
            new SpyContextRouteRepository());

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.MalformedPolicy, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_HotDaysNegative_ReturnsMalformedPolicy()
    {
        var runtime = MakeRuntime(
            PolicyJson(coldDays: 365, hotDays: -1),
            new SpyContextRouteRepository());

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.MalformedPolicy, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedArchiveStrategy_ReturnsMalformedPolicy()
    {
        var runtime = MakeRuntime(
            PolicyJson(coldDays: 365, hotDays: null, archiveStrategy: "compress"),
            new SpyContextRouteRepository());

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.MalformedPolicy, result.Status);
        Assert.Contains("compress", result.StatusDetail);
    }

    // -----------------------------------------------------------------------
    // Disabled
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_Disabled_ReturnsDisabledWithoutCallingRepository()
    {
        var spy = new SpyContextRouteRepository();
        var runtime = MakeRuntime(PolicyJson(coldDays: 365, hotDays: null, enabled: false), spy);

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.Disabled, result.Status);
        Assert.Equal(0, spy.DeleteCalls);
        Assert.Equal(0, spy.ArchiveCalls);
    }

    // -----------------------------------------------------------------------
    // delete strategy — success
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_DeleteStrategy_ReturnsOkAndCallsDelete()
    {
        var spy = new SpyContextRouteRepository();
        var runtime = MakeRuntime(PolicyJson(coldDays: 365, hotDays: null, archiveStrategy: "delete"), spy);

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.Ok, result.Status);
        Assert.Equal(1, spy.DeleteCalls);
        Assert.Equal(0, spy.ArchiveCalls);
        Assert.Equal(365, spy.LastDeleteColdDays);
        Assert.Null(spy.LastDeleteHotDays);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteStrategyWithHotDays_PassesHotDaysToRepository()
    {
        var spy = new SpyContextRouteRepository();
        var runtime = MakeRuntime(PolicyJson(coldDays: 365, hotDays: 90, archiveStrategy: "delete"), spy);

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.Ok, result.Status);
        Assert.Equal(1, spy.DeleteCalls);
        Assert.Equal(90, spy.LastDeleteHotDays);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteStrategy_RowsAffectedFromRepository()
    {
        var spy = new SpyContextRouteRepository(); // returns 42
        var runtime = MakeRuntime(PolicyJson(coldDays: 365, hotDays: null), spy);

        var result = await runtime.ExecuteAsync();

        Assert.Equal(42, result.RowsAffected);
    }

    // -----------------------------------------------------------------------
    // archive strategy — success
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ArchiveStrategy_ReturnsOkAndCallsArchive()
    {
        var spy = new SpyContextRouteRepository();
        var runtime = MakeRuntime(PolicyJson(coldDays: 365, hotDays: null, archiveStrategy: "archive"), spy);

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.Ok, result.Status);
        Assert.Equal(0, spy.DeleteCalls);
        Assert.Equal(1, spy.ArchiveCalls);
        Assert.Equal(365, spy.LastArchiveColdDays);
        Assert.Null(spy.LastArchiveHotDays);
    }

    [Fact]
    public async Task ExecuteAsync_ArchiveStrategyWithHotDays_PassesHotDaysToRepository()
    {
        var spy = new SpyContextRouteRepository();
        var runtime = MakeRuntime(PolicyJson(coldDays: 365, hotDays: 90, archiveStrategy: "archive"), spy);

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.Ok, result.Status);
        Assert.Equal(1, spy.ArchiveCalls);
        Assert.Equal(90, spy.LastArchiveHotDays);
    }

    [Fact]
    public async Task ExecuteAsync_ArchiveStrategy_RowsAffectedFromRepository()
    {
        var spy = new SpyContextRouteRepository(); // returns 7
        var runtime = MakeRuntime(PolicyJson(coldDays: 365, hotDays: null, archiveStrategy: "archive"), spy);

        var result = await runtime.ExecuteAsync();

        Assert.Equal(7, result.RowsAffected);
    }

    [Fact]
    public async Task ExecuteAsync_ArchiveStrategy_StatusDetailMentionsColdStorage()
    {
        var spy = new SpyContextRouteRepository();
        var runtime = MakeRuntime(PolicyJson(coldDays: 365, hotDays: null, archiveStrategy: "archive"), spy);

        var result = await runtime.ExecuteAsync();

        Assert.Contains("cold storage", result.StatusDetail, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // hot_days absent (backward compat — should succeed with null hotDays)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_HotDaysAbsentInJson_SucceedsWithNullHotDays()
    {
        var spy = new SpyContextRouteRepository();
        // JSON without hot_days field — verifies backward compat deserialization
        const string policyNoHotDays =
            """{"cold_days":365,"archive_strategy":"delete","batch_size":1000,"enabled":true,"schedule_interval_hours":24}""";
        var runtime = MakeRuntime(policyNoHotDays, spy);

        var result = await runtime.ExecuteAsync();

        Assert.Equal(RetentionExecutionStatus.Ok, result.Status);
        Assert.Null(spy.LastDeleteHotDays);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static LogRetentionRuntime MakeRuntime(
        string policyJson,
        ContextRouteRepository contextRouteRepository) =>
        new(
            NullLogger<LogRetentionRuntime>.Instance,
            new StubRetentionPolicyTopologyRepository(policyJson),
            contextRouteRepository);

    private static string PolicyJson(
        int coldDays = 365,
        int? hotDays = null,
        string archiveStrategy = "delete",
        bool enabled = true,
        int batchSize = 1000,
        int scheduleIntervalHours = 24)
    {
        var hotDaysPart = hotDays.HasValue ? $@"""hot_days"":{hotDays}," : "";
        return $$"""{"cold_days":{{coldDays}},{{hotDaysPart}}"archive_strategy":"{{archiveStrategy}}","batch_size":{{batchSize}},"enabled":{{(enabled ? "true" : "false")}},"schedule_interval_hours":{{scheduleIntervalHours}}}""";
    }
}
