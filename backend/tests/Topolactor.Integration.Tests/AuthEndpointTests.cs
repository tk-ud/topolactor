using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Topolactor.Service;
using Xunit;

namespace Topolactor.Integration.Tests;

[Collection("Auth tests")]
public class AuthEndpointTests
{
    private const string TestPasswordHash = "$2b$04$kDDyIYQ5J6BCC8o6rP1N1Ol2ZuxQ2ie3NR4WHwCd/sN2.Re5ausly";
    private const string TestPassword = "test-password";
    private const string TestUsername = "testuser";
    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000099");

    private static AuthEndpoint CreateEndpoint(FakeAuthRepository? authRepo = null)
    {
        authRepo ??= new FakeAuthRepository([
            (TestUsername, TestUserId, TestPasswordHash, AuthRealm.UserRole, AuthRealm.UserRealm),
        ]);
        var jwt = new JwtTokenIssuer();
        var service = new AuthService(NullLogger<AuthService>.Instance, authRepo, jwt);
        var manifestRepo = new FakeLoginManifestRepository();
        var runtime = new AuthRuntime(service, manifestRepo);
        return new AuthEndpoint(runtime);
    }

    [Fact]
    public async Task NullRequest_Returns_AUTH_REQUEST_NULL()
    {
        var endpoint = CreateEndpoint();
        var (response, _) = await endpoint.LoginUserAsync(null!);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_REQUEST_NULL");
    }

    [Fact]
    public async Task ValidLogin_Returns_Success_With_ThreePartToken()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");
        var endpoint = CreateEndpoint();
        var (response, refresh) = await endpoint.LoginUserAsync(new(TestUsername, TestPassword));
        Assert.True(response.Success);
        Assert.NotNull(response.Token);
        Assert.NotNull(refresh);
        Assert.Equal(3, response.Token!.Split('.').Length);
    }

    [Fact]
    public async Task RegisterUser_CreatesPendingApprovalNormalUserWithoutToken()
    {
        var repo = new FakeAuthRepository([]);
        var endpoint = CreateEndpoint(repo);

        var response = await endpoint.RegisterUserAsync(new("new-user", "new-password"));

        Assert.True(response.Success);
        Assert.Equal("new-user", response.Username);
        Assert.False(response.Approve);
        Assert.Equal("active", response.Status);
        Assert.NotNull(repo.LastCreatedPendingUser);
        Assert.True(BCrypt.Net.BCrypt.Verify("new-password", repo.LastCreatedPasswordHash));
        Assert.Equal(AuthRealm.UserRole, await repo.GetGrantRoleForRealmAsync(repo.LastCreatedPendingUser!.UserId, AuthRealm.UserRealm));
        Assert.Null(await repo.GetGrantRoleForRealmAsync(repo.LastCreatedPendingUser.UserId, AuthRealm.AdminRealm));
    }

    [Fact]
    public async Task RegisterExistingUsername_ReturnsExplicitDuplicateError()
    {
        var endpoint = CreateEndpoint();

        var response = await endpoint.RegisterUserAsync(new(TestUsername, "new-password"));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_REGISTER_USERNAME_EXISTS");
    }

    [Fact]
    public async Task WrongPassword_Returns_AUTH_INVALID_CREDENTIALS()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");
        var endpoint = CreateEndpoint();
        var (response, _) = await endpoint.LoginUserAsync(new(TestUsername, "wrong"));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_INVALID_CREDENTIALS");
    }

}
