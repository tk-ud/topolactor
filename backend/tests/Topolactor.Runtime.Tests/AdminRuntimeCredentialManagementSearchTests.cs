using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for AdminRuntime layer=credential_management action=search -- the unified,
/// category-routed search action (docs/design/admin-normal-surface-projection-seed-ssot.yaml
/// surface_axes.admin.surfaces.credentials.seed_contract.component_tree: credential_search/
/// credential_category_filter/credential_result_list are shared across all three categories).
///
/// Round 5: this action replaces the old per-category "external_api_credential:search" (see
/// AdminRuntimeExternalApiCredentialTests.cs, which now only covers get/create/update/delete) so
/// manifest 092's single dispatcher_mapping slot can give ALL THREE categories the same
/// same-manifest Emission-adoption live result-list binding, not just one.
///
/// Verifies:
///   - category is required and validated (CREDENTIAL_MANAGEMENT_CATEGORY_INVALID).
///   - each category reaches its OWN existing/canonical read authority (AuthMasterRepository /
///     IExternalApiCredentialAdminRepository / IExternalInstanceCredentialAdminRepository) -- never
///     a parallel implementation.
///   - secret/hash/token material never appears in any category's response.
///   - expires_at boundary filtering (strict &lt;/&gt;, excludes null-expiry rows) holds for both
///     credential categories, reusing the exact Round 4 boundary semantics now reached through the
///     unified dispatch path.
///   - unconfigured repository fails closed per category, distinctly from an invalid category.
/// </summary>
public class AdminRuntimeCredentialManagementSearchTests
{
    private static AdminRuntime CreateRuntime(
        AuthMasterRepository? authMasterRepository = null,
        IExternalApiCredentialAdminRepository? externalApiCredentialAdminRepository = null,
        IExternalInstanceCredentialAdminRepository? externalInstanceCredentialAdminRepository = null)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            authMasterRepository: authMasterRepository,
            externalApiCredentialAdminRepository: externalApiCredentialAdminRepository,
            externalInstanceCredentialAdminRepository: externalInstanceCredentialAdminRepository);
    }

    private static OperationVector Vector(object payload) =>
        new("admin", "credential_management", "search", null, "admin", JsonSerializer.SerializeToElement(payload), null);

    // --- category validation ---

    [Fact]
    public async Task Search_MissingCategory_FailsClosed()
    {
        var runtime = CreateRuntime();
        var (data, error) = await runtime.ExecuteDataAsync(Vector(new { }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("CREDENTIAL_MANAGEMENT_CATEGORY_INVALID", error!.Code);
    }

    [Fact]
    public async Task Search_UnknownCategory_FailsClosed()
    {
        var runtime = CreateRuntime();
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "not_a_real_category" }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("CREDENTIAL_MANAGEMENT_CATEGORY_INVALID", error!.Code);
    }

    // --- users category: reuses the SAME canonical AuthMasterRepository.ListUsersAsync auth_users:
    // list/search already call -- never a parallel account search implementation. ---

    [Fact]
    public async Task Search_UsersCategory_ReturnsRosterWithoutSecretFields()
    {
        var seedUser = new AuthUserRosterDto(
            Guid.NewGuid(), "roster_search_user", true, true, "active",
            null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "user");
        var runtime = CreateRuntime(authMasterRepository: new InMemoryAuthMasterRepository([seedUser]));

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "users" }), CancellationToken.None);

        Assert.Null(error);
        var json = data!.Value.GetRawText();
        Assert.Equal("users", data.Value.GetProperty("category").GetString());
        Assert.Contains("roster_search_user", json);
        foreach (var forbidden in new[] { "passwordHash", "password_hash", "tokenHash", "refreshToken", "plaintextSecret", "secretValue" })
            Assert.DoesNotContain(forbidden, json);
    }

    [Fact]
    public async Task Search_UsersCategory_StatusFilter_Narrows()
    {
        var active = new AuthUserRosterDto(
            Guid.NewGuid(), "status_filter_active", true, true, "active",
            null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "user");
        var suspended = new AuthUserRosterDto(
            Guid.NewGuid(), "status_filter_suspended", true, true, "suspended",
            null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "user");
        var runtime = CreateRuntime(authMasterRepository: new InMemoryAuthMasterRepository([active, suspended]));

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "users", status = "active" }), CancellationToken.None);

        Assert.Null(error);
        var json = data!.Value.GetRawText();
        Assert.Contains("status_filter_active", json);
        Assert.DoesNotContain("status_filter_suspended", json);
    }

    [Fact]
    public async Task Search_UsersCategory_RepositoryNotConfigured_FailsClosed()
    {
        var runtime = CreateRuntime();
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "users" }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("AUTH_USERS_NOT_AVAILABLE", error!.Code);
    }

    // --- external_api_credential category: unchanged Round 3/4 repository authority, now reached
    // via the unified dispatch action instead of its own "external_api_credential:search". ---

    private static ExternalApiCredentialRecord SampleVaultRecord(string? referenceKey, DateTimeOffset? expiresAt = null) =>
        new(ExternalApiCredentialRecordKinds.Vault, Guid.NewGuid(), "stripe", "billing_bundle", referenceKey,
            true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "bearer", expiresAt, 300, 1, null, null, null, null, null);

    [Fact]
    public async Task Search_ExternalApiCredentialCategory_ReturnsProjectionWithoutSecretFields()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        repo.Rows.Add(SampleVaultRecord("ref-1"));
        var runtime = CreateRuntime(externalApiCredentialAdminRepository: repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_api_credential" }), CancellationToken.None);

        Assert.Null(error);
        var json = data!.Value.GetRawText();
        Assert.Equal("external_api_credential", data.Value.GetProperty("category").GetString());
        Assert.Contains("\"referenceKey\":\"ref-1\"", json);
        foreach (var forbidden in new[] { "tokenHash", "encryptedPayload", "encryptionKeyReference", "plaintextSecret", "decryptedPayload", "tokenResponse" })
            Assert.DoesNotContain(forbidden, json);
    }

    [Fact]
    public async Task Search_ExternalApiCredentialCategory_UnknownRecordKind_FailsClosed()
    {
        var runtime = CreateRuntime(externalApiCredentialAdminRepository: new FakeExternalApiCredentialAdminRepository());
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_api_credential", recordKind = "not_a_real_kind" }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_API_CREDENTIAL_RECORD_KIND_INVALID", error!.Code);
    }

    [Fact]
    public async Task Search_ExternalApiCredentialCategory_ExpiresBefore_ExcludesAtOrAfterBoundary_IncludesStrictlyEarlier()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var boundary = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        repo.Rows.Add(SampleVaultRecord("expires-before-boundary", boundary.AddDays(-1)));
        repo.Rows.Add(SampleVaultRecord("expires-at-boundary", boundary));
        repo.Rows.Add(SampleVaultRecord("expires-after-boundary", boundary.AddDays(1)));
        var runtime = CreateRuntime(externalApiCredentialAdminRepository: repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_api_credential", expiresBefore = boundary }), CancellationToken.None);

        Assert.Null(error);
        var json = data!.Value.GetRawText();
        Assert.Contains("expires-before-boundary", json);
        Assert.DoesNotContain("expires-at-boundary", json);
        Assert.DoesNotContain("expires-after-boundary", json);
    }

    [Fact]
    public async Task Search_ExternalApiCredentialCategory_ExpiresAfter_ExcludesAtOrBeforeBoundary_IncludesStrictlyLater()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        var boundary = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        repo.Rows.Add(SampleVaultRecord("expires-before-boundary", boundary.AddDays(-1)));
        repo.Rows.Add(SampleVaultRecord("expires-at-boundary", boundary));
        repo.Rows.Add(SampleVaultRecord("expires-after-boundary", boundary.AddDays(1)));
        var runtime = CreateRuntime(externalApiCredentialAdminRepository: repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_api_credential", expiresAfter = boundary }), CancellationToken.None);

        Assert.Null(error);
        var json = data!.Value.GetRawText();
        Assert.DoesNotContain("expires-before-boundary", json);
        Assert.DoesNotContain("expires-at-boundary", json);
        Assert.Contains("expires-after-boundary", json);
    }

    [Fact]
    public async Task Search_ExternalApiCredentialCategory_ExpiresFilter_ExcludesRecordsWithNoExpiresAt()
    {
        var repo = new FakeExternalApiCredentialAdminRepository();
        repo.Rows.Add(SampleVaultRecord("no-expiry-record")); // ExpiresAt is null
        var runtime = CreateRuntime(externalApiCredentialAdminRepository: repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_api_credential", expiresBefore = DateTimeOffset.UtcNow.AddYears(10) }),
            CancellationToken.None);

        Assert.Null(error);
        Assert.DoesNotContain("no-expiry-record", data!.Value.GetRawText());
    }

    [Fact]
    public async Task Search_ExternalApiCredentialCategory_RepositoryNotConfigured_FailsClosed()
    {
        var runtime = CreateRuntime();
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_api_credential" }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_API_CREDENTIAL_NOT_AVAILABLE", error!.Code);
    }

    // --- external_instance_credential category (round 5, new repository authority) ---

    private static ExternalInstanceCredentialRecord SampleInstanceRecord(
        string referenceKey, DateTimeOffset? expiresAt = null, string recordKind = ExternalInstanceCredentialRecordKinds.DbInstancePort) =>
        new(recordKind, Guid.NewGuid(), "authority-1", "postgres", "core_bundle", referenceKey,
            true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, expiresAt, 1);

    [Fact]
    public async Task Search_ExternalInstanceCredentialCategory_ReturnsProjectionWithoutSecretFields()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        repo.Rows.Add(SampleInstanceRecord("instance-ref-1"));
        var runtime = CreateRuntime(externalInstanceCredentialAdminRepository: repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_instance_credential" }), CancellationToken.None);

        Assert.Null(error);
        var json = data!.Value.GetRawText();
        Assert.Equal("external_instance_credential", data.Value.GetProperty("category").GetString());
        Assert.Contains("\"referenceKey\":\"instance-ref-1\"", json);
        foreach (var forbidden in new[] { "tokenHash", "encryptedPayload", "encryptionKeyReference", "plaintextSecret", "decryptedPayload", "connectionSecret" })
            Assert.DoesNotContain(forbidden, json);
    }

    [Fact]
    public async Task Search_ExternalInstanceCredentialCategory_UnknownRecordKind_FailsClosed()
    {
        var runtime = CreateRuntime(externalInstanceCredentialAdminRepository: new FakeExternalInstanceCredentialAdminRepository());
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_instance_credential", recordKind = "not_a_real_kind" }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_INSTANCE_CREDENTIAL_RECORD_KIND_INVALID", error!.Code);
    }

    [Fact]
    public async Task Search_ExternalInstanceCredentialCategory_ExpiresBefore_ExcludesAtOrAfterBoundary_IncludesStrictlyEarlier()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        var boundary = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        repo.Rows.Add(SampleInstanceRecord("expires-before-boundary", boundary.AddDays(-1)));
        repo.Rows.Add(SampleInstanceRecord("expires-at-boundary", boundary));
        repo.Rows.Add(SampleInstanceRecord("expires-after-boundary", boundary.AddDays(1)));
        var runtime = CreateRuntime(externalInstanceCredentialAdminRepository: repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_instance_credential", expiresBefore = boundary }), CancellationToken.None);

        Assert.Null(error);
        var json = data!.Value.GetRawText();
        Assert.Contains("expires-before-boundary", json);
        Assert.DoesNotContain("expires-at-boundary", json);
        Assert.DoesNotContain("expires-after-boundary", json);
    }

    [Fact]
    public async Task Search_ExternalInstanceCredentialCategory_ExpiresFilter_ExcludesRecordsWithNoExpiresAt()
    {
        var repo = new FakeExternalInstanceCredentialAdminRepository();
        repo.Rows.Add(SampleInstanceRecord("no-expiry-record"));
        var runtime = CreateRuntime(externalInstanceCredentialAdminRepository: repo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_instance_credential", expiresBefore = DateTimeOffset.UtcNow.AddYears(10) }),
            CancellationToken.None);

        Assert.Null(error);
        Assert.DoesNotContain("no-expiry-record", data!.Value.GetRawText());
    }

    [Fact]
    public async Task Search_ExternalInstanceCredentialCategory_RepositoryNotConfigured_FailsClosed()
    {
        var runtime = CreateRuntime();
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(new { category = "external_instance_credential" }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("EXTERNAL_INSTANCE_CREDENTIAL_NOT_AVAILABLE", error!.Code);
    }
}
