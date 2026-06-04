using Topolactor.Repository;
using Topolactor.Service;

namespace Topolactor.Integration.Tests;

internal sealed class FakeAuthRepository : AuthRepository
{
    private readonly Dictionary<string, (Guid UserId, string PasswordHash, string Role, string Realm)> _users;

    public FakeAuthRepository(
        IEnumerable<(string Username, Guid UserId, string PasswordHash, string Role, string Realm)> users)
        : base("fake")
    {
        _users = users.ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase);
    }

    public override Task<AuthUserRecord?> FindUserByUsernameAsync(string username, CancellationToken ct = default)
    {
        if (!_users.TryGetValue(username, out var u)) return Task.FromResult<AuthUserRecord?>(null);
        return Task.FromResult<AuthUserRecord?>(new AuthUserRecord(u.UserId, username));
    }

    public override Task<string?> GetPasswordHashAsync(Guid userId, CancellationToken ct = default)
    {
        var entry = _users.FirstOrDefault(kv => kv.Value.UserId == userId);
        return Task.FromResult(entry.Key is null ? null : entry.Value.PasswordHash);
    }

    public override Task<string?> GetGrantRoleForRealmAsync(Guid userId, string realm, CancellationToken ct = default)
    {
        var entry = _users.FirstOrDefault(kv =>
            kv.Value.UserId == userId && kv.Value.Realm == realm);
        return Task.FromResult(entry.Key is null ? null : entry.Value.Role);
    }

    public override Task<Guid> CreateSessionAsync(
        Guid userId, string realm, string audience, DateTimeOffset expiresAt, CancellationToken ct = default) =>
        Task.FromResult(Guid.NewGuid());

    public override Task<Guid> CreateRefreshTokenAsync(
        Guid sessionId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default) =>
        Task.FromResult(Guid.NewGuid());

    private readonly Dictionary<string, AuthRefreshTokenRecord> _refresh = new();

    public void SeedRefreshToken(string plain, AuthRefreshTokenRecord record) =>
        _refresh[JwtTokenIssuer.HashRefreshToken(plain)] = record;

    public override Task<AuthRefreshTokenRecord?> FindActiveRefreshTokenByHashAsync(
        string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(_refresh.TryGetValue(tokenHash, out var r) ? r : null);

    public override Task RevokeRefreshTokenAsync(Guid refreshTokenId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public override Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public override Task InsertLoginEventAsync(
        Guid? userId, string realm, bool success, string? failureCode, CancellationToken ct = default) =>
        Task.CompletedTask;
}
