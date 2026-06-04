using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Runtime for admin CSV/JSON import validate-preview-apply cycle.
///
/// Owns:
/// - CSV/JSON parsing
/// - Schema-driven validation (manifest/schema conformity only; not business state)
/// - Snapshot + record persistence (preview only; no canonical mutation)
/// - Apply log write with canonical diff linkage (staged at MVP; no canonical entity mutation)
/// - Manifest and schema list queries for the admin UI selector
///
/// Invariants:
/// - PreviewAsync MUST NOT mutate canonical entity/hub/topology state.
/// - ApplyAsync writes apply_log with canonical diff linkage (sourceSnapshotId, manifestId).
/// - admin_import_records.status is conformity status only — not business or hub lifecycle state.
/// - All failure paths return explicit error codes. No silent fallback.
/// </summary>
public class AdminImportRuntime
{
    private readonly ILogger<AdminImportRuntime> _logger;
    private readonly AdminImportRepository _repository;
    private readonly TopologyRepository _topologyRepository;

    public AdminImportRuntime(
        ILogger<AdminImportRuntime> logger,
        AdminImportRepository repository,
        TopologyRepository topologyRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    // ---------------------------------------------------------------------------
    // Preview
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Parses sourceType content, validates each row against schemaId,
    /// stores snapshot + records, and returns preview summary.
    /// Does NOT mutate canonical state.
    /// </summary>
    public async Task<AdminImportPreviewResult> PreviewAsync(
        string sourceType,
        string fileName,
        Guid manifestId,
        Guid schemaId,
        string content,
        CancellationToken ct = default)
    {
        // Validate sourceType early (free check before any I/O)
        if (!string.Equals(sourceType, "csv", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sourceType, "json", StringComparison.OrdinalIgnoreCase))
            return Error("UNSUPPORTED_FILE_TYPE", $"sourceType must be csv or json; got: {sourceType}");

        // Load schema from schema_registry
        var schema = await _topologyRepository.LoadSchemaAsync(schemaId, ct);
        if (schema is null)
            return Error("SCHEMA_NOT_FOUND", $"Schema not found: {schemaId}");

        // Parse schema fields — fail-close; malformed schema_def is an explicit error
        var schemaFieldsResult = ParseSchemaFields(schema.RawDefinition);
        if (!schemaFieldsResult.Success)
            return Error(schemaFieldsResult.ErrorCode!, schemaFieldsResult.ErrorMessage!);
        var schemaFields = schemaFieldsResult.Fields!;

        // Validate manifest exists before any writes
        bool manifestExists;
        try
        {
            manifestExists = await _repository.ManifestExistsAsync(manifestId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminImportRuntime.PreviewAsync: ManifestExistsAsync failed for manifestId={Mid}", manifestId);
            return Error("REPOSITORY_UNAVAILABLE", "Failed to check manifest existence.");
        }
        if (!manifestExists)
            return Error("MANIFEST_NOT_FOUND", $"Manifest not found: {manifestId}");

        // Parse content
        List<ParsedRow> parsedRows;
        JsonElement rawHeaderJsonb;

        if (string.Equals(sourceType, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var parseResult = ParseCsv(content);
            if (!parseResult.Success)
                return Error(parseResult.ErrorCode!, parseResult.ErrorMessage!);
            parsedRows = parseResult.Rows!;
            rawHeaderJsonb = parseResult.HeaderJsonb;
        }
        else
        {
            var parseResult = ParseJson(content);
            if (!parseResult.Success)
                return Error(parseResult.ErrorCode!, parseResult.ErrorMessage!);
            parsedRows = parseResult.Rows!;
            rawHeaderJsonb = JsonSerializer.SerializeToElement(Array.Empty<string>());
        }

        // Validate each row against schema fields
        var recordDataList = new List<AdminImportRecordData>();
        var rawRowsList = new List<JsonElement>();

        foreach (var parsedRow in parsedRows)
        {
            var preErrors = parsedRow.PreParseErrors ?? Array.Empty<string>();
            var schemaErrors = ContentDataConformance.ValidateRow(
                parsedRow.Row,
                schemaFields.Select(f => new ContentDataConformance.SchemaField(f.Key, f.Type, f.Required)).ToList());
            var allErrors = preErrors.Concat(schemaErrors).ToList();
            var status = allErrors.Count == 0 ? "valid" : "invalid";

            rawRowsList.Add(JsonSerializer.SerializeToElement(parsedRow.Row));
            recordDataList.Add(new AdminImportRecordData(
                RowIndex: parsedRow.Index,
                Records: parsedRow.Row,
                Status: status,
                ValidationErrors: allErrors));
        }

        int validCount = recordDataList.Count(r => r.Status == "valid");
        int invalidCount = recordDataList.Count(r => r.Status == "invalid");

        var validationSummary = new { validCount, invalidCount, totalCount = parsedRows.Count };
        var validationSummaryJsonb = JsonSerializer.SerializeToElement(validationSummary);
        var rawRowsJsonb = JsonSerializer.SerializeToElement(rawRowsList);

        var snapshotId = Guid.NewGuid();

        // Write snapshot — repository exception becomes structured error
        try
        {
            var snapshotOk = await _repository.CreateSnapshotAsync(
                snapshotId, sourceType, fileName, manifestId,
                rawHeaderJsonb, rawRowsJsonb, validationSummaryJsonb, ct);
            if (!snapshotOk)
                return Error("REPOSITORY_UNAVAILABLE", "Failed to create import snapshot.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminImportRuntime.PreviewAsync: CreateSnapshotAsync failed for snapshotId={Sid}", snapshotId);
            return Error("REPOSITORY_UNAVAILABLE", "Failed to create import snapshot.");
        }

        // Write records — repository exception becomes structured error
        var recordRows = recordDataList.Select(r => (
            records: JsonSerializer.SerializeToElement(r.Records),
            status: r.Status,
            validationErrors: JsonSerializer.SerializeToElement(r.ValidationErrors)
        )).ToList();

        try
        {
            var recordsOk = await _repository.CreateRecordsAsync(manifestId, snapshotId, recordRows, ct);
            if (!recordsOk)
                return Error("REPOSITORY_UNAVAILABLE", "Failed to create import records.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminImportRuntime.PreviewAsync: CreateRecordsAsync failed for snapshotId={Sid}", snapshotId);
            return Error("REPOSITORY_UNAVAILABLE", "Failed to create import records.");
        }

        _logger.LogInformation(
            "AdminImportRuntime.PreviewAsync: snapshotId={Sid} validCount={V} invalidCount={I}",
            snapshotId, validCount, invalidCount);

        return new AdminImportPreviewResult(
            Success: true,
            SnapshotId: snapshotId.ToString(),
            ErrorCode: null,
            ErrorMessage: null,
            ValidCount: validCount,
            InvalidCount: invalidCount,
            Records: recordDataList);
    }

    // ---------------------------------------------------------------------------
    // Apply
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Applies valid records from the given snapshot.
    /// Writes apply_log with canonical diff linkage (sourceSnapshotId + manifestId in diff JSONB).
    /// At MVP, status='staged' — no canonical entity mutation is performed.
    /// </summary>
    public async Task<AdminImportApplyResult> ApplyAsync(
        Guid snapshotId,
        CancellationToken ct = default)
    {
        bool exists;
        try
        {
            exists = await _repository.SnapshotExistsAsync(snapshotId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminImportRuntime.ApplyAsync: SnapshotExistsAsync failed for snapshotId={Sid}", snapshotId);
            return ApplyError("REPOSITORY_UNAVAILABLE", "Failed to check snapshot existence.");
        }
        if (!exists)
            return ApplyError("SNAPSHOT_NOT_FOUND", $"Snapshot not found: {snapshotId}");

        int validCount;
        try
        {
            validCount = await _repository.CountValidRecordsAsync(snapshotId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminImportRuntime.ApplyAsync: CountValidRecordsAsync failed for snapshotId={Sid}", snapshotId);
            return ApplyError("REPOSITORY_UNAVAILABLE", "Failed to count valid records.");
        }

        // Load snapshot metadata for canonical diff linkage
        AdminImportSnapshotMeta? meta;
        try
        {
            meta = await _repository.GetSnapshotMetaAsync(snapshotId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminImportRuntime.ApplyAsync: GetSnapshotMetaAsync failed for snapshotId={Sid}", snapshotId);
            return ApplyError("REPOSITORY_UNAVAILABLE", "Failed to load snapshot metadata for diff linkage.");
        }
        if (meta is null)
            return ApplyError("SNAPSHOT_META_NOT_FOUND", $"Snapshot metadata not found: {snapshotId}");

        var applyLogId = Guid.NewGuid();

        // Canonical diff JSONB: records source intake snapshot + manifest linkage + staged mutation status.
        // apply_log.snapshot_id FK provides DB-level linkage; this JSONB adds explicit auditability.
        var diff = new
        {
            sourceSnapshotId = snapshotId.ToString(),
            manifestId = meta.ManifestId.ToString(),
            sourceType = meta.SourceType,
            fileName = meta.FileName,
            validRecordCount = validCount,
            canonicalMutationStatus = "staged",
            canonicalMutationNote = "Valid records staged for canonical mutation. Mutation not yet implemented at MVP.",
            appliedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        var diffJsonb = JsonSerializer.SerializeToElement(diff);

        try
        {
            var ok = await _repository.CreateApplyLogAsync(
                applyLogId, snapshotId, validCount, diffJsonb, "staged", ct);
            if (!ok)
                return ApplyError("REPOSITORY_UNAVAILABLE", "Failed to write apply log.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminImportRuntime.ApplyAsync: CreateApplyLogAsync failed for applyLogId={Lid}", applyLogId);
            return ApplyError("REPOSITORY_UNAVAILABLE", "Failed to write apply log.");
        }

        _logger.LogInformation(
            "AdminImportRuntime.ApplyAsync: applyLogId={Lid} snapshotId={Sid} validCount={V} status=staged",
            applyLogId, snapshotId, validCount);

        return new AdminImportApplyResult(
            Success: true,
            ApplyLogId: applyLogId.ToString(),
            SnapshotId: snapshotId.ToString(),
            AppliedRecordCount: validCount,
            Status: "staged",
            Note: "Apply log written with canonical diff linkage. Canonical entity mutation is not yet implemented (MVP staged boundary).",
            ErrorCode: null,
            ErrorMessage: null);
    }

    // ---------------------------------------------------------------------------
    // List queries
    // ---------------------------------------------------------------------------

    public async Task<IReadOnlyList<AdminImportManifestSummary>> ListManifestsAsync(
        CancellationToken ct = default)
        => await _repository.ListManifestsAsync(ct);

    public async Task<IReadOnlyList<AdminImportSchemaSummary>> ListSchemasAsync(
        CancellationToken ct = default)
        => await _repository.ListSchemasAsync(ct);

    /// <summary>
    /// Reloads persisted import records for contents Step3 re-edit (lineage via snapshotId).
    /// </summary>
    public async Task<AdminImportListSnapshotRecordsResult> ListSnapshotRecordsAsync(
        Guid snapshotId,
        CancellationToken ct = default)
    {
        bool exists;
        try
        {
            exists = await _repository.SnapshotExistsAsync(snapshotId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AdminImportRuntime.ListSnapshotRecordsAsync: SnapshotExistsAsync failed for snapshotId={Sid}",
                snapshotId);
            return ListRecordsError("REPOSITORY_UNAVAILABLE", "Failed to check snapshot existence.");
        }

        if (!exists)
            return ListRecordsError("SNAPSHOT_NOT_FOUND", $"Snapshot not found: {snapshotId}");

        try
        {
            var records = await _repository.ListSnapshotRecordsAsync(snapshotId, ct);
            return new AdminImportListSnapshotRecordsResult(
                Success: true,
                SnapshotId: snapshotId.ToString(),
                ErrorCode: null,
                ErrorMessage: null,
                Records: records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AdminImportRuntime.ListSnapshotRecordsAsync: ListSnapshotRecordsAsync failed for snapshotId={Sid}",
                snapshotId);
            return ListRecordsError("REPOSITORY_UNAVAILABLE", "Failed to load import records.");
        }
    }

    private static AdminImportListSnapshotRecordsResult ListRecordsError(string code, string message)
        => new(false, null, code, message, Array.Empty<AdminImportRecordData>());

    // ---------------------------------------------------------------------------
    // CSV parsing
    // ---------------------------------------------------------------------------

    private static ParseResult ParseCsv(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new ParseResult(false, "CONTENT_EMPTY", "CSV content is empty.", null, default);

        var lines = content
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .ToList();

        if (lines.Count == 0 || string.IsNullOrWhiteSpace(lines[0]))
            return new ParseResult(false, "MALFORMED_CSV", "CSV has no header line.", null, default);

        var (headerMalformed, headers) = SplitCsvLine(lines[0]);
        if (headerMalformed)
            return new ParseResult(false, "MALFORMED_CSV", "CSV header line has an unclosed quote.", null, default);
        if (headers.Count == 0)
            return new ParseResult(false, "MALFORMED_CSV", "CSV header line is empty.", null, default);

        var headerJsonb = JsonSerializer.SerializeToElement(headers);
        var rows = new List<ParsedRow>();

        for (int i = 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var (malformed, values) = SplitCsvLine(line);

            if (malformed)
            {
                rows.Add(new ParsedRow(new Dictionary<string, object?>(), i - 1,
                    new[] { "CSV row has an unclosed quote." }));
                continue;
            }

            if (values.Count != headers.Count)
            {
                // Build partial row for display; mark invalid
                var partialRow = new Dictionary<string, object?>();
                for (int j = 0; j < headers.Count; j++)
                    partialRow[headers[j]] = j < values.Count ? (object?)values[j] : null;
                rows.Add(new ParsedRow(partialRow, i - 1,
                    new[] { $"field count mismatch: expected {headers.Count}, got {values.Count}" }));
                continue;
            }

            var row = new Dictionary<string, object?>();
            for (int j = 0; j < headers.Count; j++)
                row[headers[j]] = values[j];
            rows.Add(new ParsedRow(row, i - 1));
        }

        if (rows.Count == 0)
            return new ParseResult(false, "CSV_NO_DATA_ROWS", "CSV has a header but no data rows.", null, default);

        return new ParseResult(true, null, null, rows, headerJsonb);
    }

    private static (bool Malformed, List<string> Fields) SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        if (inQuotes)
            return (true, fields); // unclosed quote detected

        fields.Add(sb.ToString());
        return (false, fields);
    }

    // ---------------------------------------------------------------------------
    // JSON parsing
    // ---------------------------------------------------------------------------

    private static ParseResult ParseJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new ParseResult(false, "CONTENT_EMPTY", "JSON content is empty.", null, default);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            return new ParseResult(false, "MALFORMED_JSON", $"JSON parse error: {ex.Message}", null, default);
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return new ParseResult(false, "UNSUPPORTED_JSON_SHAPE",
                "JSON must be an array of objects. Root is not an array.", null, default);

        var rows = new List<ParsedRow>();
        int index = 0;

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                return new ParseResult(false, "UNSUPPORTED_JSON_SHAPE",
                    $"Each array element must be a JSON object. Element at index {index} is not an object.", null, default);

            var row = new Dictionary<string, object?>();
            foreach (var prop in element.EnumerateObject())
            {
                row[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String  => (object?)prop.Value.GetString(),
                    JsonValueKind.Number  => prop.Value.TryGetDouble(out var d) ? d : (object?)prop.Value.GetRawText(),
                    JsonValueKind.True    => true,
                    JsonValueKind.False   => false,
                    JsonValueKind.Null    => null,
                    _                    => prop.Value.GetRawText()
                };
            }
            rows.Add(new ParsedRow(row, index));
            index++;
        }

        return new ParseResult(true, null, null, rows, default);
    }

    // ---------------------------------------------------------------------------
    // Schema validation — fail-close
    // ---------------------------------------------------------------------------

    private static ParseSchemaResult ParseSchemaFields(string? rawDefinition)
    {
        if (string.IsNullOrWhiteSpace(rawDefinition))
            return new ParseSchemaResult(false, "SCHEMA_DEF_INVALID", "Schema definition is null or empty.", null);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawDefinition);
        }
        catch (JsonException ex)
        {
            return new ParseSchemaResult(false, "SCHEMA_DEF_INVALID",
                $"Schema definition is not valid JSON: {ex.Message}", null);
        }

        if (!doc.RootElement.TryGetProperty("fields", out var fieldsEl))
            return new ParseSchemaResult(false, "SCHEMA_FIELDS_REQUIRED",
                "Schema definition is missing required 'fields' property.", null);

        if (fieldsEl.ValueKind != JsonValueKind.Array)
            return new ParseSchemaResult(false, "SCHEMA_FIELDS_REQUIRED",
                "Schema definition 'fields' must be a JSON array.", null);

        var fields = new List<SchemaField>();
        int fieldIndex = 0;
        foreach (var fieldEl in fieldsEl.EnumerateArray())
        {
            var key = fieldEl.TryGetProperty("key", out var k) ? k.GetString() : null;
            if (string.IsNullOrWhiteSpace(key))
                return new ParseSchemaResult(false, "SCHEMA_FIELD_KEY_REQUIRED",
                    $"Schema field at index {fieldIndex} is missing required 'key' property.", null);
            var type = fieldEl.TryGetProperty("type", out var t) ? t.GetString() : "text";
            var required = fieldEl.TryGetProperty("required", out var r) && r.GetBoolean();
            fields.Add(new SchemaField(key!, type ?? "text", required));
            fieldIndex++;
        }

        return new ParseSchemaResult(true, null, null, fields);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static AdminImportPreviewResult Error(string code, string message)
        => new(false, null, code, message, 0, 0, Array.Empty<AdminImportRecordData>());

    private static AdminImportApplyResult ApplyError(string code, string message)
        => new(false, null, null, 0, "error", null, code, message);

    private sealed record SchemaField(string Key, string Type, bool Required);

    private sealed record ParsedRow(
        Dictionary<string, object?> Row,
        int Index,
        IReadOnlyList<string>? PreParseErrors = null);

    private sealed record ParseResult(
        bool Success,
        string? ErrorCode,
        string? ErrorMessage,
        List<ParsedRow>? Rows,
        JsonElement HeaderJsonb);

    private sealed record ParseSchemaResult(
        bool Success,
        string? ErrorCode,
        string? ErrorMessage,
        List<SchemaField>? Fields);
}
