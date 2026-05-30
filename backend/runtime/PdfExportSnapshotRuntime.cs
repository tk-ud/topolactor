using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// PDF export snapshot runtime — snapshot boundary only.
///
/// Purpose:
///   Creates an export snapshot record that captures the DocumentCanvas state
///   (templateKey, backgroundImageUrl, fields with values) at the time of export
///   request. This snapshot is the audit evidence that an export was requested
///   and what data was present.
///
/// NOT a production PDF layout engine:
///   - Does not generate PDF bytes.
///   - Does not implement pagination, printer calibration, or font rendering.
///   - The snapshot is the canonical diff/audit linkage surface for PDF export.
///
/// Canonical diff linkage:
///   - sourceSnapshotId links back to admin_import_snapshot (if applicable).
///   - sourceApplyLogId links back to admin_import_apply_log (if applicable).
///   - Both are optional (canvas may be filled via other routes).
///
/// Invariants:
///   - CreateSnapshotAsync does not mutate canonical entity/hub/topology state.
///   - All failure paths return explicit error codes. No silent fallback.
///   - Snapshot record is created even when no source links are provided.
/// </summary>
public class PdfExportSnapshotRuntime
{
    private readonly ILogger<PdfExportSnapshotRuntime> _logger;

    // In-memory snapshot log for MVP boundary. At production, replace with
    // NpgsqlPdfExportSnapshotRepository writing to topology.pdf_export_snapshot.
    private readonly List<PdfExportSnapshotRecord> _snapshotLog = [];

    public PdfExportSnapshotRuntime(ILogger<PdfExportSnapshotRuntime> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a PDF export snapshot capturing the canvas state at export request time.
    /// </summary>
    public Task<PdfExportSnapshotResult> CreateSnapshotAsync(
        PdfExportSnapshotRequest request,
        CancellationToken ct = default)
    {
        _ = ct;

        if (request is null)
            return Task.FromResult(Error("PDF_EXPORT_REQUEST_NULL", "request is required."));

        if (string.IsNullOrWhiteSpace(request.TemplateKey))
            return Task.FromResult(Error("PDF_EXPORT_TEMPLATE_KEY_REQUIRED",
                "templateKey is required and must be non-empty."));

        if (request.Fields is null)
            return Task.FromResult(Error("PDF_EXPORT_FIELDS_REQUIRED",
                "fields is required."));

        // Validate field entries
        for (var i = 0; i < request.Fields.Count; i++)
        {
            var field = request.Fields[i];
            if (string.IsNullOrWhiteSpace(field.Key))
                return Task.FromResult(Error("PDF_EXPORT_FIELD_KEY_REQUIRED",
                    $"fields[{i}].key is required and must be non-empty."));
        }

        // Validate optional source linkage IDs
        if (request.SourceSnapshotId is not null &&
            !Guid.TryParse(request.SourceSnapshotId, out _))
            return Task.FromResult(Error("PDF_EXPORT_INVALID_SOURCE_SNAPSHOT_ID",
                $"sourceSnapshotId '{request.SourceSnapshotId}' is not a valid UUID."));

        if (request.SourceApplyLogId is not null &&
            !Guid.TryParse(request.SourceApplyLogId, out _))
            return Task.FromResult(Error("PDF_EXPORT_INVALID_SOURCE_APPLY_LOG_ID",
                $"sourceApplyLogId '{request.SourceApplyLogId}' is not a valid UUID."));

        var exportSnapshotId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var record = new PdfExportSnapshotRecord(
            ExportSnapshotId: exportSnapshotId,
            TemplateKey: request.TemplateKey,
            BackgroundImageUrl: request.BackgroundImageUrl,
            Fields: request.Fields,
            SourceSnapshotId: request.SourceSnapshotId,
            SourceApplyLogId: request.SourceApplyLogId,
            RequestedBy: request.RequestedBy,
            Status: "snapshot_created",
            CreatedAt: now);

        _snapshotLog.Add(record);

        _logger.LogInformation(
            "PdfExportSnapshotRuntime.CreateSnapshotAsync: " +
            "exportSnapshotId={Id} templateKey={Key} fieldCount={Count} " +
            "sourceSnapshotId={SourceId} sourceApplyLogId={ApplyId}",
            exportSnapshotId, request.TemplateKey, request.Fields.Count,
            request.SourceSnapshotId ?? "none",
            request.SourceApplyLogId ?? "none");

        return Task.FromResult(new PdfExportSnapshotResult(
            Success: true,
            ExportSnapshotId: exportSnapshotId.ToString(),
            TemplateKey: request.TemplateKey,
            FieldCount: request.Fields.Count,
            SourceSnapshotId: request.SourceSnapshotId,
            SourceApplyLogId: request.SourceApplyLogId,
            Status: "snapshot_created",
            Note: "Export snapshot created. PDF byte generation is outside this snapshot boundary " +
                  "(snapshot_boundary only). Export record links to source intake snapshot and apply log.",
            ErrorCode: null,
            ErrorMessage: null));
    }

    /// <summary>
    /// Returns a read-only view of the in-memory snapshot log for testing.
    /// </summary>
    public IReadOnlyList<PdfExportSnapshotRecord> GetSnapshotLog() => _snapshotLog.AsReadOnly();

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static PdfExportSnapshotResult Error(string code, string message)
        => new(Success: false, ExportSnapshotId: null, TemplateKey: null,
               FieldCount: 0, SourceSnapshotId: null, SourceApplyLogId: null,
               Status: "error", Note: null, ErrorCode: code, ErrorMessage: message);
}
