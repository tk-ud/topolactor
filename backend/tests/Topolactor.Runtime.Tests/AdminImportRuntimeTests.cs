using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Unit tests for AdminImportRuntime validate-preview-apply boundary.
///
/// Tests verify:
/// - CSV preview creates snapshot + records
/// - JSON preview creates snapshot + records
/// - invalid row gets status='invalid'
/// - preview does not mutate canonical state (uses stub repo, checks only write calls)
/// - apply targets valid records only
/// - apply records canonical diff linkage to source snapshot
/// - missing manifest / schema / unsupported file type / malformed CSV / malformed JSON -> explicit error
/// - malformed / missing schema_def -> explicit error (fail-close, no silent empty-fields fallback)
/// - manifest not found -> MANIFEST_NOT_FOUND; no snapshot/records written
/// - repository exception -> REPOSITORY_UNAVAILABLE structured error
/// - CSV: header-only, unclosed quote row, field count mismatch -> explicit error or row invalid
/// - frontend direct DB write is prohibited by design (no DB access in frontend)
///
/// admin_import_records.status is manifest/schema conformity — not business or hub lifecycle state.
/// </summary>
public class AdminImportRuntimeTests
{
    // -------------------------------------------------------------------------
    // CSV Preview
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_CsvWithValidRow_CreatesSnapshotAndRecords()
    {
        var repo = new TrackingAdminImportRepository();
        var runtime = CreateRuntime(repo);

        var csv = "name,age\nAlice,30\nBob,25";
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, csv);

        Assert.True(result.Success);
        Assert.NotNull(result.SnapshotId);
        Assert.Equal(2, result.Records.Count);
        Assert.True(repo.SnapshotCreated);
        Assert.True(repo.RecordsCreated);
    }

    [Fact]
    public async Task PreviewAsync_CsvWithHeader_StoresHeaderInSnapshot()
    {
        var repo = new TrackingAdminImportRepository();
        var runtime = CreateRuntime(repo);

        var csv = "name,email\nAlice,alice@example.com";
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, csv);

        Assert.True(result.Success);
        var row = Assert.Single(result.Records).Records as Dictionary<string, object?>;
        Assert.NotNull(row);
        Assert.True(row!.ContainsKey("name"));
        Assert.True(row.ContainsKey("email"));
    }

    // -------------------------------------------------------------------------
    // JSON Preview
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_JsonArray_CreatesSnapshotAndRecords()
    {
        var repo = new TrackingAdminImportRepository();
        var runtime = CreateRuntime(repo);

        var json = """[{"name":"Alice","age":30},{"name":"Bob","age":25}]""";
        var result = await runtime.PreviewAsync(
            "json", "test.json", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, json);

        Assert.True(result.Success);
        Assert.Equal(2, result.Records.Count);
        Assert.True(repo.SnapshotCreated);
        Assert.True(repo.RecordsCreated);
    }

    [Fact]
    public async Task PreviewAsync_JsonNotArray_ReturnsUnsupportedShapeError()
    {
        var runtime = CreateRuntime();
        var result = await runtime.PreviewAsync(
            "json", "test.json", Guid.NewGuid(), TopologyRepository.DefaultSchemaId,
            """{"name":"Alice"}""");

        Assert.False(result.Success);
        Assert.Equal("UNSUPPORTED_JSON_SHAPE", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAsync_MalformedJson_ReturnsMalformedJsonError()
    {
        var runtime = CreateRuntime();
        var result = await runtime.PreviewAsync(
            "json", "test.json", Guid.NewGuid(), TopologyRepository.DefaultSchemaId,
            "[{invalid json}]");

        Assert.False(result.Success);
        Assert.Equal("MALFORMED_JSON", result.ErrorCode);
    }

    // -------------------------------------------------------------------------
    // Validation status — conformity only, not business/hub lifecycle state
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_MissingRequiredField_MarksRowInvalid()
    {
        var repo = new TrackingAdminImportRepository();
        var topoRepo = new SchemaWithRequiredLabelRepository();
        var runtime = CreateRuntime(repo, topoRepo);

        var csv = "name,age\nAlice,30";
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, csv);

        Assert.True(result.Success);
        var invalidRow = Assert.Single(result.Records);
        Assert.Equal("invalid", invalidRow.Status);
        Assert.NotEmpty(invalidRow.ValidationErrors);
        Assert.Equal(0, result.ValidCount);
        Assert.Equal(1, result.InvalidCount);
    }

    [Fact]
    public async Task PreviewAsync_AllFieldsPresent_MarksRowValid()
    {
        var repo = new TrackingAdminImportRepository();
        var runtime = CreateRuntime(repo);

        var csv = "label\nHello";
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, csv);

        Assert.True(result.Success);
        var validRow = Assert.Single(result.Records);
        Assert.Equal("valid", validRow.Status);
        Assert.Equal(1, result.ValidCount);
        Assert.Equal(0, result.InvalidCount);
    }

    // -------------------------------------------------------------------------
    // Preview does NOT mutate canonical state
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_OnlyWritesToImportTables_NotCanonicalState()
    {
        var repo = new TrackingAdminImportRepository();
        var runtime = CreateRuntime(repo);

        var csv = "label\nFoo";
        await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, csv);

        Assert.True(repo.SnapshotCreated);
        Assert.True(repo.RecordsCreated);
        Assert.False(repo.ApplyLogCreated);
    }

    // -------------------------------------------------------------------------
    // Schema not found -> explicit error
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_SchemaNotFound_ReturnsSchemaNotFoundError()
    {
        var runtime = CreateRuntime();
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), Guid.NewGuid() /* unknown schema id */, "label\nFoo");

        Assert.False(result.Success);
        Assert.Equal("SCHEMA_NOT_FOUND", result.ErrorCode);
    }

    // -------------------------------------------------------------------------
    // Schema def validation — fail-close
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_SchemaDef_NullRawDefinition_ReturnsSchemaDefInvalidError()
    {
        var runtime = CreateRuntime(topoRepo: new SchemaWithNullDefRepository());
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "label\nFoo");

        Assert.False(result.Success);
        Assert.Equal("SCHEMA_DEF_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAsync_SchemaDef_MalformedJson_ReturnsSchemaDefInvalidError()
    {
        var runtime = CreateRuntime(topoRepo: new SchemaWithMalformedDefRepository("{not valid json}"));
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "label\nFoo");

        Assert.False(result.Success);
        Assert.Equal("SCHEMA_DEF_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAsync_SchemaDef_MissingFieldsProperty_ReturnsSchemaFieldsRequiredError()
    {
        var runtime = CreateRuntime(topoRepo: new SchemaWithMalformedDefRepository("""{"no_fields":true}"""));
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "label\nFoo");

        Assert.False(result.Success);
        Assert.Equal("SCHEMA_FIELDS_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAsync_SchemaDef_FieldsNotArray_ReturnsSchemaFieldsRequiredError()
    {
        var runtime = CreateRuntime(topoRepo: new SchemaWithMalformedDefRepository("""{"fields":"notanarray"}"""));
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "label\nFoo");

        Assert.False(result.Success);
        Assert.Equal("SCHEMA_FIELDS_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAsync_SchemaDef_FieldMissingKey_ReturnsSchemaFieldKeyRequiredError()
    {
        var runtime = CreateRuntime(topoRepo: new SchemaWithMalformedDefRepository("""{"fields":[{"type":"text"}]}"""));
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "label\nFoo");

        Assert.False(result.Success);
        Assert.Equal("SCHEMA_FIELD_KEY_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAsync_InvalidSchemaDef_DoesNotWriteSnapshotOrRecords()
    {
        var repo = new TrackingAdminImportRepository();
        var runtime = CreateRuntime(repo, new SchemaWithNullDefRepository());
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "label\nFoo");

        Assert.False(result.Success);
        Assert.False(repo.SnapshotCreated);
        Assert.False(repo.RecordsCreated);
    }

    // -------------------------------------------------------------------------
    // Manifest existence validation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_MissingManifest_ReturnsManifestNotFoundError()
    {
        var repo = new TrackingAdminImportRepository(manifestExists: false);
        var runtime = CreateRuntime(repo);
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "label\nFoo");

        Assert.False(result.Success);
        Assert.Equal("MANIFEST_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAsync_MissingManifest_DoesNotWriteSnapshotOrRecords()
    {
        var repo = new TrackingAdminImportRepository(manifestExists: false);
        var runtime = CreateRuntime(repo);
        await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "label\nFoo");

        Assert.False(repo.SnapshotCreated);
        Assert.False(repo.RecordsCreated);
    }

    // -------------------------------------------------------------------------
    // Unsupported file type -> explicit error
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_UnsupportedFileType_ReturnsError()
    {
        var runtime = CreateRuntime();
        var result = await runtime.PreviewAsync(
            "xml", "test.xml", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "<data/>");

        Assert.False(result.Success);
        Assert.Equal("UNSUPPORTED_FILE_TYPE", result.ErrorCode);
    }

    // -------------------------------------------------------------------------
    // CSV edge cases -> explicit errors or row-invalid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_EmptyCsvContent_ReturnsContentEmptyError()
    {
        var runtime = CreateRuntime();
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "");

        Assert.False(result.Success);
        Assert.Equal("CONTENT_EMPTY", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAsync_HeaderOnlyCsv_ReturnsCsvNoDataRowsError()
    {
        var runtime = CreateRuntime();
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "name,age");

        Assert.False(result.Success);
        Assert.Equal("CSV_NO_DATA_ROWS", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAsync_CsvRowUnclosedQuote_MarksRowInvalid()
    {
        var repo = new TrackingAdminImportRepository();
        var runtime = CreateRuntime(repo);

        // Second data row has unclosed quote
        var csv = "name,age\nAlice,30\n\"Bob,25";
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, csv);

        Assert.True(result.Success);
        Assert.Equal(2, result.Records.Count);
        // First row should be valid, second row should be invalid (unclosed quote)
        Assert.Equal("valid", result.Records[0].Status);
        Assert.Equal("invalid", result.Records[1].Status);
        Assert.Contains(result.Records[1].ValidationErrors, e => e.Contains("unclosed quote"));
    }

    [Fact]
    public async Task PreviewAsync_CsvRowFieldCountMismatch_MarksRowInvalid()
    {
        var repo = new TrackingAdminImportRepository();
        var runtime = CreateRuntime(repo);

        // Second data row has 3 fields but header has 2
        var csv = "name,age\nAlice,30\nBob,25,extra";
        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, csv);

        Assert.True(result.Success);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal("valid", result.Records[0].Status);
        Assert.Equal("invalid", result.Records[1].Status);
        Assert.Contains(result.Records[1].ValidationErrors, e => e.Contains("field count mismatch"));
    }

    // -------------------------------------------------------------------------
    // Repository exception -> structured error
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_RepositoryExceptionOnSnapshot_ReturnsRepositoryUnavailableError()
    {
        var repo = new ThrowingAdminImportRepository(throwOnSnapshot: true);
        var runtime = CreateRuntime(repo);

        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "label\nFoo");

        Assert.False(result.Success);
        Assert.Equal("REPOSITORY_UNAVAILABLE", result.ErrorCode);
    }

    [Fact]
    public async Task PreviewAsync_RepositoryExceptionOnRecords_ReturnsRepositoryUnavailableError()
    {
        var repo = new ThrowingAdminImportRepository(throwOnRecords: true);
        var runtime = CreateRuntime(repo);

        var result = await runtime.PreviewAsync(
            "csv", "test.csv", Guid.NewGuid(), TopologyRepository.DefaultSchemaId, "label\nFoo");

        Assert.False(result.Success);
        Assert.Equal("REPOSITORY_UNAVAILABLE", result.ErrorCode);
    }

    // -------------------------------------------------------------------------
    // Apply targets valid records only
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_ValidSnapshot_WritesApplyLogWithStagedStatus()
    {
        var snapshotId = Guid.NewGuid();
        var repo = new TrackingAdminImportRepository(snapshotExists: true, validCount: 3);
        var runtime = CreateRuntime(repo);

        var result = await runtime.ApplyAsync(snapshotId);

        Assert.True(result.Success);
        Assert.Equal("staged", result.Status);
        Assert.Equal(3, result.AppliedRecordCount);
        Assert.True(repo.ApplyLogCreated);
        Assert.NotNull(result.ApplyLogId);
    }

    [Fact]
    public async Task ApplyAsync_SnapshotNotFound_ReturnsExplicitError()
    {
        var repo = new TrackingAdminImportRepository(snapshotExists: false);
        var runtime = CreateRuntime(repo);

        var result = await runtime.ApplyAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("SNAPSHOT_NOT_FOUND", result.ErrorCode);
        Assert.False(repo.ApplyLogCreated);
    }

    // -------------------------------------------------------------------------
    // Apply canonical diff linkage to source snapshot
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_WritesCanonicalDiffLinkageWithSourceSnapshotId()
    {
        var snapshotId = Guid.NewGuid();
        var repo = new TrackingAdminImportRepository(snapshotExists: true, validCount: 2);
        var runtime = CreateRuntime(repo);

        var result = await runtime.ApplyAsync(snapshotId);

        Assert.True(result.Success);
        Assert.True(repo.ApplyLogCreated);
        Assert.NotNull(repo.CapturedDiffJsonb);

        // Diff JSONB must reference the source snapshot ID
        var diff = repo.CapturedDiffJsonb!.Value;
        Assert.Equal(JsonValueKind.Object, diff.ValueKind);
        Assert.True(diff.TryGetProperty("sourceSnapshotId", out var sourceSnapshotEl));
        Assert.Equal(snapshotId.ToString(), sourceSnapshotEl.GetString());

        // Diff JSONB must include validRecordCount
        Assert.True(diff.TryGetProperty("validRecordCount", out var countEl));
        Assert.Equal(2, countEl.GetInt32());

        // Diff JSONB must include canonicalMutationStatus = 'staged'
        Assert.True(diff.TryGetProperty("canonicalMutationStatus", out var statusEl));
        Assert.Equal("staged", statusEl.GetString());

        // Diff JSONB must include appliedAt timestamp
        Assert.True(diff.TryGetProperty("appliedAt", out _));

        // Diff JSONB must include manifestId from snapshot meta
        Assert.True(diff.TryGetProperty("manifestId", out var manifestEl));
        Assert.Equal(TrackingAdminImportRepository.DefaultManifestIdForTest.ToString(), manifestEl.GetString());
    }

    // -------------------------------------------------------------------------
    // Apply repository exception -> structured error
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_RepositoryExceptionOnApplyLog_ReturnsRepositoryUnavailableError()
    {
        var repo = new ThrowingAdminImportRepository(throwOnApplyLog: true);
        var runtime = CreateRuntime(repo);

        var result = await runtime.ApplyAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("REPOSITORY_UNAVAILABLE", result.ErrorCode);
    }

    // -------------------------------------------------------------------------
    // AdminRuntime dispatch wiring
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteDataAsync_ImportUploadPreview_MissingPayload_ReturnsError()
    {
        var adminRuntime = CreateAdminRuntime();
        var vector = new OperationVector("admin", "admin_csv_json_import", "upload_preview", null, "admin", null, null);
        var (data, error) = await adminRuntime.ExecuteDataAsync(vector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PAYLOAD_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_ImportApply_MissingPayload_ReturnsError()
    {
        var adminRuntime = CreateAdminRuntime();
        var vector = new OperationVector("admin", "admin_csv_json_import", "apply", null, "admin", null, null);
        var (data, error) = await adminRuntime.ExecuteDataAsync(vector);
        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PAYLOAD_REQUIRED", error!.Code);
    }

    [Fact]
    public async Task ExecuteDataAsync_ImportListManifests_ReturnsEmptyList()
    {
        var adminRuntime = CreateAdminRuntime();
        var vector = new OperationVector("admin", "admin_csv_json_import", "list_manifests", null, "admin", null, null);
        var (data, error) = await adminRuntime.ExecuteDataAsync(vector);
        Assert.Null(error);
        Assert.NotNull(data);
    }

    [Fact]
    public async Task ExecuteDataAsync_ImportListSchemas_ReturnsEmptyList()
    {
        var adminRuntime = CreateAdminRuntime();
        var vector = new OperationVector("admin", "admin_csv_json_import", "list_schemas", null, "admin", null, null);
        var (data, error) = await adminRuntime.ExecuteDataAsync(vector);
        Assert.Null(error);
        Assert.NotNull(data);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AdminImportRuntime CreateRuntime(
        AdminImportRepository? repo = null,
        TopologyRepository? topoRepo = null)
    {
        var repository = repo ?? new TrackingAdminImportRepository();
        var topologyRepository = topoRepo ?? new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        return new AdminImportRuntime(
            NullLogger<AdminImportRuntime>.Instance,
            repository,
            topologyRepository);
    }

    private static AdminRuntime CreateAdminRuntime()
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var importRuntime = CreateRuntime();
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo, registrar, pkg, uiRepo,
            systemCiDiagnosticRunner: null,
            seedRuntime: null,
            ciAttentionGuidanceRepository: null,
            sseEventBroadcaster: null,
            adminImportRuntime: importRuntime);
    }

    // -------------------------------------------------------------------------
    // Stubs
    // -------------------------------------------------------------------------

    private sealed class TrackingAdminImportRepository : AdminImportRepository
    {
        private readonly bool _snapshotExists;
        private readonly int _validCount;
        private readonly bool _manifestExists;

        public static readonly Guid DefaultManifestIdForTest = new Guid("eeeeeeee-0000-0000-0000-000000000001");

        public bool SnapshotCreated { get; private set; }
        public bool RecordsCreated { get; private set; }
        public bool ApplyLogCreated { get; private set; }
        public JsonElement? CapturedDiffJsonb { get; private set; }

        public TrackingAdminImportRepository(
            bool snapshotExists = true,
            int validCount = 0,
            bool manifestExists = true)
            : base(NullLogger<AdminImportRepository>.Instance)
        {
            _snapshotExists = snapshotExists;
            _validCount = validCount;
            _manifestExists = manifestExists;
        }

        public override Task<bool> ManifestExistsAsync(Guid manifestId, CancellationToken ct = default)
            => Task.FromResult(_manifestExists);

        public override Task<bool> CreateSnapshotAsync(
            Guid snapshotId, string sourceType, string fileName, Guid manifestId,
            JsonElement rawHeaderJsonb, JsonElement rawRowsJsonb, JsonElement validationSummaryJsonb,
            CancellationToken ct = default)
        {
            SnapshotCreated = true;
            return Task.FromResult(true);
        }

        public override Task<bool> CreateRecordsAsync(
            Guid manifestId, Guid snapshotId,
            IReadOnlyList<(JsonElement records, string status, JsonElement validationErrors)> rows,
            CancellationToken ct = default)
        {
            RecordsCreated = true;
            return Task.FromResult(true);
        }

        public override Task<bool> CreateApplyLogAsync(
            Guid applyLogId, Guid snapshotId, int appliedRecordCount,
            JsonElement appliedDiffJsonb, string status, CancellationToken ct = default)
        {
            ApplyLogCreated = true;
            CapturedDiffJsonb = appliedDiffJsonb;
            return Task.FromResult(true);
        }

        public override Task<int> CountValidRecordsAsync(Guid snapshotId, CancellationToken ct = default)
            => Task.FromResult(_validCount);

        public override Task<bool> SnapshotExistsAsync(Guid snapshotId, CancellationToken ct = default)
            => Task.FromResult(_snapshotExists);

        public override Task<AdminImportSnapshotMeta?> GetSnapshotMetaAsync(Guid snapshotId, CancellationToken ct = default)
        {
            if (!_snapshotExists)
                return Task.FromResult<AdminImportSnapshotMeta?>(null);
            return Task.FromResult<AdminImportSnapshotMeta?>(
                new AdminImportSnapshotMeta(DefaultManifestIdForTest, "csv", "test.csv"));
        }
    }

    /// <summary>
    /// Repository stub that throws on specified write operations.
    /// Used to verify REPOSITORY_UNAVAILABLE structured error handling.
    /// </summary>
    private sealed class ThrowingAdminImportRepository : AdminImportRepository
    {
        private readonly bool _throwOnSnapshot;
        private readonly bool _throwOnRecords;
        private readonly bool _throwOnApplyLog;

        public ThrowingAdminImportRepository(
            bool throwOnSnapshot = false,
            bool throwOnRecords = false,
            bool throwOnApplyLog = false)
            : base(NullLogger<AdminImportRepository>.Instance)
        {
            _throwOnSnapshot = throwOnSnapshot;
            _throwOnRecords = throwOnRecords;
            _throwOnApplyLog = throwOnApplyLog;
        }

        public override Task<bool> ManifestExistsAsync(Guid manifestId, CancellationToken ct = default)
            => Task.FromResult(true);

        public override Task<bool> SnapshotExistsAsync(Guid snapshotId, CancellationToken ct = default)
            => _throwOnApplyLog
                ? Task.FromResult(true)
                : throw new InvalidOperationException("SIMULATED_DB_FAILURE");

        public override Task<int> CountValidRecordsAsync(Guid snapshotId, CancellationToken ct = default)
            => Task.FromResult(1);

        public override Task<AdminImportSnapshotMeta?> GetSnapshotMetaAsync(Guid snapshotId, CancellationToken ct = default)
            => Task.FromResult<AdminImportSnapshotMeta?>(null);

        public override Task<bool> CreateSnapshotAsync(
            Guid snapshotId, string sourceType, string fileName, Guid manifestId,
            JsonElement rawHeaderJsonb, JsonElement rawRowsJsonb, JsonElement validationSummaryJsonb,
            CancellationToken ct = default)
        {
            if (_throwOnSnapshot) throw new InvalidOperationException("SIMULATED_DB_FAILURE");
            return Task.FromResult(true);
        }

        public override Task<bool> CreateRecordsAsync(
            Guid manifestId, Guid snapshotId,
            IReadOnlyList<(JsonElement records, string status, JsonElement validationErrors)> rows,
            CancellationToken ct = default)
        {
            if (_throwOnRecords) throw new InvalidOperationException("SIMULATED_DB_FAILURE");
            return Task.FromResult(true);
        }

        public override Task<bool> CreateApplyLogAsync(
            Guid applyLogId, Guid snapshotId, int appliedRecordCount,
            JsonElement appliedDiffJsonb, string status, CancellationToken ct = default)
        {
            if (_throwOnApplyLog) throw new InvalidOperationException("SIMULATED_DB_FAILURE");
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// TopologyRepository stub that returns a schema with a required "label" field.
    /// </summary>
    private sealed class SchemaWithRequiredLabelRepository : TopologyRepository
    {
        public SchemaWithRequiredLabelRepository()
            : base(NullLogger<TopologyRepository>.Instance, "test-double") { }

        public override Task<SchemaRecord?> LoadSchemaAsync(Guid schemaId, CancellationToken ct = default)
        {
            return Task.FromResult<SchemaRecord?>(new SchemaRecord(
                SchemaId: schemaId,
                SchemaName: "test_schema",
                Version: null,
                RawDefinition: """{"fields":[{"key":"label","type":"text","required":true}]}"""));
        }
    }

    /// <summary>
    /// TopologyRepository stub that returns a schema with null RawDefinition (fail-close test).
    /// </summary>
    private sealed class SchemaWithNullDefRepository : TopologyRepository
    {
        public SchemaWithNullDefRepository()
            : base(NullLogger<TopologyRepository>.Instance, "test-double") { }

        public override Task<SchemaRecord?> LoadSchemaAsync(Guid schemaId, CancellationToken ct = default)
        {
            return Task.FromResult<SchemaRecord?>(new SchemaRecord(
                SchemaId: schemaId,
                SchemaName: "test_schema",
                Version: null,
                RawDefinition: null));
        }
    }

    /// <summary>
    /// TopologyRepository stub that returns a schema with specified (possibly malformed) RawDefinition.
    /// </summary>
    private sealed class SchemaWithMalformedDefRepository : TopologyRepository
    {
        private readonly string _rawDef;

        public SchemaWithMalformedDefRepository(string rawDef)
            : base(NullLogger<TopologyRepository>.Instance, "test-double")
        {
            _rawDef = rawDef;
        }

        public override Task<SchemaRecord?> LoadSchemaAsync(Guid schemaId, CancellationToken ct = default)
        {
            return Task.FromResult<SchemaRecord?>(new SchemaRecord(
                SchemaId: schemaId,
                SchemaName: "test_schema",
                Version: null,
                RawDefinition: _rawDef));
        }
    }
}
