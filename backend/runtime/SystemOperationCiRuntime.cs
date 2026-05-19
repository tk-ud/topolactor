using System.Text.Json;
using Microsoft.Extensions.Logging;
using Topolactor.Repository;
using Topolactor.Schema;

namespace Topolactor.Runtime;

/// <summary>
/// System Operation CI for SQL Attention and Registry Continuity.
///
/// This class is the system-internal invariant inspector — not a user-facing recommendation engine.
/// Inspection rules and thresholds are hardcoded because they represent system invariants,
/// not runtime policy. Runtime policy (EMA alpha, scope_limits, etc.) remains in function_parameters.
///
/// Two inspection modes:
///   EventDriven   — lightweight in-memory checks after key runtime events (no extra DB round-trip).
///   CronContinuity — DB-backed continuity exploration (called by cron trigger; wiring: TODO).
///
/// Failure handling:
///   Blocking → caller must ExplicitError (fail-close); do not proceed.
///   Gap      → reportable diagnostic; caller may log and continue.
///   Pass     → no action required.
///
/// System CI / runtime policy boundary:
///   System CI:     hardcoded invariants. Thresholds here are invariant checks, not tuning values.
///   Runtime policy: EMA alpha, scope_limits, delta values — remain in function_parameters.
///   Do not confuse these two: moving runtime policy into this class is prohibited.
///
/// Canonical route connection:
///   context_event append / hub attention upsert / evidence extraction / feedback append
///   → InspectHubAttentionAfterUpdate / InspectEvidenceIntegrity / InspectFeedbackEvents (event-driven)
///   → Blocking result → caller throws → ExplicitError
///   → Gap result     → caller logs warning; recommendation continues
///
/// Cron continuity (wiring to background worker / scheduled job: TODO):
///   → InspectHubAttentionContinuityAsync / InspectCurrentRebuildabilityAsync
///   → reportable diagnostic results
/// </summary>
public class SystemOperationCiRuntime
{
    private readonly ILogger<SystemOperationCiRuntime> _logger;
    private readonly ContextRouteRepository _repository;

    // Hardcoded inspection target identifiers (system invariants — not policy)
    private const string SqlAttentionContinuityTarget  = "sql_attention_continuity";
    private const string EvidenceIntegrityTarget       = "evidence_integrity";
    private const string HubAttentionContinuityTarget  = "hub_attention_continuity";
    private const string CurrentRebuildabilityTarget   = "current_rebuildability";

    public SystemOperationCiRuntime(
        ILogger<SystemOperationCiRuntime> logger,
        ContextRouteRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    // ─── Event-driven inspection (in-memory, no extra DB round-trip) ─────────

    /// <summary>
    /// Inspects a hub attention current record immediately after upsert.
    /// Checks: hub_id non-empty, candidate_id non-empty, attention_score / EMA finiteness,
    /// evidence_json and mlp_feature_json parseability.
    ///
    /// Blocking: hub_id empty, candidate_id empty, any score is NaN or Infinity.
    /// Gap: evidence_json or mlp_feature_json is not a valid JSON array.
    ///
    /// All thresholds here are invariants (finite IEEE-754, non-empty UUID) — not policy values.
    /// </summary>
    public SystemCiDiagnosticResult InspectHubAttentionAfterUpdate(
        HubAttentionCurrentRecord record,
        string evidenceJson,
        string mlpFeatureJson)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(evidenceJson);
        ArgumentNullException.ThrowIfNull(mlpFeatureJson);

        var findings = new List<SystemCiFinding>();

        // BLOCKING: hub identity must be present
        if (record.HubId == Guid.Empty)
        {
            findings.Add(new SystemCiFinding(
                "HUB_ID_EMPTY",
                SystemCiStatus.Blocking,
                "hub_id is Guid.Empty — hub identity is required for hub attention.",
                record.HubId.ToString()
            ));
        }

        // BLOCKING: candidate identity must be present
        if (record.CandidateId == Guid.Empty)
        {
            findings.Add(new SystemCiFinding(
                "CANDIDATE_ID_EMPTY",
                SystemCiStatus.Blocking,
                "candidate_id is Guid.Empty — candidate identity is required.",
                record.CandidateId.ToString()
            ));
        }

        // BLOCKING: attention_score must be finite (NaN / Infinity indicates a computation failure)
        if (record.AttentionScore.HasValue && !float.IsFinite(record.AttentionScore.Value))
        {
            findings.Add(new SystemCiFinding(
                "ATTENTION_SCORE_NOT_FINITE",
                SystemCiStatus.Blocking,
                $"attention_score={record.AttentionScore.Value} is not finite — " +
                "indicates a scoring computation failure.",
                $"hub={record.HubId} candidate={record.CandidateId}"
            ));
        }

        // BLOCKING: ema_fast must be finite when present
        if (record.EmaFast.HasValue && !float.IsFinite(record.EmaFast.Value))
        {
            findings.Add(new SystemCiFinding(
                "EMA_FAST_NOT_FINITE",
                SystemCiStatus.Blocking,
                $"ema_fast={record.EmaFast.Value} is not finite — EMA computation failure.",
                $"hub={record.HubId} candidate={record.CandidateId}"
            ));
        }

        // BLOCKING: ema_slow must be finite when present
        if (record.EmaSlow.HasValue && !float.IsFinite(record.EmaSlow.Value))
        {
            findings.Add(new SystemCiFinding(
                "EMA_SLOW_NOT_FINITE",
                SystemCiStatus.Blocking,
                $"ema_slow={record.EmaSlow.Value} is not finite — EMA computation failure.",
                $"hub={record.HubId} candidate={record.CandidateId}"
            ));
        }

        // GAP: evidence_json must be a parseable JSON array
        if (!TryParseJsonArray(evidenceJson, out _))
        {
            findings.Add(new SystemCiFinding(
                "EVIDENCE_JSON_NOT_PARSEABLE",
                SystemCiStatus.Gap,
                "evidence_json is not a valid JSON array — evidence integrity cannot be verified.",
                $"hub={record.HubId}"
            ));
        }

        // GAP: mlp_feature_json must be a parseable JSON array
        if (!TryParseJsonArray(mlpFeatureJson, out _))
        {
            findings.Add(new SystemCiFinding(
                "MLP_FEATURE_JSON_NOT_PARSEABLE",
                SystemCiStatus.Gap,
                "mlp_feature_json is not a valid JSON array — MLP feature integrity cannot be verified.",
                $"hub={record.HubId}"
            ));
        }

        return BuildResult(SqlAttentionContinuityTarget, SystemCiInspectionKind.EventDriven, findings);
    }

    /// <summary>
    /// Inspects transition key evidence entries for integrity.
    /// Called after evidence extraction in the TVR extension.
    ///
    /// Gap: evidence is empty when a current operation is present
    ///   (operation drives transition but no key evidence was extracted).
    /// Blocking: any evidence entry has contribution_score &lt; 0
    ///   (negative contribution score is a computation invariant violation).
    /// </summary>
    public SystemCiDiagnosticResult InspectEvidenceIntegrity(
        string? currentOperation,
        IReadOnlyList<TransitionKeyEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var findings = new List<SystemCiFinding>();

        // GAP: evidence should explain the current operation when one is present
        if (!string.IsNullOrWhiteSpace(currentOperation) && evidence.Count == 0)
        {
            findings.Add(new SystemCiFinding(
                "EVIDENCE_EMPTY_WITH_OPERATION",
                SystemCiStatus.Gap,
                $"Evidence is empty but currentOperation='{currentOperation}' is present. " +
                "Evidence should explain why this operation contributes to the transition key. " +
                "Check TransitionKeyEvidencePolicy.Enabled and policy resolution.",
                currentOperation
            ));
        }

        // BLOCKING: contribution_score must be non-negative (negative is a computation invariant violation)
        foreach (var entry in evidence)
        {
            if (entry.ContributionScore < 0.0f)
            {
                findings.Add(new SystemCiFinding(
                    "EVIDENCE_NEGATIVE_CONTRIBUTION",
                    SystemCiStatus.Blocking,
                    $"Evidence entry key_kind='{entry.KeyKind}' key_id='{entry.KeyId}' " +
                    $"has contribution_score={entry.ContributionScore:F4} — must be >= 0. " +
                    "Negative contribution scores are an invariant violation.",
                    entry.KeyId
                ));
            }
        }

        return BuildResult(EvidenceIntegrityTarget, SystemCiInspectionKind.EventDriven, findings);
    }

    /// <summary>
    /// Inspects feedback events for integrity.
    /// Called after BuildFeedbackEvents in the TVR extension.
    ///
    /// Blocking: delta_applied is zero (must not be zero; indicates a policy misconfiguration).
    /// Blocking: delta sign contradicts feedback_kind
    ///   (selected → positive, ignored → negative, missing_candidate → positive).
    /// </summary>
    public SystemCiDiagnosticResult InspectFeedbackEvents(
        IReadOnlyList<HubFeedbackEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var findings = new List<SystemCiFinding>();

        foreach (var ev in events)
        {
            // BLOCKING: delta must not be zero
            if (ev.DeltaApplied == 0.0f)
            {
                findings.Add(new SystemCiFinding(
                    "FEEDBACK_DELTA_ZERO",
                    SystemCiStatus.Blocking,
                    $"feedback_kind='{ev.FeedbackKind}' has delta_applied=0 — " +
                    "delta must be non-zero. Check FeedbackWeightUpdatePolicy values.",
                    $"hub={ev.HubId} candidate={ev.CandidateId}"
                ));
                continue;
            }

            // BLOCKING: delta sign must match feedback_kind invariant
            var signViolation = ev.FeedbackKind switch
            {
                "selected"          => ev.DeltaApplied < 0.0f
                                        ? "selected feedback must have positive delta" : null,
                "ignored"           => ev.DeltaApplied > 0.0f
                                        ? "ignored feedback must have negative delta" : null,
                "missing_candidate" => ev.DeltaApplied < 0.0f
                                        ? "missing_candidate feedback must have positive delta" : null,
                _                   => null
            };

            if (signViolation is not null)
            {
                findings.Add(new SystemCiFinding(
                    "FEEDBACK_DELTA_SIGN_VIOLATION",
                    SystemCiStatus.Blocking,
                    $"feedback_kind='{ev.FeedbackKind}' delta_applied={ev.DeltaApplied:F4}: {signViolation}. " +
                    "Delta sign must match feedback kind — this is a system invariant.",
                    $"hub={ev.HubId} candidate={ev.CandidateId}"
                ));
            }
        }

        return BuildResult(SqlAttentionContinuityTarget, SystemCiInspectionKind.EventDriven, findings);
    }

    // ─── Cron-triggered inspection (DB-backed) ────────────────────────────────

    /// <summary>
    /// Cron-triggered hub attention continuity inspection.
    /// Loads HubAttentionCiSummary from the repository and verifies system invariants:
    ///   Blocking: attention_score is not finite (NaN / Infinity).
    ///   Blocking: ema_fast or ema_slow is not finite when present.
    ///   Gap: evidence is absent (HasEvidence=false) — no transition key evidence for this record.
    ///
    /// Returns a diagnostic result for the hub_attention_continuity target.
    /// Cron trigger wiring: TODO — background worker / scheduled job integration pending.
    /// </summary>
    public async Task<SystemCiDiagnosticResult> InspectHubAttentionContinuityAsync(
        CancellationToken ct = default)
    {
        IReadOnlyList<HubAttentionCiSummary> summaries;
        try
        {
            summaries = await _repository.LoadHubAttentionSummaryForCiAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SystemOperationCiRuntime.InspectHubAttentionContinuityAsync: repository read failed.");
            throw;
        }

        var findings = new List<SystemCiFinding>();

        foreach (var s in summaries)
        {
            if (s.AttentionScore.HasValue && !float.IsFinite(s.AttentionScore.Value))
            {
                findings.Add(new SystemCiFinding(
                    "CRON_ATTENTION_SCORE_NOT_FINITE",
                    SystemCiStatus.Blocking,
                    $"attention_score={s.AttentionScore.Value} is not finite for " +
                    $"hub={s.HubId} candidate={s.CandidateId} scope={s.ScopeLimit}.",
                    $"hub={s.HubId} candidate={s.CandidateId}"
                ));
            }

            if (s.EmaFast.HasValue && !float.IsFinite(s.EmaFast.Value))
            {
                findings.Add(new SystemCiFinding(
                    "CRON_EMA_FAST_NOT_FINITE",
                    SystemCiStatus.Blocking,
                    $"ema_fast={s.EmaFast.Value} is not finite for " +
                    $"hub={s.HubId} candidate={s.CandidateId}.",
                    $"hub={s.HubId} candidate={s.CandidateId}"
                ));
            }

            if (s.EmaSlow.HasValue && !float.IsFinite(s.EmaSlow.Value))
            {
                findings.Add(new SystemCiFinding(
                    "CRON_EMA_SLOW_NOT_FINITE",
                    SystemCiStatus.Blocking,
                    $"ema_slow={s.EmaSlow.Value} is not finite for " +
                    $"hub={s.HubId} candidate={s.CandidateId}.",
                    $"hub={s.HubId} candidate={s.CandidateId}"
                ));
            }

            if (!s.HasEvidence)
            {
                findings.Add(new SystemCiFinding(
                    "CRON_EVIDENCE_MISSING",
                    SystemCiStatus.Gap,
                    $"evidence_json is empty for hub={s.HubId} candidate={s.CandidateId} scope={s.ScopeLimit}. " +
                    "Hub attention record has no transition key evidence.",
                    $"hub={s.HubId} candidate={s.CandidateId}"
                ));
            }
        }

        var result = BuildResult(HubAttentionContinuityTarget, SystemCiInspectionKind.CronContinuity, findings);
        _logger.LogDebug(
            "SystemOperationCiRuntime.InspectHubAttentionContinuityAsync: " +
            "inspected {Count} records — status={Status} findings={FindingCount}.",
            summaries.Count, result.OverallStatus, findings.Count);
        return result;
    }

    /// <summary>
    /// Cron-triggered current rebuildability inspection.
    /// Checks: if hub attention records exist but context_event has no rows,
    /// the materialized current cannot be rebuilt from the append-only log.
    ///
    /// Gap: hub attention records > 0 but context_event count = 0.
    ///   This suggests current may have been seeded without event history,
    ///   violating the rebuildability invariant.
    ///
    /// Pass: either no hub attention records exist, or events are present.
    /// Cron trigger wiring: TODO — background worker / scheduled job integration pending.
    /// </summary>
    public async Task<SystemCiDiagnosticResult> InspectCurrentRebuildabilityAsync(
        CancellationToken ct = default)
    {
        var summaries = await _repository.LoadHubAttentionSummaryForCiAsync(ct);
        var eventCount = await _repository.CountContextEventsAsync(ct);

        var findings = new List<SystemCiFinding>();

        if (summaries.Count > 0 && eventCount == 0)
        {
            findings.Add(new SystemCiFinding(
                "CURRENT_NOT_REBUILDABLE_NO_EVENTS",
                SystemCiStatus.Gap,
                $"context_hub_recommendation_current has {summaries.Count} record(s) " +
                "but context_event has 0 rows. " +
                "Materialized current cannot be rebuilt from append-only log. " +
                "Verify that context_event append is wired correctly before hub attention upsert.",
                null
            ));
        }

        var result = BuildResult(CurrentRebuildabilityTarget, SystemCiInspectionKind.CronContinuity, findings);
        _logger.LogDebug(
            "SystemOperationCiRuntime.InspectCurrentRebuildabilityAsync: " +
            "hubAttentionRecords={HubCount} contextEvents={EventCount} status={Status}.",
            summaries.Count, eventCount, result.OverallStatus);
        return result;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static SystemCiDiagnosticResult BuildResult(
        string target,
        SystemCiInspectionKind kind,
        IReadOnlyList<SystemCiFinding> findings)
    {
        var overallStatus = findings.Count == 0
            ? SystemCiStatus.Pass
            : findings.Any(f => f.Status == SystemCiStatus.Blocking)
                ? SystemCiStatus.Blocking
                : SystemCiStatus.Gap;

        return new SystemCiDiagnosticResult(
            InspectionTarget: target,
            InspectionKind: kind,
            OverallStatus: overallStatus,
            Findings: findings,
            InspectedAt: DateTimeOffset.UtcNow
        );
    }

    private static bool TryParseJsonArray(string json, out int elementCount)
    {
        elementCount = 0;
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            elementCount = doc.RootElement.GetArrayLength();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
