using Topolactor.Repository;

namespace Topolactor.Runtime.Tests;

public sealed class InMemoryAuthRepository : AuthRepository
{
    private readonly Dictionary<Guid, AuthUserRecord> _users = new();
    private readonly Dictionary<Guid, string> _passwords = new();
    private readonly List<(Guid UserId, string Role, string Realm)> _grants = new();

    public InMemoryAuthRepository() : base("in-memory") { }

    public void SeedUser(AuthUserRecord user, string passwordHash)
    {
        _users[user.UserId] = user;
        _passwords[user.UserId] = passwordHash;
    }

    public void SeedGrant(Guid userId, string role, string realm) =>
        _grants.Add((userId, role, realm));

    public override Task<AuthUserRecord?> FindUserByUsernameAsync(string username, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public override Task<string?> GetPasswordHashAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_passwords.TryGetValue(userId, out var h) ? h : null);

    public override Task<string?> GetGrantRoleForRealmAsync(Guid userId, string realm, CancellationToken ct = default)
    {
        var grant = _grants.FirstOrDefault(g => g.UserId == userId && g.Realm == realm);
        return Task.FromResult(grant.UserId == userId ? grant.Role : null);
    }

    public override Task<Guid> CreateSessionAsync(
        Guid userId, string realm, string audience, DateTimeOffset expiresAt, CancellationToken ct = default) =>
        Task.FromResult(Guid.NewGuid());

    public override Task<Guid> CreateRefreshTokenAsync(
        Guid sessionId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default) =>
        Task.FromResult(Guid.NewGuid());

    public override Task<AuthRefreshTokenRecord?> FindActiveRefreshTokenByHashAsync(
        string tokenHash, CancellationToken ct = default) =>
        Task.FromResult<AuthRefreshTokenRecord?>(null);

    public override Task RevokeRefreshTokenAsync(Guid refreshTokenId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public override Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public override Task InsertLoginEventAsync(
        Guid? userId, string realm, bool success, string? failureCode, CancellationToken ct = default) =>
        Task.CompletedTask;
}
