using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Topolactor.Service;
using Xunit;

namespace Topolactor.Integration.Tests;

[Collection("Auth tests")]
public class AuthRefreshStateDenialTests
{
    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000097");
    private const string TestUsername = "refreshuser";
    private const string TestPasswordHash = "$2b$04$kDDyIYQ5J6BCC8o6rP1N1Ol2ZuxQ2ie3NR4WHwCd/sN2.Re5ausly";
    private const string RefreshPlaintext = "test-refresh-token-plaintext";

    private static (AuthService Service, FakeAuthRepository Repo) BuildService()
    {
        var repo = new FakeAuthRepository([
            (TestUsername, TestUserId, TestPasswordHash, AuthRealm.UserRole, AuthRealm.UserRealm),
        ]);
        var service = new AuthService(NullLogger<AuthService>.Instance, repo, new JwtTokenIssuer());
        return (service, repo);
    }

    private static AuthRefreshTokenRecord ActiveToken() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TestUserId, TestUsername,
            AuthRealm.UserRealm, AuthRealm.UserAudience, AuthRealm.UserRole);

    [Fact]
    public async Task Refresh_InactiveUser_ReturnsAuthUserInactive_NoNewToken()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");

        var (service, repo) = BuildService();
        repo.SeedRefreshToken(RefreshPlaintext, ActiveToken());
        repo.SetUserState(new AuthUserRecord(TestUserId, TestUsername, Active: false, Approve: true, Status: "active"));

        var (response, newToken) = await service.RefreshAsync(RefreshPlaintext, AuthRealm.User);

        Assert.False(response.Success);
        Assert.Null(newToken);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_USER_INACTIVE");
    }

    [Fact]
    public async Task Refresh_UnapprovedUser_ReturnsAuthUserNotApproved_NoNewToken()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");

        var (service, repo) = BuildService();
        repo.SeedRefreshToken(RefreshPlaintext, ActiveToken());
        repo.SetUserState(new AuthUserRecord(TestUserId, TestUsername, Active: true, Approve: false, Status: "active"));

        var (response, newToken) = await service.RefreshAsync(RefreshPlaintext, AuthRealm.User);

        Assert.False(response.Success);
        Assert.Null(newToken);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_USER_NOT_APPROVED");
    }

    [Fact]
    public async Task Refresh_SuspendedStatus_ReturnsAuthUserSuspended_NoNewToken()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");

        var (service, repo) = BuildService();
        repo.SeedRefreshToken(RefreshPlaintext, ActiveToken());
        repo.SetUserState(new AuthUserRecord(TestUserId, TestUsername, Active: true, Approve: true, Status: "suspended"));

        var (response, newToken) = await service.RefreshAsync(RefreshPlaintext, AuthRealm.User);

        Assert.False(response.Success);
        Assert.Null(newToken);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_USER_SUSPENDED");
    }

    [Fact]
    public async Task Refresh_SuspensionWindowActive_ReturnsAuthUserSuspended_NoNewToken()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");

        var (service, repo) = BuildService();
        repo.SeedRefreshToken(RefreshPlaintext, ActiveToken());
        var now = DateTimeOffset.UtcNow;
        repo.SetUserState(new AuthUserRecord(
            TestUserId, TestUsername, Active: true, Approve: true, Status: "active",
            SuspendedFrom: now.AddHours(-1),
            SuspendedUntil: now.AddHours(1)));

        var (response, newToken) = await service.RefreshAsync(RefreshPlaintext, AuthRealm.User);

        Assert.False(response.Success);
        Assert.Null(newToken);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_USER_SUSPENDED");
    }

    [Fact]
    public async Task Refresh_ActiveApprovedUser_Succeeds_ReturnsNewToken()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");

        var (service, repo) = BuildService();
        repo.SeedRefreshToken(RefreshPlaintext, ActiveToken());

        var (response, newToken) = await service.RefreshAsync(RefreshPlaintext, AuthRealm.User);

        Assert.True(response.Success);
        Assert.NotNull(newToken);
        Assert.NotNull(response.Token);
    }

    [Fact]
    public async Task Refresh_SuspensionWindowExpired_Succeeds_ReturnsNewToken()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");

        var (service, repo) = BuildService();
        repo.SeedRefreshToken(RefreshPlaintext, ActiveToken());
        var now = DateTimeOffset.UtcNow;
        repo.SetUserState(new AuthUserRecord(
            TestUserId, TestUsername, Active: true, Approve: true, Status: "active",
            SuspendedFrom: now.AddHours(-3),
            SuspendedUntil: now.AddHours(-1)));

        var (response, newToken) = await service.RefreshAsync(RefreshPlaintext, AuthRealm.User);

        Assert.True(response.Success);
        Assert.NotNull(newToken);
    }
}
