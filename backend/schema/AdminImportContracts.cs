using System.Text.Json.Serialization;

namespace Topolactor.Schema;

// ---------------------------------------------------------------------------
// Request DTOs
// ---------------------------------------------------------------------------

/// <summary>
/// Request DTO for admin_csv_json_import:upload_preview.
/// content is the raw file text (CSV or JSON).
/// </summary>
public record AdminImportUploadPreviewRequestDto(
    [property: JsonPropertyName("sourceType")]  string SourceType,
    [property: JsonPropertyName("fileName")]    string FileName,
    [property: JsonPropertyName("manifestId")]  string ManifestId,
    [property: JsonPropertyName("schemaId")]    string SchemaId,
    [property: JsonPropertyName("content")]     string Content
);

/// <summary>
/// Request DTO for admin_csv_json_import:apply.
/// </summary>
public record AdminImportApplyRequestDto(
    [property: JsonPropertyName("snapshotId")] string SnapshotId
);

/// <summary>
/// Request DTO for admin_csv_json_import:list_snapshot_records.
/// </summary>
public record AdminImportListSnapshotRecordsRequestDto(
    [property: JsonPropertyName("snapshotId")] string SnapshotId
);

// ---------------------------------------------------------------------------
// Response DTOs
// ---------------------------------------------------------------------------

/// <summary>
/// Per-row preview record returned to the frontend.
/// </summary>
public record AdminImportRecordPreviewDto(
    [property: JsonPropertyName("rowIndex")]          int RowIndex,
    [property: JsonPropertyName("records")]           object Records,
    [property: JsonPropertyName("status")]            string Status,
    [property: JsonPropertyName("validationErrors")]  IReadOnlyList<string> ValidationErrors
);

/// <summary>
/// Response DTO for upload_preview.
/// validCount / invalidCount are row-level counts based on manifest/schema conformity only.
/// status is not business state or hub lifecycle state.
/// </summary>
public record AdminImportUploadPreviewResponseDto(
    [property: JsonPropertyName("ok")]           bool Ok,
    [property: JsonPropertyName("snapshotId")]   string SnapshotId,
    [property: JsonPropertyName("sourceType")]   string SourceType,
    [property: JsonPropertyName("manifestId")]   string ManifestId,
    [property: JsonPropertyName("schemaId")]     string SchemaId,
    [property: JsonPropertyName("validCount")]   int ValidCount,
    [property: JsonPropertyName("invalidCount")] int InvalidCount,
    [property: JsonPropertyName("records")]      IReadOnlyList<AdminImportRecordPreviewDto> Records
);

/// <summary>
/// Response DTO for apply.
/// status='staged' means apply_log was written but no canonical mutation occurred yet (MVP partial boundary).
/// </summary>
public record AdminImportApplyResponseDto(
    [property: JsonPropertyName("ok")]                 bool Ok,
    [property: JsonPropertyName("applyLogId")]         string ApplyLogId,
    [property: JsonPropertyName("snapshotId")]         string SnapshotId,
    [property: JsonPropertyName("appliedRecordCount")] int AppliedRecordCount,
    [property: JsonPropertyName("status")]             string Status,
    [property: JsonPropertyName("note")]               string Note
);

/// <summary>
/// Manifest list item for the import selector.
/// </summary>
public record AdminImportManifestListItemDto(
    [property: JsonPropertyName("manifestId")] string ManifestId,
    [property: JsonPropertyName("status")]     string Status,
    [property: JsonPropertyName("createdAt")]  string CreatedAt,
    [property: JsonPropertyName("manifestKey")]  string? ManifestKey = null,
    [property: JsonPropertyName("hubId")]        string? HubId = null
);

/// <summary>
/// Schema list item for the import selector.
/// </summary>
public record AdminImportSchemaListItemDto(
    [property: JsonPropertyName("schemaId")] string SchemaId,
    [property: JsonPropertyName("name")]     string Name
);

// ---------------------------------------------------------------------------
// Internal data structures used by AdminImportRuntime
// ---------------------------------------------------------------------------

/// <summary>
/// A single parsed and validated import record.
/// status is manifest/schema conformity only — not business state.
/// </summary>
public record AdminImportRecordData(
    int RowIndex,
    object Records,
    string Status,
    IReadOnlyList<string> ValidationErrors
);

/// <summary>
/// Result of AdminImportRuntime.PreviewAsync.
/// </summary>
public record AdminImportPreviewResult(
    bool Success,
    string? SnapshotId,
    string? ErrorCode,
    string? ErrorMessage,
    int ValidCount,
    int InvalidCount,
    IReadOnlyList<AdminImportRecordData> Records
);

/// <summary>
/// Result of AdminImportRuntime.ListSnapshotRecordsAsync.
/// </summary>
public record AdminImportListSnapshotRecordsResult(
    bool Success,
    string? SnapshotId,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<AdminImportRecordData> Records
);

/// <summary>
/// Result of AdminImportRuntime.ApplyAsync.
/// </summary>
public record AdminImportApplyResult(
    bool Success,
    string? ApplyLogId,
    string? SnapshotId,
    int AppliedRecordCount,
    string Status,
    string? Note,
    string? ErrorCode,
    string? ErrorMessage
);

/// <summary>
/// A manifest summary row loaded from the manifest table.
/// </summary>
public record AdminImportManifestSummary(
    Guid ManifestId,
    string Status,
    DateTimeOffset CreatedAt,
    string? ManifestKey = null,
    string? HubId = null
);

/// <summary>
/// A schema summary row loaded from schema_registry.
/// </summary>
public record AdminImportSchemaSummary(
    Guid SchemaId,
    string Name
);

/// <summary>
/// Snapshot metadata loaded from admin_import_snapshot for canonical diff linkage in apply.
/// </summary>
public record AdminImportSnapshotMeta(
    Guid ManifestId,
    string SourceType,
    string FileName
);
