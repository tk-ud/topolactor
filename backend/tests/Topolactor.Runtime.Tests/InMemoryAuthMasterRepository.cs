using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

public sealed class InMemoryAuthMasterRepository : AuthMasterRepository
{
    private readonly Dictionary<Guid, AuthUserRosterDto> _users = new();

    public InMemoryAuthMasterRepository(IEnumerable<AuthUserRosterDto>? seed = null)
        : base("in-memory")
    {
        if (seed is not null)
            foreach (var u in seed)
                _users[u.UserId] = u;
    }

    public override Task<IReadOnlyList<AuthUserRosterDto>> ListUsersAsync(
        string? query, CancellationToken ct = default)
    {
        var list = _users.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            list = list.Where(u =>
                u.Username.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (u.Status?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return Task.FromResult<IReadOnlyList<AuthUserRosterDto>>(list.OrderBy(u => u.Username).ToList());
    }

    public override Task<AuthUserRosterDto?> GetUserAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_users.TryGetValue(userId, out var u) ? u : null);

    public override Task<AuthUserRosterDto> CreateUserAsync(
        string username,
        string passwordHash,
        bool approve,
        string? status,
        string roleName,
        string realm,
        DateTimeOffset? suspendedFrom,
        DateTimeOffset? suspendedUntil,
        string? stateNote,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new AuthUserRosterDto(
            Guid.NewGuid(),
            username,
            true,
            approve,
            status,
            suspendedFrom,
            suspendedUntil,
            stateNote,
            now,
            now,
            null);
        _users[user.UserId] = user;
        return Task.FromResult(user);
    }

    public override Task<AuthUserRosterDto?> UpdateUserAsync(
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
        CancellationToken ct = default)
    {
        if (!_users.TryGetValue(userId, out var existing)) return Task.FromResult<AuthUserRosterDto?>(null);
        var updated = existing with
        {
            Username = username ?? existing.Username,
            Active = active ?? existing.Active,
            Approve = approve ?? existing.Approve,
            Status = status ?? existing.Status,
            SuspendedFrom = clearSuspendedFrom ? null : suspendedFrom ?? existing.SuspendedFrom,
            SuspendedUntil = clearSuspendedUntil ? null : suspendedUntil ?? existing.SuspendedUntil,
            StateNote = stateNote ?? existing.StateNote,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _users[userId] = updated;
        return Task.FromResult<AuthUserRosterDto?>(updated);
    }

    public override Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_users.Remove(userId));

    public override Task<bool> UsernameExistsAsync(
        string username, Guid? excludeUserId, CancellationToken ct = default) =>
        Task.FromResult(_users.Values.Any(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) &&
            (!excludeUserId.HasValue || u.UserId != excludeUserId.Value)));
}
