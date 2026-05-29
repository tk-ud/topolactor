using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Unit tests for JsonbManifestRuntime — DB/JSONB label/value manifest resolution.
///
/// Proves:
///   - Fields are resolved from raw JSONB using jsonPath dot-notation.
///   - Display policies (as_is, format_currency, format_date, format_number, uppercase, lowercase) are applied.
///   - Missing JSONB paths resolve to null value (not error).
///   - Empty sourceJsonb returns explicit error (no silent fallback).
///   - Empty fieldManifest returns explicit error.
///   - Invalid JSON returns explicit error.
///   - Unknown aggregation policy returns explicit error.
///   - Unknown display policy returns explicit error.
///   - Field key required — missing key returns explicit error.
///
/// Does not prove:
///   - DB persistence (in-memory resolution only).
///   - DocumentCanvas rendering (frontend boundary).
///   - PDF generation (PdfExportSnapshotRuntime scope).
/// </summary>
public class JsonbManifestRuntimeTests
{
    private static JsonbManifestRuntime CreateRuntime()
        => new(NullLogger<JsonbManifestRuntime>.Instance);

    // ─── Basic resolution ────────────────────────────────────────────────────

    [Fact]
    public void ResolveFields_SimpleJsonPath_ResolvesFieldValue()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""company_name"":""株式会社テスト"",""amount"":12345}",
            [
                new JsonbFieldManifestEntry("company_name", "$.company_name", "会社名", 10, 20, "first", "as_is"),
                new JsonbFieldManifestEntry("amount", "$.amount", "金額", 100, 50, "first", "as_is"),
            ]);

        Assert.True(result.Success);
        Assert.Equal(2, result.Fields!.Count);
        Assert.Equal("株式会社テスト", result.Fields[0].Value);
        Assert.Equal("会社名", result.Fields[0].Label);
        Assert.Equal(10, result.Fields[0].X);
        Assert.Equal(20, result.Fields[0].Y);
        Assert.Equal("12345", result.Fields[1].Value);
    }

    [Fact]
    public void ResolveFields_NestedJsonPath_ResolvesNestedValue()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""customer"":{""name"":""田中太郎"",""address"":{""city"":""東京""}}}",
            [
                new JsonbFieldManifestEntry("customer_name", "$.customer.name", "顧客名", 0, 0, null, null),
                new JsonbFieldManifestEntry("city", "$.customer.address.city", "市区町村", 0, 30, null, null),
            ]);

        Assert.True(result.Success);
        Assert.Equal("田中太郎", result.Fields![0].Value);
        Assert.Equal("東京", result.Fields[1].Value);
    }

    [Fact]
    public void ResolveFields_MissingJsonPath_ResolvesToNull()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""name"":""test""}",
            [new JsonbFieldManifestEntry("missing_field", "$.not_present", "Missing", 0, 0, null, null)]);

        Assert.True(result.Success);
        Assert.Null(result.Fields![0].Value);
    }

    [Fact]
    public void ResolveFields_NullJsonPath_ResolvesRootAsString()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"""simple_string_value""",
            [new JsonbFieldManifestEntry("root", null, "Root", 0, 0, null, null)]);

        Assert.True(result.Success);
        Assert.Equal("simple_string_value", result.Fields![0].Value);
    }

    // ─── Display policy ──────────────────────────────────────────────────────

    [Fact]
    public void ResolveFields_FormatCurrencyPolicy_FormatsNumber()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""amount"":1234567}",
            [new JsonbFieldManifestEntry("amount", "$.amount", "金額", 0, 0, "first", "format_currency")]);

        Assert.True(result.Success);
        Assert.Equal("1,234,567", result.Fields![0].Value);
    }

    [Fact]
    public void ResolveFields_FormatDatePolicy_FormatsDate()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""invoice_date"":""2025-06-15T00:00:00Z""}",
            [new JsonbFieldManifestEntry("invoice_date", "$.invoice_date", "請求日", 0, 0, "first", "format_date")]);

        Assert.True(result.Success);
        Assert.Equal("2025-06-15", result.Fields![0].Value);
    }

    [Fact]
    public void ResolveFields_UppercasePolicy_Uppercases()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""code"":""abc-001""}",
            [new JsonbFieldManifestEntry("code", "$.code", "コード", 0, 0, "first", "uppercase")]);

        Assert.True(result.Success);
        Assert.Equal("ABC-001", result.Fields![0].Value);
    }

    [Fact]
    public void ResolveFields_LowercasePolicy_Lowercases()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""status"":""ACTIVE""}",
            [new JsonbFieldManifestEntry("status", "$.status", "ステータス", 0, 0, "first", "lowercase")]);

        Assert.True(result.Success);
        Assert.Equal("active", result.Fields![0].Value);
    }

    // ─── Explicit errors ─────────────────────────────────────────────────────

    [Fact]
    public void ResolveFields_EmptySourceJsonb_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            "",
            [new JsonbFieldManifestEntry("key", "$.key", null, null, null, null, null)]);

        Assert.False(result.Success);
        Assert.Equal("JSONB_MANIFEST_SOURCE_EMPTY", result.ErrorCode);
    }

    [Fact]
    public void ResolveFields_EmptyFieldManifest_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(@"{""key"":""val""}", []);

        Assert.False(result.Success);
        Assert.Equal("JSONB_MANIFEST_FIELD_MANIFEST_EMPTY", result.ErrorCode);
    }

    [Fact]
    public void ResolveFields_InvalidJson_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            "not-valid-json{",
            [new JsonbFieldManifestEntry("key", "$.key", null, null, null, null, null)]);

        Assert.False(result.Success);
        Assert.Equal("JSONB_MANIFEST_INVALID_SOURCE_JSON", result.ErrorCode);
    }

    [Fact]
    public void ResolveFields_UnknownAggregationPolicy_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""key"":""val""}",
            [new JsonbFieldManifestEntry("key", "$.key", null, null, null, "average", null)]);

        Assert.False(result.Success);
        Assert.Equal("JSONB_MANIFEST_UNSUPPORTED_AGGREGATION_POLICY", result.ErrorCode);
    }

    [Fact]
    public void ResolveFields_UnknownDisplayPolicy_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""key"":""val""}",
            [new JsonbFieldManifestEntry("key", "$.key", null, null, null, null, "bold_text")]);

        Assert.False(result.Success);
        Assert.Equal("JSONB_MANIFEST_UNSUPPORTED_DISPLAY_POLICY", result.ErrorCode);
    }

    [Fact]
    public void ResolveFields_MissingFieldKey_ReturnsExplicitError()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""key"":""val""}",
            [new JsonbFieldManifestEntry("", "$.key", null, null, null, null, null)]);

        Assert.False(result.Success);
        Assert.Equal("JSONB_MANIFEST_FIELD_KEY_REQUIRED", result.ErrorCode);
    }

    // ─── Field coordinate preservation ───────────────────────────────────────

    [Fact]
    public void ResolveFields_PreservesXYCoordinates()
    {
        var rt = CreateRuntime();
        var result = rt.ResolveFields(
            @"{""name"":""テスト""}",
            [new JsonbFieldManifestEntry("name", "$.name", "名前", X: 42.5, Y: 88.0, null, null)]);

        Assert.True(result.Success);
        Assert.Equal(42.5, result.Fields![0].X);
        Assert.Equal(88.0, result.Fields[0].Y);
    }

    // ─── Multiple fields end-to-end (DocumentCanvas ready) ───────────────────

    [Fact]
    public void ResolveFields_MultiFieldManifest_ProducesDocumentCanvasReadyFields()
    {
        // Proves: DB/JSONB → fields[key,label,x,y,value] end-to-end at unit boundary
        var rt = CreateRuntime();
        var sourceJsonb = @"{
            ""company_name"": ""株式会社サンプル"",
            ""invoice_number"": ""INV-2025-001"",
            ""total_amount"": 98765,
            ""due_date"": ""2025-07-31T00:00:00Z""
        }";

        var manifest = new List<JsonbFieldManifestEntry>
        {
            new("company_name",   "$.company_name",   "会社名",   X: 50,  Y: 30,  "first", "as_is"),
            new("invoice_number", "$.invoice_number", "請求番号", X: 50,  Y: 60,  "first", "as_is"),
            new("total_amount",   "$.total_amount",   "合計金額", X: 300, Y: 200, "first", "format_currency"),
            new("due_date",       "$.due_date",       "支払期日", X: 300, Y: 230, "first", "format_date"),
        };

        var result = rt.ResolveFields(sourceJsonb, manifest);

        Assert.True(result.Success);
        Assert.Equal(4, result.Fields!.Count);
        Assert.Equal("株式会社サンプル", result.Fields[0].Value);
        Assert.Equal("INV-2025-001",    result.Fields[1].Value);
        Assert.Equal("98,765",          result.Fields[2].Value);
        Assert.Equal("2025-07-31",      result.Fields[3].Value);

        // Verify DocumentCanvas field shape completeness (key, label, x, y, value)
        foreach (var f in result.Fields)
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Key),   "field.key must be non-empty");
            Assert.NotNull(f.Label);
            Assert.NotNull(f.X);
            Assert.NotNull(f.Y);
        }
    }
}
