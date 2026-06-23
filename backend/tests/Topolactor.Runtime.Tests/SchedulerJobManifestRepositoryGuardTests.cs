using Topolactor.Repository;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Identifier authority guard tests for NpgsqlSchedulerJobManifestRepository.
/// SSOT: docs/design/scheduler-job-manifest-ssot.yaml (input_and_table_authority).
///
/// Table/column authority is manifest-defined; any non-identifier value (e.g. an
/// injected / payload-shaped table or column reference) is fail-closed BEFORE a
/// database connection is opened. These tests assert the guard without a live DB.
/// </summary>
public class SchedulerJobManifestRepositoryGuardTests
{
    private static readonly NpgsqlSchedulerJobManifestRepository Repo = new("Host=localhost;Database=unused");

    private static SchedulerJobRecord JobWithInputTable(string inputTableRef, string idColumn = "id", string statusColumn = "status") =>
        new(Guid.NewGuid(), "j", "cron", "interval_seconds", null, 60, false, true,
            inputTableRef, statusColumn, "pending", "processing", "completed", "failed",
            10, 60, "scope", null, null, new Dictionary<string, object?>(StringComparer.Ordinal))
        {
            InputIdColumn = idColumn,
        };

    [Fact]
    public async Task LeaseDueInputRows_InjectedTableRef_FailClosed()
    {
        var job = JobWithInputTable("public.users; DROP TABLE x");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Repo.LeaseDueInputRowsAsync(job, DateTimeOffset.UtcNow));
        Assert.Contains("SCHEDULER_MANIFEST_TABLE_REF_INVALID", ex.Message);
    }

    [Fact]
    public async Task LeaseDueInputRows_InjectedIdColumn_FailClosed()
    {
        var job = JobWithInputTable("demo.input_rows", idColumn: "id, (SELECT secret)");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Repo.LeaseDueInputRowsAsync(job, DateTimeOffset.UtcNow));
        Assert.Contains("SCHEDULER_MANIFEST_COLUMN_INVALID", ex.Message);
    }

    [Fact]
    public async Task UpsertAuthorizedOutput_InjectedColumn_FailClosed()
    {
        var job = new SchedulerJobRecord(Guid.NewGuid(), "j", "cron", "interval_seconds", null, 60, false, true,
            null, null, null, null, null, null, 10, 60, "scope", null, null,
            new Dictionary<string, object?>(StringComparer.Ordinal))
        {
            OutputTableRef = "demo.output_rows",
        };
        var binding = new SchedulerStepResultBinding("output_upsert", "k", Array.Empty<string>(),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["region"] = "k" });
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { ["bad column;"] = "x" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Repo.UpsertAuthorizedOutputAsync(job, binding, values));
        Assert.Contains("SCHEDULER_MANIFEST_COLUMN_INVALID", ex.Message);
    }

    [Fact]
    public async Task UpsertAuthorizedOutput_InjectedTableRef_FailClosed()
    {
        var job = new SchedulerJobRecord(Guid.NewGuid(), "j", "cron", "interval_seconds", null, 60, false, true,
            null, null, null, null, null, null, 10, 60, "scope", null, null,
            new Dictionary<string, object?>(StringComparer.Ordinal))
        {
            OutputTableRef = "evil table",
        };
        var binding = new SchedulerStepResultBinding("output_upsert", "k", Array.Empty<string>(),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["region"] = "k" });
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { ["region"] = "x" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Repo.UpsertAuthorizedOutputAsync(job, binding, values));
        Assert.Contains("SCHEDULER_MANIFEST_TABLE_REF_INVALID", ex.Message);
    }
}
