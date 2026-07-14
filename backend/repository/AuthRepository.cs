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

public record AuthSessionRecord(
    Guid SessionId,
    string Realm,
    string Audience,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    bool Revoked);

/// <summary>Result of a self-service password change — never carries password material.</summary>
public enum ChangeOwnPasswordOutcome
{
    Success,
    CredentialNotFound,
    CurrentPasswordInvalid,
}

public record ChangeOwnPasswordResult(ChangeOwnPasswordOutcome Outcome, int SessionsRevoked);

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

    public abstract Task<AuthUserRecord?> GetUserStateByIdAsync(
        Guid userId, CancellationToken ct = default);

    public abstract Task<AuthRefreshTokenRecord?> FindActiveRefreshTokenByHashAsync(
        string tokenHash, CancellationToken ct = default);

    public abstract Task RevokeRefreshTokenAsync(
        Guid refreshTokenId, CancellationToken ct = default);

    public abstract Task RevokeSessionAsync(
        Guid sessionId, CancellationToken ct = default);

    public abstract Task InsertLoginEventAsync(
        Guid? userId, string realm, bool success, string? failureCode, CancellationToken ct = default);

    /// <summary>
    /// Self-service password change completion boundary: verifies currentPasswordPlain against the
    /// stored hash, updates the credential hash, and revokes every active session for the user, all
    /// in one transaction. Also appends non-secret evidence (no password material) into logs.diff in
    /// the same transaction — this is the durable completion proof, not a best-effort side call.
    /// </summary>
    public abstract Task<ChangeOwnPasswordResult> ChangeOwnPasswordAsync(
        Guid userId, string currentPasswordPlain, string newPasswordHash, string actorUsername,
        CancellationToken ct = default);

    /// <summary>Active (non-revoked) sessions for a user, newest first.</summary>
    public abstract Task<IReadOnlyList<AuthSessionRecord>> ListActiveSessionsByUserAsync(
        Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Revokes one session, but only when it belongs to userId — ownership-checked so a caller can
    /// never revoke a session belonging to a different account by guessing/supplying a sessionId.
    /// Returns false when the session does not exist or does not belong to userId.
    /// </summary>
    public abstract Task<bool> RevokeOwnedSessionAsync(
        Guid userId, Guid sessionId, string actorUsername, CancellationToken ct = default);

    /// <summary>
    /// Revokes every active session for userId except exceptSessionId (when supplied). Returns the
    /// number of sessions revoked. Used both for "revoke other sessions" (self, exceptSessionId =
    /// caller's current session) and admin-driven full session revoke (exceptSessionId = null).
    /// </summary>
    public abstract Task<int> RevokeSessionsForUserAsync(
        Guid userId, Guid? exceptSessionId, string actorUsername, CancellationToken ct = default);

    /// <summary>
    /// Resolves the session_id bound to an active refresh token hash, without exposing the refresh
    /// token itself. Used to identify "the caller's current session" from the refresh-token cookie.
    /// </summary>
    public abstract Task<Guid?> FindActiveSessionIdByRefreshTokenHashAsync(
        string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Admin-driven credential revoke: deletes the user's auth.credentials row (login becomes
    /// impossible until a new credential is provisioned) and revokes every active session, in one
    /// transaction, with non-secret evidence appended in the same transaction. Never accepts or
    /// stores a replacement password — this is a kill-switch, not a password reset.
    /// </summary>
    public abstract Task<bool> RevokeCredentialAsync(
        Guid userId, string actorUsername, CancellationToken ct = default);

    /// <summary>
    /// The single DB-side authority JwtGuard consults to decide whether an access token's
    /// revocation identity (the sid claim) still backs a usable session: the session row must
    /// exist, be non-revoked, non-expired, and the owning account must currently be active,
    /// approved, and outside any suspension window (the same account-state gate LoginAsync/
    /// RefreshAsync already enforce). This makes password change, session revoke, credential
    /// revoke, and account deactivation all take effect on already-issued access JWTs immediately,
    /// without waiting for token expiry or a refresh.
    /// </summary>
    public abstract Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Full identity-aware session check: everything IsSessionActiveAsync checks, PLUS that the
    /// session's owning account's username matches <paramref name="username"/> and the session's
    /// own realm/audience match <paramref name="realm"/>/<paramref name="audience"/>. This is what
    /// JwtGuard calls — session-active alone is not sufficient, because a signed JWT's sub/realm/aud
    /// claims must never be trusted without verifying them against the canonical session/user
    /// identity the token's sid actually points to. username/realm/audience here are the JWT's own
    /// claims (untrusted input); this call is what makes them trustworthy or not.
    /// </summary>
    public abstract Task<bool> IsSessionIdentityActiveAsync(
        Guid sessionId, string username, string realm, string audience, CancellationToken ct = default);
}
