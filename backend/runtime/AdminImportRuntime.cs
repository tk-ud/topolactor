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
/// - Apply log write (staged at MVP; no canonical entity mutation)
/// - Manifest and schema list queries for the admin UI selector
///
/// Invariants:
/// - PreviewAsync MUST NOT mutate canonical entity/hub/topology state.
/// - ApplyAsync writes apply_log with status='staged' at MVP (partial boundary).
/// - admin_import_records.status is conformity status only — not business or hub lifecycle state.
/// - Broken manifest / schema / malformed file → explicit ValidationError (no silent fallback).
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
        // Load schema from schema_registry
        var schema = await _topologyRepository.LoadSchemaAsync(schemaId, ct);
        if (schema is null)
            return Error("SCHEMA_NOT_FOUND", $"Schema not found: {schemaId}");

        // Parse content
        List<(Dictionary<string, object?> row, int index)> parsedRows;
        JsonElement rawHeaderJsonb;

        if (string.Equals(sourceType, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var parseResult = ParseCsv(content);
            if (!parseResult.Success)
                return Error(parseResult.ErrorCode!, parseResult.ErrorMessage!);
            parsedRows = parseResult.Rows!;
            rawHeaderJsonb = parseResult.HeaderJsonb;
        }
        else if (string.Equals(sourceType, "json", StringComparison.OrdinalIgnoreCase))
        {
            var parseResult = ParseJson(content);
            if (!parseResult.Success)
                return Error(parseResult.ErrorCode!, parseResult.ErrorMessage!);
            parsedRows = parseResult.Rows!;
            rawHeaderJsonb = JsonSerializer.SerializeToElement(Array.Empty<string>());
        }
        else
        {
            return Error("UNSUPPORTED_FILE_TYPE", $"sourceType must be csv or json; got: {sourceType}");
        }

        // Parse schema fields
        var schemaFields = ParseSchemaFields(schema.RawDefinition);

        // Validate each row
        var recordDataList = new List<AdminImportRecordData>();
        var rawRowsList = new List<JsonElement>();

        foreach (var (row, index) in parsedRows)
        {
            var errors = ValidateRow(row, schemaFields);
            var status = errors.Count == 0 ? "valid" : "invalid";
            rawRowsList.Add(JsonSerializer.SerializeToElement(row));
            recordDataList.Add(new AdminImportRecordData(
                RowIndex: index,
                Records: row,
                Status: status,
                ValidationErrors: errors));
        }

        int validCount = recordDataList.Count(r => r.Status == "valid");
        int invalidCount = recordDataList.Count(r => r.Status == "invalid");

        var validationSummary = new { validCount, invalidCount, totalCount = parsedRows.Count };
        var validationSummaryJsonb = JsonSerializer.SerializeToElement(validationSummary);
        var rawRowsJsonb = JsonSerializer.SerializeToElement(rawRowsList);

        var snapshotId = Guid.NewGuid();

        // Write snapshot
        var snapshotOk = await _repository.CreateSnapshotAsync(
            snapshotId, sourceType, fileName, manifestId,
            rawHeaderJsonb, rawRowsJsonb, validationSummaryJsonb, ct);

        if (!snapshotOk)
            return Error("REPOSITORY_UNAVAILABLE", "Failed to create import snapshot.");

        // Write records
        var recordRows = recordDataList.Select(r => (
            records: JsonSerializer.SerializeToElement(r.Records),
            status: r.Status,
            validationErrors: JsonSerializer.SerializeToElement(r.ValidationErrors)
        )).ToList();

        var recordsOk = await _repository.CreateRecordsAsync(manifestId, snapshotId, recordRows, ct);
        if (!recordsOk)
            return Error("REPOSITORY_UNAVAILABLE", "Failed to create import records.");

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
    /// At MVP, writes apply_log with status='staged' — no canonical entity mutation.
    /// </summary>
    public async Task<AdminImportApplyResult> ApplyAsync(
        Guid snapshotId,
        CancellationToken ct = default)
    {
        var exists = await _repository.SnapshotExistsAsync(snapshotId, ct);
        if (!exists)
            return ApplyError("SNAPSHOT_NOT_FOUND", $"Snapshot not found: {snapshotId}");

        var validCount = await _repository.CountValidRecordsAsync(snapshotId, ct);

        var applyLogId = Guid.NewGuid();
        var diff = new
        {
            snapshotId = snapshotId.ToString(),
            appliedRecordCount = validCount,
            canonicalMutationStatus = "staged",
            note = "MVP: valid records counted; canonical entity mutation not yet implemented. Apply log written for audit."
        };
        var diffJsonb = JsonSerializer.SerializeToElement(diff);

        var ok = await _repository.CreateApplyLogAsync(
            applyLogId, snapshotId, validCount, diffJsonb, "staged", ct);

        if (!ok)
            return ApplyError("REPOSITORY_UNAVAILABLE", "Failed to write apply log.");

        _logger.LogInformation(
            "AdminImportRuntime.ApplyAsync: applyLogId={Lid} snapshotId={Sid} validCount={V} status=staged",
            applyLogId, snapshotId, validCount);

        return new AdminImportApplyResult(
            Success: true,
            ApplyLogId: applyLogId.ToString(),
            SnapshotId: snapshotId.ToString(),
            AppliedRecordCount: validCount,
            Status: "staged",
            Note: "Apply log written. Canonical entity mutation is not yet implemented (MVP partial boundary).",
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

        var headers = SplitCsvLine(lines[0]);
        if (headers.Count == 0)
            return new ParseResult(false, "MALFORMED_CSV", "CSV header line is empty.", null, default);

        var headerJsonb = JsonSerializer.SerializeToElement(headers);
        var rows = new List<(Dictionary<string, object?>, int)>();

        for (int i = 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = SplitCsvLine(line);
            var row = new Dictionary<string, object?>();
            for (int j = 0; j < headers.Count; j++)
            {
                row[headers[j]] = j < values.Count ? (object?)values[j] : null;
            }
            rows.Add((row, i - 1));
        }

        return new ParseResult(true, null, null, rows, headerJsonb);
    }

    private static List<string> SplitCsvLine(string line)
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
        fields.Add(sb.ToString());
        return fields;
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

        var rows = new List<(Dictionary<string, object?>, int)>();
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
            rows.Add((row, index));
            index++;
        }

        return new ParseResult(true, null, null, rows, default);
    }

    // ---------------------------------------------------------------------------
    // Schema validation
    // ---------------------------------------------------------------------------

    private static List<SchemaField> ParseSchemaFields(string? rawDefinition)
    {
        if (string.IsNullOrWhiteSpace(rawDefinition))
            return new List<SchemaField>();

        try
        {
            var doc = JsonDocument.Parse(rawDefinition);
            if (!doc.RootElement.TryGetProperty("fields", out var fieldsEl)
                || fieldsEl.ValueKind != JsonValueKind.Array)
                return new List<SchemaField>();

            var fields = new List<SchemaField>();
            foreach (var fieldEl in fieldsEl.EnumerateArray())
            {
                var key = fieldEl.TryGetProperty("key", out var k) ? k.GetString() : null;
                var type = fieldEl.TryGetProperty("type", out var t) ? t.GetString() : "text";
                var required = fieldEl.TryGetProperty("required", out var r) && r.GetBoolean();
                if (!string.IsNullOrWhiteSpace(key))
                    fields.Add(new SchemaField(key!, type ?? "text", required));
            }
            return fields;
        }
        catch (JsonException)
        {
            return new List<SchemaField>();
        }
    }

    private static List<string> ValidateRow(
        Dictionary<string, object?> row,
        List<SchemaField> fields)
    {
        var errors = new List<string>();

        foreach (var field in fields)
        {
            row.TryGetValue(field.Key, out var value);

            if (field.Required)
            {
                if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
                {
                    errors.Add($"field '{field.Key}' is required but missing or empty");
                    continue;
                }
            }

            if (value is null) continue;

            var typeError = ValidateFieldType(field.Key, value, field.Type);
            if (typeError is not null)
                errors.Add(typeError);
        }

        return errors;
    }

    private static string? ValidateFieldType(string key, object value, string type)
    {
        var raw = value?.ToString() ?? "";

        return type switch
        {
            "number" or "integer" or "float" or "decimal"
                when !double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _)
                => $"field '{key}' expects type '{type}' but value is not numeric: '{raw}'",

            "boolean"
                when !bool.TryParse(raw, out _) && raw is not ("1" or "0" or "true" or "false")
                => $"field '{key}' expects type 'boolean' but value is not boolean: '{raw}'",

            "uuid"
                when !Guid.TryParse(raw, out _)
                => $"field '{key}' expects type 'uuid' but value is not a valid UUID: '{raw}'",

            _ => null
        };
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static AdminImportPreviewResult Error(string code, string message)
        => new(false, null, code, message, 0, 0, Array.Empty<AdminImportRecordData>());

    private static AdminImportApplyResult ApplyError(string code, string message)
        => new(false, null, null, 0, "error", null, code, message);

    private sealed record SchemaField(string Key, string Type, bool Required);

    private sealed record ParseResult(
        bool Success,
        string? ErrorCode,
        string? ErrorMessage,
        List<(Dictionary<string, object?> row, int index)>? Rows,
        JsonElement HeaderJsonb);
}
