namespace Topolactor.Repository;

public record AuthUserRecord(
    Guid UserId,
    string Username,
    bool Active = true,
    bool Approve = false,
    string? Status = null,
    DateTimeOffset? SuspendedFrom = null,
    DateTimeOffset? SuspendedUntil = null);

public record AuthRefreshTokenRecord(
    Guid RefreshTokenId,
    Guid SessionId,
    Guid UserId,
    string Username,
    string Realm,
    string Audience,
    string Role);

public abstract class AuthRepository
{
    protected readonly string _connectionString;

    protected AuthRepository(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public abstract Task<AuthUserRecord?> FindUserByUsernameAsync(
        string username, CancellationToken ct = default);

    public abstract Task<string?> GetPasswordHashAsync(
        Guid userId, CancellationToken ct = default);

    public abstract Task<AuthUserRecord> CreatePendingUserWithCredentialAsync(
        string username, string passwordHash, CancellationToken ct = default);

    public abstract Task<string?> GetGrantRoleForRealmAsync(
        Guid userId, string realm, CancellationToken ct = default);

    public abstract Task<Guid> CreateSessionAsync(
        Guid userId, string realm, string audience, DateTimeOffset expiresAt, CancellationToken ct = default);

    public abstract Task<Guid> CreateRefreshTokenAsync(
        Guid sessionId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default);

    public abstract Task<AuthRefreshTokenRecord?> FindActiveRefreshTokenByHashAsync(
        string tokenHash, CancellationToken ct = default);

    public abstract Task RevokeRefreshTokenAsync(
        Guid refreshTokenId, CancellationToken ct = default);

    public abstract Task RevokeSessionAsync(
        Guid sessionId, CancellationToken ct = default);

    public abstract Task InsertLoginEventAsync(
        Guid? userId, string realm, bool success, string? failureCode, CancellationToken ct = default);
}
