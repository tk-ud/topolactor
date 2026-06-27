using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public sealed class AuthorizedCliReaderPortRuntimeTests
{
    [Theory]
    [InlineData("read")]
    [InlineData("search")]
    [InlineData("aggregate")]
    [InlineData("analyze")]
    [InlineData("validate")]
    public async Task Success_operations_require_dispatch_and_authorized_scope(string operation)
    {
        var repo = new InMemoryCliReaderPortRepository(DefaultConfig());
        var runtime = new AuthorizedCliReaderPortRuntime(NullLogger<AuthorizedCliReaderPortRuntime>.Instance, repo);

        var response = await runtime.ExecuteAsync(Request(operation), Guid.NewGuid());

        Assert.True(response.Success);
        Assert.Equal(operation, repo.Queries.Single().Operation);
        Assert.Equal("state_id=active", repo.Queries.Single().RowScope);
        Assert.DoesNotContain(repo.Events, e => e.ScopeSummary.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(repo.Events, e => e.Status == "success" && e.Operation == operation);
    }

    [Theory]
    [InlineData(null, "CLI_READER_AUTH_REQUIRED")]
    [InlineData("missing_role", "CLI_READER_ROLE_DENIED")]
    [InlineData("disabled", "CLI_READER_PORT_DISABLED")]
    [InlineData("expired", "CLI_READER_PORT_EXPIRED")]
    [InlineData("bad_user", "CLI_READER_USER_DENIED")]
    [InlineData("bad_capability", "CLI_READER_CAPABILITY_UNRESOLVED")]
    [InlineData("bad_table", "CLI_READER_TABLE_DENIED")]
    [InlineData("bad_column", "CLI_READER_COLUMN_DENIED")]
    [InlineData("bad_filter", "CLI_READER_FILTER_DENIED")]
    [InlineData("bad_period", "CLI_READER_PERIOD_DENIED")]
    [InlineData("missing_row_scope", "CLI_READER_ROW_SCOPE_UNRESOLVED")]
    public async Task Fail_close_auth_scope_and_capability_errors(string? scenario, string code)
    {
        var config = scenario switch
        {
            "disabled" => DefaultConfig() with { Enabled = false },
            "expired" => DefaultConfig() with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) },
            "missing_row_scope" => DefaultConfig() with { RowScopeByUser = new Dictionary<string, string>() },
            _ => DefaultConfig()
        };
        if (scenario == "bad_user") config = config with { AllowedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "other" } };
        var repo = new InMemoryCliReaderPortRepository(config);
        var request = scenario switch
        {
            null => Request("read", userId: null),
            "missing_role" => Request("read", roles: ["guest"]),
            "bad_capability" => Request("read", capabilities: []),
            "bad_table" => Request("read", table: "topology.secret_table"),
            "bad_column" => Request("read", columns: ["entity_id", "credential"]),
            "bad_filter" => Request("read", filters: new Dictionary<string, string> { ["unapproved_status"] = "active" }),
            "bad_period" => Request("read", period: "all_time"),
            _ => Request("read")
        };
        var runtime = new AuthorizedCliReaderPortRuntime(NullLogger<AuthorizedCliReaderPortRuntime>.Instance, repo);

        var response = await runtime.ExecuteAsync(request, Guid.NewGuid());

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == code);
        Assert.Empty(repo.Queries);
        Assert.Contains(repo.Events, e => e.Status == "fail_close" && e.Code == code);
    }


    [Fact]
    public async Task Create_export_job_records_manifest_checksum_source_ids_and_runtime_event()
    {
        var repo = new InMemoryCliReaderPortRepository(DefaultConfig());
        var runtime = new AuthorizedCliReaderPortRuntime(NullLogger<AuthorizedCliReaderPortRuntime>.Instance, repo);

        var response = await runtime.ExecuteAsync(Request("create_export_job", extra: new Dictionary<string, object?> { ["export_format"] = "json" }), Guid.NewGuid());

        Assert.True(response.Success);
        Assert.Single(repo.ExportJobs);
        var job = repo.ExportJobs.Single();
        Assert.Equal("json", job.ExportFormat);
        Assert.NotEmpty(job.SourceRecordIds);
        Assert.NotEmpty(job.GeneratedFiles);
        Assert.False(string.IsNullOrWhiteSpace(job.Checksum));
        Assert.Equal($"topolactor://exports/{job.ExportJobId}/manifest.json", job.ManifestPath);
        Assert.Contains(repo.Events, e => e.Status == "success" && e.Code == "CLI_READER_EXPORT_JOB_CREATED");
    }


    [Fact]
    public async Task Create_export_job_rejects_missing_port_id()
    {
        var repo = new InMemoryCliReaderPortRepository(DefaultConfig());
        var runtime = new AuthorizedCliReaderPortRuntime(NullLogger<AuthorizedCliReaderPortRuntime>.Instance, repo);

        var response = await runtime.ExecuteAsync(Request("create_export_job", extra: new Dictionary<string, object?> { ["export_format"] = "json", ["port_id"] = null }), Guid.NewGuid());

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CLI_READER_EXPORT_PORT_ID_REQUIRED");
        Assert.Empty(repo.ExportJobs);
    }

    [Fact]
    public async Task Create_export_job_rejects_missing_source_record_ids()
    {
        var repo = new InMemoryCliReaderPortRepository(DefaultConfig()) { ReturnRowsWithoutSourceIds = true };
        var runtime = new AuthorizedCliReaderPortRuntime(NullLogger<AuthorizedCliReaderPortRuntime>.Instance, repo);

        var response = await runtime.ExecuteAsync(Request("create_export_job", columns: ["entity_jsonb"], extra: new Dictionary<string, object?> { ["export_format"] = "csv" }), Guid.NewGuid());

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CLI_READER_EXPORT_SOURCE_RECORD_IDS_REQUIRED");
        Assert.Empty(repo.ExportJobs);
    }

    [Fact]
    public async Task Rejects_non_dispatch_resolved_request()
    {
        var repo = new InMemoryCliReaderPortRepository(DefaultConfig());
        var runtime = new AuthorizedCliReaderPortRuntime(NullLogger<AuthorizedCliReaderPortRuntime>.Instance, repo);

        var response = await runtime.ExecuteAsync(Request("read"), manifestId: null);

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CLI_READER_DISPATCH_REQUIRED");
        Assert.Empty(repo.Queries);
    }

    [Fact]
    public async Task Rejects_direct_sql_db_core_api_and_plaintext_credential_bypass_fields_without_logging_secret()
    {
        var repo = new InMemoryCliReaderPortRepository(DefaultConfig());
        var runtime = new AuthorizedCliReaderPortRuntime(NullLogger<AuthorizedCliReaderPortRuntime>.Instance, repo);
        var request = Request("read", extra: new Dictionary<string, object?> { ["raw_sql"] = "select password from users", ["credential"] = "plain" });

        var response = await runtime.ExecuteAsync(request, Guid.NewGuid());

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CLI_READER_BYPASS_OR_SECRET_FIELD");
        Assert.Empty(repo.Queries);
        Assert.DoesNotContain(repo.Events, e => e.ScopeSummary.Contains("password", StringComparison.OrdinalIgnoreCase) || e.ScopeSummary.Contains("raw_sql", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public async Task Rejects_client_supplied_user_role_and_capability_authority_in_payload()
    {
        var repo = new InMemoryCliReaderPortRepository(DefaultConfig());
        var runtime = new AuthorizedCliReaderPortRuntime(NullLogger<AuthorizedCliReaderPortRuntime>.Instance, repo);
        var request = Request("read", extra: new Dictionary<string, object?> { ["user_id"] = "payload-user", ["roles"] = new[] { "admin" }, ["capabilities"] = new[] { "cli_reader_port.read" } });

        var response = await runtime.ExecuteAsync(request, Guid.NewGuid());

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "CLI_READER_BYPASS_OR_SECRET_FIELD");
        Assert.Empty(repo.Queries);
    }

    private static CliReaderPortConfig DefaultConfig() => new(
        "cli_reader_port.default",
        true,
        DateTimeOffset.UtcNow.AddHours(1),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "reader" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "reader-user" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "topology.entity" },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["topology.entity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "entity_id", "entity_jsonb", "state_id" }
        },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "state_id" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "today" },
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["reader-user"] = "state_id=active" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cli_reader_port.read" },
        true,
        60);

    private static EndpointRequestDto Request(string operation, string? userId = "reader-user", string[]? roles = null, string[]? capabilities = null, string table = "topology.entity", string[]? columns = null, Dictionary<string, string>? filters = null, string period = "today", Dictionary<string, object?>? extra = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["port_key"] = "cli_reader_port.default",
            ["table"] = table,
            ["columns"] = columns ?? ["entity_id", "state_id"],
            ["filters"] = filters ?? new Dictionary<string, string> { ["state_id"] = "active" },
            ["period"] = period,
            ["request_id"] = "req-1",
            ["idempotency_key"] = "idem-1",
            ["port_id"] = "11111111-1111-1111-1111-111111111111"
        };
        if (extra is not null)
            foreach (var (key, value) in extra) payload[key] = value;
        var context = new Dictionary<string, string>();
        if (userId is not null) context["authenticated_user_id"] = userId;
        if (roles is not null) context["authenticated_roles"] = string.Join(",", roles);
        if (capabilities is not null) context["resolved_capabilities"] = string.Join(",", capabilities);
        if (roles is null) context["authenticated_roles"] = "reader";
        if (capabilities is null) context["resolved_capabilities"] = "cli_reader_port.read";
        return new EndpointRequestDto(operation, "cli_reader_port", "reader", operation, null, JsonSerializer.SerializeToElement(payload), context, "client", roles is null ? "reader" : null);
    }

    private sealed class InMemoryCliReaderPortRepository(CliReaderPortConfig? config) : CliReaderPortRepository
    {
        public List<AuthorizedCliReaderQuery> Queries { get; } = [];
        public List<CliReaderPortRuntimeEvent> Events { get; } = [];
        public List<CliReaderExportJobResult> ExportJobs { get; } = [];
        public bool ReturnRowsWithoutSourceIds { get; init; }
        public Task<CliReaderPortConfig?> LoadPortAsync(string portKey, CancellationToken ct = default) => Task.FromResult(config);
        public Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(AuthorizedCliReaderQuery query, CancellationToken ct = default)
        {
            Queries.Add(query);
            IReadOnlyList<Dictionary<string, object?>> rows = query.Operation switch
            {
                "aggregate" => [new Dictionary<string, object?> { ["count"] = 1L }],
                "analyze" => [new Dictionary<string, object?> { ["row_count"] = 1L }],
                "validate" => [new Dictionary<string, object?> { ["valid"] = 1 }],
                _ => [ReturnRowsWithoutSourceIds ? new Dictionary<string, object?> { ["entity_jsonb"] = "{}" } : query.Columns.ToDictionary(c => c, c => (object?)(c.EndsWith("_id", StringComparison.OrdinalIgnoreCase) ? "entity-1" : $"value:{c}"))]
            };
            return Task.FromResult(rows);
        }
        public Task AppendRuntimeEventAsync(CliReaderPortRuntimeEvent runtimeEvent, CancellationToken ct = default)
        {
            Events.Add(runtimeEvent);
            return Task.CompletedTask;
        }

        public Task<CliReaderExportJobResult> CreateExportJobAsync(CreateCliReaderExportJobCommand command, CancellationToken ct = default)
        {
            var exportJobId = Guid.NewGuid();
            var generatedFiles = new[] { new CliReaderGeneratedFile($"export.{command.ExportFormat}", command.ExportFormat, 2, "checksum-file", $"cli-reader-export-job://{exportJobId}/export.{command.ExportFormat}") };
            var manifest = JsonSerializer.SerializeToElement(new
            {
                manifest_version = "1.0",
                export_job_id = exportJobId,
                generated_by = command.Query.UserId,
                source_tables = new[] { command.Query.Table },
                source_record_ids = command.SourceRecordIds,
                files = generatedFiles,
                checksum = "checksum-manifest"
            });
            Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), command.PortId);
            var result = new CliReaderExportJobResult(exportJobId, "completed", command.ExportFormat, command.SourceRecordIds, generatedFiles, "checksum-manifest", $"topolactor://exports/{exportJobId}/manifest.json", manifest);
            ExportJobs.Add(result);
            return Task.FromResult(result);
        }
    }
}
