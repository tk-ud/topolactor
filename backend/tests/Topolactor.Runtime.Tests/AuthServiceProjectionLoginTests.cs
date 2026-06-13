using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;
using Topolactor.Service;
using Xunit;

namespace Topolactor.Runtime.Tests;

[Collection("env-sequential")]
/// <summary>
/// Unit tests for AuthService.LoginProjectionAsync.
/// Verifies admin-first / realm-denied fallthrough contract and explicit failure propagation.
/// Uses InMemoryAuthRepository (no DB) and InMemory JwtTokenIssuer via env var injection.
/// </summary>
public class AuthServiceProjectionLoginTests
{
    private const string TestSecret = "projection-login-test-secret-32ch";
    private static readonly Guid AdminUserId = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid UserOnlyUserId = new("bbbbbbbb-0000-0000-0000-000000000002");

    private static AuthService BuildService(InMemoryAuthRepository repo) =>
        new(NullLogger<AuthService>.Instance, repo, new JwtTokenIssuer());

    private static InMemoryAuthRepository BuildRepo()
    {
        var repo = new InMemoryAuthRepository();
        var hash = BCrypt.Net.BCrypt.HashPassword("correct-password");

        // demo_admin — admin realm only (no user grant)
        var adminUser = new AuthUserRecord(AdminUserId, "demo_admin", Active: true, Approve: true, Status: "active");
        repo.SeedUser(adminUser, hash);
        repo.SeedGrant(AdminUserId, "admin", "admin/system");

        // demo_user — user realm only
        var userRecord = new AuthUserRecord(UserOnlyUserId, "demo_user", Active: true, Approve: true, Status: "active");
        repo.SeedUser(userRecord, hash);
        repo.SeedGrant(UserOnlyUserId, "user", "user");

        return repo;
    }

    // ─── Admin user: gets admin JWT via projection login ──────────────────────

    [Fact]
    public async Task LoginProjectionAsync_AdminUserWithAdminGrant_ReturnsAdminJwt()
    {
        using var env = new EnvScope("DEMO_JWT_SECRET", TestSecret);
        using var expiry = new EnvScope("DEMO_JWT_EXPIRY_HOURS", "1");
        var repo = BuildRepo();
        var svc = BuildService(repo);

        var (resp, _) = await svc.LoginProjectionAsync(new LoginRequestDto("demo_admin", "correct-password"));

        Assert.True(resp.Success, $"Expected success but got errors: {string.Join(", ", resp.Errors.Select(e => e.Code))}");
        Assert.NotNull(resp.Token);

        // Verify the issued token carries admin claims
        var guard = new Topolactor.Guard.JwtGuard();
        Assert.Empty(guard.Validate(resp.Token));
        Assert.Equal("admin", guard.TryGetRole(resp.Token));
        Assert.Equal("admin/system", guard.TryGetRealm(resp.Token));
        Assert.Equal("admin_console", guard.TryGetAudience(resp.Token));
    }

    // ─── User-only user: gets user JWT via projection login ───────────────────

    [Fact]
    public async Task LoginProjectionAsync_UserOnlyUser_RealmDeniedOnAdmin_FallsThroughToUserJwt()
    {
        using var env = new EnvScope("DEMO_JWT_SECRET", TestSecret);
        using var expiry = new EnvScope("DEMO_JWT_EXPIRY_HOURS", "1");
        var repo = BuildRepo();
        var svc = BuildService(repo);

        var (resp, _) = await svc.LoginProjectionAsync(new LoginRequestDto("demo_user", "correct-password"));

        Assert.True(resp.Success, $"Expected success but got errors: {string.Join(", ", resp.Errors.Select(e => e.Code))}");
        Assert.NotNull(resp.Token);

        var guard = new Topolactor.Guard.JwtGuard();
        Assert.Equal("user", guard.TryGetRole(resp.Token));
        Assert.Equal("user", guard.TryGetRealm(resp.Token));
    }

    // ─── Bad credentials: immediate failure, no fallthrough ───────────────────

    [Fact]
    public async Task LoginProjectionAsync_WrongPassword_FailsWithoutFallthrough()
    {
        using var env = new EnvScope("DEMO_JWT_SECRET", TestSecret);
        using var expiry = new EnvScope("DEMO_JWT_EXPIRY_HOURS", "1");
        var repo = BuildRepo();
        var svc = BuildService(repo);

        var (resp, _) = await svc.LoginProjectionAsync(new LoginRequestDto("demo_admin", "wrong-password"));

        Assert.False(resp.Success);
        Assert.Contains(resp.Errors, e => e.Code == "AUTH_INVALID_CREDENTIALS");
    }

    // ─── Inactive account: immediate failure ──────────────────────────────────

    [Fact]
    public async Task LoginProjectionAsync_InactiveUser_FailsWithUserInactive()
    {
        using var env = new EnvScope("DEMO_JWT_SECRET", TestSecret);
        using var expiry = new EnvScope("DEMO_JWT_EXPIRY_HOURS", "1");
        var repo = new InMemoryAuthRepository();
        var hash = BCrypt.Net.BCrypt.HashPassword("pass");
        var userId = Guid.NewGuid();
        repo.SeedUser(new AuthUserRecord(userId, "inactive_user", Active: false, Approve: true, Status: "active"), hash);
        repo.SeedGrant(userId, "admin", "admin/system");
        var svc = BuildService(repo);

        var (resp, _) = await svc.LoginProjectionAsync(new LoginRequestDto("inactive_user", "pass"));

        Assert.False(resp.Success);
        Assert.Contains(resp.Errors, e => e.Code == "AUTH_USER_INACTIVE");
    }

    // ─── Admin user: no user grant added (no role downgrade) ─────────────────

    [Fact]
    public async Task LoginProjectionAsync_AdminUser_DoesNotIssueUserJwt()
    {
        using var env = new EnvScope("DEMO_JWT_SECRET", TestSecret);
        using var expiry = new EnvScope("DEMO_JWT_EXPIRY_HOURS", "1");
        var repo = BuildRepo();
        var svc = BuildService(repo);

        var (resp, _) = await svc.LoginProjectionAsync(new LoginRequestDto("demo_admin", "correct-password"));

        Assert.True(resp.Success);
        var guard = new Topolactor.Guard.JwtGuard();
        // Admin user must NOT receive a user-realm JWT — that would be a capability downgrade.
        Assert.NotEqual("user", guard.TryGetRole(resp.Token!));
        Assert.NotEqual("user", guard.TryGetRealm(resp.Token!));
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _prev;
        public EnvScope(string name, string value)
        {
            _name = name;
            _prev = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
        public void Dispose() => Environment.SetEnvironmentVariable(_name, _prev);
    }
}
