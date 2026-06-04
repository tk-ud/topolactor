using Topolactor.Schema;

namespace Topolactor.Repository;

public abstract class AuthMasterRepository
{
    protected readonly string _connectionString;

    protected AuthMasterRepository(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public abstract Task<IReadOnlyList<AuthUserRosterDto>> ListUsersAsync(
        string? query, CancellationToken ct = default);

    public abstract Task<AuthUserRosterDto?> GetUserAsync(Guid userId, CancellationToken ct = default);

    public abstract Task<AuthUserRosterDto> CreateUserAsync(
        string username,
        string passwordHash,
        bool approve,
        string? status,
        string roleName,
        string realm,
        DateTimeOffset? suspendedFrom,
        DateTimeOffset? suspendedUntil,
        string? stateNote,
        CancellationToken ct = default);

    public abstract Task<AuthUserRosterDto?> UpdateUserAsync(
        Guid userId,
        string? username,
        bool? active,
        bool? approve,
        string? status,
        DateTimeOffset? suspendedFrom,
        DateTimeOffset? suspendedUntil,
        bool clearSuspendedFrom,
        bool clearSuspendedUntil,
        string? stateNote,
        CancellationToken ct = default);

    public abstract Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default);

    public abstract Task<bool> UsernameExistsAsync(string username, Guid? excludeUserId, CancellationToken ct = default);
}
