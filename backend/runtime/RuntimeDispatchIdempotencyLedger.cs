using System.Text.Json;

namespace Topolactor.Runtime;

/// <summary>
/// Retry-safe dispatch idempotency: dispatchExternalPort / dispatchInstanceOperation
/// runtime dispatch lanes only.
/// </summary>
public enum RuntimeDispatchIdempotencyClaimStatus
{
    /// <summary>No prior attempt recorded (or a prior attempt failed and was reclaimed) — caller must execute.</summary>
    Claimed,
    /// <summary>Another attempt with the same key is currently processing — caller must NOT execute.</summary>
    InFlight,
    /// <summary>A prior attempt already completed — caller must NOT execute; return PreviousResult.</summary>
    AlreadyCompleted,
}

public sealed record RuntimeDispatchIdempotencyClaimResult(
    RuntimeDispatchIdempotencyClaimStatus Status,
    JsonElement? PreviousResult);

/// <summary>
/// Retry-safe idempotency ledger boundary for the dispatchExternalPort / dispatchInstanceOperation
/// runtime dispatch lanes (external_port_runtime / instance_port_runtime).
///
/// Distinct from IExternalPortRuntimeEventLogRepository (see ExternalPortCredentialRefresher.cs):
/// that is an append-only effect-OBSERVED evidence log, written only after execution and never
/// read to decide whether to execute. This ledger IS read before execution (ClaimAsync) and
/// written after (CompleteAsync/FailAsync) specifically to prevent duplicate execution of the
/// external API call / instance operation across communication loss, retry, reload, or
/// runtime-runner recreation — the frontend fired-registry only guards duplicate dispatch
/// within a single runner's lifetime and provides no cross-request/cross-process guarantee.
///
/// SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml lifecycle_policy
/// retry_safe_dispatch_idempotency.
/// </summary>
public interface IRuntimeDispatchIdempotencyLedger
{
    Task<RuntimeDispatchIdempotencyClaimResult> ClaimAsync(
        string idempotencyKey, string dispatchLane, CancellationToken ct = default);

    Task CompleteAsync(string idempotencyKey, JsonElement resultJson, CancellationToken ct = default);

    Task FailAsync(string idempotencyKey, string failureCode, CancellationToken ct = default);
}
