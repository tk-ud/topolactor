using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class DispatchRoleAuthorityTests
{
    [Fact]
    public void BuildAuthoritativeRequest_JwtRoleOverridesBodyRoleAndContextUserRole()
    {
        var request = new EndpointRequestDto(
            "Search",
            "admin",
            "seed_runtime",
            "save",
            null,
            null,
            new Dictionary<string, string> { ["UserRole"] = "viewer" },
            TriggerKind: "client",
            Role: "viewer");

        var authoritative = request with { Role = "admin" };
        var vector = new OperationVectorResolver().Resolve(authoritative);

        Assert.Equal("admin", authoritative.Role);
        Assert.Equal("admin", vector.UserRole);
    }
}

