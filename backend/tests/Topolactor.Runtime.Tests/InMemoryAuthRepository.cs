using Topolactor.Repository;

namespace Topolactor.Runtime.Tests;

public sealed class InMemorySession
{
    public Guid SessionId { get; init; }
    public Guid UserId { get; init; }
    public string Realm { get; init; } = "";
    public string Audience { get; init; } = "";
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool Revoked { get; set; }
}

public sealed class InMemoryAuthRepository : AuthRepository
{
    private readonly Dictionary<Guid, AuthUserRecord> _users = new();
    private readonly Dictionary<Guid, string> _passwords = new();
    private readonly List<(Guid UserId, string Role, string Realm)> _grants = new();
    private readonly Dictionary<Guid, InMemorySession> _sessions = new();
    private readonly Dictionary<string, Guid> _refreshTokenHashToSession = new();

    public InMemoryAuthRepository() : base("in-memory") { }

    public void SeedUser(AuthUserRecord user, string passwordHash)
    {
        _users[user.UserId] = user;
        _passwords[user.UserId] = passwordHash;
    }

    public void SeedGrant(Guid userId, string role, string realm) =>
        _grants.Add((userId, role, realm));

    public Guid SeedSession(Guid userId, string realm = "user", string audience = "user_app", string? refreshTokenHash = null)
    {
        var sessionId = Guid.NewGuid();
        _sessions[sessionId] = new InMemorySession
        {
            SessionId = sessionId,
            UserId = userId,
            Realm = realm,
            Audience = audience,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        };
        if (refreshTokenHash is not null) _refreshTokenHashToSession[refreshTokenHash] = sessionId;
        return sessionId;
    }

    public bool IsSessionRevoked(Guid sessionId) => _sessions.TryGetValue(sessionId, out var s) && s.Revoked;

    public string? PasswordHashFor(Guid userId) => _passwords.TryGetValue(userId, out var h) ? h : null;

    public override Task<AuthUserRecord?> FindUserByUsernameAsync(string username, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public override Task<string?> GetPasswordHashAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_passwords.TryGetValue(userId, out var h) ? h : null);

    public override Task<AuthUserRecord> CreatePendingUserWithCredentialAsync(
        string username, string passwordHash, CancellationToken ct = default)
    {
        var user = new AuthUserRecord(Guid.NewGuid(), username, Active: true, Approve: false, Status: "active");
        _users[user.UserId] = user;
        _passwords[user.UserId] = passwordHash;
        _grants.Add((user.UserId, "user", "user"));
        return Task.FromResult(user);
    }

    public override Task<string?> GetGrantRoleForRealmAsync(Guid userId, string realm, CancellationToken ct = default)
    {
        var grant = _grants.FirstOrDefault(g => g.UserId == userId && g.Realm == realm);
        return Task.FromResult(grant.UserId == userId ? grant.Role : null);
    }

    public override Task<Guid> CreateSessionAsync(
        Guid userId, string realm, string audience, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid();
        _sessions[sessionId] = new InMemorySession
        {
            SessionId = sessionId,
            UserId = userId,
            Realm = realm,
            Audience = audience,
            ExpiresAt = expiresAt,
        };
        return Task.FromResult(sessionId);
    }

    public override Task<Guid> CreateRefreshTokenAsync(
        Guid sessionId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default) =>
        Task.FromResult(Guid.NewGuid());

    public override Task<AuthUserRecord?> GetUserStateByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_users.TryGetValue(userId, out var u) ? u : null);

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

    public override Task<ChangeOwnPasswordResult> ChangeOwnPasswordAsync(
        Guid userId, string currentPasswordPlain, string newPasswordHash, string actorUsername,
        CancellationToken ct = default)
    {
        if (!_passwords.TryGetValue(userId, out var currentHash))
            return Task.FromResult(new ChangeOwnPasswordResult(ChangeOwnPasswordOutcome.CredentialNotFound, 0));

        if (!BCrypt.Net.BCrypt.Verify(currentPasswordPlain, currentHash))
            return Task.FromResult(new ChangeOwnPasswordResult(ChangeOwnPasswordOutcome.CurrentPasswordInvalid, 0));

        _passwords[userId] = newPasswordHash;
        var revoked = 0;
        foreach (var session in _sessions.Values.Where(s => s.UserId == userId && !s.Revoked))
        {
            session.Revoked = true;
            revoked++;
        }
        return Task.FromResult(new ChangeOwnPasswordResult(ChangeOwnPasswordOutcome.Success, revoked));
    }

    public override Task<IReadOnlyList<AuthSessionRecord>> ListActiveSessionsByUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        IReadOnlyList<AuthSessionRecord> result = _sessions.Values
            .Where(s => s.UserId == userId && !s.Revoked)
            .Select(s => new AuthSessionRecord(s.SessionId, s.Realm, s.Audience, s.ExpiresAt, s.CreatedAt, s.Revoked))
            .ToList();
        return Task.FromResult(result);
    }

    public override Task<bool> RevokeOwnedSessionAsync(
        Guid userId, Guid sessionId, string actorUsername, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session) || session.UserId != userId || session.Revoked)
            return Task.FromResult(false);
        session.Revoked = true;
        return Task.FromResult(true);
    }

    public override Task<int> RevokeSessionsForUserAsync(
        Guid userId, Guid? exceptSessionId, string actorUsername, CancellationToken ct = default)
    {
        var count = 0;
        foreach (var session in _sessions.Values.Where(s =>
            s.UserId == userId && !s.Revoked && (exceptSessionId is null || s.SessionId != exceptSessionId.Value)))
        {
            session.Revoked = true;
            count++;
        }
        return Task.FromResult(count);
    }

    public override Task<Guid?> FindActiveSessionIdByRefreshTokenHashAsync(
        string tokenHash, CancellationToken ct = default)
    {
        if (_refreshTokenHashToSession.TryGetValue(tokenHash, out var sessionId) &&
            _sessions.TryGetValue(sessionId, out var session) && !session.Revoked)
            return Task.FromResult<Guid?>(sessionId);
        return Task.FromResult<Guid?>(null);
    }

    public override Task<bool> RevokeCredentialAsync(
        Guid userId, string actorUsername, CancellationToken ct = default)
    {
        if (!_passwords.Remove(userId)) return Task.FromResult(false);
        foreach (var session in _sessions.Values.Where(s => s.UserId == userId && !s.Revoked))
            session.Revoked = true;
        return Task.FromResult(true);
    }

    public override Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session)) return Task.FromResult(false);
        if (session.Revoked || session.ExpiresAt <= DateTimeOffset.UtcNow) return Task.FromResult(false);
        if (!_users.TryGetValue(session.UserId, out var user)) return Task.FromResult(false);
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
