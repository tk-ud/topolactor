namespace Topolactor.Schema;

/// <summary>
/// Backend error classification result.
///
/// Owned by docs/design/backend-error-evidence-report-notify-ssot.yaml (classification block).
/// Only <see cref="SystemError"/> is appended to logs.error. Every other class is an ordinary
/// reject / input mistake / domain-evidence concern that must NOT be persisted as backend system
/// error evidence.
/// </summary>
public enum BackendErrorClass
{
    /// <summary>Backend execution substrate failure — append to logs.error.</summary>
    SystemError,

    /// <summary>User/client input, normal validation reject — never appended.</summary>
    DtoFailure,

    /// <summary>Ordinary authError / 401 / 403 — never appended.</summary>
    AuthReject,

    /// <summary>Business policy reject — never appended.</summary>
    PolicyReject,

    /// <summary>Missing/invalid required user input value — never appended.</summary>
    UserInputMiss,

    /// <summary>Security/audit evidence requiring its own SSOT/domain table — never appended here.</summary>
    SecurityOrAuditEvidence,

    /// <summary>Cooperative cancellation (service stop) — not a substrate failure, never appended.</summary>
    Cancellation,
}

/// <summary>
/// Non-system reject categories that the classifier must keep out of logs.error.
/// These correspond to the SSOT classification.dto_failure family.
/// </summary>
public enum BackendRejectCategory
{
    DtoValidation,
    OrdinaryAuth,
    BusinessPolicy,
    UserInputMiss,
    SecurityOrAuditEvidence,
}

/// <summary>
/// Canonical origin_layer vocabulary for logs.error evidence. Stable strings shared by every
/// backend boundary so all system errors connect to the same evidence envelope.
/// </summary>
public static class BackendErrorOriginLayer
{
    public const string AbstractFunctionExecutor = "abstract_function_executor";
    public const string RuntimeExecutor = "runtime_executor";
    public const string ManifestDispatcher = "manifest_dispatcher";
    public const string AdminRuntime = "admin_runtime";
    public const string Scheduler = "scheduler";
    public const string DbNotifyListener = "db_notify_listener";
    public const string Repository = "repository";
    public const string ExternalPortDispatch = "external_port_dispatch_runtime";
    public const string ExternalPortCredentialRefresher = "external_port_credential_refresher";
    public const string PrimitiveAdapter = "append_error_evidence_primitive";
}

/// <summary>
/// error_kind vocabulary persisted in logs.error.error_kind.
/// </summary>
public static class BackendErrorKind
{
    public const string SystemError = "system_error";
    public const string DiagnosticGapSubstrate = "diagnostic_gap_substrate";
}

/// <summary>
/// Append-only system error evidence envelope. Mirrors logs.error required_fields.
/// error_id and occurred_at default in the database when not supplied.
///
/// Redaction / bounding of <see cref="MessagePublic"/> and <see cref="EvidenceJson"/> is the
/// appender's responsibility (see IBackendErrorEvidenceAppender / NpgsqlBackendErrorEvidenceAppender).
/// </summary>
public sealed record BackendErrorEvidence(
    string OriginLayer,
    string BoundaryKey,
    string ErrorKind,
    string ErrorCode,
    string MessagePublic,
    string StackHash,
    string Severity,
    bool Retryable,
    string? FunctionKey = null,
    string? RuntimeLane = null,
    string? PrimitiveKey = null,
    int? StepOrder = null,
    string? ExceptionType = null,
    IReadOnlyDictionary<string, string>? EvidenceJson = null,
    DateTimeOffset? OccurredAt = null);

/// <summary>
/// Append-only system error evidence appender boundary.
///
/// Responsibilities (SSOT appender_contract):
///   - validate envelope
///   - redact + bound evidence
///   - append logs.error
///   - preserve the original exception flow (never convert the original failure into success)
///   - expose appender failure by throwing — callers swallow/log it without masking the original.
/// </summary>
public interface IBackendErrorEvidenceAppender
{
    Task AppendAsync(BackendErrorEvidence evidence, CancellationToken ct = default);
}

/// <summary>
/// logs.error_notify_queue lifecycle status vocabulary. The durable queue — not the pg_notify
/// wake-up signal — is the source of truth for the post-notify bridge.
/// </summary>
public static class BackendErrorNotifyQueueStatus
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Acknowledged = "acknowledged";
    public const string FailedRetryable = "failed_retryable";
    public const string FailedTerminal = "failed_terminal";
}

/// <summary>
/// One claimed logs.error_notify_queue row joined to its logs.error evidence row. The post-notify
/// bridge builds the hook trigger dispatch payload from these authoritative fields.
/// </summary>
public sealed record BackendErrorNotifyClaim(
    Guid QueueId,
    Guid ErrorId,
    int AttemptCount,
    string OriginLayer,
    string BoundaryKey,
    string ErrorCode,
    string Severity,
    string StackHash);

/// <summary>
/// Durable claim/ack/fail boundary for logs.error_notify_queue. Claiming is atomic
/// (FOR UPDATE SKIP LOCKED) so concurrent bridge instances never double-process a row.
/// </summary>
public interface IBackendErrorNotifyQueueRepository
{
    /// <summary>
    /// Atomically claims up to <paramref name="batchSize"/> pending rows (and failed_retryable rows
    /// whose last attempt is older than <paramref name="retryBackoff"/>), marking them claimed and
    /// incrementing attempt_count, then returns them joined to logs.error.
    /// </summary>
    Task<IReadOnlyList<BackendErrorNotifyClaim>> ClaimPendingAsync(
        int batchSize, TimeSpan retryBackoff, CancellationToken ct = default);

    /// <summary>Transitions a claimed row to acknowledged after an accepted dispatch boundary result.</summary>
    Task AcknowledgeAsync(Guid queueId, CancellationToken ct = default);

    /// <summary>
    /// Transitions a claimed row to failed_retryable (retry later) or failed_terminal (give up).
    /// The row is never deleted — durable failure evidence is preserved.
    /// </summary>
    Task FailAsync(Guid queueId, string reason, bool terminal, CancellationToken ct = default);
}

/// <summary>
/// Dispatch seam for the post-notify bridge. The implementation MUST route through the runtime
/// scheduler's hook trigger path (RuntimeTimelineScheduler) and MUST NOT call ExternalPortDispatchRuntime
/// or any external provider directly.
/// </summary>
public interface IBackendErrorNotifyHookDispatcher
{
    Task<EndpointResponseDto> DispatchAsync(EndpointRequestDto request, CancellationToken ct = default);
}
