using System.Text.Json;

namespace Topolactor.Schema;

public sealed record CliReaderPortConfig(
    string PortKey,
    Guid PortId,
    bool Enabled,
    DateTimeOffset? ExpiresAt,
    IReadOnlySet<string> AllowedRoles,
    IReadOnlySet<string> AllowedUsers,
    IReadOnlySet<string> AllowedTables,
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


public sealed record CreateCliReaderExportJobCommand(
    AuthorizedCliReaderQuery Query,
    Guid PortId,
    string ExportFormat,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    IReadOnlyList<string> SourceRecordIds,
    string IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record CliReaderExportJobResult(
    Guid ExportJobId,
    string Status,
    string ExportFormat,
    IReadOnlyList<string> SourceRecordIds,
    IReadOnlyList<CliReaderGeneratedFile> GeneratedFiles,
    string Checksum,
    string ManifestPath,
    JsonElement Manifest);

public sealed record CliReaderGeneratedFile(
    string FileName,
    string Format,
    long ByteSize,
    string Checksum,
    string ContentRef);
