using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Authorized CLI/MCP read-side runtime. This handler is intentionally usable only
/// after ManifestDispatcher resolves a manifest/runtime mapping (manifestId required).
/// It resolves authenticated user/role/capability from server-side dispatch context,
/// resolves cli_reader_port scope, and executes only read/search/aggregate/analyze/validate/create_export_job/download_export_file
/// inside the repository-authorized read model. File streaming is limited to already-authorized
/// export_job ledger artifacts. It never accepts direct SQL, direct DB connection strings,
/// Core API URLs, plaintext credentials, direct file streams outside export jobs, imports,
/// commit candidates, or mutation operations.
/// </summary>
public sealed class AuthorizedCliReaderPortRuntime : IDispatchableRuntime
{
    private static readonly HashSet<string> AllowedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "search", "aggregate", "analyze", "validate", "create_export_job", "download_export_file"
    };

    private static readonly string[] SecretFieldNames =
    [
        "credential", "credentials", "secret", "password", "token", "api_key", "connection_string", "sql", "raw_sql", "core_api_url", "user_id", "userId", "roles", "capabilities"
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
            return await FailAsync(seed, "CLI_READER_OPERATION_UNSUPPORTED", "CLI/MCP Data Reader operation must be read/search/aggregate/analyze/validate/create_export_job/download_export_file.", ct);

        if (request.Payload.HasValue && ContainsForbiddenBypassOrSecretField(request.Payload.Value))
            return await FailAsync(seed, "CLI_READER_BYPASS_OR_SECRET_FIELD", "Direct SQL/DB/Core API bypass, client-supplied auth authority, and plaintext credential fields are forbidden.", ct);

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

        // File stream port: keyed by an existing authorized export_job_id, not by a fresh
        // table/column/period read. It never re-runs create_export_job or re-reads rows; the
        // canonical export_jobs / export_manifests ledger is the only source of truth.
        if (string.Equals(parsed.Operation, "download_export_file", StringComparison.OrdinalIgnoreCase))
            return await DownloadExportFileAsync(parsed, config, seed, ct);

        if (!config.AllowedTables.Contains(parsed.Table!))
            return await FailAsync(seed, "CLI_READER_TABLE_DENIED", "Requested table is not allowed for this cli_reader_port.", ct);

        if (!config.AllowedColumnsByTable.TryGetValue(parsed.Table!, out var allowedColumns))
            return await FailAsync(seed, "CLI_READER_TABLE_SCOPE_UNRESOLVED", "Requested table column scope is unresolved for this cli_reader_port.", ct);

        if (parsed.Columns.Count == 0 || parsed.Columns.Any(column => !allowedColumns.Contains(column)))
            return await FailAsync(seed, "CLI_READER_COLUMN_DENIED", "Requested columns exceed cli_reader_port scope.", ct);

        if (parsed.Filters.Keys.Any(filter => !config.AllowedFilters.Contains(filter)))
            return await FailAsync(seed, "CLI_READER_FILTER_DENIED", "Requested filters exceed cli_reader_port scope.", ct);

        if (!string.IsNullOrWhiteSpace(parsed.Period) && !config.AllowedPeriods.Contains(parsed.Period!))
            return await FailAsync(seed, "CLI_READER_PERIOD_DENIED", "Requested period exceeds cli_reader_port scope.", ct);

        if (!config.RowScopeByUser.TryGetValue(parsed.UserId!, out var rowScope) || string.IsNullOrWhiteSpace(rowScope))
            return await FailAsync(seed, "CLI_READER_ROW_SCOPE_UNRESOLVED", "Row scope must resolve explicitly for the authenticated user.", ct);

        var readOperation = string.Equals(parsed.Operation, "create_export_job", StringComparison.OrdinalIgnoreCase) ? "read" : parsed.Operation!;
        var query = new AuthorizedCliReaderQuery(parsed.PortKey!, readOperation, parsed.UserId!, parsed.Roles, parsed.Table!, parsed.Columns, parsed.Filters, parsed.Period, rowScope, parsed.RequestId, parsed.IdempotencyKey);
        IReadOnlyList<Dictionary<string, object?>> rows;
        try
        {
            rows = await _repository.ReadRowsAsync(query, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("CLI_READER_", StringComparison.Ordinal))
        {
            return await FailAsync(seed, ex.Message, "Authorized read query validation failed.", ct);
        }
        if (string.Equals(parsed.Operation, "create_export_job", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var exportResult = await CreateExportJobAsync(parsed, config, query, rows, ct);
                await AppendAsync(seed with { Status = "success", Code = "CLI_READER_EXPORT_JOB_CREATED", ScopeSummary = BuildScopeSummary(query) }, ct);
                return new EndpointResponseDto(true, new Emission(null, null, null, [], JsonSerializer.SerializeToElement(exportResult), []), []);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("CLI_READER_", StringComparison.Ordinal))
            {
                return await FailAsync(seed, ex.Message, "Export job creation validation failed.", ct);
            }
        }

        var shaped = ShapeResult(parsed.Operation!, rows, parsed.Columns);

        await AppendAsync(seed with { Status = "success", Code = "CLI_READER_OK", ScopeSummary = BuildScopeSummary(query) }, ct);
        return new EndpointResponseDto(true, new Emission(null, null, null, [], JsonSerializer.SerializeToElement(shaped), []), []);
    }


    private async Task<object> CreateExportJobAsync(ParsedCliReaderRequest parsed, CliReaderPortConfig config, AuthorizedCliReaderQuery query, IReadOnlyList<Dictionary<string, object?>> rows, CancellationToken ct)
    {
        var exportFormat = parsed.ExportFormat!;
        var idempotencyKey = parsed.IdempotencyKey!;
        var sourceRecordIds = ExtractSourceRecordIds(rows);
        if (sourceRecordIds.Count == 0)
            throw new InvalidOperationException("CLI_READER_EXPORT_SOURCE_RECORD_IDS_REQUIRED");

        var result = await _repository.CreateExportJobAsync(
            new CreateCliReaderExportJobCommand(query, config.PortId, exportFormat, rows, sourceRecordIds, idempotencyKey, _timeProvider.GetUtcNow()), ct);

        return new
        {
            operation = "create_export_job",
            exportJobId = result.ExportJobId,
            status = result.Status,
            exportFormat = result.ExportFormat,
            sourceRecordIds = result.SourceRecordIds,
            generatedFiles = result.GeneratedFiles,
            checksum = result.Checksum,
            manifestPath = result.ManifestPath,
            manifest = result.Manifest
        };
    }

    private async Task<EndpointResponseDto> DownloadExportFileAsync(ParsedCliReaderRequest parsed, CliReaderPortConfig config, CliReaderPortRuntimeEvent seed, CancellationToken ct)
    {
        // file_stream_enabled is a port-level scope control field. A port without file
        // stream permission must fail-close even when every other read scope check passes.
        if (!config.FileStreamEnabled)
            return await FailAsync(seed, "CLI_READER_FILE_STREAM_DISABLED", "cli_reader_port does not permit file streaming.", ct);

        if (parsed.ExportJobId is not Guid exportJobId)
            return await FailAsync(seed, "CLI_READER_FILE_EXPORT_JOB_ID_REQUIRED", "download_export_file requires an export_job_id.", ct);

        var fileSeed = seed with { ScopeSummary = $"export_job_id={exportJobId};operation=download_export_file" };

        var file = await _repository.LoadAuthorizedExportFileAsync(new LoadAuthorizedExportFileQuery(config.PortId, parsed.UserId!, exportJobId), ct);
        if (file is null)
            return await FailAsync(fileSeed, "CLI_READER_FILE_JOB_UNAUTHORIZED", "Export job is not authorized for this port and user, or does not exist.", ct);

        if (!string.Equals(file.Status, "completed", StringComparison.OrdinalIgnoreCase))
            return await FailAsync(fileSeed, "CLI_READER_FILE_JOB_NOT_READY", "Export job is not in a completed state.", ct);

        // approval_required / approval_status are read-only here. CLI/MCP never executes
        // approval; it only refuses to stream an unapproved approval-gated export job.
        if (file.ApprovalRequired && !string.Equals(file.ApprovalStatus, "approved", StringComparison.OrdinalIgnoreCase))
            return await FailAsync(fileSeed, "CLI_READER_FILE_APPROVAL_REQUIRED", "Export job requires UI/Human approval before file streaming.", ct);

        var checksumError = VerifyExportChecksum(file);
        if (checksumError is not null)
        {
            await _repository.RecordExportDownloadFailureEvidenceAsync(exportJobId, checksumError, _timeProvider.GetUtcNow(), ct);
            await AppendAsync(fileSeed with { Status = "fail_close", Code = checksumError, ScopeSummary = $"export_job_id={exportJobId};checksum_verified=false" }, ct);
            return new EndpointResponseDto(false, null, [new ValidationError(checksumError, "Export file checksum/source/manifest/artifact verification failed; file stream refused.")]);
        }

        await _repository.RecordExportDownloadEvidenceAsync(exportJobId, checksumVerified: true, _timeProvider.GetUtcNow(), ct);
        await AppendAsync(fileSeed with { Status = "success", Code = "CLI_READER_FILE_CHECKSUM_VERIFIED", ScopeSummary = $"export_job_id={exportJobId};checksum_verified=true" }, ct);
        await AppendAsync(fileSeed with { Status = "success", Code = "CLI_READER_FILE_DOWNLOAD_COMPLETED", ScopeSummary = $"export_job_id={exportJobId};checksum_verified=true" }, ct);

        var response = BuildExportResourceResponse(file);
        return new EndpointResponseDto(true, new Emission(null, null, null, [], JsonSerializer.SerializeToElement(response), []), []);
    }

    // Cross-checks the canonical export_jobs ledger against the export_manifests record:
    // job checksum vs manifest checksum, every generated file checksum, and the
    // source_record_ids set captured at export time. Any mismatch fails closed.
    private static string? VerifyExportChecksum(AuthorizedExportFile file)
    {
        if (!file.ManifestPresent)
            return "CLI_READER_FILE_MANIFEST_MISSING";
        if (string.IsNullOrWhiteSpace(file.JobChecksum))
            return "CLI_READER_FILE_CHECKSUM_MISSING";
        if (string.IsNullOrWhiteSpace(file.ManifestChecksum) || !string.Equals(file.JobChecksum, file.ManifestChecksum, StringComparison.OrdinalIgnoreCase))
            return "CLI_READER_FILE_CHECKSUM_MISMATCH";
        if (file.GeneratedFiles.Count == 0)
            return "CLI_READER_FILE_ARTIFACT_MISSING";
        if (file.GeneratedFiles.Any(generated => !string.Equals(generated.Checksum, file.JobChecksum, StringComparison.OrdinalIgnoreCase)))
            return "CLI_READER_FILE_CHECKSUM_MISMATCH";

        var jobIds = new HashSet<string>(file.SourceRecordIds, StringComparer.OrdinalIgnoreCase);
        var manifestIds = new HashSet<string>(file.ManifestSourceRecordIds, StringComparer.OrdinalIgnoreCase);
        if (jobIds.Count == 0 || !jobIds.SetEquals(manifestIds))
            return "CLI_READER_FILE_SOURCE_IDS_MISMATCH";

        return null;
    }

    // MCP resource/tool response boundary. Projects only ledger-derived safe metadata.
    // No credential, bucket, endpoint, plaintext storage URL, or actual signed URL.
    private static object BuildExportResourceResponse(AuthorizedExportFile file) => new
    {
        operation = "download_export_file",
        resourceUri = $"topolactor://exports/{file.ExportJobId}/file",
        manifestResourceUri = $"topolactor://exports/{file.ExportJobId}/manifest.json",
        exportJobId = file.ExportJobId,
        status = file.Status,
        exportFormat = file.ExportFormat,
        period = file.Period,
        generatedBy = file.GeneratedBy,
        generatedAt = file.GeneratedAt,
        sourceRecordIds = file.SourceRecordIds,
        generatedFiles = file.GeneratedFiles,
        checksum = file.JobChecksum,
        checksumVerified = true,
        manifestVersion = file.ManifestVersion,
        manifest = file.ManifestJsonb
    };

    private static IReadOnlyList<string> ExtractSourceRecordIds(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var ids = new List<string>();
        foreach (var row in rows)
        {
            var match = row.FirstOrDefault(kvp => string.Equals(kvp.Key, "id", StringComparison.OrdinalIgnoreCase) || string.Equals(kvp.Key, "hub_id", StringComparison.OrdinalIgnoreCase) || kvp.Key.EndsWith("_id", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Key) && match.Value is not null) ids.Add(Convert.ToString(match.Value, System.Globalization.CultureInfo.InvariantCulture)!);
        }
        return ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static object ShapeResult(string operation, IReadOnlyList<Dictionary<string, object?>> rows, IReadOnlyList<string> columns) =>
        operation.ToLowerInvariant() switch
        {
            "aggregate" => new { operation, count = ReadLong(rows, "count"), columns },
            "analyze" => new { operation, rowCount = ReadLong(rows, "row_count"), columnCount = columns.Count },
            "validate" => new { operation, valid = rows.Count > 0, rowCount = rows.Count },
            _ => new { operation, rows }
        };

    private static long ReadLong(IReadOnlyList<Dictionary<string, object?>> rows, string key)
    {
        if (rows.Count == 0 || !rows[0].TryGetValue(key, out var value) || value is null) return 0;
        return Convert.ToInt64(value);
    }

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
            return new(eventSeed, null, null, null, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null, [], new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), null, [], null, null, null, null, new ValidationError("CLI_READER_PAYLOAD_REQUIRED", "CLI/MCP reader payload object is required."));

        var payload = request.Payload.Value;
        var portKey = ReadString(payload, "port_key") ?? ReadString(payload, "portKey");
        var operation = request.Action ?? request.OperationType ?? ReadString(payload, "operation");
        var userId = ReadContextValue(request.Context, "authenticated_user_id", "user_id");
        var roles = ReadContextSet(request.Context, "authenticated_roles", "roles");
        if (!string.IsNullOrWhiteSpace(request.Role)) roles.Add(request.Role!);
        var table = ReadString(payload, "table");
        var columns = ReadStringArray(payload, "columns");
        var filters = ReadStringMap(payload, "filters");
        var period = ReadString(payload, "period");
        var capabilities = ReadContextSet(request.Context, "resolved_capabilities", "capabilities");
        var requestId = ReadString(payload, "request_id") ?? ReadString(payload, "requestId");
        var idempotencyKey = ReadString(payload, "idempotency_key") ?? ReadString(payload, "idempotencyKey");
        var exportFormat = ReadString(payload, "export_format") ?? ReadString(payload, "exportFormat");
        var exportJobIdRaw = ReadString(payload, "export_job_id") ?? ReadString(payload, "exportJobId");
        Guid? exportJobId = Guid.TryParse(exportJobIdRaw, out var parsedExportJobId) ? parsedExportJobId : null;
        eventSeed = eventSeed with { PortKey = portKey ?? "unknown", Operation = operation ?? "unknown", UserId = userId, Roles = roles.ToArray(), RequestId = requestId, IdempotencyKey = idempotencyKey, ScopeSummary = $"table={table ?? "unknown"}" };
        if (string.IsNullOrWhiteSpace(portKey)) return new(eventSeed, null, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, null, exportJobId, new ValidationError("CLI_READER_PORT_REQUIRED", "port_key is required."));
        // File stream port is keyed by an existing export_job_id, not by a table/column read.
        if (string.Equals(operation, "download_export_file", StringComparison.OrdinalIgnoreCase))
        {
            if (exportJobId is null) return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, null, new ValidationError("CLI_READER_FILE_EXPORT_JOB_ID_REQUIRED", "download_export_file requires a valid export_job_id."));
            return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, null);
        }
        if (string.IsNullOrWhiteSpace(table)) return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, new ValidationError("CLI_READER_TABLE_REQUIRED", "table is required."));
        if (string.Equals(operation, "create_export_job", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey)) return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, new ValidationError("CLI_READER_EXPORT_IDEMPOTENCY_KEY_REQUIRED", "create_export_job requires idempotency_key."));
            if (string.IsNullOrWhiteSpace(exportFormat) || !new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "csv", "json", "pdf", "zip" }.Contains(exportFormat)) return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, new ValidationError("CLI_READER_EXPORT_FORMAT_DENIED", "export_format must be csv/json/pdf/zip."));
        }
        return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, null);
    }

    private static string? ReadContextValue(Dictionary<string, string>? context, params string[] names)
    {
        if (context is null) return null;
        foreach (var name in names)
            if (context.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        return null;
    }

    private static HashSet<string> ReadContextSet(Dictionary<string, string>? context, params string[] names)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var raw = ReadContextValue(context, name);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            foreach (var value in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                values.Add(value);
        }
        return values;
    }

    private static string? ReadString(JsonElement obj, string name) => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static IReadOnlyList<string> ReadStringArray(JsonElement obj, string name) => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() : [];
    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement obj, string name) => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.String).ToDictionary(x => x.Name, x => x.Value.GetString()!, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private sealed record ParsedCliReaderRequest(CliReaderPortRuntimeEvent EventSeed, string? PortKey, string? Operation, string? UserId, IReadOnlySet<string> Roles, string? Table, IReadOnlyList<string> Columns, IReadOnlySet<string> Capabilities, IReadOnlyDictionary<string, string> Filters, string? Period, IReadOnlyList<string> RowScope, string? RequestId, string? IdempotencyKey, string? ExportFormat, Guid? ExportJobId, ValidationError? Error);
}
