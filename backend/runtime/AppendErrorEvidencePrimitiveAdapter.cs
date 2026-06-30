using System.Security.Cryptography;
using System.Text;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// Primitive adapter for the optional <c>append_error_evidence</c> primitive.
///
/// SSOT: docs/design/backend-error-evidence-report-notify-ssot.yaml (primitive_adapter_contract).
///
/// This is an EXPLICIT seeded-step adapter only. It appends one logs.error row when a manifest
/// step is configured to do so. It is NOT the global error-capture mechanism and cannot replace
/// the AbstractFunctionExecutor boundary classification: it runs only when a manifest explicitly
/// invokes it as a step, and it has no catch-all behaviour.
///
/// step_config (manifest-authority; fail-close when missing):
///   - origin_layer  : logs.error origin_layer to record
///   - boundary_key  : logs.error boundary_key to record
///   - error_code    : stable machine error code
///   - error_kind    : optional (default system_error)
///   - severity      : optional (default error)
/// inputs (manifest-bound):
///   - message_public: required, redacted/bounded by the appender
///   - any remaining string inputs are passed as evidence_json (appender redacts/bounds).
/// </summary>
public sealed class AppendErrorEvidencePrimitiveAdapter : IAbstractFunctionPrimitiveAdapter
{
    private readonly IBackendErrorEvidenceAppender _appender;

    public AppendErrorEvidencePrimitiveAdapter(IBackendErrorEvidenceAppender appender) =>
        _appender = appender ?? throw new ArgumentNullException(nameof(appender));

    public string PrimitiveKey => "append_error_evidence";

    public async Task<object?> ExecuteAsync(
        AbstractFunctionStep step,
        IReadOnlyDictionary<string, object?> inputs,
        AbstractFunctionExecutionContext context,
        CancellationToken ct = default)
    {
        var originLayer = RequireConfig(step, "origin_layer");
        var boundaryKey = RequireConfig(step, "boundary_key");
        var errorCode = RequireConfig(step, "error_code");
        var errorKind = step.StepConfig.TryGetValue("error_kind", out var k) && !string.IsNullOrWhiteSpace(k)
            ? k : BackendErrorKind.SystemError;
        var severity = step.StepConfig.TryGetValue("severity", out var s) && !string.IsNullOrWhiteSpace(s)
            ? s : "error";

        if (!inputs.TryGetValue("message_public", out var messageRaw) || messageRaw is not string message || string.IsNullOrWhiteSpace(message))
            throw new AbstractFunctionFailCloseException(AbstractFunctionFailCloseStatus.MissingInput, "APPEND_ERROR_EVIDENCE_MESSAGE_MISSING");

        var evidencePairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in inputs)
        {
            if (string.Equals(kvp.Key, "message_public", StringComparison.Ordinal)) continue;
            if (kvp.Value is string str) evidencePairs[kvp.Key] = str;
        }

        var evidence = new BackendErrorEvidence(
            OriginLayer: originLayer,
            BoundaryKey: boundaryKey,
            ErrorKind: errorKind,
            ErrorCode: errorCode,
            MessagePublic: message,
            StackHash: ComputeStackHash(boundaryKey, errorCode, message),
            Severity: severity,
            Retryable: false,
            FunctionKey: context.RequiredRuntimeLane,
            EvidenceJson: evidencePairs.Count > 0 ? evidencePairs : null);

        await _appender.AppendAsync(evidence, ct);
        return "append_error_evidence:appended";
    }

    private static string RequireConfig(AbstractFunctionStep step, string key) =>
        step.StepConfig.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new AbstractFunctionFailCloseException(
                AbstractFunctionFailCloseStatus.InvalidAuthority,
                $"APPEND_ERROR_EVIDENCE_STEP_CONFIG_MISSING: {key}");

    private static string ComputeStackHash(string boundaryKey, string errorCode, string message)
    {
        var basis = $"{boundaryKey}|{errorCode}|{message}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(basis));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
