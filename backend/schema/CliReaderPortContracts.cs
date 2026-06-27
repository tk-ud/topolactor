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
    IReadOnlySet<string> AllowedBusinessObjects,
    IReadOnlySet<string> AllowedAssignmentTargetScopes,
    bool AuditRequired,
    int? RateLimitPerMinute = null,
    bool FileStreamEnabled = false);

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

// File stream port: lookup key for an already-authorized export job file.
// Authorization is enforced server-side by matching the dispatch-resolved port_id
// and the authenticated requesting user against the existing export_jobs ledger.
public sealed record LoadAuthorizedExportFileQuery(
    Guid PortId,
    string UserId,
    Guid ExportJobId);

// Authorized export file projection assembled from the canonical export_jobs ledger
// joined with the export_manifests record. Never carries credential / bucket /
// endpoint / actual signed URL values.
public sealed record AuthorizedExportFile(
    Guid ExportJobId,
    string Status,
    string ExportFormat,
    string? Period,
    string GeneratedBy,
    DateTimeOffset GeneratedAt,
    bool ApprovalRequired,
    string ApprovalStatus,
    IReadOnlyList<string> SourceRecordIds,
    IReadOnlyList<CliReaderGeneratedFile> GeneratedFiles,
    string JobChecksum,
    bool ManifestPresent,
    string ManifestVersion,
    string ManifestChecksum,
    IReadOnlyList<string> ManifestSourceRecordIds,
    JsonElement ManifestJsonb);


public sealed record CreateCliReaderImportCandidateCommand(
    Guid PortId,
    string PortKey,
    string Operation,
    string CandidateKind,
    string RequestedBy,
    string? SourceTranscriptRef,
    string? RootUtterance,
    JsonElement StructuredOutputPayload,
    decimal? Confidence,
    JsonElement UnresolvedFields,
    JsonElement PreviewDiff,
    JsonElement AssignedBusinessObjectCandidate,
    Guid? DraftOperationId,
    JsonElement? AssignedBusinessObject,
    JsonElement? AssignmentTargetScope,
    JsonElement EvidenceRefs,
    string ApprovalStatus,
    string Status,
    string IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record CliReaderImportCandidateResult(
    Guid CandidateId,
    string CandidateKind,
    string Status,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    string IdempotencyKey,
    string EvidenceUri,
    JsonElement PreviewDiff,
    JsonElement UnresolvedFields,
    JsonElement AssignedBusinessObjectCandidate,
    Guid? DraftOperationId,
    JsonElement? AssignedBusinessObject,
    JsonElement? AssignmentTargetScope,
    JsonElement EvidenceRefs,
    decimal? Confidence,
    string? SourceTranscriptRef,
    string? RootUtterance,
    string ApprovalStatus = "not_requested",
    bool WasCreated = true);

public sealed record LoadCliReaderImportCandidateQuery(
    Guid PortId,
    string UserId,
    Guid CandidateId);
