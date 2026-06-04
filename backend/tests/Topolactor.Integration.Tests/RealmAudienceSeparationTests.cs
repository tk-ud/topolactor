using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Guard;
using Topolactor.Runtime;
using Topolactor.Schema;
using Topolactor.Service;
using Xunit;

namespace Topolactor.Integration.Tests;

[Collection("Auth tests")]
public class RealmAudienceSeparationTests
{
    [Fact]
    public async Task UserLogin_Token_Has_User_Realm_And_Audience()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");

        var repo = new FakeAuthRepository([
            ("u", Guid.NewGuid(), "$2b$04$kDDyIYQ5J6BCC8o6rP1N1Ol2ZuxQ2ie3NR4WHwCd/sN2.Re5ausly",
                AuthRealm.UserRole, AuthRealm.UserRealm),
        ]);
        var endpoint = new AuthEndpoint(new AuthRuntime(
            new AuthService(NullLogger<AuthService>.Instance, repo, new JwtTokenIssuer()),
            new FakeLoginManifestRepository()));

        var (response, _) = await endpoint.LoginUserAsync(new("u", "test-password"));
        Assert.True(response.Success);

        var guard = new JwtGuard();
        Assert.Empty(guard.ValidateForContext(
            response.Token, AuthRealm.UserRealm, AuthRealm.UserAudience, AuthRealm.UserRole));
        Assert.NotEmpty(guard.ValidateForContext(
            response.Token, AuthRealm.AdminRealm, AuthRealm.AdminAudience, AuthRealm.AdminRole));
    }
}
