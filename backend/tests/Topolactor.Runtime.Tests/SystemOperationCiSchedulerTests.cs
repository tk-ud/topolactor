using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// ---------------------------------------------------------------------------
// Capturing logger
// ---------------------------------------------------------------------------

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public record LogEntry(LogLevel Level, string Message);
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}

// ---------------------------------------------------------------------------
// Stub runtimes
// ---------------------------------------------------------------------------

/// <summary>
/// Stub runtime with controllable per-method result delegates and call tracking.
/// </summary>
internal sealed class ControlledCiRuntime(
    Func<Task<SystemCiDiagnosticResult>> hubInspect,
    Func<Task<SystemCiDiagnosticResult>> rebuildInspect,
    Func<Task<SystemCiDiagnosticResult>> registryInspect)
    : SystemOperationCiRuntime(
        NullLogger<SystemOperationCiRuntime>.Instance,
        new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy"))
{
    public int HubCallCount { get; private set; }
    public int RebuildCallCount { get; private set; }
    public int RegistryCallCount { get; private set; }

    public override Task<SystemCiDiagnosticResult> InspectHubAttentionContinuityAsync(
        CancellationToken ct = default)
    {
        HubCallCount++;
        return hubInspect();
    }

    public override Task<SystemCiDiagnosticResult> InspectCurrentRebuildabilityAsync(
        CancellationToken ct = default)
    {
        RebuildCallCount++;
        return rebuildInspect();
    }

    public override Task<SystemCiDiagnosticResult> InspectRegistryContinuityAsync(
        CancellationToken ct = default)
    {
        RegistryCallCount++;
        return registryInspect();
    }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

internal static class CiResultFactory
{
    public static SystemCiDiagnosticResult Pass(string target = "test_target") =>
        new(target, SystemCiInspectionKind.CronContinuity,
            SystemCiStatus.Pass, [], DateTimeOffset.UtcNow);

    public static SystemCiDiagnosticResult Gap(string target = "test_target") =>
        new(target, SystemCiInspectionKind.CronContinuity,
            SystemCiStatus.Gap,
            [new SystemCiFinding("TEST_GAP", SystemCiStatus.Gap, "gap detail", null)],
            DateTimeOffset.UtcNow);

    public static SystemCiDiagnosticResult Blocking(string target = "test_target") =>
        new(target, SystemCiInspectionKind.CronContinuity,
            SystemCiStatus.Blocking,
            [new SystemCiFinding("TEST_BLOCKING", SystemCiStatus.Blocking, "blocking detail", "id-1")],
            DateTimeOffset.UtcNow);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Tests for SystemOperationCiScheduler: log severity separation and exception isolation.
///
/// Log severity contract:
///   Pass     -> LogInformation (no Warning or Error)
///   Gap      -> LogWarning for each finding (finding-level)
///   Blocking -> LogError for each finding (finding-level)
///
/// Exception isolation:
///   Non-cancellation exception in one inspection -> LogError, subsequent inspections run.
///   OperationCanceledException -> re-thrown, subsequent inspections do not run.
/// </summary>
public class SystemOperationCiSchedulerTests
{
    private static (SystemOperationCiScheduler scheduler, CapturingLogger<SystemOperationCiScheduler> logger)
        CreateScheduler(ControlledCiRuntime runtime)
    {
        var logger = new CapturingLogger<SystemOperationCiScheduler>();
        var scheduler = new SystemOperationCiScheduler(logger, runtime);
        return (scheduler, logger);
    }

    private static ControlledCiRuntime AllPassRuntime() =>
        new(
            () => Task.FromResult(CiResultFactory.Pass("hub_attention_continuity")),
            () => Task.FromResult(CiResultFactory.Pass("current_rebuildability")),
            () => Task.FromResult(CiResultFactory.Pass("registry_continuity")));

    // ─── Pass: only Information entries ──────────────────────────────────────

    [Fact]
    public async Task RunInspectionsAsync_AllPass_OnlyLogsInformation()
    {
        var runtime = AllPassRuntime();
        var (scheduler, logger) = CreateScheduler(runtime);

        await scheduler.RunInspectionsAsync(CancellationToken.None);

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RunInspectionsAsync_AllPass_CallsAllThreeMethods()
    {
        var runtime = AllPassRuntime();
        var (scheduler, _) = CreateScheduler(runtime);

        await scheduler.RunInspectionsAsync(CancellationToken.None);

        Assert.Equal(1, runtime.HubCallCount);
        Assert.Equal(1, runtime.RebuildCallCount);
        Assert.Equal(1, runtime.RegistryCallCount);
    }

    // ─── Gap: Warning per finding, no Error ──────────────────────────────────

    [Fact]
    public async Task RunInspectionsAsync_GapResult_LogsWarningNotError()
    {
        var runtime = new ControlledCiRuntime(
            () => Task.FromResult(CiResultFactory.Gap("hub_attention_continuity")),
            () => Task.FromResult(CiResultFactory.Pass("current_rebuildability")),
            () => Task.FromResult(CiResultFactory.Pass("registry_continuity")));
        var (scheduler, logger) = CreateScheduler(runtime);

        await scheduler.RunInspectionsAsync(CancellationToken.None);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RunInspectionsAsync_GapResult_WarningMessageContainsGap()
    {
        var runtime = new ControlledCiRuntime(
            () => Task.FromResult(CiResultFactory.Gap()),
            () => Task.FromResult(CiResultFactory.Pass()),
            () => Task.FromResult(CiResultFactory.Pass()));
        var (scheduler, logger) = CreateScheduler(runtime);

        await scheduler.RunInspectionsAsync(CancellationToken.None);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.NotEmpty(warnings);
        Assert.All(warnings, w => Assert.Contains("GAP", w.Message));
    }

    // ─── Blocking: Error per finding ─────────────────────────────────────────

    [Fact]
    public async Task RunInspectionsAsync_BlockingResult_LogsError()
    {
        var runtime = new ControlledCiRuntime(
            () => Task.FromResult(CiResultFactory.Blocking("hub_attention_continuity")),
            () => Task.FromResult(CiResultFactory.Pass("current_rebuildability")),
            () => Task.FromResult(CiResultFactory.Pass("registry_continuity")));
        var (scheduler, logger) = CreateScheduler(runtime);

        await scheduler.RunInspectionsAsync(CancellationToken.None);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RunInspectionsAsync_BlockingResult_ErrorMessageContainsBlocking()
    {
        var runtime = new ControlledCiRuntime(
            () => Task.FromResult(CiResultFactory.Blocking()),
            () => Task.FromResult(CiResultFactory.Pass()),
            () => Task.FromResult(CiResultFactory.Pass()));
        var (scheduler, logger) = CreateScheduler(runtime);

        await scheduler.RunInspectionsAsync(CancellationToken.None);

        var errors = logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.Contains("BLOCKING", e.Message));
    }

    [Fact]
    public async Task RunInspectionsAsync_BlockingResult_NoWarningForBlockingFinding()
    {
        // Blocking findings must not appear as Warning — they must be Error only.
        var runtime = new ControlledCiRuntime(
            () => Task.FromResult(CiResultFactory.Blocking()),
            () => Task.FromResult(CiResultFactory.Pass()),
            () => Task.FromResult(CiResultFactory.Pass()));
        var (scheduler, logger) = CreateScheduler(runtime);

        await scheduler.RunInspectionsAsync(CancellationToken.None);

        // Warnings may appear from other inspections but must not contain "BLOCKING"
        var warningsWithBlocking = logger.Entries
            .Where(e => e.Level == LogLevel.Warning && e.Message.Contains("BLOCKING"))
            .ToList();
        Assert.Empty(warningsWithBlocking);
    }

    // ─── Exception isolation ─────────────────────────────────────────────────

    [Fact]
    public async Task RunInspectionsAsync_HubThrowsException_SubsequentInspectionsContinue()
    {
        var runtime = new ControlledCiRuntime(
            () => Task.FromException<SystemCiDiagnosticResult>(new InvalidOperationException("hub DB down")),
            () => Task.FromResult(CiResultFactory.Pass("current_rebuildability")),
            () => Task.FromResult(CiResultFactory.Pass("registry_continuity")));
        var (scheduler, _) = CreateScheduler(runtime);

        await scheduler.RunInspectionsAsync(CancellationToken.None);

        // Rebuild and registry must still be called even though hub threw.
        Assert.Equal(1, runtime.RebuildCallCount);
        Assert.Equal(1, runtime.RegistryCallCount);
    }

    [Fact]
    public async Task RunInspectionsAsync_HubThrowsException_LogsError()
    {
        var runtime = new ControlledCiRuntime(
            () => Task.FromException<SystemCiDiagnosticResult>(new InvalidOperationException("hub DB down")),
            () => Task.FromResult(CiResultFactory.Pass()),
            () => Task.FromResult(CiResultFactory.Pass()));
        var (scheduler, logger) = CreateScheduler(runtime);

        await scheduler.RunInspectionsAsync(CancellationToken.None);

        Assert.Contains(logger.Entries,
            e => e.Level == LogLevel.Error && e.Message.Contains("InspectHubAttentionContinuityAsync"));
    }

    // ─── OperationCanceledException propagation ───────────────────────────────

    [Fact]
    public async Task RunInspectionsAsync_HubThrowsOperationCanceled_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var runtime = new ControlledCiRuntime(
            () =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
            () => Task.FromResult(CiResultFactory.Pass()),
            () => Task.FromResult(CiResultFactory.Pass()));
        var (scheduler, _) = CreateScheduler(runtime);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => scheduler.RunInspectionsAsync(cts.Token));
    }

    [Fact]
    public async Task RunInspectionsAsync_HubThrowsOperationCanceled_SubsequentNotCalled()
    {
        using var cts = new CancellationTokenSource();
        var runtime = new ControlledCiRuntime(
            () =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
            () => Task.FromResult(CiResultFactory.Pass()),
            () => Task.FromResult(CiResultFactory.Pass()));
        var (scheduler, _) = CreateScheduler(runtime);

        try { await scheduler.RunInspectionsAsync(cts.Token); } catch (OperationCanceledException) { }

        // Rebuild and registry must NOT be called after cancellation.
        Assert.Equal(0, runtime.RebuildCallCount);
        Assert.Equal(0, runtime.RegistryCallCount);
    }

    // ─── ParseEnvSeconds bounds ───────────────────────────────────────────────

    [Theory]
    [InlineData("1",    1)]
    [InlineData("30",   30)]
    [InlineData("3600", 3600)]
    public void ParseEnvSeconds_ValidRange_ReturnsParsed(string value, int expectedSeconds)
    {
        const string envKey = "TEST_SYSTEM_CI_DELAY_VALID";
        Environment.SetEnvironmentVariable(envKey, value);
        try
        {
            var result = SystemOperationCiScheduler.ParseEnvSeconds(envKey, TimeSpan.FromSeconds(999));
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, null);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3601")]
    [InlineData("-1")]
    [InlineData("notanumber")]
    [InlineData("")]
    public void ParseEnvSeconds_OutOfRange_ReturnsFallback(string? value)
    {
        const string envKey = "TEST_SYSTEM_CI_DELAY_OUTOFRANGE";
        var fallback = TimeSpan.FromSeconds(999);
        Environment.SetEnvironmentVariable(envKey, value);
        try
        {
            var result = SystemOperationCiScheduler.ParseEnvSeconds(envKey, fallback);
            Assert.Equal(fallback, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, null);
        }
    }

    // ─── ParseEnvHours bounds ─────────────────────────────────────────────────

    [Theory]
    [InlineData("1",  1)]
    [InlineData("12", 12)]
    [InlineData("24", 24)]
    public void ParseEnvHours_ValidRange_ReturnsParsed(string value, int expectedHours)
    {
        const string envKey = "TEST_SYSTEM_CI_INTERVAL_VALID";
        Environment.SetEnvironmentVariable(envKey, value);
        try
        {
            var result = SystemOperationCiScheduler.ParseEnvHours(envKey, TimeSpan.FromHours(999));
            Assert.Equal(TimeSpan.FromHours(expectedHours), result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, null);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("25")]
    [InlineData("-1")]
    [InlineData("notanumber")]
    [InlineData("")]
    public void ParseEnvHours_OutOfRange_ReturnsFallback(string? value)
    {
        const string envKey = "TEST_SYSTEM_CI_INTERVAL_OUTOFRANGE";
        var fallback = TimeSpan.FromHours(999);
        Environment.SetEnvironmentVariable(envKey, value);
        try
        {
            var result = SystemOperationCiScheduler.ParseEnvHours(envKey, fallback);
            Assert.Equal(fallback, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, null);
        }
    }
}
