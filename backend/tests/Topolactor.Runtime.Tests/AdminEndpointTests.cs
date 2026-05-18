using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Endpoint;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// ---------------------------------------------------------------------------
// Stub repository for AdminEndpoint tests.
// Simulates context_token_registry with in-memory state, including
// UNIQUE(label, "group") conflict detection that mirrors the DB constraint.
// ---------------------------------------------------------------------------

internal sealed class StubAdminContextRouteRepository()
    : ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy")
{
    private readonly List<ContextTokenRecord> _tokens = [];

    public override Task<IReadOnlyList<ContextTokenRecord>> ListAllContextTokensAsync(
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ContextTokenRecord>>(_tokens.ToList());

    public override Task<CreateTokenResult> CreateContextTokenAsync(
        string label, string? group, float value, CancellationToken ct = default)
    {
        // Mirror UNIQUE(label, "group") constraint
        if (_tokens.Any(t => t.Label == label && t.Group == group))
            return Task.FromResult(new CreateTokenResult(CreateTokenCode.Conflict, null));

        var tokenId = Guid.NewGuid();
        _tokens.Add(new ContextTokenRecord(tokenId, label, group, value, "active"));
        return Task.FromResult(new CreateTokenResult(CreateTokenCode.Success, tokenId));
    }

    public override Task<bool> DeprecateContextTokenAsync(
        Guid tokenId, CancellationToken ct = default)
    {
        var idx = _tokens.FindIndex(t => t.TokenId == tokenId);
        if (idx < 0) return Task.FromResult(false);
        var t = _tokens[idx];
        _tokens[idx] = t with { Status = "deprecated" };
        return Task.FromResult(true);
    }

    public IReadOnlyList<ContextTokenRecord> AllTokens => _tokens;
}

internal static class AdminEndpointFactory
{
    internal static (AdminEndpoint Endpoint, StubAdminContextRouteRepository Repository) Create()
    {
        var repo = new StubAdminContextRouteRepository();
        var endpoint = new AdminEndpoint(NullLogger<AdminEndpoint>.Instance, repo);
        return (endpoint, repo);
    }
}

// ---------------------------------------------------------------------------
// AdminEndpoint unit tests
// ---------------------------------------------------------------------------

public class AdminEndpointTests
{
    // -----------------------------------------------------------------------
    // HandleListTokensAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleListTokensAsync_ReturnsEmptyList_WhenRepositoryEmpty()
    {
        var (endpoint, _) = AdminEndpointFactory.Create();

        var result = await endpoint.HandleListTokensAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleListTokensAsync_ReturnsTokens_WhenRepositoryHasData()
    {
        var (endpoint, repo) = AdminEndpointFactory.Create();
        await repo.CreateContextTokenAsync("active_token", "status", 1.0f);
        await repo.CreateContextTokenAsync("warning_token", "status", 0.0f);

        var result = await endpoint.HandleListTokensAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Label == "active_token");
        Assert.Contains(result, t => t.Label == "warning_token");
    }

    // -----------------------------------------------------------------------
    // HandleCreateTokenAsync — success
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleCreateTokenAsync_ReturnsSuccess_WithValidInput()
    {
        var (endpoint, _) = AdminEndpointFactory.Create();

        var result = await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("new_token", "status", 0.5f));

        Assert.True(result.Ok);
        Assert.NotNull(result.TokenId);
        Assert.True(Guid.TryParse(result.TokenId, out _));
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task HandleCreateTokenAsync_TokenAppearsInList_AfterCreate()
    {
        var (endpoint, _) = AdminEndpointFactory.Create();
        await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("listed_token", "group_a", -0.5f));

        var list = await endpoint.HandleListTokensAsync();

        Assert.Single(list);
        Assert.Equal("listed_token", list[0].Label);
        Assert.Equal("group_a", list[0].Group);
        Assert.Equal(-0.5f, list[0].Value, precision: 5);
        Assert.Equal("active", list[0].Status);
    }

    // -----------------------------------------------------------------------
    // HandleCreateTokenAsync — input validation failures
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleCreateTokenAsync_ReturnsError_WhenLabelIsEmpty()
    {
        var (endpoint, _) = AdminEndpointFactory.Create();

        var result = await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("", null, 0.5f));

        Assert.False(result.Ok);
        Assert.Null(result.TokenId);
        Assert.Equal("LABEL_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task HandleCreateTokenAsync_ReturnsError_WhenLabelIsWhitespace()
    {
        var (endpoint, _) = AdminEndpointFactory.Create();

        var result = await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("   ", null, 0.5f));

        Assert.False(result.Ok);
        Assert.Equal("LABEL_REQUIRED", result.ErrorCode);
    }

    [Theory]
    [InlineData(-1.1f)]
    [InlineData(1.1f)]
    [InlineData(2.0f)]
    [InlineData(-2.0f)]
    public async Task HandleCreateTokenAsync_ReturnsError_WhenValueOutOfRange(float outOfRangeValue)
    {
        var (endpoint, _) = AdminEndpointFactory.Create();

        var result = await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("token", null, outOfRangeValue));

        Assert.False(result.Ok);
        Assert.Null(result.TokenId);
        Assert.Equal("VALUE_OUT_OF_RANGE", result.ErrorCode);
    }

    [Theory]
    [InlineData(-1.0f)]
    [InlineData(0.0f)]
    [InlineData(1.0f)]
    public async Task HandleCreateTokenAsync_AcceptsValue_AtBoundary(float boundaryValue)
    {
        var (endpoint, _) = AdminEndpointFactory.Create();

        var result = await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("token", null, boundaryValue));

        Assert.True(result.Ok);
    }

    // -----------------------------------------------------------------------
    // HandleCreateTokenAsync — duplicate / UNIQUE constraint
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleCreateTokenAsync_ReturnsConflict_WhenDuplicateLabelAndGroup()
    {
        var (endpoint, _) = AdminEndpointFactory.Create();
        await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("dup_label", "status", 0.5f));

        var result = await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("dup_label", "status", -0.5f));

        Assert.False(result.Ok);
        Assert.Null(result.TokenId);
        Assert.Equal("DUPLICATE_LABEL_GROUP", result.ErrorCode);
        Assert.Contains("already exists", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleCreateTokenAsync_AllowsDuplicateLabel_WhenGroupDiffers()
    {
        var (endpoint, _) = AdminEndpointFactory.Create();
        await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("shared_label", "group_a", 0.5f));

        var result = await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("shared_label", "group_b", 0.5f));

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task HandleCreateTokenAsync_AllowsDuplicateLabel_WhenFirstGroupIsNull()
    {
        var (endpoint, _) = AdminEndpointFactory.Create();
        await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("shared_label", null, 0.5f));

        var result = await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("shared_label", "group_a", 0.5f));

        Assert.True(result.Ok);
    }

    // -----------------------------------------------------------------------
    // HandleCreateTokenAsync — not-connected (skeleton repository)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleCreateTokenAsync_ReturnsNotConnected_WhenRepositoryIsSkeletonBase()
    {
        var skeletonRepo = new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "dummy");
        var endpoint = new AdminEndpoint(NullLogger<AdminEndpoint>.Instance, skeletonRepo);

        var result = await endpoint.HandleCreateTokenAsync(
            new AdminCreateTokenRequestDto("test_token", null, 0.5f));

        Assert.False(result.Ok);
        Assert.Null(result.TokenId);
        Assert.Equal("NOT_CONNECTED", result.ErrorCode);
        Assert.Contains("not connected", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // HandleDeprecateTokenAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleDeprecateTokenAsync_ReturnsSuccess_WhenTokenExists()
    {
        var (endpoint, repo) = AdminEndpointFactory.Create();
        var createResult = await repo.CreateContextTokenAsync("active_token", null, 0.5f);
        var tokenId = createResult.TokenId!.Value;

        var (response, found) = await endpoint.HandleDeprecateTokenAsync(tokenId);

        Assert.True(found);
        Assert.True(response.Ok);
        Assert.Contains("deprecated", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleDeprecateTokenAsync_TokenAppearsDeprecated_InListAfterDeprecate()
    {
        var (endpoint, repo) = AdminEndpointFactory.Create();
        var createResult = await repo.CreateContextTokenAsync("to_deprecate", null, 0.5f);
        var tokenId = createResult.TokenId!.Value;
        await endpoint.HandleDeprecateTokenAsync(tokenId);

        var list = await endpoint.HandleListTokensAsync();

        var token = Assert.Single(list);
        Assert.Equal("deprecated", token.Status);
    }

    [Fact]
    public async Task HandleDeprecateTokenAsync_ReturnsNotFound_WhenTokenMissing()
    {
        var (endpoint, _) = AdminEndpointFactory.Create();
        var nonexistentId = Guid.NewGuid();

        var (response, found) = await endpoint.HandleDeprecateTokenAsync(nonexistentId);

        Assert.False(found);
        Assert.False(response.Ok);
        Assert.Contains("not found", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleDeprecateTokenAsync_IsIdempotent_WhenAlreadyDeprecated()
    {
        var (endpoint, repo) = AdminEndpointFactory.Create();
        var createResult = await repo.CreateContextTokenAsync("once_active", null, 0.5f);
        var tokenId = createResult.TokenId!.Value;
        await endpoint.HandleDeprecateTokenAsync(tokenId);

        var (response, found) = await endpoint.HandleDeprecateTokenAsync(tokenId);

        Assert.True(found);
        Assert.True(response.Ok);
    }
}
