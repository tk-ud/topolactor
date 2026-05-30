using System.Text.Json.Serialization;

namespace Topolactor.Schema;

// ---------------------------------------------------------------------------
// PDF Export Snapshot Runtime Contracts
// ---------------------------------------------------------------------------
// Snapshot boundary for document canvas PDF export requests.
//
// Scope:
//   - Records export snapshot (canvas state, field values, backgroundImageUrl).
//   - Links back to source intake snapshot / apply log for audit trail.
//   - Does NOT generate PDF bytes — production PDF layout engine is out of scope.
//   - Snapshot record proves the export was requested and the canvas state was captured.
//
// Out of scope: production PDF layout engine, pagination, printer calibration,
//               actual PDF byte generation, file storage.
// ---------------------------------------------------------------------------

/// <summary>
/// Request to create a PDF export snapshot.
/// Contains the canvas state at the time of export request.
/// </summary>
public record PdfExportSnapshotRequest(
    [property: JsonPropertyName("templateKey")]         string TemplateKey,
    [property: JsonPropertyName("backgroundImageUrl")]  string? BackgroundImageUrl,
    [property: JsonPropertyName("fields")]              IReadOnlyList<PdfExportSnapshotField> Fields,
    [property: JsonPropertyName("sourceSnapshotId")]    string? SourceSnapshotId,
    [property: JsonPropertyName("sourceApplyLogId")]    string? SourceApplyLogId,
    [property: JsonPropertyName("requestedBy")]         string? RequestedBy
);

/// <summary>
/// A single field captured in the PDF export snapshot.
/// </summary>
public record PdfExportSnapshotField(
    [property: JsonPropertyName("key")]   string Key,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("x")]     double? X,
    [property: JsonPropertyName("y")]     double? Y,
    [property: JsonPropertyName("value")] string? Value
);

/// <summary>
/// Result of PdfExportSnapshotRuntime.CreateSnapshotAsync.
/// snapshotId is the created export_snapshot record.
/// </summary>
public record PdfExportSnapshotResult(
    bool Success,
    string? ExportSnapshotId,
    string? TemplateKey,
    int FieldCount,
    string? SourceSnapshotId,
    string? SourceApplyLogId,
    string? Status,
    string? Note,
    string? ErrorCode,
    string? ErrorMessage
);

/// <summary>
/// Internal export snapshot record written to the in-memory log at MVP.
/// At production, this would be persisted to topology.pdf_export_snapshot.
/// </summary>
public record PdfExportSnapshotRecord(
    Guid ExportSnapshotId,
    string TemplateKey,
    string? BackgroundImageUrl,
    IReadOnlyList<PdfExportSnapshotField> Fields,
    string? SourceSnapshotId,
    string? SourceApplyLogId,
    string? RequestedBy,
    string Status,
    DateTimeOffset CreatedAt
);
