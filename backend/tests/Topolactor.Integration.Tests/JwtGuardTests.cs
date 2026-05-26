using Topolactor.Guard;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Behavior tests for JwtGuard.Validate.
/// Covers: missing token, missing secret, malformed structure, invalid signature,
/// missing exp, invalid exp type, expired token, valid token.
///
/// Placed in the "Auth tests" collection (AuthTestCollection) so these tests run
/// sequentially with AuthEndpointTests and avoid env-var interference.
/// </summary>
[Collection("Auth tests")]
public class JwtGuardTests
{
    private const string TestSecret = "test-jwt-secret-for-guard-tests";

    [Fact]
    public void NullToken_Returns_AUTH_TOKEN_MISSING()
    {
        var guard = new JwtGuard();
        var errors = guard.Validate(null);
        Assert.Contains(errors, e => e.Code == "AUTH_TOKEN_MISSING");
    }

    [Fact]
    public void WhitespaceToken_Returns_AUTH_TOKEN_MISSING()
    {
        var guard = new JwtGuard();
        var errors = guard.Validate("   ");
        Assert.Contains(errors, e => e.Code == "AUTH_TOKEN_MISSING");
    }

    [Fact]
    public void MissingSecret_Returns_AUTH_JWT_SECRET_NOT_CONFIGURED()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", null);
        var guard = new JwtGuard();
        var errors = guard.Validate("header.payload.sig");
        Assert.Contains(errors, e => e.Code == "AUTH_JWT_SECRET_NOT_CONFIGURED");
    }

    [Fact]
    public void MalformedToken_TwoParts_Returns_AUTH_TOKEN_MALFORMED()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret);
        var guard = new JwtGuard();
        var errors = guard.Validate("onlytwo.parts");
        Assert.Contains(errors, e => e.Code == "AUTH_TOKEN_MALFORMED");
    }

    [Fact]
    public void MalformedToken_FourParts_Returns_AUTH_TOKEN_MALFORMED()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret);
        var guard = new JwtGuard();
        var errors = guard.Validate("a.b.c.d");
        Assert.Contains(errors, e => e.Code == "AUTH_TOKEN_MALFORMED");
    }

    [Fact]
    public void InvalidSignature_Returns_AUTH_TOKEN_INVALID_SIGNATURE()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret);
        // Token built with a different secret
        var token = TestJwtBuilder.BuildToken("wrong-secret");
        var guard = new JwtGuard();
        var errors = guard.Validate(token);
        Assert.Contains(errors, e => e.Code == "AUTH_TOKEN_INVALID_SIGNATURE");
    }

    [Fact]
    public void MissingExpClaim_Returns_AUTH_TOKEN_EXP_MISSING()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret);
        // Token built without exp field
        var token = TestJwtBuilder.BuildToken(TestSecret, includeExp: false);
        var guard = new JwtGuard();
        var errors = guard.Validate(token);
        Assert.Contains(errors, e => e.Code == "AUTH_TOKEN_EXP_MISSING");
    }

    [Fact]
    public void InvalidExpType_Returns_AUTH_TOKEN_EXP_INVALID()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret);
        // Token with exp as a string instead of integer
        var token = TestJwtBuilder.BuildTokenWithStringExp(TestSecret);
        var guard = new JwtGuard();
        var errors = guard.Validate(token);
        Assert.Contains(errors, e => e.Code == "AUTH_TOKEN_EXP_INVALID");
    }

    [Fact]
    public void ExpiredToken_Returns_AUTH_TOKEN_EXPIRED()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret);
        // exp set to 1 second in the past
        var expiredUnix = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds();
        var token = TestJwtBuilder.BuildToken(TestSecret, expUnix: expiredUnix);
        var guard = new JwtGuard();
        var errors = guard.Validate(token);
        Assert.Contains(errors, e => e.Code == "AUTH_TOKEN_EXPIRED");
    }

    [Fact]
    public void ValidToken_Returns_Empty_Errors()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret);
        // exp set to 1 hour in the future
        var futureUnix = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var token = TestJwtBuilder.BuildToken(TestSecret, expUnix: futureUnix);
        var guard = new JwtGuard();
        var errors = guard.Validate(token);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidToken_TryGetSubject_Returns_Sub()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret);
        var token = TestJwtBuilder.BuildToken(TestSecret, subject: "actor-1");
        var guard = new JwtGuard();

        var sub = guard.TryGetSubject(token);

        Assert.Equal("actor-1", sub);
    }

    [Fact]
    public void TokenWithoutSub_TryGetSubject_Returns_Null()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", TestSecret);
        var token = TestJwtBuilder.BuildTokenWithoutSub(TestSecret);
        var guard = new JwtGuard();

        var sub = guard.TryGetSubject(token);

        Assert.Null(sub);
    }
}
