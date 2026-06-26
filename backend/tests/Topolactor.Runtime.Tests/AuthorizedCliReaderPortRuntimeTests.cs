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
        Assert.Equal("tenant:default", repo.Queries.Single().RowScope);
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

    private static CliReaderPortConfig DefaultConfig() => new(
        "cli_reader_port.default",
        true,
        DateTimeOffset.UtcNow.AddHours(1),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "reader" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "reader-user" },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["topology.entity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "entity_id", "entity_jsonb", "state_id" }
        },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "state_id" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "today" },
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["reader-user"] = "tenant:default" },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cli_reader_port.read" },
        true,
        60);

    private static EndpointRequestDto Request(string operation, string? userId = "reader-user", string[]? roles = null, string[]? capabilities = null, string table = "topology.entity", string[]? columns = null, Dictionary<string, string>? filters = null, string period = "today", Dictionary<string, object?>? extra = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["port_key"] = "cli_reader_port.default",
            ["user_id"] = userId,
            ["roles"] = roles ?? ["reader"],
            ["capabilities"] = capabilities ?? ["cli_reader_port.read"],
            ["table"] = table,
            ["columns"] = columns ?? ["entity_id", "state_id"],
            ["filters"] = filters ?? new Dictionary<string, string> { ["state_id"] = "active" },
            ["period"] = period,
            ["request_id"] = "req-1",
            ["idempotency_key"] = "idem-1"
        };
        if (extra is not null)
            foreach (var (key, value) in extra) payload[key] = value;
        return new EndpointRequestDto(operation, "cli_reader_port", "reader", operation, null, JsonSerializer.SerializeToElement(payload), null, "client", "reader");
    }

    private sealed class InMemoryCliReaderPortRepository(CliReaderPortConfig? config) : CliReaderPortRepository
    {
        public List<AuthorizedCliReaderQuery> Queries { get; } = [];
        public List<CliReaderPortRuntimeEvent> Events { get; } = [];
        public Task<CliReaderPortConfig?> LoadPortAsync(string portKey, CancellationToken ct = default) => Task.FromResult(config);
        public Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(AuthorizedCliReaderQuery query, CancellationToken ct = default)
        {
            Queries.Add(query);
            IReadOnlyList<Dictionary<string, object?>> rows = [query.Columns.ToDictionary(c => c, c => (object?)$"value:{c}")];
            return Task.FromResult(rows);
        }
        public Task AppendRuntimeEventAsync(CliReaderPortRuntimeEvent runtimeEvent, CancellationToken ct = default)
        {
            Events.Add(runtimeEvent);
            return Task.CompletedTask;
        }
    }
}
