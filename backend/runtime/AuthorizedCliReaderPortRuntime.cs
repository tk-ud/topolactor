using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Authorized CLI/MCP read-side runtime. This handler is intentionally usable only
/// after ManifestDispatcher resolves a manifest/runtime mapping (manifestId required).
/// It resolves cli_reader_port scope and executes only read/search/aggregate/analyze/validate
/// inside the repository-authorized read model. It never accepts direct SQL, direct DB
/// connection strings, Core API URLs, plaintext credentials, export jobs, file streams,
/// imports, commit candidates, or mutation operations.
/// </summary>
public sealed class AuthorizedCliReaderPortRuntime : IDispatchableRuntime
{
    private static readonly HashSet<string> AllowedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "search", "aggregate", "analyze", "validate"
    };

    private static readonly string[] SecretFieldNames =
    [
        "credential", "credentials", "secret", "password", "token", "api_key", "connection_string", "sql", "raw_sql", "core_api_url"
    ];

    private readonly ILogger<AuthorizedCliReaderPortRuntime> _logger;
    private readonly CliReaderPortRepository _repository;
    private readonly TimeProvider _timeProvider;

    public AuthorizedCliReaderPortRuntime(
        ILogger<AuthorizedCliReaderPortRuntime> logger,
        CliReaderPortRepository repository,
        TimeProvider? timeProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<EndpointResponseDto> ExecuteAsync(EndpointRequestDto request, Guid? manifestId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parsed = ParseRequest(request);
        if (parsed.Error is not null)
            return await FailAsync(parsed.EventSeed, parsed.Error.Code, parsed.Error.Message, ct);

        var seed = parsed.EventSeed;
        if (manifestId is null)
            return await FailAsync(seed, "CLI_READER_DISPATCH_REQUIRED", "CLI/MCP Data Reader accepts only ManifestDispatcher/runtime dispatch resolved requests.", ct);

        if (!string.Equals(request.Target, "cli_reader_port", StringComparison.OrdinalIgnoreCase))
            return await FailAsync(seed, "CLI_READER_TARGET_INVALID", "CLI/MCP Data Reader target must be cli_reader_port.", ct);

        if (!AllowedOperations.Contains(parsed.Operation!))
            return await FailAsync(seed, "CLI_READER_OPERATION_UNSUPPORTED", "CLI/MCP Data Reader operation must be read/search/aggregate/analyze/validate.", ct);

        if (request.Payload.HasValue && ContainsForbiddenBypassOrSecretField(request.Payload.Value))
            return await FailAsync(seed, "CLI_READER_BYPASS_OR_SECRET_FIELD", "Direct SQL/DB/Core API bypass and plaintext credential fields are forbidden.", ct);

        if (string.IsNullOrWhiteSpace(parsed.UserId))
            return await FailAsync(seed, "CLI_READER_AUTH_REQUIRED", "Authenticated user id is required.", ct);

        if (parsed.Roles.Count == 0)
            return await FailAsync(seed, "CLI_READER_ROLE_REQUIRED", "At least one authenticated role is required.", ct);

        var config = await _repository.LoadPortAsync(parsed.PortKey!, ct);
        if (config is null)
            return await FailAsync(seed, "CLI_READER_PORT_UNRESOLVED", "cli_reader_port config could not be resolved.", ct);

        if (!config.Enabled)
            return await FailAsync(seed, "CLI_READER_PORT_DISABLED", "cli_reader_port is disabled.", ct);

        if (config.ExpiresAt is not null && config.ExpiresAt <= _timeProvider.GetUtcNow())
            return await FailAsync(seed, "CLI_READER_PORT_EXPIRED", "cli_reader_port is expired.", ct);

        if (!parsed.Roles.Any(role => config.AllowedRoles.Contains(role)))
            return await FailAsync(seed, "CLI_READER_ROLE_DENIED", "Authenticated role is not allowed for this cli_reader_port.", ct);

        if (config.AllowedUsers.Count > 0 && !config.AllowedUsers.Contains(parsed.UserId!))
            return await FailAsync(seed, "CLI_READER_USER_DENIED", "Authenticated user is not allowed for this cli_reader_port.", ct);

        if (config.RequiredCapabilities.Count > 0)
        {
            if (parsed.Capabilities.Count == 0 || !config.RequiredCapabilities.All(required => parsed.Capabilities.Contains(required)))
                return await FailAsync(seed, "CLI_READER_CAPABILITY_UNRESOLVED", "Required credential/capability reference could not be resolved.", ct);
        }

        if (!config.AllowedColumnsByTable.TryGetValue(parsed.Table!, out var allowedColumns))
            return await FailAsync(seed, "CLI_READER_TABLE_DENIED", "Requested table is not allowed for this cli_reader_port.", ct);

        if (parsed.Columns.Count == 0 || parsed.Columns.Any(column => !allowedColumns.Contains(column)))
            return await FailAsync(seed, "CLI_READER_COLUMN_DENIED", "Requested columns exceed cli_reader_port scope.", ct);

        if (parsed.Filters.Keys.Any(filter => !config.AllowedFilters.Contains(filter)))
            return await FailAsync(seed, "CLI_READER_FILTER_DENIED", "Requested filters exceed cli_reader_port scope.", ct);

        if (!string.IsNullOrWhiteSpace(parsed.Period) && !config.AllowedPeriods.Contains(parsed.Period!))
            return await FailAsync(seed, "CLI_READER_PERIOD_DENIED", "Requested period exceeds cli_reader_port scope.", ct);

        if (!config.RowScopeByUser.TryGetValue(parsed.UserId!, out var rowScope) || string.IsNullOrWhiteSpace(rowScope))
            return await FailAsync(seed, "CLI_READER_ROW_SCOPE_UNRESOLVED", "Row scope must resolve explicitly for the authenticated user.", ct);

        var query = new AuthorizedCliReaderQuery(parsed.PortKey!, parsed.Operation!, parsed.UserId!, parsed.Roles, parsed.Table!, parsed.Columns, parsed.Filters, parsed.Period, rowScope, parsed.RequestId, parsed.IdempotencyKey);
        var rows = await _repository.ReadRowsAsync(query, ct);
        var shaped = ShapeResult(parsed.Operation!, rows, parsed.Columns);

        await AppendAsync(seed with { Status = "success", Code = "CLI_READER_OK", ScopeSummary = BuildScopeSummary(query) }, ct);
        return new EndpointResponseDto(true, new Emission(null, null, null, [], JsonSerializer.SerializeToElement(shaped), []), []);
    }

    private static object ShapeResult(string operation, IReadOnlyList<Dictionary<string, object?>> rows, IReadOnlyList<string> columns) =>
        operation.ToLowerInvariant() switch
        {
            "aggregate" => new { operation, count = rows.Count, columns },
            "analyze" => new { operation, rowCount = rows.Count, columnCount = columns.Count },
            "validate" => new { operation, valid = true, rowCount = rows.Count },
            _ => new { operation, rows }
        };

    private async Task<EndpointResponseDto> FailAsync(CliReaderPortRuntimeEvent seed, string code, string message, CancellationToken ct)
    {
        _logger.LogWarning("CLI/MCP reader rejected request with {Code}", code);
        await AppendAsync(seed with { Status = "fail_close", Code = code, ScopeSummary = Sanitize(seed.ScopeSummary) }, ct);
        return new EndpointResponseDto(false, null, [new ValidationError(code, message)]);
    }

    private Task AppendAsync(CliReaderPortRuntimeEvent runtimeEvent, CancellationToken ct) =>
        _repository.AppendRuntimeEventAsync(runtimeEvent with { ObservedAt = _timeProvider.GetUtcNow() }, ct);

    private static string BuildScopeSummary(AuthorizedCliReaderQuery query) =>
        $"table={query.Table};columns={string.Join(',', query.Columns)};filters={string.Join(',', query.Filters.Keys)};period={query.Period ?? "none"};row_scope=resolved";

    private static string Sanitize(string value)
    {
        var sanitized = value;
        foreach (var field in SecretFieldNames)
            sanitized = sanitized.Replace(field, "[redacted-field]", StringComparison.OrdinalIgnoreCase);
        return sanitized;
    }

    private static bool ContainsForbiddenBypassOrSecretField(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (SecretFieldNames.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
                    return true;
                if (ContainsForbiddenBypassOrSecretField(property.Value))
                    return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (ContainsForbiddenBypassOrSecretField(item)) return true;
        }

        return false;
    }

    private static ParsedCliReaderRequest ParseRequest(EndpointRequestDto request)
    {
        var eventSeed = new CliReaderPortRuntimeEvent("unknown", request.Action ?? request.OperationType ?? "unknown", null, [], "fail_close", "CLI_READER_PARSE_PENDING", null, null, "parse", DateTimeOffset.UnixEpoch);
        if (!request.Payload.HasValue || request.Payload.Value.ValueKind != JsonValueKind.Object)
            return new(eventSeed, null, null, null, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null, [], new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), null, [], null, null, new ValidationError("CLI_READER_PAYLOAD_REQUIRED", "CLI/MCP reader payload object is required."));

        var payload = request.Payload.Value;
        var portKey = ReadString(payload, "port_key") ?? ReadString(payload, "portKey");
        var operation = request.Action ?? request.OperationType ?? ReadString(payload, "operation");
        var userId = ReadString(payload, "user_id") ?? ReadString(payload, "userId");
        var roles = ReadStringArray(payload, "roles").DefaultIfEmpty(request.Role).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var table = ReadString(payload, "table");
        var columns = ReadStringArray(payload, "columns");
        var filters = ReadStringMap(payload, "filters");
        var period = ReadString(payload, "period");
        var capabilities = ReadStringArray(payload, "capabilities").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestId = ReadString(payload, "request_id") ?? ReadString(payload, "requestId");
        var idempotencyKey = ReadString(payload, "idempotency_key") ?? ReadString(payload, "idempotencyKey");

        eventSeed = eventSeed with { PortKey = portKey ?? "unknown", Operation = operation ?? "unknown", UserId = userId, Roles = roles.ToArray(), RequestId = requestId, IdempotencyKey = idempotencyKey, ScopeSummary = $"table={table ?? "unknown"}" };
        if (string.IsNullOrWhiteSpace(portKey)) return new(eventSeed, null, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, new ValidationError("CLI_READER_PORT_REQUIRED", "port_key is required."));
        if (string.IsNullOrWhiteSpace(table)) return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, new ValidationError("CLI_READER_TABLE_REQUIRED", "table is required."));
        return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, null);
    }

    private static string? ReadString(JsonElement obj, string name) => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static IReadOnlyList<string> ReadStringArray(JsonElement obj, string name) => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() : [];
    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement obj, string name) => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.String).ToDictionary(x => x.Name, x => x.Value.GetString()!, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private sealed record ParsedCliReaderRequest(CliReaderPortRuntimeEvent EventSeed, string? PortKey, string? Operation, string? UserId, IReadOnlySet<string> Roles, string? Table, IReadOnlyList<string> Columns, IReadOnlySet<string> Capabilities, IReadOnlyDictionary<string, string> Filters, string? Period, IReadOnlyList<string> RowScope, string? RequestId, string? IdempotencyKey, ValidationError? Error);
}
