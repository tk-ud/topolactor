namespace Topolactor.Schema;

public sealed record CliReaderPortConfig(
    string PortKey,
    bool Enabled,
    DateTimeOffset? ExpiresAt,
    IReadOnlySet<string> AllowedRoles,
    IReadOnlySet<string> AllowedUsers,
    IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedColumnsByTable,
    IReadOnlySet<string> AllowedFilters,
    IReadOnlySet<string> AllowedPeriods,
    IReadOnlyDictionary<string, string> RowScopeByUser,
    IReadOnlySet<string> RequiredCapabilities,
    bool AuditRequired,
    int? RateLimitPerMinute = null);

public sealed record AuthorizedCliReaderQuery(
    string PortKey,
    string Operation,
    string UserId,
    IReadOnlySet<string> Roles,
    string Table,
    IReadOnlyList<string> Columns,
    IReadOnlyDictionary<string, string> Filters,
    string? Period,
    string? RowScope,
    string? RequestId,
    string? IdempotencyKey);

public sealed record CliReaderPortRuntimeEvent(
    string PortKey,
    string Operation,
    string? UserId,
    IReadOnlyList<string> Roles,
    string Status,
    string Code,
    string? RequestId,
    string? IdempotencyKey,
    string ScopeSummary,
    DateTimeOffset ObservedAt);
