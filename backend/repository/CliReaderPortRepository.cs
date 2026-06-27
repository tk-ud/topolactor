using Topolactor.Schema;

namespace Topolactor.Repository;

public interface CliReaderPortRepository
{
    Task<CliReaderPortConfig?> LoadPortAsync(string portKey, CancellationToken ct = default);
    Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(AuthorizedCliReaderQuery query, CancellationToken ct = default);
    Task AppendRuntimeEventAsync(CliReaderPortRuntimeEvent runtimeEvent, CancellationToken ct = default);
    Task<CliReaderExportJobResult> CreateExportJobAsync(CreateCliReaderExportJobCommand command, CancellationToken ct = default);

    // File stream port: resolve an already-authorized export job file from the canonical
    // export_jobs / export_manifests ledger. Returns null when the export job is not
    // owned by the dispatch-resolved port + authenticated user (fail-close at the runtime).
    Task<AuthorizedExportFile?> LoadAuthorizedExportFileAsync(LoadAuthorizedExportFileQuery query, CancellationToken ct = default);

    // File stream port: append checksum_verified / download_completed runtime evidence to
    // runtime_event_log after a successful, checksum-verified authorized file stream.
    Task RecordExportDownloadEvidenceAsync(Guid exportJobId, bool checksumVerified, DateTimeOffset observedAt, CancellationToken ct = default);

    // File stream port: append checksum/source/manifest/artifact rejection evidence to
    // runtime_event_log when an authorized export file fails bundle verification.
    Task RecordExportDownloadFailureEvidenceAsync(Guid exportJobId, string failureCode, DateTimeOffset observedAt, CancellationToken ct = default);
    Task<CliReaderImportCandidateResult> CreateImportCandidateAsync(CreateCliReaderImportCandidateCommand command, CancellationToken ct = default);
    Task<CliReaderImportCandidateResult?> LoadImportCandidateAsync(LoadCliReaderImportCandidateQuery query, CancellationToken ct = default);
    Task RecordImportCandidateEvidenceAsync(Guid candidateId, string eventType, DateTimeOffset observedAt, CancellationToken ct = default);
}
