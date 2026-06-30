using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Classifies backend failures into logs.error-eligible system errors versus ordinary rejects.
///
/// Owned by docs/design/backend-error-evidence-report-notify-ssot.yaml (classification block).
/// Only <see cref="BackendErrorClass.SystemError"/> is appended to logs.error.
///
/// Fail-close status mapping (AbstractFunctionExecutor boundary):
///   - missing_input            -> UserInputMiss   (a required input value was absent: input miss, not substrate)
///   - missing_authority        -> SystemError     (manifest-load / authority binding missing: malformed runtime authority)
///   - invalid_authority        -> SystemError     (inactive manifest, wrong lane/scope, bad table/policy/output authority)
///   - invalid_input_binding    -> SystemError     (unsupported/unresolvable binding source: malformed manifest binding)
///   - invalid_projection       -> SystemError     (projection/lane invariant break)
///   - secret_projection_denied -> SystemError     (secret-projection invariant break)
///   - unsupported_primitive    -> SystemError     (primitive registry failure)
///
/// Any non-fail-close exception is an unexpected substrate failure and classifies as SystemError,
/// except cooperative cancellation (OperationCanceledException), which is not a substrate failure.
/// </summary>
public static class BackendErrorClassifier
{
    public static BackendErrorClass Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
            return BackendErrorClass.Cancellation;

        if (exception is AbstractFunctionFailCloseException failClose)
            return ClassifyFailCloseStatus(failClose.Status);

        // Unexpected exception (dependency failure, repository/db unavailable, invariant break).
        return BackendErrorClass.SystemError;
    }

    public static BackendErrorClass ClassifyFailCloseStatus(string status) => status switch
    {
        AbstractFunctionFailCloseStatus.MissingInput => BackendErrorClass.UserInputMiss,
        AbstractFunctionFailCloseStatus.MissingAuthority => BackendErrorClass.SystemError,
        AbstractFunctionFailCloseStatus.InvalidAuthority => BackendErrorClass.SystemError,
        AbstractFunctionFailCloseStatus.InvalidInputBinding => BackendErrorClass.SystemError,
        AbstractFunctionFailCloseStatus.InvalidProjection => BackendErrorClass.SystemError,
        AbstractFunctionFailCloseStatus.SecretProjectionDenied => BackendErrorClass.SystemError,
        AbstractFunctionFailCloseStatus.UnsupportedPrimitive => BackendErrorClass.SystemError,
        // Unknown fail-close status defaults to substrate failure (fail-safe toward evidence capture).
        _ => BackendErrorClass.SystemError,
    };

    /// <summary>
    /// Classifies an ordinary reject category. DTO validation, ordinary auth, business policy,
    /// user input miss, and security/audit evidence are all non-system and never append to logs.error.
    /// </summary>
    public static BackendErrorClass Classify(BackendRejectCategory category) => category switch
    {
        BackendRejectCategory.DtoValidation => BackendErrorClass.DtoFailure,
        BackendRejectCategory.OrdinaryAuth => BackendErrorClass.AuthReject,
        BackendRejectCategory.BusinessPolicy => BackendErrorClass.PolicyReject,
        BackendRejectCategory.UserInputMiss => BackendErrorClass.UserInputMiss,
        BackendRejectCategory.SecurityOrAuditEvidence => BackendErrorClass.SecurityOrAuditEvidence,
        _ => BackendErrorClass.DtoFailure,
    };

    public static bool AppendsToLogsError(BackendErrorClass classification) =>
        classification == BackendErrorClass.SystemError;
}

/// <summary>
/// Optional hint metadata supplied by a boundary when recording a system error.
/// </summary>
public sealed record BackendErrorEvidenceHint(
    string? ErrorCode = null,
    string? FunctionKey = null,
    string? RuntimeLane = null,
    string? PrimitiveKey = null,
    int? StepOrder = null,
    string? Severity = null,
    bool Retryable = false,
    IReadOnlyDictionary<string, string>? Evidence = null);

/// <summary>
/// Shared connection point that wires every backend boundary's system errors into the same
/// logs.error evidence envelope. Classification happens here so DTO/auth/policy/input rejects
/// are never appended.
///
/// Re-throw dedup: an exception that flows through multiple boundaries is recorded once. The
/// first recording marks the exception via <see cref="Exception.Data"/>; later boundaries skip it.
/// Appender failures are logged and swallowed — they never mask or convert the original failure.
/// </summary>
public static class BackendErrorBoundary
{
    internal const string RecordedMarker = "__backend_error_evidence_recorded";

    /// <summary>
    /// Records an exception as a system error when (and only when) it classifies as one.
    /// Safe to call from any catch block: it never throws and never alters control flow.
    /// </summary>
    public static async Task RecordSystemErrorAsync(
        IBackendErrorEvidenceAppender? appender,
        ILogger? logger,
        Exception exception,
        string originLayer,
        string boundaryKey,
        BackendErrorEvidenceHint? hint = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (appender is null) return;
        if (exception.Data.Contains(RecordedMarker)) return;

        var classification = BackendErrorClassifier.Classify(exception);
        if (!BackendErrorClassifier.AppendsToLogsError(classification))
        {
            TryMark(exception, "skipped:" + classification);
            return;
        }

        var evidence = FromException(exception, originLayer, boundaryKey, hint);
        try
        {
            await appender.AppendAsync(evidence, ct);
            TryMark(exception, "recorded");
        }
        catch (Exception appendEx)
        {
            // Expose appender failure without converting the original failure into success.
            logger?.LogError(appendEx,
                "BackendErrorBoundary: logs.error append failed for origin={Origin} boundary={Boundary}; original failure preserved.",
                originLayer, boundaryKey);
            TryMark(exception, "append_failed");
        }
    }

    /// <summary>
    /// Records a system error for a substrate failure that surfaced without an exception object
    /// (e.g. a missing runtime destination mapping). The caller has already classified it as a
    /// system error by constructing the envelope.
    /// </summary>
    public static async Task RecordSystemErrorAsync(
        IBackendErrorEvidenceAppender? appender,
        ILogger? logger,
        BackendErrorEvidence evidence,
        CancellationToken ct = default)
    {
        if (appender is null) return;
        try
        {
            await appender.AppendAsync(evidence, ct);
        }
        catch (Exception appendEx)
        {
            logger?.LogError(appendEx,
                "BackendErrorBoundary: logs.error append failed for origin={Origin} boundary={Boundary}.",
                evidence.OriginLayer, evidence.BoundaryKey);
        }
    }

    public static BackendErrorEvidence FromException(
        Exception exception,
        string originLayer,
        string boundaryKey,
        BackendErrorEvidenceHint? hint = null)
    {
        var failClose = exception as AbstractFunctionFailCloseException;
        var errorCode = hint?.ErrorCode ?? failClose?.Status ?? exception.GetType().Name;
        return new BackendErrorEvidence(
            OriginLayer: originLayer,
            BoundaryKey: boundaryKey,
            ErrorKind: BackendErrorKind.SystemError,
            ErrorCode: errorCode,
            MessagePublic: exception.Message,
            StackHash: ComputeStackHash(exception),
            Severity: hint?.Severity ?? "error",
            Retryable: hint?.Retryable ?? false,
            FunctionKey: hint?.FunctionKey,
            RuntimeLane: hint?.RuntimeLane,
            PrimitiveKey: hint?.PrimitiveKey,
            StepOrder: hint?.StepOrder,
            ExceptionType: exception.GetType().FullName,
            EvidenceJson: hint?.Evidence);
    }

    /// <summary>
    /// Stable grouping hash from exception type and the top stack frame. The raw stack trace is
    /// never persisted; only this hash is stored, satisfying bounded/redacted evidence.
    /// </summary>
    /// <summary>Stable grouping hash from an arbitrary basis string (no exception available).</summary>
    public static string ComputeHash(string basis)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(basis ?? string.Empty));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeStackHash(Exception exception)
    {
        var topFrame = exception.StackTrace?
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim() ?? string.Empty;
        var basis = $"{exception.GetType().FullName}|{topFrame}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(basis));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryMark(Exception exception, string state)
    {
        try { exception.Data[RecordedMarker] = state; }
        catch { /* some exceptions have read-only Data; dedup is best-effort */ }
    }
}
