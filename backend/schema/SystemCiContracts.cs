namespace Topolactor.Schema;

/// <summary>
/// Kind of system CI inspection trigger.
/// EventDriven: triggered in-memory after a runtime event (hub attention upsert, evidence extraction, feedback append).
/// CronContinuity: triggered periodically to explore full DB topology continuity.
/// </summary>
public enum SystemCiInspectionKind
{
    EventDriven,
    CronContinuity
}

/// <summary>
/// Status of a system CI check or finding.
/// Pass: invariant holds; no action required.
/// Gap: non-blocking issue; reportable diagnostic; caller may continue.
/// Blocking: fail-close invariant violated; caller must not proceed (ExplicitError).
/// </summary>
public enum SystemCiStatus
{
    Pass,
    Gap,
    Blocking
}

/// <summary>
/// Classification axis for a finding reason.
/// This is separate from SystemCiStatus (overall severity aggregation axis).
/// </summary>
public enum SystemCiFindingClassification
{
    Pass,
    MissingRequired,
    UndefinedImplementationValue,
    InvalidShape,
    ProhibitedValue,
    NotCovered,
    RuntimeFailure
}

/// <summary>
/// A single finding from a system CI inspection check.
/// CheckName: hardcoded invariant identifier (e.g. "ATTENTION_SCORE_NOT_FINITE").
/// TargetId: optional record identifier for the specific failing row.
/// </summary>
public record SystemCiFinding(
    string CheckName,
    SystemCiStatus Status,
    string Detail,
    string? TargetId = null,
    // Pass classification is reserved for no-finding/pass-result semantics.
    // Gap/Blocking findings must use an explicit non-Pass classification.
    SystemCiFindingClassification Classification = SystemCiFindingClassification.Pass
);

/// <summary>
/// Full result of one system CI inspection run for a single target.
/// InspectionTarget: e.g. "sql_attention_continuity", "evidence_integrity",
///   "hub_attention_continuity", "current_rebuildability".
/// OverallStatus: worst status across all findings (Pass when Findings is empty).
/// Findings: all individual check results.
/// InspectedAt: UTC timestamp of the inspection.
/// </summary>
public record SystemCiDiagnosticResult(
    string InspectionTarget,
    SystemCiInspectionKind InspectionKind,
    SystemCiStatus OverallStatus,
    IReadOnlyList<SystemCiFinding> Findings,
    DateTimeOffset InspectedAt
);

/// <summary>
/// Callable target descriptor for AdminRuntime system CI diagnostic surface.
/// </summary>
public record SystemCiTargetDto(
    string Target
);

/// <summary>
/// Lightweight summary of a context_hub_recommendation_current row for cron CI inspection.
/// Avoids loading full evidence_json and mlp_feature_json blobs.
/// HasEvidence: true when evidence_json contains at least one element.
/// </summary>
public record HubAttentionCiSummary(
    Guid HubId,
    Guid CandidateId,
    int ScopeLimit,
    float? AttentionScore,
    float? EmaFast,
    float? EmaSlow,
    bool HasEvidence,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// Summary of context_token_registry state for cron CI inspection.
/// TotalActiveTokens: count of tokens with status='active'.
/// UnreferencedTokenCount: active tokens not referenced by any hub attention record
///   (candidate_kind = 'token' in context_hub_recommendation_current).
/// </summary>
public record RegistryTokenCiSummary(
    int TotalActiveTokens,
    int UnreferencedTokenCount
);
