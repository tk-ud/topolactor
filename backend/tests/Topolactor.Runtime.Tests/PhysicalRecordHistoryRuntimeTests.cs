using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class PhysicalRecordHistoryRuntimeTests
{
    [Fact]
    public async Task Repository_TestDouble_LoadPhysicalRecordHistory_ReturnsExplicitEmptyHistory()
    {
        var repo = new SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "test-double");

        var history = await repo.LoadPhysicalRecordHistoryAsync("42", "rec-1");

        Assert.Empty(history);
    }

    [Fact]
    public async Task ExecuteDataAsync_PhysicalRecordListHistory_ReturnsLogsDiffHistory()
    {
        var history = new[]
        {
            new PhysicalRecordHistoryEntry(Guid.NewGuid(), "42", "public.orders", "rec-1", "create", "{}", "{\"name\":\"A\"}", DateTimeOffset.Parse("2026-06-12T00:00:00Z"), "test", "required"),
            new PhysicalRecordHistoryEntry(Guid.NewGuid(), "42", "public.orders", "rec-1", "update", "{\"name\":\"A\"}", "{\"name\":\"B\"}", DateTimeOffset.Parse("2026-06-12T00:01:00Z"), "test", "required"),
            new PhysicalRecordHistoryEntry(Guid.NewGuid(), "42", "public.orders", "rec-1", "logical_delete", "{\"deleted\":false}", "{\"deleted\":true}", DateTimeOffset.Parse("2026-06-12T00:02:00Z"), "test", "required"),
            new PhysicalRecordHistoryEntry(Guid.NewGuid(), "42", "public.orders", "rec-1", "restore", "{\"deleted\":true}", "{\"deleted\":false}", DateTimeOffset.Parse("2026-06-12T00:03:00Z"), "test", "required"),
            new PhysicalRecordHistoryEntry(Guid.NewGuid(), "42", "public.orders", "rec-1", "physical_delete", "{\"id\":\"rec-1\"}", "{}", DateTimeOffset.Parse("2026-06-12T00:04:00Z"), "test", "required"),
        };
        var repo = new StubPhysicalRecordHistoryRepository(history);
        var runtime = CreateRuntime(repo);

        var vector = new OperationVector(
            "admin",
            "physical_record",
            "list_history",
            null,
            "admin",
            JsonSerializer.SerializeToElement(new { tableId = "42", recordId = "rec-1" }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("ok", data!.Value.GetProperty("status").GetString());
        Assert.Equal("42", data.Value.GetProperty("tableId").GetString());
        Assert.Equal("rec-1", data.Value.GetProperty("recordId").GetString());
        var responseHistory = data.Value.GetProperty("history");
        Assert.Equal(5, responseHistory.GetArrayLength());
        Assert.Equal("create", responseHistory[0].GetProperty("operation_kind").GetString());
        Assert.Equal("{\"name\":\"A\"}", responseHistory[0].GetProperty("after_state_or_diff_json").GetString());
        Assert.Equal(new[] { "create", "update", "logical_delete", "restore", "physical_delete" }, repo.OperationsRequested);
        Assert.Equal("42", repo.LastTableId);
        Assert.Equal("rec-1", repo.LastRecordId);
    }

    [Fact]
    public async Task ExecuteDataAsync_PhysicalRecordListHistory_EmptyRowsReturnsExplicitEmptyStatus()
    {
        var runtime = CreateRuntime(new StubPhysicalRecordHistoryRepository([]));
        var vector = new OperationVector(
            "admin",
            "physical_record",
            "list_history",
            null,
            "admin",
            JsonSerializer.SerializeToElement(new { physicalTableId = "42", recordId = "rec-missing" }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("empty_history", data!.Value.GetProperty("status").GetString());
        Assert.Equal(0, data.Value.GetProperty("history").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteDataAsync_PhysicalRecordListHistory_RequiresExplicitIdentity()
    {
        var runtime = CreateRuntime(new StubPhysicalRecordHistoryRepository([]));
        var vector = new OperationVector(
            "admin",
            "physical_record",
            "list_history",
            null,
            "admin",
            JsonSerializer.SerializeToElement(new { tableId = "42" }),
            null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PHYSICAL_RECORD_RECORD_ID_REQUIRED", error!.Code);
    }

    [Fact]
    public void NpgsqlRepository_Source_QueryUsesLogsDiffAndNotTopologyEditLog()
    {
        var source = File.ReadAllText(FindRepositoryFile("backend/repository/NpgsqlSqlAttentionLogsRepository.cs"));

        Assert.Contains("FROM logs.diff", source);
        Assert.Contains("physical_table_id = @physical_table_id", source);
        Assert.Contains("record_id = @record_id", source);
        Assert.DoesNotContain("FROM topology_edit_log", source);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file {relativePath}");
    }

    private static AdminRuntime CreateRuntime(SqlAttentionLogsRepository logsRepo)
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
            sqlAttentionLogsRepository: logsRepo);
    }

    private sealed class StubPhysicalRecordHistoryRepository(IReadOnlyList<PhysicalRecordHistoryEntry> history)
        : SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "test-double")
    {
        public string? LastTableId { get; private set; }
        public string? LastRecordId { get; private set; }
        public string[] OperationsRequested => history.Select(h => h.OperationKind).ToArray();

        public override Task<IReadOnlyList<PhysicalRecordHistoryEntry>> LoadPhysicalRecordHistoryAsync(string tableId, string recordId, CancellationToken ct = default)
        {
            LastTableId = tableId;
            LastRecordId = recordId;
            return Task.FromResult(history);
        }
    }
}
