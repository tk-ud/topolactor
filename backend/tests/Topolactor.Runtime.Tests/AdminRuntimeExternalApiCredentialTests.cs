using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for AdminRuntime layer=external_api_credential (search/get/create/update/delete).
/// SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
/// credentials.categories.external_api_credential; docs/design/external-port-substrate-ssot.yaml
/// admin_setting_projection.
///
/// Verifies:
///   - search/get/create/update/delete reach the repository authority boundary via a real
///     recordKind-driven fake (not per-recordKind branching in the test either).
///   - dryRun runs REAL validation (invalid candidates never report valid=true unchecked).
///   - create/update/delete require payload.confirmed=true before writing.
///   - secret material (plaintextSecret/newPlaintextSecret/encryptionKeyReference) never appears
///     in any response projection, preview, or audit metadata this file builds.
///   - unconfigured repository fails closed with EXTERNAL_API_CREDENTIAL_NOT_AVAILABLE.
///   - unknown recordKind / missing required fields fail closed without ever calling into the
///     repository's write path.
/// </summary>
public class AdminRuntimeExternalApiCredentialTests
{
    private static AdminRuntime CreateRuntime(IExternalApiCredentialAdminRepository? repo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            externalApiCredentialAdminRepository: repo);
    }

    private static OperationVector Vector(string action, object payload) =>
        new("admin", "external_api_credential", action, null, "admin", JsonSerializer.SerializeToElement(payload), null);

    private sealed class FakeExternalApiCredentialAdminRepository : IExternalApiCredentialAdminRepository
    {
        public List<ExternalApiCredentialRecord> Rows { get; } = new();
        public List<ExternalApiCredentialCreateRequest> Created { get; } = new();
        public List<ExternalApiCredentialUpdateRequest> Updated { get; } = new();
        public List<(string RecordKind, Guid RecordId)> Deactivated { get; } = new();

        public Task<IReadOnlyList<ExternalApiCredentialRecord>> SearchAsync(
            string? query, string? recordKind, string? providerKind, string? requiredByBundle, bool? active,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalApiCredentialRecord>>(Rows);

        public Task<ExternalApiCredentialRecord?> GetAsync(string recordKind, Guid recordId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.RecordKind == recordKind && r.RecordId == recordId));

        public ExternalApiCredentialWriteResult ValidateCreateRequest(ExternalApiCredentialCreateRequest request)
        {
            if (!ExternalApiCredentialRecordKinds.All.Contains(request.RecordKind))
                return new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.RecordKindInvalid, Detail: $"Unknown recordKind '{request.RecordKind}'.");
            if (string.IsNullOrWhiteSpace(request.ProviderKind) || string.IsNullOrWhiteSpace(request.RequiredByBundle))
                return new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.RequiredFieldMissing, Detail: "providerKind and requiredByBundle are required.");
            return new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.Ok);
        }

        public Task<ExternalApiCredentialWriteResult> CreateAsync(ExternalApiCredentialCreateRequest request, CancellationToken ct = default)
        {
            var validation = ValidateCreateRequest(request);
            if (validation.Outcome != ExternalApiCredentialOutcome.Ok) return Task.FromResult(validation);
            Created.Add(request);
            var record = new ExternalApiCredentialRecord(
                request.RecordKind, Guid.NewGuid(), request.ProviderKind, request.RequiredByBundle, request.ReferenceKey,
                true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, request.TokenKind, null, request.RefreshBeforeSeconds,
                1, request.UrlOrEnvReference, request.CredentialKind, request.HookPath, request.HeaderKey, request.RouteKey);
            Rows.Add(record);
            return Task.FromResult(new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.Ok, record));
        }

        public Task<ExternalApiCredentialWriteResult> ValidateUpdateRequestAsync(ExternalApiCredentialUpdateRequest request, CancellationToken ct = default)
        {
            var existing = Rows.FirstOrDefault(r => r.RecordKind == request.RecordKind && r.RecordId == request.RecordId);
            if (existing is null)
                return Task.FromResult(new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.NotFound, Detail: "not found"));
            return Task.FromResult(new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.Ok, existing));
        }

        public async Task<ExternalApiCredentialWriteResult> UpdateAsync(ExternalApiCredentialUpdateRequest request, CancellationToken ct = default)
        {
            var validation = await ValidateUpdateRequestAsync(request, ct);
            if (validation.Outcome != ExternalApiCredentialOutcome.Ok) return validation;
            Updated.Add(request);
            var existing = validation.Record!;
            var updated = existing with
            {
                ProviderKind = request.ProviderKind ?? existing.ProviderKind,
                RequiredByBundle = request.RequiredByBundle ?? existing.RequiredByBundle,
                ReferenceKey = request.ReferenceKey ?? existing.ReferenceKey,
                Active = request.Active ?? existing.Active,
            };
            Rows.Remove(existing);
            Rows.Add(updated);
            return new ExternalApiCredentialWriteResult(ExternalApiCredentialOutcome.Ok, updated);
        }

        public Task<ExternalApiCredentialOutcome> DeactivateAsync(string recordKind, Guid recordId, CancellationToken ct = default)
        {
            var existing = Rows.FirstOrDefault(r => r.RecordKind == recordKind && r.RecordId == recordId);
            if (existing is null) return Task.FromResult(ExternalApiCredentialOutcome.NotFound);
            Deactivated.Add((recordKind, recordId));
            Rows.Remove(existing);
            Rows.Add(existing with { Active = false });
            return Task.FromResult(ExternalApiCredentialOutcome.Ok);
        }
    }

    private static ExternalApiCredentialRecord SampleVaultRecord(string? referenceKey = "ref-1") =>
        new(ExternalApiCredentialRecordKinds.Vault, Guid.NewGuid(), "stripe", "billing_bundle", referenceKey,
            true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "bearer", null, 300, 1, null, null, null, null, null);

    // --- unconfigured repository fails closed ---

    [Theory]
    [InlineData("search")]
    [InlineData("get")]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task ExecuteDataAsync_RepositoryNotConfigured_FailsClosedWithNotAvailable(string action)
    {
        var runtime = CreateRuntime(null);
        var (data, error) = await runtime.ExecuteDataAsync(Vector(action, new { }), CancellationToken.None);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("EXTERNAL_API_CREDENTIAL_NOT_AVAILABLE", error!.Code);
    }

    // --- search ---

    [Fact]
    public async Task Search_ReturnsProjectionWithoutSecretFields()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        repo.Rows.Add(SampleVaultRecord());
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("search", new { }), CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(data);
        var json = data!.Value.GetRawText();
        Assert.Contains("\"referenceKey\":\"ref-1\"", json);
        foreach (var forbidden in new[] { "tokenHash", "encryptedPayload", "encryptionKeyReference", "plaintextSecret", "decryptedPayload", "tokenResponse" })
            Assert.DoesNotContain(forbidden, json);
    }

    [Fact]
    public async Task Search_UnknownRecordKind_FailsClosed()
    {
        var runtime = CreateRuntime(new FakeExternalApiCredentialAdminRepository());
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("search", new { recordKind = "not_a_real_kind" }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_API_CREDENTIAL_RECORD_KIND_INVALID", error!.Code);
    }

    // --- create ---

    [Fact]
    public async Task Create_DryRun_ValidatesForReal_NeverReportsValidForBadCandidate()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var runtime = CreateRuntime(repo);

        // Missing providerKind/requiredByBundle -- must fail validation even under dryRun, not
        // report valid=true unchecked (mirrors the Round 2 dryRun-must-validate finding).
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("create", new { recordKind = "vault", dryRun = true }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_API_CREDENTIAL_REQUIRED_FIELD_MISSING", error!.Code);
        Assert.Empty(repo.Created);
    }

    [Fact]
    public async Task Create_DryRun_ValidCandidate_PreviewsWithoutWriting()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("create", new
            {
                recordKind = "vault",
                providerKind = "stripe",
                requiredByBundle = "billing_bundle",
                plaintextSecret = "super-secret-value",
                dryRun = true,
            }), CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Empty(repo.Created);
        var json = data!.Value.GetRawText();
        Assert.Contains("\"dryRun\":true", json);
        Assert.DoesNotContain("super-secret-value", json);
        Assert.DoesNotContain("plaintextSecret", json);
    }

    [Fact]
    public async Task Create_WithoutConfirmed_FailsClosed_NeverWrites()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("create", new { recordKind = "vault", providerKind = "stripe", requiredByBundle = "billing_bundle" }),
            CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_API_CREDENTIAL_WRITE_NOT_CONFIRMED", error!.Code);
        Assert.Empty(repo.Created);
    }

    [Fact]
    public async Task Create_Confirmed_Writes_And_ResponseNeverIncludesSecret()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("create", new
            {
                recordKind = "vault",
                providerKind = "stripe",
                requiredByBundle = "billing_bundle",
                plaintextSecret = "super-secret-value",
                encryptionKeyReference = "env:TEST_KEY",
                confirmed = true,
            }), CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Single(repo.Created);
        var json = data!.Value.GetRawText();
        Assert.DoesNotContain("super-secret-value", json);
        Assert.DoesNotContain("env:TEST_KEY", json);
        Assert.DoesNotContain("plaintextSecret", json);
        Assert.DoesNotContain("encryptionKeyReference", json);
    }

    // --- update ---

    [Fact]
    public async Task Update_UnknownRecordId_FailsClosed_NeverWrites()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("update", new { recordKind = "vault", recordId = Guid.NewGuid().ToString(), confirmed = true }),
            CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_API_CREDENTIAL_NOT_FOUND", error!.Code);
        Assert.Empty(repo.Updated);
    }

    [Fact]
    public async Task Update_DryRun_DoesNotWrite_ConfirmedWrites()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var record = SampleVaultRecord();
        repo.Rows.Add(record);
        var runtime = CreateRuntime(repo);

        var (previewData, previewError) = await runtime.ExecuteDataAsync(
            Vector("update", new { recordKind = "vault", recordId = record.RecordId.ToString(), referenceKey = "ref-2", dryRun = true }),
            CancellationToken.None);
        Assert.Null(previewError);
        Assert.NotNull(previewData);
        Assert.Empty(repo.Updated);

        var (confirmData, confirmError) = await runtime.ExecuteDataAsync(
            Vector("update", new { recordKind = "vault", recordId = record.RecordId.ToString(), referenceKey = "ref-2", confirmed = true }),
            CancellationToken.None);
        Assert.Null(confirmError);
        Assert.NotNull(confirmData);
        Assert.Single(repo.Updated);
    }

    [Fact]
    public async Task Update_SecretRotation_NeverEchoesNewSecretInResponse()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var record = SampleVaultRecord();
        repo.Rows.Add(record);
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("update", new
            {
                recordKind = "vault",
                recordId = record.RecordId.ToString(),
                newPlaintextSecret = "rotated-secret-value",
                encryptionKeyReference = "env:TEST_KEY",
                confirmed = true,
            }), CancellationToken.None);

        Assert.Null(error);
        var json = data!.Value.GetRawText();
        Assert.DoesNotContain("rotated-secret-value", json);
        Assert.DoesNotContain("newPlaintextSecret", json);
        Assert.DoesNotContain("env:TEST_KEY", json);
    }

    // --- delete ---

    [Fact]
    public async Task Delete_UnknownRecord_FailsClosed()
    {
        var runtime = CreateRuntime(new FakeExternalApiCredentialAdminRepository());
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("delete", new { recordKind = "vault", recordId = Guid.NewGuid().ToString(), confirmed = true }),
            CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_API_CREDENTIAL_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task Delete_DryRun_DoesNotDeactivate_ConfirmedDeactivates()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var record = SampleVaultRecord();
        repo.Rows.Add(record);
        var runtime = CreateRuntime(repo);

        var (previewData, previewError) = await runtime.ExecuteDataAsync(
            Vector("delete", new { recordKind = "vault", recordId = record.RecordId.ToString(), dryRun = true }),
            CancellationToken.None);
        Assert.Null(previewError);
        Assert.NotNull(previewData);
        Assert.Empty(repo.Deactivated);

        var (confirmData, confirmError) = await runtime.ExecuteDataAsync(
            Vector("delete", new { recordKind = "vault", recordId = record.RecordId.ToString(), confirmed = true }),
            CancellationToken.None);
        Assert.Null(confirmError);
        Assert.NotNull(confirmData);
        Assert.Single(repo.Deactivated);
    }

    [Fact]
    public async Task Delete_WithoutConfirmed_FailsClosed_NeverDeactivates()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var record = SampleVaultRecord();
        repo.Rows.Add(record);
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("delete", new { recordKind = "vault", recordId = record.RecordId.ToString() }),
            CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_API_CREDENTIAL_WRITE_NOT_CONFIRMED", error!.Code);
        Assert.Empty(repo.Deactivated);
    }
}
