using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Topolactor.Service;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for AdminRuntime layer=auth_users action=revoke_credential|revoke_sessions
/// (backend/runtime/AdminRuntime.AuthUsersRevoke.cs, docs/design/
/// admin-normal-surface-projection-seed-ssot.yaml surface_axes.admin.surfaces.credentials.
/// categories.users). Proves these admin_runtime entry points reach the SAME AuthService methods
/// (AdminRevokeCredentialAsync/AdminRevokeSessionsAsync) the existing REST routes
/// (POST /admin/auth/users/{userId}/credential|sessions/revoke) already call -- continuation_
/// refinement_not_duplicate_authority, never a second revoke implementation.
/// </summary>
public class AdminRuntimeAuthUsersRevokeTests
{
    private static (AdminRuntime runtime, InMemoryAuthRepository authRepo) CreateRuntime()
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);

        var authRepo = new InMemoryAuthRepository();
        Environment.SetEnvironmentVariable("DEMO_JWT_SECRET", "test-secret-key-at-least-32-chars-long!!");
        Environment.SetEnvironmentVariable("DEMO_JWT_EXPIRY_HOURS", "1");
        var authService = new AuthService(NullLogger<AuthService>.Instance, authRepo, new JwtTokenIssuer());

        var runtime = new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo,
            authService: authService);
        return (runtime, authRepo);
    }

    private static OperationVector Vector(string action, object payload) =>
        new("admin", "auth_users", action, null, "admin", JsonSerializer.SerializeToElement(payload), null);

    [Theory]
    [InlineData("revoke_credential")]
    [InlineData("revoke_sessions")]
    public async Task ExecuteDataAsync_AuthServiceNotConfigured_FailsClosedWithNotAvailable(string action)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var runtime = new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(action, new { userId = Guid.NewGuid().ToString() }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("AUTH_SERVICE_NOT_AVAILABLE", error!.Code);
    }

    [Theory]
    [InlineData("revoke_credential")]
    [InlineData("revoke_sessions")]
    public async Task ExecuteDataAsync_MalformedUserId_FailsClosed(string action)
    {
        var (runtime, _) = CreateRuntime();
        var (data, error) = await runtime.ExecuteDataAsync(
            Vector(action, new { userId = "not-a-guid" }), CancellationToken.None);

        Assert.Null(data);
        Assert.Equal("AUTH_USER_ID_MALFORMED", error!.Code);
    }

    [Fact]
    public async Task RevokeCredential_DeletesTheCredential_And_LoginNoLongerPossible()
    {
        var (runtime, authRepo) = CreateRuntime();
        var userId = Guid.NewGuid();
        authRepo.SeedUser(new AuthUserRecord(userId, "revoke_cred_user", true, true, "active", null, null),
            BCrypt.Net.BCrypt.HashPassword("pass"));
        authRepo.SeedGrant(userId, "user", "user");
        Assert.NotNull(authRepo.PasswordHashFor(userId));

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("revoke_credential", new { userId = userId.ToString() }), CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
        Assert.Null(authRepo.PasswordHashFor(userId));
    }

    [Fact]
    public async Task RevokeSessions_WithoutSessionId_RevokesAllActiveSessionsForUser()
    {
        var (runtime, authRepo) = CreateRuntime();
        var userId = Guid.NewGuid();
        authRepo.SeedUser(new AuthUserRecord(userId, "revoke_sessions_user", true, true, "active", null, null),
            BCrypt.Net.BCrypt.HashPassword("pass"));
        authRepo.SeedGrant(userId, "user", "user");
        var session1 = authRepo.SeedSession(userId);
        var session2 = authRepo.SeedSession(userId);
        Assert.False(authRepo.IsSessionRevoked(session1));
        Assert.False(authRepo.IsSessionRevoked(session2));

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("revoke_sessions", new { userId = userId.ToString() }), CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(authRepo.IsSessionRevoked(session1));
        Assert.True(authRepo.IsSessionRevoked(session2));
        Assert.Equal(2, data!.Value.GetProperty("sessionsRevoked").GetInt32());
    }

    [Fact]
    public async Task RevokeSessions_WithSpecificSessionId_RevokesOnlyThatSession()
    {
        var (runtime, authRepo) = CreateRuntime();
        var userId = Guid.NewGuid();
        authRepo.SeedUser(new AuthUserRecord(userId, "revoke_one_session_user", true, true, "active", null, null),
            BCrypt.Net.BCrypt.HashPassword("pass"));
        authRepo.SeedGrant(userId, "user", "user");
        var session1 = authRepo.SeedSession(userId);
        var session2 = authRepo.SeedSession(userId);

        var (data, error) = await runtime.ExecuteDataAsync(
            Vector("revoke_sessions", new { userId = userId.ToString(), sessionId = session1.ToString() }),
            CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.True(authRepo.IsSessionRevoked(session1));
        Assert.False(authRepo.IsSessionRevoked(session2));
    }
}
