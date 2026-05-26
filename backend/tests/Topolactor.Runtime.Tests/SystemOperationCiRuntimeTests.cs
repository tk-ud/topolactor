using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Tests for SystemOperationCiRuntime: event-driven and cron-triggered system CI checks.
///
/// System CI thresholds are hardcoded invariants — not policy values.
/// Tests verify that invariant violations produce the correct status (Blocking / Gap / Pass).
/// </summary>
public class SystemOperationCiRuntimeTests
{
    private static SystemOperationCiRuntime CreateRuntime(
        ContextRouteRepository? repo = null)
    {
        repo ??= new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "dummy");
        return new SystemOperationCiRuntime(
            NullLogger<SystemOperationCiRuntime>.Instance, repo);
    }

    // ─── HubAttentionAfterUpdate: valid record ────────────────────────────────

    [Fact]
    public void InspectHubAttentionAfterUpdate_ValidRecord_ReturnsPass()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubAttentionRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
        Assert.Empty(result.Findings);
        Assert.Equal("sql_attention_continuity", result.InspectionTarget);
        Assert.Equal(SystemCiInspectionKind.EventDriven, result.InspectionKind);
    }

    // ─── HubAttentionAfterUpdate: hub_id empty ────────────────────────────────

    [Fact]
    public void InspectHubAttentionAfterUpdate_EmptyHubId_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubAttentionRecord() with { HubId = Guid.Empty };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "HUB_ID_EMPTY");
        Assert.All(result.Findings.Where(f => f.CheckName == "HUB_ID_EMPTY"),
            f => Assert.Equal(SystemCiStatus.Blocking, f.Status));
    }

    // ─── HubAttentionAfterUpdate: candidate_id empty ─────────────────────────

    [Fact]
    public void InspectHubAttentionAfterUpdate_EmptyCandidateId_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubAttentionRecord() with { CandidateId = Guid.Empty };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "CANDIDATE_ID_EMPTY");
    }

    // ─── HubAttentionAfterUpdate: NaN attention_score ────────────────────────

    [Fact]
    public void InspectHubAttentionAfterUpdate_NaNAttentionScore_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubAttentionRecord() with { AttentionScore = float.NaN };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "ATTENTION_SCORE_NOT_FINITE");
    }

    [Fact]
    public void InspectHubAttentionAfterUpdate_InfinityAttentionScore_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubAttentionRecord() with { AttentionScore = float.PositiveInfinity };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "ATTENTION_SCORE_NOT_FINITE");
    }

    // ─── HubAttentionAfterUpdate: NaN EMA ────────────────────────────────────

    [Fact]
    public void InspectHubAttentionAfterUpdate_NaNEmaFast_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubAttentionRecord() with { EmaFast = float.NaN };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "EMA_FAST_NOT_FINITE");
    }

    [Fact]
    public void InspectHubAttentionAfterUpdate_NaNEmaSlow_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubAttentionRecord() with { EmaSlow = float.NaN };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "EMA_SLOW_NOT_FINITE");
    }

    // ─── HubAttentionAfterUpdate: unparseable JSON ───────────────────────────

    [Fact]
    public void InspectHubAttentionAfterUpdate_InvalidEvidenceJson_ReturnsGap()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubAttentionRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "not-json", "[]");

        Assert.Equal(SystemCiStatus.Gap, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "EVIDENCE_JSON_NOT_PARSEABLE");
    }

    [Fact]
    public void InspectHubAttentionAfterUpdate_InvalidMlpFeatureJson_ReturnsGap()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubAttentionRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "{not-array}");

        Assert.Equal(SystemCiStatus.Gap, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "MLP_FEATURE_JSON_NOT_PARSEABLE");
    }

    [Fact]
    public void InspectHubAttentionAfterUpdate_JsonObjectNotArray_ReturnsGap()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubAttentionRecord();

        // Valid JSON but not an array — should be treated as not-parseable-as-array
        var result = runtime.InspectHubAttentionAfterUpdate(record, "{\"key\":1}", "[]");

        Assert.Equal(SystemCiStatus.Gap, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "EVIDENCE_JSON_NOT_PARSEABLE");
    }

    // ─── InspectEvidenceIntegrity: valid ─────────────────────────────────────

    [Fact]
    public void InspectEvidenceIntegrity_ValidEvidence_ReturnsPass()
    {
        var runtime = CreateRuntime();
        var evidence = new List<TransitionKeyEvidence>
        {
            new("operation", "op:edit", "op:edit", 1.0f, "current_operation")
        };

        var result = runtime.InspectEvidenceIntegrity("op:edit", evidence);

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
        Assert.Empty(result.Findings);
        Assert.Equal("evidence_integrity", result.InspectionTarget);
    }

    [Fact]
    public void InspectEvidenceIntegrity_EmptyEvidenceWithNoOperation_ReturnsPass()
    {
        var runtime = CreateRuntime();

        // No operation → empty evidence is acceptable
        var result = runtime.InspectEvidenceIntegrity(null, []);

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
        Assert.Empty(result.Findings);
    }

    // ─── InspectEvidenceIntegrity: GAP cases ─────────────────────────────────

    [Fact]
    public void InspectEvidenceIntegrity_EmptyEvidenceWithOperation_ReturnsGap()
    {
        var runtime = CreateRuntime();

        var result = runtime.InspectEvidenceIntegrity("op:view", []);

        Assert.Equal(SystemCiStatus.Gap, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "EVIDENCE_EMPTY_WITH_OPERATION");
        Assert.All(result.Findings.Where(f => f.CheckName == "EVIDENCE_EMPTY_WITH_OPERATION"),
            f => Assert.Equal(SystemCiStatus.Gap, f.Status));
    }

    // ─── InspectEvidenceIntegrity: BLOCKING cases ────────────────────────────

    [Fact]
    public void InspectEvidenceIntegrity_NegativeContributionScore_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var evidence = new List<TransitionKeyEvidence>
        {
            new("relation", "rel-id", "rel-id", -0.5f, "active_relation")
        };

        var result = runtime.InspectEvidenceIntegrity("op:edit", evidence);

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "EVIDENCE_NEGATIVE_CONTRIBUTION");
    }

    // ─── InspectFeedbackEvents: valid ────────────────────────────────────────

    [Fact]
    public void InspectFeedbackEvents_ValidEvents_ReturnsPass()
    {
        var runtime = CreateRuntime();
        var hubId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var events = new List<HubFeedbackEvent>
        {
            new(hubId, "context_token_registry", candidateId, "token", 1000, "selected",      0.05f),
            new(hubId, "context_token_registry", candidateId, "token", 1000, "ignored",       -0.02f),
            new(hubId, "context_token_registry", candidateId, "token", 1000, "missing_candidate", 0.03f)
        };

        var result = runtime.InspectFeedbackEvents(events);

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void InspectFeedbackEvents_EmptyList_ReturnsPass()
    {
        var runtime = CreateRuntime();

        var result = runtime.InspectFeedbackEvents([]);

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
    }

    // ─── InspectFeedbackEvents: BLOCKING cases ───────────────────────────────

    [Fact]
    public void InspectFeedbackEvents_ZeroDelta_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var ev = new HubFeedbackEvent(
            Guid.NewGuid(), "tbl", Guid.NewGuid(), "token", 1000, "selected", 0.0f);

        var result = runtime.InspectFeedbackEvents([ev]);

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "FEEDBACK_DELTA_ZERO");
    }

    [Fact]
    public void InspectFeedbackEvents_SelectedWithNegativeDelta_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var ev = new HubFeedbackEvent(
            Guid.NewGuid(), "tbl", Guid.NewGuid(), "token", 1000, "selected", -0.05f);

        var result = runtime.InspectFeedbackEvents([ev]);

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "FEEDBACK_DELTA_SIGN_VIOLATION");
    }

    [Fact]
    public void InspectFeedbackEvents_IgnoredWithPositiveDelta_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var ev = new HubFeedbackEvent(
            Guid.NewGuid(), "tbl", Guid.NewGuid(), "token", 1000, "ignored", 0.05f);

        var result = runtime.InspectFeedbackEvents([ev]);

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "FEEDBACK_DELTA_SIGN_VIOLATION");
    }

    [Fact]
    public void InspectFeedbackEvents_MissingCandidateWithNegativeDelta_ReturnsBlocking()
    {
        var runtime = CreateRuntime();
        var ev = new HubFeedbackEvent(
            Guid.NewGuid(), "tbl", Guid.NewGuid(), "token", 1000, "missing_candidate", -0.03f);

        var result = runtime.InspectFeedbackEvents([ev]);

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "FEEDBACK_DELTA_SIGN_VIOLATION");
    }

    // ─── Cron: InspectHubAttentionContinuityAsync ────────────────────────────

    [Fact]
    public async Task InspectHubAttentionContinuityAsync_EmptySummaries_ReturnsPass()
    {
        var runtime = CreateRuntime();

        var result = await runtime.InspectHubAttentionContinuityAsync();

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
        Assert.Empty(result.Findings);
        Assert.Equal("hub_attention_continuity", result.InspectionTarget);
        Assert.Equal(SystemCiInspectionKind.CronContinuity, result.InspectionKind);
    }

    [Fact]
    public async Task InspectHubAttentionContinuityAsync_NaNAttentionScore_ReturnsBlocking()
    {
        var stub = new StubHubAttentionCiRepository(
        [
            new HubAttentionCiSummary(
                Guid.NewGuid(), Guid.NewGuid(), 1000,
                AttentionScore: float.NaN, EmaFast: 0.5f, EmaSlow: 0.3f,
                HasEvidence: true, UpdatedAt: DateTimeOffset.UtcNow)
        ], eventCount: 10);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectHubAttentionContinuityAsync();

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "CRON_ATTENTION_SCORE_NOT_FINITE");
    }

    [Fact]
    public async Task InspectHubAttentionContinuityAsync_MissingEvidence_ReturnsGap()
    {
        var stub = new StubHubAttentionCiRepository(
        [
            new HubAttentionCiSummary(
                Guid.NewGuid(), Guid.NewGuid(), 1000,
                AttentionScore: 0.8f, EmaFast: 0.5f, EmaSlow: 0.3f,
                HasEvidence: false, UpdatedAt: DateTimeOffset.UtcNow)
        ], eventCount: 5);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectHubAttentionContinuityAsync();

        Assert.Equal(SystemCiStatus.Gap, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "CRON_EVIDENCE_MISSING");
    }

    [Fact]
    public async Task InspectHubAttentionContinuityAsync_ValidSummaries_ReturnsPass()
    {
        var stub = new StubHubAttentionCiRepository(
        [
            new HubAttentionCiSummary(
                Guid.NewGuid(), Guid.NewGuid(), 1000,
                AttentionScore: 0.8f, EmaFast: 0.5f, EmaSlow: 0.3f,
                HasEvidence: true, UpdatedAt: DateTimeOffset.UtcNow)
        ], eventCount: 5);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectHubAttentionContinuityAsync();

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
    }

    // ─── Cron: InspectCurrentRebuildabilityAsync ──────────────────────────────

    [Fact]
    public async Task InspectCurrentRebuildabilityAsync_NoRecords_ReturnsPass()
    {
        var runtime = CreateRuntime();

        var result = await runtime.InspectCurrentRebuildabilityAsync();

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
        Assert.Empty(result.Findings);
        Assert.Equal("current_rebuildability", result.InspectionTarget);
    }

    [Fact]
    public async Task InspectCurrentRebuildabilityAsync_RecordsButNoEvents_ReturnsGap()
    {
        var stub = new StubHubAttentionCiRepository(
        [
            new HubAttentionCiSummary(
                Guid.NewGuid(), Guid.NewGuid(), 1000,
                AttentionScore: 0.5f, EmaFast: null, EmaSlow: null,
                HasEvidence: true, UpdatedAt: DateTimeOffset.UtcNow)
        ], eventCount: 0, feedbackEventCount: 0);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectCurrentRebuildabilityAsync();

        Assert.Equal(SystemCiStatus.Gap, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "CURRENT_NOT_REBUILDABLE_NO_EVENTS");
    }

    [Fact]
    public async Task InspectCurrentRebuildabilityAsync_RecordsWithEvents_ReturnsPass()
    {
        var stub = new StubHubAttentionCiRepository(
        [
            new HubAttentionCiSummary(
                Guid.NewGuid(), Guid.NewGuid(), 1000,
                AttentionScore: 0.5f, EmaFast: null, EmaSlow: null,
                HasEvidence: true, UpdatedAt: DateTimeOffset.UtcNow)
        ], eventCount: 100);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectCurrentRebuildabilityAsync();

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
    }

    [Fact]
    public async Task InspectCurrentRebuildabilityAsync_RecordsNoEventsButHasFeedbackEvents_ReturnsPass()
    {
        // feedback_event count > 0 means a rebuild source exists; Gap must not fire.
        var stub = new StubHubAttentionCiRepository(
        [
            new HubAttentionCiSummary(
                Guid.NewGuid(), Guid.NewGuid(), 1000,
                AttentionScore: 0.5f, EmaFast: null, EmaSlow: null,
                HasEvidence: true, UpdatedAt: DateTimeOffset.UtcNow)
        ], eventCount: 0, feedbackEventCount: 42);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectCurrentRebuildabilityAsync();

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
        Assert.Empty(result.Findings);
    }

    // ─── InspectRegistryContinuityAsync ──────────────────────────────────────

    [Fact]
    public async Task InspectRegistryContinuityAsync_NoActiveTokens_ReturnsPass()
    {
        var stub = new StubRegistryCiRepository(totalActiveTokens: 0, unreferencedTokenCount: 0);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectRegistryContinuityAsync();

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
        Assert.Empty(result.Findings);
        Assert.Equal("registry_continuity", result.InspectionTarget);
        Assert.Equal(SystemCiInspectionKind.CronContinuity, result.InspectionKind);
    }

    [Fact]
    public async Task InspectRegistryContinuityAsync_AllTokensReferenced_ReturnsPass()
    {
        var stub = new StubRegistryCiRepository(totalActiveTokens: 5, unreferencedTokenCount: 0);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectRegistryContinuityAsync();

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task InspectRegistryContinuityAsync_OrphanedTokens_ReturnsGap()
    {
        var stub = new StubRegistryCiRepository(totalActiveTokens: 10, unreferencedTokenCount: 3);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectRegistryContinuityAsync();

        Assert.Equal(SystemCiStatus.Gap, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "CRON_ORPHANED_REGISTRY");
        Assert.All(result.Findings.Where(f => f.CheckName == "CRON_ORPHANED_REGISTRY"),
            f => Assert.Equal(SystemCiStatus.Gap, f.Status));
    }

    [Fact]
    public void SystemCiFindingClassification_Enum_CoversPhase1Vocabulary()
    {
        var expected = new[]
        {
            SystemCiFindingClassification.Pass,
            SystemCiFindingClassification.MissingRequired,
            SystemCiFindingClassification.UndefinedImplementationValue,
            SystemCiFindingClassification.InvalidShape,
            SystemCiFindingClassification.ProhibitedValue,
            SystemCiFindingClassification.NotCovered,
            SystemCiFindingClassification.RuntimeFailure
        };

        Assert.Equal(expected, Enum.GetValues<SystemCiFindingClassification>());
    }

    [Fact]
    public void InspectHubAttentionAfterUpdate_HubIdEmpty_ClassificationIsMissingRequired()
    {
        var runtime = CreateRuntime(new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "dummy"));
        var record = MakeValidHubAttentionRecord() with { HubId = Guid.Empty };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        var finding = Assert.Single(result.Findings.Where(f => f.CheckName == "HUB_ID_EMPTY"));
        Assert.Equal(SystemCiFindingClassification.MissingRequired, finding.Classification);
        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
    }

    [Fact]
    public void InspectHubAttentionAfterUpdate_EvidenceJsonNotParseable_ClassificationIsInvalidShape()
    {
        var runtime = CreateRuntime(new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "dummy"));

        var result = runtime.InspectHubAttentionAfterUpdate(
            MakeValidHubAttentionRecord(), "{", "[]");

        var finding = Assert.Single(result.Findings.Where(f => f.CheckName == "EVIDENCE_JSON_NOT_PARSEABLE"));
        Assert.Equal(SystemCiFindingClassification.InvalidShape, finding.Classification);
        Assert.Equal(SystemCiStatus.Gap, result.OverallStatus);
    }

    [Fact]
    public void InspectEvidenceIntegrity_EmptyEvidenceWithOperation_ClassificationIsNotCovered()
    {
        var runtime = CreateRuntime(new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "dummy"));

        var result = runtime.InspectEvidenceIntegrity("search", []);

        var finding = Assert.Single(result.Findings.Where(f => f.CheckName == "EVIDENCE_EMPTY_WITH_OPERATION"));
        Assert.Equal(SystemCiFindingClassification.NotCovered, finding.Classification);
        Assert.Equal(SystemCiStatus.Gap, result.OverallStatus);
    }

    [Fact]
    public void InspectEvidenceIntegrity_NegativeContribution_ClassificationIsProhibitedValue()
    {
        var runtime = CreateRuntime(new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "dummy"));
        var evidence = new[]
        {
            new TransitionKeyEvidence("token", "id-1", "Token 1", -0.1f, "test negative")
        };

        var result = runtime.InspectEvidenceIntegrity("search", evidence);

        var finding = Assert.Single(result.Findings.Where(f => f.CheckName == "EVIDENCE_NEGATIVE_CONTRIBUTION"));
        Assert.Equal(SystemCiFindingClassification.ProhibitedValue, finding.Classification);
        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
    }

    [Fact]
    public async Task InspectHubAttentionContinuityAsync_NonFiniteScore_ClassificationIsRuntimeFailure()
    {
        var summaries = new[]
        {
            new HubAttentionCiSummary(Guid.NewGuid(), Guid.NewGuid(), 1000, float.NaN, 0.1f, 0.1f, true, DateTimeOffset.UtcNow)
        };
        var runtime = CreateRuntime(new StubHubAttentionCiRepository(summaries, eventCount: 1));

        var result = await runtime.InspectHubAttentionContinuityAsync();

        var finding = Assert.Single(result.Findings.Where(f => f.CheckName == "CRON_ATTENTION_SCORE_NOT_FINITE"));
        Assert.Equal(SystemCiFindingClassification.RuntimeFailure, finding.Classification);
        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
    }

    [Fact]
    public void SystemCiFindingClassification_UndefinedImplementationValue_CanRepresentBlockingDrift()
    {
        var finding = new SystemCiFinding(
            CheckName: "UNDEFINED_IMPLEMENTATION_VALUE_EXAMPLE",
            Status: SystemCiStatus.Blocking,
            Detail: "Observed implementation value is not declared in SSOT allowed vocabulary.",
            Classification: SystemCiFindingClassification.UndefinedImplementationValue);

        Assert.Equal(SystemCiStatus.Blocking, finding.Status);
        Assert.Equal(SystemCiFindingClassification.UndefinedImplementationValue, finding.Classification);
    }

    [Fact]
    public void SystemCiFindingClassification_Pass_IsNotUsedByRuntimeGapOrBlockingFindings()
    {
        var findings = new[]
        {
            new SystemCiFinding("HUB_ID_EMPTY", SystemCiStatus.Blocking, "missing hub", Classification: SystemCiFindingClassification.MissingRequired),
            new SystemCiFinding("CRON_EVIDENCE_MISSING", SystemCiStatus.Gap, "not covered", Classification: SystemCiFindingClassification.NotCovered),
            new SystemCiFinding("CRON_ATTENTION_SCORE_NOT_FINITE", SystemCiStatus.Blocking, "runtime failure", Classification: SystemCiFindingClassification.RuntimeFailure)
        };

        var hasInvalid = findings.Any(f =>
            f.Status != SystemCiStatus.Pass &&
            f.Classification == SystemCiFindingClassification.Pass);

        Assert.False(hasInvalid, "Gap/Blocking findings must use explicit non-Pass classification.");
    }

    [Fact]
    public async Task MissingRequired_Classification_CanBeBlockingOrGap_DependingOnInvariantSeverity()
    {
        var eventRuntime = CreateRuntime(new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "dummy"));
        var eventResult = eventRuntime.InspectHubAttentionAfterUpdate(
            MakeValidHubAttentionRecord() with { HubId = Guid.Empty }, "[]", "[]");
        var blockingFinding = Assert.Single(eventResult.Findings.Where(f => f.CheckName == "HUB_ID_EMPTY"));
        Assert.Equal(SystemCiFindingClassification.MissingRequired, blockingFinding.Classification);
        Assert.Equal(SystemCiStatus.Blocking, blockingFinding.Status);

        var cronRuntime = CreateRuntime(new StubRegistryCiRepository(totalActiveTokens: 10, unreferencedTokenCount: 3));
        var cronResult = await cronRuntime.InspectRegistryContinuityAsync();
        var gapFinding = Assert.Single(cronResult.Findings.Where(f => f.CheckName == "CRON_ORPHANED_REGISTRY"));
        Assert.Equal(SystemCiFindingClassification.MissingRequired, gapFinding.Classification);
        Assert.Equal(SystemCiStatus.Gap, gapFinding.Status);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static HubAttentionCurrentRecord MakeValidHubAttentionRecord() =>
        new(
            HubId:                Guid.NewGuid(),
            TargetTable:          "context_token_registry",
            CandidateKind:        "token",
            CandidateId:          Guid.NewGuid(),
            ScopeLimit:           1000,
            BaseProbability:      null,
            CosineSimilarity:     null,
            StaticRelationWeight: null,
            StatisticalWeight:    1.0f,
            MlpFeatureScore:      null,
            FeedbackAdjustment:   0.0f,
            EmaFast:              0.3f,
            EmaSlow:              0.1f,
            Trend:                0.2f,
            CrossState:           "none",
            AttentionScore:       0.3f,
            Rank:                 1,
            EvidenceJson:         "[]",
            MlpFeatureJson:       "[]",
            UpdatedAt:            DateTimeOffset.UtcNow
        );
}

/// <summary>
/// Stub ContextRouteRepository for cron CI tests that need non-empty hub attention summaries.
/// feedbackEventCount defaults to 0; supply it to test the rebuildability feedback-source path.
/// </summary>
internal sealed class StubHubAttentionCiRepository(
    IReadOnlyList<HubAttentionCiSummary> summaries,
    long eventCount,
    long feedbackEventCount = 0)
    : ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy")
{
    public override Task<IReadOnlyList<HubAttentionCiSummary>> LoadHubAttentionSummaryForCiAsync(
        CancellationToken ct = default)
        => Task.FromResult(summaries);

    public override Task<long> CountContextEventsAsync(CancellationToken ct = default)
        => Task.FromResult(eventCount);

    public override Task<long> CountFeedbackEventsAsync(CancellationToken ct = default)
        => Task.FromResult(feedbackEventCount);
}

/// <summary>
/// Stub ContextRouteRepository for registry continuity CI tests.
/// </summary>
internal sealed class StubRegistryCiRepository(
    int totalActiveTokens,
    int unreferencedTokenCount)
    : ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "dummy")
{
    public override Task<RegistryTokenCiSummary> LoadRegistryTokenSummaryForCiAsync(
        CancellationToken ct = default)
        => Task.FromResult(new RegistryTokenCiSummary(
            TotalActiveTokens:      totalActiveTokens,
            UnreferencedTokenCount: unreferencedTokenCount));
}
