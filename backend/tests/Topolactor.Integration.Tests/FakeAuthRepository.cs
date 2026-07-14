using Topolactor.Repository;
using Topolactor.Service;

namespace Topolactor.Integration.Tests;

internal sealed class FakeAuthRepository : AuthRepository
{
    private readonly Dictionary<string, (Guid UserId, string PasswordHash, string Role, string Realm)> _users;
    public AuthUserRecord? LastCreatedPendingUser { get; private set; }
    public string? LastCreatedPasswordHash { get; private set; }

    public FakeAuthRepository(
        IEnumerable<(string Username, Guid UserId, string PasswordHash, string Role, string Realm)> users)
        : base("fake")
    {
        _users = users.ToDictionary(
            u => u.Username,
            u => (u.UserId, u.PasswordHash, u.Role, u.Realm),
            StringComparer.OrdinalIgnoreCase);
    }

    public override Task<AuthUserRecord?> FindUserByUsernameAsync(string username, CancellationToken ct = default)
    {
        if (!_users.TryGetValue(username, out var u)) return Task.FromResult<AuthUserRecord?>(null);
        return Task.FromResult<AuthUserRecord?>(new AuthUserRecord(
            u.UserId, username, Active: true, Approve: true, Status: "active"));
    }

    public override Task<string?> GetPasswordHashAsync(Guid userId, CancellationToken ct = default)
    {
        var entry = _users.FirstOrDefault(kv => kv.Value.UserId == userId);
        return Task.FromResult(entry.Key is null ? null : entry.Value.PasswordHash);
    }

    public override Task<AuthUserRecord> CreatePendingUserWithCredentialAsync(
        string username, string passwordHash, CancellationToken ct = default)
    {
        var userId = Guid.NewGuid();
        _users[username] = (userId, passwordHash, "user", "user");
        LastCreatedPasswordHash = passwordHash;
        LastCreatedPendingUser = new AuthUserRecord(userId, username, Active: true, Approve: false, Status: "active");
        return Task.FromResult(LastCreatedPendingUser);
    }

    public override Task<string?> GetGrantRoleForRealmAsync(Guid userId, string realm, CancellationToken ct = default)
    {
        var entry = _users.FirstOrDefault(kv =>
            kv.Value.UserId == userId && kv.Value.Realm == realm);
        return Task.FromResult(entry.Key is null ? null : entry.Value.Role);
    }

    private readonly Dictionary<Guid, (Guid UserId, DateTimeOffset ExpiresAt)> _liveSessions = new();

    public override Task<Guid> CreateSessionAsync(
        Guid userId, string realm, string audience, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid();
        _liveSessions[sessionId] = (userId, expiresAt);
        return Task.FromResult(sessionId);
    }

    public override Task<Guid> CreateRefreshTokenAsync(
        Guid sessionId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default) =>
        Task.FromResult(Guid.NewGuid());

    private readonly Dictionary<Guid, AuthUserRecord> _userStates = new();

    public void SetUserState(AuthUserRecord record) =>
        _userStates[record.UserId] = record;

    public override Task<AuthUserRecord?> GetUserStateByIdAsync(Guid userId, CancellationToken ct = default)
    {
        if (_userStates.TryGetValue(userId, out var r)) return Task.FromResult<AuthUserRecord?>(r);
        var entry = _users.FirstOrDefault(kv => kv.Value.UserId == userId);
        if (entry.Key is null) return Task.FromResult<AuthUserRecord?>(null);
        return Task.FromResult<AuthUserRecord?>(new AuthUserRecord(
            entry.Value.UserId, entry.Key, Active: true, Approve: true, Status: "active"));
    }

    private readonly Dictionary<string, AuthRefreshTokenRecord> _refresh = new();

    public void SeedRefreshToken(string plain, AuthRefreshTokenRecord record) =>
        _refresh[JwtTokenIssuer.HashRefreshToken(plain)] = record;

    public override Task<AuthRefreshTokenRecord?> FindActiveRefreshTokenByHashAsync(
        string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(_refresh.TryGetValue(tokenHash, out var r) ? r : null);

    public override Task RevokeRefreshTokenAsync(Guid refreshTokenId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public override Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        _sessionRevoked[sessionId] = true;
        return Task.CompletedTask;
    }

    public override Task InsertLoginEventAsync(
        Guid? userId, string realm, bool success, string? failureCode, CancellationToken ct = default) =>
        Task.CompletedTask;

    private readonly Dictionary<Guid, bool> _sessionRevoked = new();

    public override Task<ChangeOwnPasswordResult> ChangeOwnPasswordAsync(
        Guid userId, string currentPasswordPlain, string newPasswordHash, string actorUsername,
        CancellationToken ct = default)
    {
        var entry = _users.FirstOrDefault(kv => kv.Value.UserId == userId);
        if (entry.Key is null)
            return Task.FromResult(new ChangeOwnPasswordResult(ChangeOwnPasswordOutcome.CredentialNotFound, 0));
        if (!BCrypt.Net.BCrypt.Verify(currentPasswordPlain, entry.Value.PasswordHash))
            return Task.FromResult(new ChangeOwnPasswordResult(ChangeOwnPasswordOutcome.CurrentPasswordInvalid, 0));
        _users[entry.Key] = entry.Value with { PasswordHash = newPasswordHash };
        return Task.FromResult(new ChangeOwnPasswordResult(ChangeOwnPasswordOutcome.Success, 0));
    }

    public override Task<IReadOnlyList<AuthSessionRecord>> ListActiveSessionsByUserAsync(
        Guid userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AuthSessionRecord>>(Array.Empty<AuthSessionRecord>());

    public override Task<bool> RevokeOwnedSessionAsync(
        Guid userId, Guid sessionId, string actorUsername, CancellationToken ct = default)
    {
        _sessionRevoked[sessionId] = true;
        return Task.FromResult(true);
    }

    public override Task<int> RevokeSessionsForUserAsync(
        Guid userId, Guid? exceptSessionId, string actorUsername, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<Guid?> FindActiveSessionIdByRefreshTokenHashAsync(
        string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(_refresh.TryGetValue(tokenHash, out var r) ? (Guid?)r.RefreshTokenId : null);

    public override Task<bool> RevokeCredentialAsync(
        Guid userId, string actorUsername, CancellationToken ct = default)
    {
        var entry = _users.FirstOrDefault(kv => kv.Value.UserId == userId);
        if (entry.Key is null) return Task.FromResult(false);
        _users.Remove(entry.Key);
        foreach (var sessionId in _liveSessions.Where(kv => kv.Value.UserId == userId).Select(kv => kv.Key).ToList())
            _sessionRevoked[sessionId] = true;
        return Task.FromResult(true);
    }

    public override Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_liveSessions.TryGetValue(sessionId, out var session)) return Task.FromResult(false);
        if (_sessionRevoked.TryGetValue(sessionId, out var revoked) && revoked) return Task.FromResult(false);
        if (session.ExpiresAt <= DateTimeOffset.UtcNow) return Task.FromResult(false);

        AuthUserRecord? user = _userStates.TryGetValue(session.UserId, out var state) ? state : null;
        if (user is null)
        {
            var entry = _users.FirstOrDefault(kv => kv.Value.UserId == session.UserId);
            if (entry.Key is not null)
                user = new AuthUserRecord(entry.Value.UserId, entry.Key, Active: true, Approve: true, Status: "active");
        }
        if (user is null) return Task.FromResult(false);
        if (!user.Active || !user.Approve) return Task.FromResult(false);
        if (string.Equals(user.Status, "suspended", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(false);
        if (user.SuspendedFrom.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            if (user.SuspendedFrom.Value <= now && (user.SuspendedUntil is null || now <= user.SuspendedUntil.Value))
                return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }
}
