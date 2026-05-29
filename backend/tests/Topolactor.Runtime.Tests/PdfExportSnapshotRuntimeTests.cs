using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Unit tests for PdfExportSnapshotRuntime — PDF export snapshot boundary.
///
/// Proves:
///   - CreateSnapshotAsync creates a snapshot record with export metadata.
///   - exportSnapshotId is a valid non-empty UUID.
///   - templateKey is preserved in the snapshot result.
///   - fieldCount reflects the number of fields in the request.
///   - sourceSnapshotId and sourceApplyLogId linkage is preserved.
///   - status is 'snapshot_created' on success.
///   - null request returns explicit error (no silent fallback).
///   - empty templateKey returns explicit error.
///   - null fields returns explicit error.
///   - field with empty key returns explicit error.
///   - invalid UUID sourceSnapshotId returns explicit error.
///   - invalid UUID sourceApplyLogId returns explicit error.
///   - Snapshot log grows by one for each successful create call.
///
/// Does not prove:
///   - PDF byte generation (out of scope — snapshot boundary only).
///   - DB persistence (in-memory MVP boundary).
///   - Production PDF layout engine.
/// </summary>
public class PdfExportSnapshotRuntimeTests
{
    private static PdfExportSnapshotRuntime CreateRuntime()
        => new(NullLogger<PdfExportSnapshotRuntime>.Instance);

    private static PdfExportSnapshotRequest ValidRequest(
        string templateKey = "invoice_v1",
        string? sourceSnapshotId = null,
        string? sourceApplyLogId = null)
        => new(
            TemplateKey: templateKey,
            BackgroundImageUrl: "https://example.com/bg.png",
            Fields: [
                new PdfExportSnapshotField("company_name", "会社名",  50, 30, "株式会社テスト"),
                new PdfExportSnapshotField("amount",       "合計金額", 300, 200, "150,000"),
            ],
            SourceSnapshotId: sourceSnapshotId,
            SourceApplyLogId: sourceApplyLogId,
            RequestedBy: "admin_user");

    // ─── Success path ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSnapshot_ValidRequest_ReturnsSuccess()
    {
        var rt = CreateRuntime();
        var result = await rt.CreateSnapshotAsync(ValidRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.ExportSnapshotId);
        Assert.True(Guid.TryParse(result.ExportSnapshotId, out _), "exportSnapshotId must be a valid UUID");
        Assert.Equal("invoice_v1", result.TemplateKey);
        Assert.Equal(2, result.FieldCount);
        Assert.Equal("snapshot_created", result.Status);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task CreateSnapshot_WithSourceLinkage_PreservesLinkIds()
    {
        var rt = CreateRuntime();
        var sourceSnapshotId = Guid.NewGuid().ToString();
        var sourceApplyLogId = Guid.NewGuid().ToString();

        var result = await rt.CreateSnapshotAsync(
            ValidRequest(sourceSnapshotId: sourceSnapshotId, sourceApplyLogId: sourceApplyLogId));

        Assert.True(result.Success);
        Assert.Equal(sourceSnapshotId, result.SourceSnapshotId);
        Assert.Equal(sourceApplyLogId, result.SourceApplyLogId);
    }

    [Fact]
    public async Task CreateSnapshot_WithoutSourceLinkage_SucceedsWithNullLinks()
    {
        var rt = CreateRuntime();
        var result = await rt.CreateSnapshotAsync(ValidRequest());

        Assert.True(result.Success);
        Assert.Null(result.SourceSnapshotId);
        Assert.Null(result.SourceApplyLogId);
    }

    [Fact]
    public async Task CreateSnapshot_SnapshotLogGrows()
    {
        var rt = CreateRuntime();
        Assert.Empty(rt.GetSnapshotLog());

        await rt.CreateSnapshotAsync(ValidRequest("tmpl_a"));
        Assert.Single(rt.GetSnapshotLog());

        await rt.CreateSnapshotAsync(ValidRequest("tmpl_b"));
        Assert.Equal(2, rt.GetSnapshotLog().Count);
    }

    [Fact]
    public async Task CreateSnapshot_EachSnapshotHasUniqueId()
    {
        var rt = CreateRuntime();
        var r1 = await rt.CreateSnapshotAsync(ValidRequest());
        var r2 = await rt.CreateSnapshotAsync(ValidRequest());

        Assert.NotEqual(r1.ExportSnapshotId, r2.ExportSnapshotId);
    }

    [Fact]
    public async Task CreateSnapshot_EmptyFields_SucceedsWithZeroFieldCount()
    {
        var rt = CreateRuntime();
        var result = await rt.CreateSnapshotAsync(new PdfExportSnapshotRequest(
            TemplateKey: "tmpl",
            BackgroundImageUrl: null,
            Fields: [],
            SourceSnapshotId: null,
            SourceApplyLogId: null,
            RequestedBy: null));

        Assert.True(result.Success);
        Assert.Equal(0, result.FieldCount);
    }

    // ─── Explicit errors ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSnapshot_NullRequest_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = await rt.CreateSnapshotAsync(null!);

        Assert.False(result.Success);
        Assert.Equal("PDF_EXPORT_REQUEST_NULL", result.ErrorCode);
    }

    [Fact]
    public async Task CreateSnapshot_EmptyTemplateKey_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = await rt.CreateSnapshotAsync(new PdfExportSnapshotRequest(
            TemplateKey: "",
            BackgroundImageUrl: null,
            Fields: [],
            SourceSnapshotId: null,
            SourceApplyLogId: null,
            RequestedBy: null));

        Assert.False(result.Success);
        Assert.Equal("PDF_EXPORT_TEMPLATE_KEY_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task CreateSnapshot_NullFields_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = await rt.CreateSnapshotAsync(new PdfExportSnapshotRequest(
            TemplateKey: "tmpl",
            BackgroundImageUrl: null,
            Fields: null!,
            SourceSnapshotId: null,
            SourceApplyLogId: null,
            RequestedBy: null));

        Assert.False(result.Success);
        Assert.Equal("PDF_EXPORT_FIELDS_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task CreateSnapshot_FieldWithEmptyKey_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = await rt.CreateSnapshotAsync(new PdfExportSnapshotRequest(
            TemplateKey: "tmpl",
            BackgroundImageUrl: null,
            Fields: [new PdfExportSnapshotField("", null, null, null, null)],
            SourceSnapshotId: null,
            SourceApplyLogId: null,
            RequestedBy: null));

        Assert.False(result.Success);
        Assert.Equal("PDF_EXPORT_FIELD_KEY_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task CreateSnapshot_InvalidSourceSnapshotId_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = await rt.CreateSnapshotAsync(new PdfExportSnapshotRequest(
            TemplateKey: "tmpl",
            BackgroundImageUrl: null,
            Fields: [],
            SourceSnapshotId: "not-a-uuid",
            SourceApplyLogId: null,
            RequestedBy: null));

        Assert.False(result.Success);
        Assert.Equal("PDF_EXPORT_INVALID_SOURCE_SNAPSHOT_ID", result.ErrorCode);
    }

    [Fact]
    public async Task CreateSnapshot_InvalidSourceApplyLogId_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = await rt.CreateSnapshotAsync(new PdfExportSnapshotRequest(
            TemplateKey: "tmpl",
            BackgroundImageUrl: null,
            Fields: [],
            SourceSnapshotId: null,
            SourceApplyLogId: "bad-id",
            RequestedBy: null));

        Assert.False(result.Success);
        Assert.Equal("PDF_EXPORT_INVALID_SOURCE_APPLY_LOG_ID", result.ErrorCode);
    }

    // ─── Snapshot boundary note ───────────────────────────────────────────────

    [Fact]
    public async Task CreateSnapshot_NoteConfirmsSnapshotBoundaryOnly()
    {
        var rt = CreateRuntime();
        var result = await rt.CreateSnapshotAsync(ValidRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Note);
        Assert.Contains("snapshot", result.Note, StringComparison.OrdinalIgnoreCase);
    }
}
