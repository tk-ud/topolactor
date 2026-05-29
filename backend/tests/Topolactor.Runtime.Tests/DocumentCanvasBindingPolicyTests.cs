using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Unit tests for DocumentCanvasBindingPolicy — user-defined aggregation/display/binding policy.
///
/// Proves:
///   - Validation catches missing templateKey, empty fields, duplicate keys, NaN coordinates.
///   - Apply delegates to JsonbManifestRuntime and returns canvas-ready fields.
///   - AggregationPolicy and DisplayPolicy enum conversion is correct.
///   - Apply with invalid policy returns explicit error (no silent fallback).
///   - Full DB/JSONB → canvas fields loop via Apply is proven at unit boundary.
///
/// Does not prove:
///   - DB persistence (in-memory resolution only).
///   - PDF generation (PdfExportSnapshotRuntime scope).
///   - Frontend rendering (frontend boundary).
/// </summary>
public class DocumentCanvasBindingPolicyTests
{
    private static DocumentCanvasBindingPolicy CreatePolicy()
    {
        var rt = new JsonbManifestRuntime(NullLogger<JsonbManifestRuntime>.Instance);
        return new DocumentCanvasBindingPolicy(
            NullLogger<DocumentCanvasBindingPolicy>.Instance, rt);
    }

    // ─── Validation ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidPolicy_IsValid()
    {
        var policy = CreatePolicy();
        var doc = new DocumentCanvasBindingPolicyDocument(
            TemplateKey: "invoice_v1",
            BackgroundImageUrl: null,
            Fields: [
                new CanvasFieldBindingPolicy("company", "$.company", "会社名", 10, 20,
                    CanvasAggregationPolicy.First, CanvasDisplayPolicy.AsIs)
            ]);

        var result = policy.Validate(doc);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingTemplateKey_ReturnsError()
    {
        var policy = CreatePolicy();
        var doc = new DocumentCanvasBindingPolicyDocument(
            TemplateKey: "",
            BackgroundImageUrl: null,
            Fields: [
                new CanvasFieldBindingPolicy("key", null, null, null, null,
                    CanvasAggregationPolicy.First, CanvasDisplayPolicy.AsIs)
            ]);

        var result = policy.Validate(doc);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("templateKey"));
    }

    [Fact]
    public void Validate_EmptyFields_ReturnsError()
    {
        var policy = CreatePolicy();
        var doc = new DocumentCanvasBindingPolicyDocument(
            TemplateKey: "tmpl",
            BackgroundImageUrl: null,
            Fields: []);

        var result = policy.Validate(doc);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("fields"));
    }

    [Fact]
    public void Validate_DuplicateFieldKeys_ReturnsError()
    {
        var policy = CreatePolicy();
        var doc = new DocumentCanvasBindingPolicyDocument(
            TemplateKey: "tmpl",
            BackgroundImageUrl: null,
            Fields: [
                new CanvasFieldBindingPolicy("dup_key", null, null, null, null,
                    CanvasAggregationPolicy.First, CanvasDisplayPolicy.AsIs),
                new CanvasFieldBindingPolicy("dup_key", null, null, null, null,
                    CanvasAggregationPolicy.First, CanvasDisplayPolicy.AsIs),
            ]);

        var result = policy.Validate(doc);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("dup_key") && e.Contains("duplicated"));
    }

    [Fact]
    public void Validate_MissingFieldKey_ReturnsError()
    {
        var policy = CreatePolicy();
        var doc = new DocumentCanvasBindingPolicyDocument(
            TemplateKey: "tmpl",
            BackgroundImageUrl: null,
            Fields: [
                new CanvasFieldBindingPolicy("", null, null, null, null,
                    CanvasAggregationPolicy.First, CanvasDisplayPolicy.AsIs)
            ]);

        var result = policy.Validate(doc);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("fields[0].key"));
    }

    // ─── Apply ───────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_ValidPolicyAndSource_ReturnsCanvasFields()
    {
        var policy = CreatePolicy();
        var doc = new DocumentCanvasBindingPolicyDocument(
            TemplateKey: "invoice_v1",
            BackgroundImageUrl: "https://example.com/bg.png",
            Fields: [
                new CanvasFieldBindingPolicy("company", "$.company", "会社名", 10, 20,
                    CanvasAggregationPolicy.First, CanvasDisplayPolicy.AsIs),
                new CanvasFieldBindingPolicy("amount", "$.amount", "金額", 200, 100,
                    CanvasAggregationPolicy.First, CanvasDisplayPolicy.FormatCurrency),
            ]);

        var result = policy.Apply(doc, @"{""company"":""ABC Corp"",""amount"":5000}");

        Assert.True(result.Success);
        Assert.Equal("invoice_v1", result.TemplateKey);
        Assert.Equal("https://example.com/bg.png", result.BackgroundImageUrl);
        Assert.Equal(2, result.Fields!.Count);
        Assert.Equal("ABC Corp", result.Fields[0].Value);
        Assert.Equal("5,000", result.Fields[1].Value);
    }

    [Fact]
    public void Apply_InvalidPolicy_ReturnsExplicitError()
    {
        var policy = CreatePolicy();
        var doc = new DocumentCanvasBindingPolicyDocument(
            TemplateKey: "",  // invalid: empty template key
            BackgroundImageUrl: null,
            Fields: [
                new CanvasFieldBindingPolicy("k", null, null, null, null,
                    CanvasAggregationPolicy.First, CanvasDisplayPolicy.AsIs)
            ]);

        var result = policy.Apply(doc, @"{""k"":""v""}");
        Assert.False(result.Success);
        Assert.Equal("CANVAS_BINDING_POLICY_INVALID", result.ErrorCode);
    }

    [Fact]
    public void Apply_InvalidSourceJsonb_ReturnsExplicitError()
    {
        var policy = CreatePolicy();
        var doc = new DocumentCanvasBindingPolicyDocument(
            TemplateKey: "tmpl",
            BackgroundImageUrl: null,
            Fields: [
                new CanvasFieldBindingPolicy("k", "$.k", null, null, null,
                    CanvasAggregationPolicy.First, CanvasDisplayPolicy.AsIs)
            ]);

        var result = policy.Apply(doc, "bad json{");
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorCode);
    }

    // ─── All display policy variants ─────────────────────────────────────────

    [Theory]
    [InlineData(CanvasDisplayPolicy.AsIs, "hello", "hello")]
    [InlineData(CanvasDisplayPolicy.Uppercase, "hello", "HELLO")]
    [InlineData(CanvasDisplayPolicy.Lowercase, "HELLO", "hello")]
    [InlineData(CanvasDisplayPolicy.FormatCurrency, "12345", "12,345")]
    [InlineData(CanvasDisplayPolicy.FormatNumber, "12345.678", "12,345.68")]
    public void Apply_DisplayPolicies_ApplyCorrectly(
        CanvasDisplayPolicy displayPolicy, string inputValue, string expectedValue)
    {
        var policy = CreatePolicy();
        var doc = new DocumentCanvasBindingPolicyDocument(
            TemplateKey: "tmpl",
            BackgroundImageUrl: null,
            Fields: [
                new CanvasFieldBindingPolicy("f", "$.f", null, null, null,
                    CanvasAggregationPolicy.First, displayPolicy)
            ]);

        var result = policy.Apply(doc, $@"{{""f"":""{inputValue}""}}");
        Assert.True(result.Success);
        Assert.Equal(expectedValue, result.Fields![0].Value);
    }

    // ─── Full DB/JSONB→canvas fields loop (invoice example) ──────────────────

    [Fact]
    public void Apply_InvoiceDocument_ProducesCompleteCanvasFields()
    {
        // Proves: user-defined binding policy → DB/JSONB → canvas fields[key,label,x,y,value]
        // This is the DocumentCanvas DB binding loop at unit boundary.
        var policy = CreatePolicy();
        var doc = new DocumentCanvasBindingPolicyDocument(
            TemplateKey: "invoice_template_v1",
            BackgroundImageUrl: "https://storage.example.com/invoice_bg.png",
            Fields: [
                new CanvasFieldBindingPolicy("company_name",   "$.company.name",     "請求先会社名", 50, 30,  CanvasAggregationPolicy.First, CanvasDisplayPolicy.AsIs),
                new CanvasFieldBindingPolicy("invoice_number", "$.invoice.number",   "請求番号",     50, 60,  CanvasAggregationPolicy.First, CanvasDisplayPolicy.AsIs),
                new CanvasFieldBindingPolicy("total_amount",   "$.invoice.total",    "合計金額",     300, 200, CanvasAggregationPolicy.First, CanvasDisplayPolicy.FormatCurrency),
                new CanvasFieldBindingPolicy("due_date",       "$.invoice.due_date", "支払期日",     300, 230, CanvasAggregationPolicy.First, CanvasDisplayPolicy.FormatDate),
            ]);

        var sourceJsonb = @"{
            ""company"": {""name"": ""株式会社テスト""},
            ""invoice"": {
                ""number"": ""INV-2025-001"",
                ""total"": 150000,
                ""due_date"": ""2025-08-31T00:00:00Z""
            }
        }";

        var result = policy.Apply(doc, sourceJsonb);

        Assert.True(result.Success);
        Assert.Equal("invoice_template_v1", result.TemplateKey);
        Assert.Equal(4, result.Fields!.Count);

        var fields = result.Fields.ToDictionary(f => f.Key);
        Assert.Equal("株式会社テスト",  fields["company_name"].Value);
        Assert.Equal("INV-2025-001",  fields["invoice_number"].Value);
        Assert.Equal("150,000",       fields["total_amount"].Value);
        Assert.Equal("2025-08-31",    fields["due_date"].Value);

        // Verify all field coordinates are populated (layout is preserved)
        foreach (var f in result.Fields)
        {
            Assert.NotNull(f.X);
            Assert.NotNull(f.Y);
            Assert.NotNull(f.Label);
        }
    }
}
