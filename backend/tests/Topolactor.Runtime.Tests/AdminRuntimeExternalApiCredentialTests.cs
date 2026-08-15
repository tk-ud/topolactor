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

    private static ExternalApiCredentialRecord SampleVaultRecord(string? referenceKey = "ref-1") =>
        new(ExternalApiCredentialRecordKinds.Vault, Guid.NewGuid(), "stripe", "billing_bundle", referenceKey,
            true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "bearer", null, 300, 1, null, null, null, null, null);

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
        Assert.Equal("EXTERNAL_API_CREDENTIAL_NOT_AVAILABLE", error!.Code);
    }

    // search moved to AdminRuntimeCredentialManagementSearchTests.cs (round 5: unified
    // credential_management:search dispatch, category-routed -- layer=external_api_credential no
    // longer has its own "search" switch case, see AdminRuntime.cs ExecuteDataAsync). That file
    // reuses FakeExternalApiCredentialAdminRepository's own SearchAsync (still implemented here,
    // since it remains part of IExternalApiCredentialAdminRepository) via its own runtime wiring.

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
