using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Behavior tests for AuthEndpoint.HandleAsync.
/// Covers: env var config errors, missing/malformed credentials, invalid password, valid login.
/// No DB connection required — uses FakeTopologyRepository.
/// </summary>
public class AuthEndpointTests
{
    // Pre-computed bcrypt hash of "test-password" at cost 4 (fast for tests).
    private const string TestPasswordHash = "$2b$04$kDDyIYQ5J6BCC8o6rP1N1Ol2ZuxQ2ie3NR4WHwCd/sN2.Re5ausly";
    private const string TestPassword = "test-password";
    private const string TestUsername = "testuser";
    private const string TestRole = "test";

    private static string ValidCredentialsJson =>
        $"""[{{"username":"{TestUsername}","password_hash":"{TestPasswordHash}","role":"{TestRole}"}}]""";

    private static AuthEndpoint CreateEndpoint(string? credentialsJson) =>
        new(NullLogger<AuthEndpoint>.Instance,
            new FakeTopologyRepository(credentialsJson));

    [Fact]
    public async Task NullRequest_Returns_AUTH_REQUEST_NULL()
    {
        var endpoint = CreateEndpoint(ValidCredentialsJson);
        var response = await endpoint.HandleAsync(null!);
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_REQUEST_NULL");
    }

    [Fact]
    public async Task EmptyUsername_Returns_AUTH_CREDENTIALS_REQUIRED()
    {
        var endpoint = CreateEndpoint(ValidCredentialsJson);
        var response = await endpoint.HandleAsync(new("", TestPassword));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CREDENTIALS_REQUIRED");
    }

    [Fact]
    public async Task EmptyPassword_Returns_AUTH_CREDENTIALS_REQUIRED()
    {
        var endpoint = CreateEndpoint(ValidCredentialsJson);
        var response = await endpoint.HandleAsync(new(TestUsername, ""));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CREDENTIALS_REQUIRED");
    }

    [Fact]
    public async Task MissingJwtSecret_Returns_AUTH_JWT_SECRET_NOT_CONFIGURED()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", null);
        var endpoint = CreateEndpoint(ValidCredentialsJson);
        var response = await endpoint.HandleAsync(new(TestUsername, TestPassword));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_JWT_SECRET_NOT_CONFIGURED");
    }

    [Fact]
    public async Task CredentialNotFound_Returns_AUTH_CREDENTIAL_NOT_CONFIGURED()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", "test-secret");
        var endpoint = CreateEndpoint(credentialsJson: null);
        var response = await endpoint.HandleAsync(new(TestUsername, TestPassword));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CREDENTIAL_NOT_CONFIGURED");
    }

    [Fact]
    public async Task MalformedCredentialsJson_Returns_AUTH_CREDENTIAL_MALFORMED()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", "test-secret");
        var endpoint = CreateEndpoint(credentialsJson: "not-valid-json{{");
        var response = await endpoint.HandleAsync(new(TestUsername, TestPassword));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CREDENTIAL_MALFORMED");
    }

    [Fact]
    public async Task EmptyCredentialList_Returns_AUTH_CREDENTIAL_NOT_CONFIGURED()
    {
        using var env = new EnvScope().Set("DEMO_JWT_SECRET", "test-secret");
        var endpoint = CreateEndpoint(credentialsJson: "[]");
        var response = await endpoint.HandleAsync(new(TestUsername, TestPassword));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CREDENTIAL_NOT_CONFIGURED");
    }

    [Fact]
    public async Task MissingExpiryHours_Returns_AUTH_JWT_EXPIRY_NOT_CONFIGURED()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", null);
        var endpoint = CreateEndpoint(ValidCredentialsJson);
        var response = await endpoint.HandleAsync(new(TestUsername, TestPassword));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_JWT_EXPIRY_NOT_CONFIGURED");
    }

    [Fact]
    public async Task InvalidExpiryHours_Returns_AUTH_JWT_EXPIRY_NOT_CONFIGURED()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "not-a-number");
        var endpoint = CreateEndpoint(ValidCredentialsJson);
        var response = await endpoint.HandleAsync(new(TestUsername, TestPassword));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_JWT_EXPIRY_NOT_CONFIGURED");
    }

    [Fact]
    public async Task ZeroExpiryHours_Returns_AUTH_JWT_EXPIRY_NOT_CONFIGURED()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "0");
        var endpoint = CreateEndpoint(ValidCredentialsJson);
        var response = await endpoint.HandleAsync(new(TestUsername, TestPassword));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_JWT_EXPIRY_NOT_CONFIGURED");
    }

    [Fact]
    public async Task WrongPassword_Returns_AUTH_INVALID_CREDENTIALS()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");
        var endpoint = CreateEndpoint(ValidCredentialsJson);
        var response = await endpoint.HandleAsync(new(TestUsername, "wrong-password"));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task UnknownUsername_Returns_AUTH_INVALID_CREDENTIALS()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");
        var endpoint = CreateEndpoint(ValidCredentialsJson);
        var response = await endpoint.HandleAsync(new("nobody", TestPassword));
        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task ValidLogin_Returns_Success_With_ThreePartToken()
    {
        using var env = new EnvScope()
            .Set("DEMO_JWT_SECRET", "test-secret")
            .Set("DEMO_JWT_EXPIRY_HOURS", "8");
        var endpoint = CreateEndpoint(ValidCredentialsJson);
        var response = await endpoint.HandleAsync(new(TestUsername, TestPassword));
        Assert.True(response.Success);
        Assert.NotNull(response.Token);
        Assert.Empty(response.Errors);
        Assert.Equal(3, response.Token!.Split('.').Length);
    }

    // ---------------------------------------------------------------------------

    private sealed class FakeTopologyRepository : TopologyRepository
    {
        private readonly string? _credentialsJson;

        public FakeTopologyRepository(string? credentialsJson)
            : base(NullLogger<TopologyRepository>.Instance, "dummy")
        {
            _credentialsJson = credentialsJson;
        }

        public override Task<string?> LoadFunctionParameterAsync(
            string functionName, string parameterKey, CancellationToken ct = default)
            => Task.FromResult(_credentialsJson);
    }
}
