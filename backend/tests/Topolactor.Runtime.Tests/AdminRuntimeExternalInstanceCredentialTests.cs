using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for AdminRuntime layer=external_instance_credential (get/create/update/delete).
/// SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.
/// credentials.categories.external_instance_credential; docs/design/instance-port-substrate-ssot.yaml
/// existing_credential_management_projection_extension. search is unified onto
/// AdminRuntimeCredentialManagementSearchTests.cs.
///
/// Verifies:
///   - dryRun runs REAL validation, including the reference_key-exists-in-vault precondition
///     (never reports valid=true for an unknown referenceKey unchecked).
///   - create/update/delete require payload.confirmed=true before writing.
///   - secret material never appears in any response projection (this category never even
///     collects a plaintext secret -- create/update only accept a referenceKey pointing at an
///     EXISTING vault row).
///   - unconfigured repository fails closed with EXTERNAL_INSTANCE_CREDENTIAL_NOT_AVAILABLE.
///   - unknown recordKind / unknown referenceKey fail closed without ever calling into the
///     repository's write path.
/// </summary>
public class AdminRuntimeExternalInstanceCredentialTests
{
    private static AdminRuntime CreateRuntime(IExternalInstanceCredentialAdminRepository? repo)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            externalInstanceCredentialAdminRepository: repo);
    }

    private static OperationVector Vector(string action, object payload) =>
        new("admin", "external_instance_credential", action, null, "admin", JsonSerializer.SerializeToElement(payload), null);

    private static ExternalInstanceCredentialRecord SampleRecord(
        string referenceKey = "ref-1", string recordKind = ExternalInstanceCredentialRecordKinds.DbInstancePort) =>
        new(recordKind, Guid.NewGuid(), "authority-1", "postgres", "core_bundle", referenceKey,
            true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, 1);

    // --- unconfigured repository fails closed ---

    [Theory]
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
        Assert.Equal("EXTERNAL_INSTANCE_CREDENTIAL_NOT_AVAILABLE", error!.Code);
    }

    // --- create ---

    [Fact]
    public async Task Create_DryRun_UnknownReferenceKey_FailsClosed_NeverReportsValidUnchecked()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("create", new
            {
                recordKind = "db_instance_port", instanceAuthorityKey = "authority-x",
                providerKind = "postgres", requiredByBundle = "core_bundle",
                referenceKey = "not-a-real-reference-key", dryRun = true,
            }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_INSTANCE_CREDENTIAL_REFERENCE_KEY_NOT_FOUND", error!.Code);
        Assert.Empty(repo.Created);
    }

    [Fact]
    public async Task Create_DryRun_UnknownRecordKind_FailsClosed()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("create", new
            {
                recordKind = "not_a_real_kind", instanceAuthorityKey = "authority-x",
                providerKind = "postgres", requiredByBundle = "core_bundle",
                referenceKey = "ref-1", dryRun = true,
            }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_INSTANCE_CREDENTIAL_RECORD_KIND_INVALID", error!.Code);
    }

    [Fact]
    public async Task Create_DryRun_ValidCandidate_PreviewsWithoutWriting()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        repo.ExistingVaultReferenceKeys.Add("ref-1");
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("create", new
            {
                recordKind = "db_instance_port", instanceAuthorityKey = "authority-x",
                providerKind = "postgres", requiredByBundle = "core_bundle",
                referenceKey = "ref-1", dryRun = true,
            }), CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Empty(repo.Created);
        var json = data!.Value.GetRawText();
        Assert.Contains("\"dryRun\":true", json);
    }

    [Fact]
    public async Task Create_WithoutConfirmed_FailsClosed_NeverWrites()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        repo.ExistingVaultReferenceKeys.Add("ref-1");
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("create", new
            {
                recordKind = "db_instance_port", instanceAuthorityKey = "authority-x",
                providerKind = "postgres", requiredByBundle = "core_bundle", referenceKey = "ref-1",
            }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_INSTANCE_CREDENTIAL_WRITE_NOT_CONFIRMED", error!.Code);
        Assert.Empty(repo.Created);
    }

    [Fact]
    public async Task Create_Confirmed_Writes_And_ResponseNeverIncludesSecretFields()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        repo.ExistingVaultReferenceKeys.Add("ref-1");
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("create", new
            {
                recordKind = "db_instance_port", instanceAuthorityKey = "authority-x",
                providerKind = "postgres", requiredByBundle = "core_bundle",
                referenceKey = "ref-1", confirmed = true,
            }), CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Single(repo.Created);
        var json = data!.Value.GetRawText();
        foreach (var forbidden in new[] { "tokenHash", "encryptedPayload", "encryptionKeyReference", "plaintextSecret", "decryptedPayload", "connectionSecret" })
            Assert.DoesNotContain(forbidden, json);
    }

    // --- update ---

    [Fact]
    public async Task Update_UnknownRecordId_FailsClosed_NeverWrites()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("update", new { recordKind = "db_instance_port", recordId = Guid.NewGuid().ToString(), confirmed = true }),
            CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_INSTANCE_CREDENTIAL_NOT_FOUND", error!.Code);
        Assert.Empty(repo.Updated);
    }

    [Fact]
    public async Task Update_UnknownReferenceKey_FailsClosed_NeverWrites()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        var record = SampleRecord();
        repo.Rows.Add(record);
        repo.ExistingVaultReferenceKeys.Add("ref-1");
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("update", new
            {
                recordKind = "db_instance_port", recordId = record.RecordId.ToString(),
                referenceKey = "not-a-real-reference-key", confirmed = true,
            }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_INSTANCE_CREDENTIAL_REFERENCE_KEY_NOT_FOUND", error!.Code);
        Assert.Empty(repo.Updated);
    }

    [Fact]
    public async Task Update_DryRun_DoesNotWrite_ConfirmedWrites()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        var record = SampleRecord();
        repo.Rows.Add(record);
        repo.ExistingVaultReferenceKeys.Add("ref-2");
        var runtime = CreateRuntime(repo);

        var (previewData, previewError) = await runtime.ExecuteDataAsync(
            Vector("update", new { recordKind = "db_instance_port", recordId = record.RecordId.ToString(), referenceKey = "ref-2", dryRun = true }),
            CancellationToken.None);
        Assert.Null(previewError);
        Assert.NotNull(previewData);
        Assert.Empty(repo.Updated);

        var (confirmData, confirmError) = await runtime.ExecuteDataAsync(
            Vector("update", new { recordKind = "db_instance_port", recordId = record.RecordId.ToString(), referenceKey = "ref-2", confirmed = true }),
            CancellationToken.None);
        Assert.Null(confirmError);
        Assert.NotNull(confirmData);
        Assert.Single(repo.Updated);
    }

    // --- delete ---

    [Fact]
    public async Task Delete_UnknownRecord_FailsClosed()
    {
        var runtime = CreateRuntime(new FakeExternalInstanceCredentialAdminRepository());
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("delete", new { recordKind = "db_instance_port", recordId = Guid.NewGuid().ToString(), confirmed = true }),
            CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_INSTANCE_CREDENTIAL_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task Delete_DryRun_DoesNotDeactivate_ConfirmedDeactivates()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        var record = SampleRecord();
        repo.Rows.Add(record);
        var runtime = CreateRuntime(repo);

        var (previewData, previewError) = await runtime.ExecuteDataAsync(
            Vector("delete", new { recordKind = "db_instance_port", recordId = record.RecordId.ToString(), dryRun = true }),
            CancellationToken.None);
        Assert.Null(previewError);
        Assert.NotNull(previewData);
        Assert.Empty(repo.Deactivated);

        var (confirmData, confirmError) = await runtime.ExecuteDataAsync(
            Vector("delete", new { recordKind = "db_instance_port", recordId = record.RecordId.ToString(), confirmed = true }),
            CancellationToken.None);
        Assert.Null(confirmError);
        Assert.NotNull(confirmData);
        Assert.Single(repo.Deactivated);
    }

    [Fact]
    public async Task Delete_WithoutConfirmed_FailsClosed_NeverDeactivates()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        var record = SampleRecord();
        repo.Rows.Add(record);
        var runtime = CreateRuntime(repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("delete", new { recordKind = "db_instance_port", recordId = record.RecordId.ToString() }),
            CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_INSTANCE_CREDENTIAL_WRITE_NOT_CONFIRMED", error!.Code);
        Assert.Empty(repo.Deactivated);
    }
}
