namespace Topolactor.Schema;

public sealed record ExportJobRecord(
    Guid ExportJobId,
    Guid? PortId,
    string PortKind,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    string? Period,
    string? TargetScope,
    string? ExportFormat,
    string Status,
    string IdempotencyKey,
    string? Checksum,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    string? FailureCode);

public sealed record FileArtifactRecord(
    Guid FileArtifactId,
    Guid ExportJobId,
    string FileName,
    string FileType,
    string StorageRef,
    long? ByteSize,
    Guid? ChecksumRecordId);

public sealed record FileChecksumRecord(
    Guid ChecksumRecordId,
    Guid ExportJobId,
    Guid? FileArtifactId,
    string Algorithm,
    string ChecksumValue,
    DateTimeOffset VerifiedAt,
    string VerificationStatus);

public sealed record ExportManifestRecord(
    Guid ManifestId,
    Guid ExportJobId,
    string ManifestVersion,
    DateTimeOffset GeneratedAt,
    string GeneratedBy,
    string? Period,
    string? ExportFormat,
    string? Checksum,
    IReadOnlyList<Guid> FileArtifactIds);

public sealed record SignedDownloadAuthorizationRecord(
    Guid AuthorizationId,
    Guid FileArtifactId,
    string AuthorizedBy,
    DateTimeOffset AuthorizedAt,
    DateTimeOffset ExpiresAt,
    string AuthorizationKey,
    string Status);

public sealed record RecordExportJobCommand(
    Guid ExportJobId,
    Guid? PortId,
    string PortKind,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    string? Period,
    string? TargetScope,
    string? ExportFormat,
    string Status,
    string IdempotencyKey);

public sealed record RecordChecksumCommand(
    Guid ExportJobId,
    Guid? FileArtifactId,
    string Algorithm,
    string ChecksumValue,
    DateTimeOffset VerifiedAt,
    string VerificationStatus);

public sealed record RecordFileArtifactCommand(
    Guid ExportJobId,
    string FileName,
    string FileType,
    string StorageRef,
    long? ByteSize,
    string? ChecksumValue);

public sealed record WriteManifestCommand(
    Guid ExportJobId,
    string ManifestVersion,
    DateTimeOffset GeneratedAt,
    string GeneratedBy,
    string? Period,
    string? ExportFormat,
    string? Checksum,
    IReadOnlyList<Guid> FileArtifactIds);

public sealed record AuthorizeSignedDownloadCommand(
    Guid FileArtifactId,
    string AuthorizedBy,
    DateTimeOffset AuthorizedAt,
    DateTimeOffset ExpiresAt,
    string AuthorizationKey);
