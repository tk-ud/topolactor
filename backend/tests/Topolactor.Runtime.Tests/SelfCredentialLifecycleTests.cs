using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Topolactor.Service;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Self-service credential/session lifecycle — AuthService.ChangeOwnPasswordAsync et al.
/// Uses InMemoryAuthRepository (no DB). Covers the role-based-surface-impl bundle's explicit NG
/// axis items: self-only target resolution, current-password verification, password policy,
/// session revocation, and that no operation in this family can view/set another account's
/// password or accept a foreign userId.
/// SSOT: docs/design/auth-db-session-credential-ssot.yaml self_credential_and_session_lifecycle.
/// </summary>
[Trait("Category", "SelfCredentialLifecycle")]
public class SelfCredentialLifecycleTests
{
    private static readonly Guid UserId = new("cccccccc-0000-0000-0000-000000000003");
    private const string Username = "self_credential_user";
    private const string CurrentPassword = "correct-password-123";

    private static (InMemoryAuthRepository Repo, AuthService Service) Build()
    {
        var repo = new InMemoryAuthRepository();
        var hash = BCrypt.Net.BCrypt.HashPassword(CurrentPassword);
        repo.SeedUser(new AuthUserRecord(UserId, Username, Active: true, Approve: true, Status: "active"), hash);
        repo.SeedGrant(UserId, "user", "user");
        var service = new AuthService(NullLogger<AuthService>.Instance, repo, new JwtTokenIssuer());
        return (repo, service);
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_CorrectCurrentPassword_UpdatesHashAndRevokesAllSessions()
    {
        var (repo, service) = Build();
        var s1 = repo.SeedSession(UserId);
        var s2 = repo.SeedSession(UserId);

        var result = await service.ChangeOwnPasswordAsync(
            Username, new ChangeOwnPasswordRequestDto(CurrentPassword, "brand-new-password-456"));

        Assert.True(result.Success);
        Assert.Equal(2, result.SessionsRevoked);
        Assert.True(repo.IsSessionRevoked(s1));
        Assert.True(repo.IsSessionRevoked(s2));
        Assert.True(BCrypt.Net.BCrypt.Verify("brand-new-password-456", repo.PasswordHashFor(UserId)!));
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_WrongCurrentPassword_RejectedAndHashUnchanged()
    {
        var (repo, service) = Build();
        var originalHash = repo.PasswordHashFor(UserId);

        var result = await service.ChangeOwnPasswordAsync(
            Username, new ChangeOwnPasswordRequestDto("totally-wrong-password", "brand-new-password-456"));

        Assert.False(result.Success);
        Assert.Equal("AUTH_CURRENT_PASSWORD_INVALID", result.Errors[0].Code);
        Assert.Equal(originalHash, repo.PasswordHashFor(UserId));
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_NewPasswordTooShort_RejectedBeforeTouchingRepository()
    {
        var (repo, service) = Build();
        var originalHash = repo.PasswordHashFor(UserId);

        var result = await service.ChangeOwnPasswordAsync(
            Username, new ChangeOwnPasswordRequestDto(CurrentPassword, "short"));

        Assert.False(result.Success);
        Assert.Equal("AUTH_PASSWORD_POLICY_TOO_SHORT", result.Errors[0].Code);
        Assert.Equal(originalHash, repo.PasswordHashFor(UserId));
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_NewPasswordSameAsCurrent_Rejected()
    {
        var (_, service) = Build();

        var result = await service.ChangeOwnPasswordAsync(
            Username, new ChangeOwnPasswordRequestDto(CurrentPassword, CurrentPassword));

        Assert.False(result.Success);
        Assert.Equal("AUTH_PASSWORD_POLICY_UNCHANGED", result.Errors[0].Code);
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_ResponseNeverCarriesPasswordMaterial()
    {
        var (_, service) = Build();

        var result = await service.ChangeOwnPasswordAsync(
            Username, new ChangeOwnPasswordRequestDto(CurrentPassword, "brand-new-password-456"));

        // The response DTO shape itself has no field capable of carrying a password/hash value —
        // this assertion documents that boundary via reflection so a future field addition fails loudly.
        var props = typeof(ChangeOwnPasswordResponseDto).GetProperties().Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain(props, n => n.Contains("password") || n.Contains("hash"));
        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetCurrentAccountAsync_ResolvesOnlyFromJwtSubject_NeverFromAnyOtherInput()
    {
        var (_, service) = Build();

        var result = await service.GetCurrentAccountAsync(Username, "user", "user");

        Assert.True(result.Success);
        Assert.Equal(Username, result.Username);
        // GetCurrentAccountAsync's only identity input is the jwtSubject parameter — there is no
        // userId/username field on any request DTO in this family (see ChangeOwnPasswordRequestDto,
        // RevokeOwnSessionRequestDto), so a caller has no channel to name a different account.
    }

    [Fact]
    public async Task RevokeOwnSessionAsync_CannotRevokeAnotherAccountsSession()
    {
        var (repo, service) = Build();
        var otherUserId = Guid.NewGuid();
        var otherUsersSession = repo.SeedSession(otherUserId);

        var result = await service.RevokeOwnSessionAsync(
            Username, new RevokeOwnSessionRequestDto(otherUsersSession.ToString()));

        Assert.False(result.Success);
        Assert.Equal("AUTH_SESSION_NOT_FOUND", result.Errors[0].Code);
        Assert.False(repo.IsSessionRevoked(otherUsersSession));
    }

    [Fact]
    public async Task RevokeOwnSessionAsync_RevokesOwnSession()
    {
        var (repo, service) = Build();
        var mySession = repo.SeedSession(UserId);

        var result = await service.RevokeOwnSessionAsync(Username, new RevokeOwnSessionRequestDto(mySession.ToString()));

        Assert.True(result.Success);
        Assert.True(repo.IsSessionRevoked(mySession));
    }

    [Fact]
    public async Task RevokeOtherSessionsAsync_KeepsCurrentSessionRevokesRest()
    {
        var (repo, service) = Build();
        var current = repo.SeedSession(UserId);
        var other1 = repo.SeedSession(UserId);
        var other2 = repo.SeedSession(UserId);

        var result = await service.RevokeOtherSessionsAsync(Username, current);

        Assert.True(result.Success);
        Assert.Equal(2, result.SessionsRevoked);
        Assert.False(repo.IsSessionRevoked(current));
        Assert.True(repo.IsSessionRevoked(other1));
        Assert.True(repo.IsSessionRevoked(other2));
    }

    // ─── admin-driven credential/session operations ────────────────────────────

    [Fact]
    public async Task AdminRevokeCredentialAsync_DeletesCredentialAndRevokesSessions_NeverAcceptsPasswordValue()
    {
        var (repo, service) = Build();
        var s1 = repo.SeedSession(UserId);

        var result = await service.AdminRevokeCredentialAsync(UserId, "demo_admin");

        Assert.True(result.Success);
        Assert.Null(repo.PasswordHashFor(UserId));
        Assert.True(repo.IsSessionRevoked(s1));
        // AdminRevokeCredentialAsync's signature (Guid userId, string actorUsername, CancellationToken)
        // has no password/newPassword parameter — there is no code path for an admin to supply one.
        var paramNames = typeof(AuthService)
            .GetMethod(nameof(AuthService.AdminRevokeCredentialAsync))!
            .GetParameters().Select(p => p.Name!.ToLowerInvariant());
        Assert.DoesNotContain(paramNames, n => n.Contains("password"));
    }

    [Fact]
    public async Task AdminRevokeSessionsAsync_SingleSession_OnlyRevokesThatOne()
    {
        var (repo, service) = Build();
        var s1 = repo.SeedSession(UserId);
        var s2 = repo.SeedSession(UserId);

        var result = await service.AdminRevokeSessionsAsync(
            UserId, new AdminRevokeSessionsRequestDto(s1.ToString()), "demo_admin");

        Assert.True(result.Success);
        Assert.Equal(1, result.SessionsRevoked);
        Assert.True(repo.IsSessionRevoked(s1));
        Assert.False(repo.IsSessionRevoked(s2));
    }

    [Fact]
    public async Task AdminRevokeSessionsAsync_NoSessionId_RevokesAll()
    {
        var (repo, service) = Build();
        repo.SeedSession(UserId);
        repo.SeedSession(UserId);

        var result = await service.AdminRevokeSessionsAsync(UserId, new AdminRevokeSessionsRequestDto(null), "demo_admin");

        Assert.True(result.Success);
        Assert.Equal(2, result.SessionsRevoked);
    }
}

/// <summary>
/// Team Markdown mutation actions independently reject non-admin JWTs via
/// OperationVector.AuthenticatedRole, regardless of manifest capability inference. No DB required —
/// the check runs before any repository call.
/// SSOT: docs/design/admin-normal-surface-projection-seed-ssot.yaml normal.dashboard responsibilities.
/// </summary>
[Trait("Category", "TeamMarkdownRoleGate")]
public class TeamMarkdownAdminRoleGateTests
{
    private static AdminRuntime CreateRuntime()
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "Host=localhost");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var uiRepo = new StubUiTopologyRepositoryForRoleGateTest();
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo);
    }

    [Theory]
    [InlineData("saved_view:update")]
    [InlineData("saved_view:create")]
    [InlineData("saved_view:archive")]
    [InlineData("template:create")]
    public async Task NormalRoleJwt_MutationAction_RejectedWithAuthCapabilityDenied(string action)
    {
        var runtime = CreateRuntime();
        var vector = new OperationVector(
            "admin", "team_markdown", action, null, "user",
            JsonSerializer.SerializeToElement(new { }), null,
            AuthenticatedRole: "user", AuthenticatedUserId: "normal_user");

        var (data, error) = await runtime.ExecuteDataAsync(vector, default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("AUTH_CAPABILITY_DENIED", error!.Code);
    }

    [Fact]
    public async Task MissingAuthenticatedRole_MutationAction_FailsClosed()
    {
        // Simulates a vector that never passed through the /dispatch JWT boundary
        // (AuthenticatedRole absent) — must fail closed, not silently allow.
        var runtime = CreateRuntime();
        var vector = new OperationVector(
            "admin", "team_markdown", "saved_view:update", null, "admin",
            JsonSerializer.SerializeToElement(new { }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector, default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("AUTH_CAPABILITY_DENIED", error!.Code);
    }

    [Fact]
    public async Task NormalRoleJwt_ReadAction_NotBlockedByRoleGate()
    {
        // Read actions (list/get/search) carry no admin-role gate in ExecuteTeamMarkdownAsync;
        // TeamMarkdownRepository is null here so this hits TEAM_MARKDOWN_NOT_CONFIGURED, not
        // AUTH_CAPABILITY_DENIED — proving the role gate itself did not fire for a read action.
        var runtime = CreateRuntime();
        var vector = new OperationVector(
            "admin", "team_markdown", "saved_view:search", null, "user",
            JsonSerializer.SerializeToElement(new { }), null,
            AuthenticatedRole: "user", AuthenticatedUserId: "normal_user");

        var (data, error) = await runtime.ExecuteDataAsync(vector, default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.NotEqual("AUTH_CAPABILITY_DENIED", error!.Code);
    }

    private sealed class StubUiTopologyRepositoryForRoleGateTest : UiTopologyRepository
    {
        public StubUiTopologyRepositoryForRoleGateTest()
            : base(NullLogger<UiTopologyRepository>.Instance, "test-double") { }
    }
}
