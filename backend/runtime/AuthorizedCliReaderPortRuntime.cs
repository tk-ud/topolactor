using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Authorized CLI/MCP read-side runtime. This handler is intentionally usable only
/// after ManifestDispatcher resolves a manifest/runtime mapping (manifestId required).
/// It resolves authenticated user/role/capability from server-side dispatch context,
/// resolves cli_reader_port scope, and executes only read/search/aggregate/analyze/validate/create_export_job/download_export_file/import-candidate
/// inside the repository-authorized read model. File streaming is limited to already-authorized
/// export_job ledger artifacts. It never accepts direct SQL, direct DB connection strings,
/// Core API URLs, plaintext credentials, direct file streams outside export jobs, imports,
/// commit candidates, or mutation operations.
/// </summary>
public sealed class AuthorizedCliReaderPortRuntime : IDispatchableRuntime
{
    private static readonly HashSet<string> AllowedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "search", "aggregate", "analyze", "validate", "create_export_job", "download_export_file",
        "import_structured_output", "assign_business_object_candidate", "create_draft_operation", "create_commit_candidate", "get_preview_diff"
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
            return await FailAsync(seed, "CLI_READER_OPERATION_UNSUPPORTED", "CLI/MCP Data Reader operation must be read/search/aggregate/analyze/validate/create_export_job/download_export_file/import-candidate.", ct);

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

        if (IsImportCandidateOperation(parsed.Operation!))
            return await ExecuteImportCandidateAsync(parsed, config, seed, ct);

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



    private static bool IsImportCandidateOperation(string operation) => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "import_structured_output", "assign_business_object_candidate", "create_draft_operation", "create_commit_candidate", "get_preview_diff"
    }.Contains(operation);


    private async Task<EndpointResponseDto?> ValidateImportCandidateRequiredFieldsAsync(ParsedCliReaderRequest parsed, CliReaderPortRuntimeEvent seed, CancellationToken ct)
    {
        if (string.Equals(parsed.Operation, "get_preview_diff", StringComparison.OrdinalIgnoreCase)) return null;
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(parsed.SourceTranscriptRef)) missing.Add("source_transcript_ref");
        if (string.IsNullOrWhiteSpace(parsed.RootUtterance)) missing.Add("root_utterance");
        if (parsed.StructuredOutputPayload is null) missing.Add("structured_output_payload");
        if (parsed.Confidence is null) missing.Add("confidence");
        if (parsed.UnresolvedFields is null) missing.Add("unresolved_fields");
        if (parsed.AssignedBusinessObjectCandidate is null) missing.Add("assigned_business_object_candidate");
        if (parsed.PreviewDiff is null) missing.Add("preview_diff");

        if (string.Equals(parsed.Operation, "assign_business_object_candidate", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parsed.Operation, "create_draft_operation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parsed.Operation, "create_commit_candidate", StringComparison.OrdinalIgnoreCase))
        {
            if (parsed.DraftOperationId is null) missing.Add("draft_operation_id");
            if (parsed.AssignedBusinessObject is null) missing.Add("assigned_business_object");
            if (parsed.AssignmentTargetScope is null) missing.Add("assignment_target_scope");
            if (parsed.EvidenceRefs is null) missing.Add("evidence_refs");
        }

        if (missing.Count > 0)
            return await FailAsync(seed, "CLI_READER_IMPORT_REQUIRED_FIELD_MISSING", $"Import candidate required fields are missing: {string.Join(',', missing)}.", ct);
        return null;
    }

    private async Task<EndpointResponseDto> ExecuteImportCandidateAsync(ParsedCliReaderRequest parsed, CliReaderPortConfig config, CliReaderPortRuntimeEvent seed, CancellationToken ct)
    {
        if (!config.AuditRequired)
            return await FailAsync(seed, "CLI_READER_IMPORT_AUDIT_REQUIRED", "Import candidate port requires audit-enabled cli_reader_port config.", ct);

        var requiredFieldError = await ValidateImportCandidateRequiredFieldsAsync(parsed, seed, ct);
        if (requiredFieldError is not null) return requiredFieldError;

        if (string.Equals(parsed.Operation, "get_preview_diff", StringComparison.OrdinalIgnoreCase))
        {
            if (parsed.CandidateId is not Guid candidateId)
                return await FailAsync(seed, "CLI_READER_IMPORT_CANDIDATE_ID_REQUIRED", "get_preview_diff requires candidate_id.", ct);
            var existing = await _repository.LoadImportCandidateAsync(new LoadCliReaderImportCandidateQuery(config.PortId, parsed.UserId!, candidateId), ct);
            if (existing is null)
                return await FailAsync(seed, "CLI_READER_IMPORT_CANDIDATE_UNAUTHORIZED", "Candidate evidence is not authorized for this port and user, or does not exist.", ct);
            await _repository.RecordImportCandidateEvidenceAsync(candidateId, "cli_mcp_import_candidate_preview_returned", _timeProvider.GetUtcNow(), ct);
            await AppendAsync(seed with { Status = "success", Code = "CLI_READER_IMPORT_PREVIEW_DIFF_RETURNED", ScopeSummary = $"candidate_id={candidateId};operation=get_preview_diff;commit=false;approval=false" }, ct);
            var preview = new { operation = "get_preview_diff", existing.CandidateId, existing.Status, existing.PreviewDiff, existing.UnresolvedFields, existing.ApprovalStatus, applied = false, approvalUpdated = false };
            return new EndpointResponseDto(true, new Emission(null, null, null, [], JsonSerializer.SerializeToElement(preview), []), []);
        }

        if (string.IsNullOrWhiteSpace(parsed.IdempotencyKey))
            return await FailAsync(seed, "CLI_READER_IMPORT_IDEMPOTENCY_KEY_REQUIRED", "Import candidate creation requires idempotency_key.", ct);

        var candidateKind = parsed.Operation!.ToLowerInvariant() switch
        {
            "assign_business_object_candidate" => "business_object_assignment_candidate",
            "create_draft_operation" => "draft_operation",
            "create_commit_candidate" => "commit_candidate",
            _ => "structured_output"
        };
        var previewDiff = parsed.PreviewDiff ?? BuildPreviewDiff(parsed.Operation!, parsed.StructuredOutputPayload, parsed.UnresolvedFields);
        var result = await _repository.CreateImportCandidateAsync(new CreateCliReaderImportCandidateCommand(
            config.PortId, parsed.PortKey!, parsed.Operation!, candidateKind, parsed.UserId!, parsed.SourceTranscriptRef,
            parsed.RootUtterance, parsed.StructuredOutputPayload!.Value, parsed.Confidence,
            parsed.UnresolvedFields!.Value, previewDiff, parsed.AssignedBusinessObjectCandidate!.Value, parsed.DraftOperationId,
            parsed.AssignedBusinessObject, parsed.AssignmentTargetScope, parsed.EvidenceRefs!.Value, parsed.ApprovalStatus ?? "not_requested",
            "candidate_created", parsed.IdempotencyKey!, _timeProvider.GetUtcNow()), ct);
        await _repository.RecordImportCandidateEvidenceAsync(result.CandidateId, "cli_mcp_import_candidate_created", _timeProvider.GetUtcNow(), ct);
        await AppendAsync(seed with { Status = "success", Code = "CLI_READER_IMPORT_CANDIDATE_CREATED", ScopeSummary = $"candidate_id={result.CandidateId};candidate_kind={result.CandidateKind};commit=false;approval=false" }, ct);
        var response = new { parsed.Operation, result.CandidateId, result.CandidateKind, result.Status, result.EvidenceUri, result.PreviewDiff, result.UnresolvedFields, result.AssignedBusinessObjectCandidate, result.DraftOperationId, result.AssignedBusinessObject, result.AssignmentTargetScope, result.EvidenceRefs, result.Confidence, result.SourceTranscriptRef, result.RootUtterance, result.ApprovalStatus, ssotConfirmed = false, committed = false, approvalUpdated = false };
        return new EndpointResponseDto(true, new Emission(null, null, null, [], JsonSerializer.SerializeToElement(response), []), []);
    }

    private static JsonElement BuildPreviewDiff(string operation, JsonElement? payload, JsonElement? unresolvedFields) => JsonSerializer.SerializeToElement(new
    {
        operation,
        proposed = payload ?? JsonSerializer.SerializeToElement(new { }),
        unresolved_fields = unresolvedFields ?? JsonSerializer.SerializeToElement(Array.Empty<string>()),
        commit = false,
        approval = false
    });

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
            return new(eventSeed, null, null, null, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null, [], new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), null, [], null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, new ValidationError("CLI_READER_PAYLOAD_REQUIRED", "CLI/MCP reader payload object is required."));

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
        var candidateIdRaw = ReadString(payload, "candidate_id") ?? ReadString(payload, "candidateId");
        Guid? candidateId = Guid.TryParse(candidateIdRaw, out var parsedCandidateId) ? parsedCandidateId : null;
        var sourceTranscriptRef = ReadString(payload, "source_transcript_ref") ?? ReadString(payload, "sourceTranscriptRef");
        var rootUtterance = ReadString(payload, "root_utterance") ?? ReadString(payload, "rootUtterance");
        var confidence = ReadDecimal(payload, "confidence");
        JsonElement? structuredOutputPayload = payload.TryGetProperty("structured_output_payload", out var sop) ? sop.Clone() : payload.TryGetProperty("structuredOutputPayload", out var sop2) ? sop2.Clone() : null;
        JsonElement? unresolvedFields = payload.TryGetProperty("unresolved_fields", out var uf) ? uf.Clone() : payload.TryGetProperty("unresolvedFields", out var uf2) ? uf2.Clone() : null;
        JsonElement? previewDiff = payload.TryGetProperty("preview_diff", out var pd) ? pd.Clone() : payload.TryGetProperty("previewDiff", out var pd2) ? pd2.Clone() : null;
        JsonElement? assignedBusinessObjectCandidate = payload.TryGetProperty("assigned_business_object_candidate", out var aboc) ? aboc.Clone() : payload.TryGetProperty("assignedBusinessObjectCandidate", out var aboc2) ? aboc2.Clone() : null;
        var draftOperationIdRaw = ReadString(payload, "draft_operation_id") ?? ReadString(payload, "draftOperationId");
        Guid? draftOperationId = Guid.TryParse(draftOperationIdRaw, out var parsedDraftOperationId) ? parsedDraftOperationId : null;
        JsonElement? assignedBusinessObject = payload.TryGetProperty("assigned_business_object", out var abo) ? abo.Clone() : payload.TryGetProperty("assignedBusinessObject", out var abo2) ? abo2.Clone() : null;
        JsonElement? assignmentTargetScope = payload.TryGetProperty("assignment_target_scope", out var ats) ? ats.Clone() : payload.TryGetProperty("assignmentTargetScope", out var ats2) ? ats2.Clone() : null;
        JsonElement? evidenceRefs = payload.TryGetProperty("evidence_refs", out var er) ? er.Clone() : payload.TryGetProperty("evidenceRefs", out var er2) ? er2.Clone() : null;
        var approvalStatus = ReadString(payload, "approval_status") ?? ReadString(payload, "approvalStatus") ?? "not_requested";
        eventSeed = eventSeed with { PortKey = portKey ?? "unknown", Operation = operation ?? "unknown", UserId = userId, Roles = roles.ToArray(), RequestId = requestId, IdempotencyKey = idempotencyKey, ScopeSummary = $"table={table ?? "unknown"}" };
        if (string.IsNullOrWhiteSpace(portKey)) return new(eventSeed, null, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, candidateId, sourceTranscriptRef, rootUtterance, structuredOutputPayload, confidence, unresolvedFields, previewDiff, assignedBusinessObjectCandidate, draftOperationId, assignedBusinessObject, assignmentTargetScope, evidenceRefs, approvalStatus, new ValidationError("CLI_READER_PORT_REQUIRED", "port_key is required."));
        // File stream port is keyed by an existing export_job_id, not by a table/column read.
        if (string.Equals(operation, "download_export_file", StringComparison.OrdinalIgnoreCase))
        {
            if (exportJobId is null) return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, null, candidateId, sourceTranscriptRef, rootUtterance, structuredOutputPayload, confidence, unresolvedFields, previewDiff, assignedBusinessObjectCandidate, draftOperationId, assignedBusinessObject, assignmentTargetScope, evidenceRefs, approvalStatus, new ValidationError("CLI_READER_FILE_EXPORT_JOB_ID_REQUIRED", "download_export_file requires a valid export_job_id."));
            return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, candidateId, sourceTranscriptRef, rootUtterance, structuredOutputPayload, confidence, unresolvedFields, previewDiff, assignedBusinessObjectCandidate, draftOperationId, assignedBusinessObject, assignmentTargetScope, evidenceRefs, approvalStatus, null);
        }
        if (operation is not null && IsImportCandidateOperation(operation))
            return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, candidateId, sourceTranscriptRef, rootUtterance, structuredOutputPayload, confidence, unresolvedFields, previewDiff, assignedBusinessObjectCandidate, draftOperationId, assignedBusinessObject, assignmentTargetScope, evidenceRefs, approvalStatus, null);
        if (string.IsNullOrWhiteSpace(table)) return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, candidateId, sourceTranscriptRef, rootUtterance, structuredOutputPayload, confidence, unresolvedFields, previewDiff, assignedBusinessObjectCandidate, draftOperationId, assignedBusinessObject, assignmentTargetScope, evidenceRefs, approvalStatus, new ValidationError("CLI_READER_TABLE_REQUIRED", "table is required."));
        if (string.Equals(operation, "create_export_job", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey)) return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, candidateId, sourceTranscriptRef, rootUtterance, structuredOutputPayload, confidence, unresolvedFields, previewDiff, assignedBusinessObjectCandidate, draftOperationId, assignedBusinessObject, assignmentTargetScope, evidenceRefs, approvalStatus, new ValidationError("CLI_READER_EXPORT_IDEMPOTENCY_KEY_REQUIRED", "create_export_job requires idempotency_key."));
            if (string.IsNullOrWhiteSpace(exportFormat) || !new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "csv", "json", "pdf", "zip" }.Contains(exportFormat)) return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, candidateId, sourceTranscriptRef, rootUtterance, structuredOutputPayload, confidence, unresolvedFields, previewDiff, assignedBusinessObjectCandidate, draftOperationId, assignedBusinessObject, assignmentTargetScope, evidenceRefs, approvalStatus, new ValidationError("CLI_READER_EXPORT_FORMAT_DENIED", "export_format must be csv/json/pdf/zip."));
        }
        return new(eventSeed, portKey, operation, userId, roles, table, columns, capabilities, filters, period, [], requestId, idempotencyKey, exportFormat, exportJobId, candidateId, sourceTranscriptRef, rootUtterance, structuredOutputPayload, confidence, unresolvedFields, previewDiff, assignedBusinessObjectCandidate, draftOperationId, assignedBusinessObject, assignmentTargetScope, evidenceRefs, approvalStatus, null);
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
    private static decimal? ReadDecimal(JsonElement obj, string name) => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var parsed) ? parsed : null;
    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement obj, string name) => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.String).ToDictionary(x => x.Name, x => x.Value.GetString()!, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private sealed record ParsedCliReaderRequest(CliReaderPortRuntimeEvent EventSeed, string? PortKey, string? Operation, string? UserId, IReadOnlySet<string> Roles, string? Table, IReadOnlyList<string> Columns, IReadOnlySet<string> Capabilities, IReadOnlyDictionary<string, string> Filters, string? Period, IReadOnlyList<string> RowScope, string? RequestId, string? IdempotencyKey, string? ExportFormat, Guid? ExportJobId, Guid? CandidateId, string? SourceTranscriptRef, string? RootUtterance, JsonElement? StructuredOutputPayload, decimal? Confidence, JsonElement? UnresolvedFields, JsonElement? PreviewDiff, JsonElement? AssignedBusinessObjectCandidate, Guid? DraftOperationId, JsonElement? AssignedBusinessObject, JsonElement? AssignmentTargetScope, JsonElement? EvidenceRefs, string? ApprovalStatus, ValidationError? Error);
}
