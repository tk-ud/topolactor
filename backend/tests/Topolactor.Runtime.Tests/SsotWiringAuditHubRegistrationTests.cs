using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// CI lane: hub registration / hub relation route / hub_current / SQL Attention observation boundary audit.
//
// SSOT contract (docs/design/system-ci-admin-runtime-callable-surface.yaml, system-roadmap.yaml):
//   - InspectAsync routes to the correct inspection target by target name (data-driven routing).
//   - All InspectAsync overloads return SystemCiDiagnosticResult (read-only; no DB write).
//   - hub_id empty → HUB_ID_EMPTY Blocking finding (invariant check is wired in hub attention inspector).
//   - InspectAsync with unknown target → ArgumentException (no silent fallback).
//   - InspectedAt is set in every result (not default DateTimeOffset).
//   - InspectionKind is always set (not default).
//   - ListTargets includes all three callable inspection targets.
//
// SQL Attention auto-mutation prohibition:
//   InspectAsync methods return SystemCiDiagnosticResult only — they never mutate hub state,
//   issue DB writes, or auto-repair. The return type structurally enforces this boundary.
public class SsotWiringAuditHubRegistrationTests
{
    private static SystemOperationCiRuntime CreateRuntime(
        ContextRouteRepository? repo = null)
    {
        repo ??= new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "dummy");
        return new SystemOperationCiRuntime(
            NullLogger<SystemOperationCiRuntime>.Instance, repo);
    }

    // ─── Target routing: InspectAsync routes by target name ──────────────────

    [Fact]
    public async Task HubRegistration_InspectAsync_HubAttentionContinuity_ReturnsCorrectInspectionTarget()
    {
        var runtime = CreateRuntime();

        var result = await runtime.InspectAsync("hub_attention_continuity");

        Assert.Equal("hub_attention_continuity", result.InspectionTarget);
    }

    [Fact]
    public async Task HubRegistration_InspectAsync_CurrentRebuildability_ReturnsCorrectInspectionTarget()
    {
        var runtime = CreateRuntime();

        var result = await runtime.InspectAsync("current_rebuildability");

        Assert.Equal("current_rebuildability", result.InspectionTarget);
    }

    [Fact]
    public async Task HubRegistration_InspectAsync_RegistryContinuity_ReturnsCorrectInspectionTarget()
    {
        var runtime = CreateRuntime();

        var result = await runtime.InspectAsync("registry_continuity");

        Assert.Equal("registry_continuity", result.InspectionTarget);
    }

    // ─── Read-only boundary: InspectAsync returns diagnostic result (not void) ─

    [Fact]
    public async Task HubRegistration_InspectAsync_ReturnsNonNullResult()
    {
        // InspectAsync must return a non-null SystemCiDiagnosticResult.
        // A null return would violate the read-only diagnostic boundary contract.
        var runtime = CreateRuntime();

        var result = await runtime.InspectAsync("hub_attention_continuity");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task HubRegistration_InspectAsync_InspectedAtIsSetAfterCallTime()
    {
        // InspectedAt must be a real timestamp, not default(DateTimeOffset).
        // This confirms the result is a fully populated diagnostic struct.
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var runtime = CreateRuntime();

        var result = await runtime.InspectAsync("hub_attention_continuity");

        Assert.True(result.InspectedAt > before,
            "InspectedAt must be set to a current timestamp by the inspection call.");
    }

    [Fact]
    public async Task HubRegistration_InspectAsync_CronTargets_HaveCronContinuityKind()
    {
        // All three cron-triggered targets must carry CronContinuity inspection kind.
        var runtime = CreateRuntime();

        var hubResult = await runtime.InspectAsync("hub_attention_continuity");
        var rebuildResult = await runtime.InspectAsync("current_rebuildability");
        var registryResult = await runtime.InspectAsync("registry_continuity");

        Assert.Equal(SystemCiInspectionKind.CronContinuity, hubResult.InspectionKind);
        Assert.Equal(SystemCiInspectionKind.CronContinuity, rebuildResult.InspectionKind);
        Assert.Equal(SystemCiInspectionKind.CronContinuity, registryResult.InspectionKind);
    }

    // ─── No silent fallback: unknown target throws ────────────────────────────

    [Fact]
    public async Task HubRegistration_InspectAsync_UnknownTarget_ThrowsArgumentException()
    {
        // Per SSOT: no silent fallback for unknown targets.
        // Silently returning Pass for an unknown target would mask wiring drift.
        var runtime = CreateRuntime();

        await Assert.ThrowsAsync<ArgumentException>(
            () => runtime.InspectAsync("unknown_hub_registration_target"));
    }

    // ─── Hub identity invariant: hub_id empty → Blocking ────────────────────

    [Fact]
    public void HubRegistration_HubIdEmpty_ProducesBlockingFinding()
    {
        // Hub identity invariant: hub_id must be non-empty.
        // HUB_ID_EMPTY Blocking finding is the SSOT-defined CI response to this violation.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord() with { HubId = Guid.Empty };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.CheckName == "HUB_ID_EMPTY");
    }

    [Fact]
    public void HubRegistration_ValidHubId_NoHubIdEmptyFinding()
    {
        // A valid (non-empty) hub_id must not trigger HUB_ID_EMPTY.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.DoesNotContain(result.Findings, f => f.CheckName == "HUB_ID_EMPTY");
    }

    // ─── SQL Attention auto-mutation boundary ─────────────────────────────────

    [Fact]
    public void HubRegistration_InspectHubAttentionAfterUpdate_IsEventDrivenKind()
    {
        // Event-driven inspection must carry EventDriven kind (not CronContinuity).
        // This confirms the boundary between event-driven and cron-triggered inspections.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiInspectionKind.EventDriven, result.InspectionKind);
    }

    [Fact]
    public void HubRegistration_InspectHubAttentionAfterUpdate_InspectionTargetIsSqlAttentionContinuity()
    {
        // InspectionTarget must identify the inspection scope, not a DB table or entity.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal("sql_attention_continuity", result.InspectionTarget);
    }

    // ─── ListTargets: all three hub-related callable targets present ──────────

    [Fact]
    public void HubRegistration_ListTargets_ContainsHubAttentionContinuity()
    {
        var runtime = CreateRuntime();

        var targets = runtime.ListTargets();

        Assert.Contains(targets, t => t.Target == "hub_attention_continuity");
    }

    [Fact]
    public void HubRegistration_ListTargets_ContainsCurrentRebuildability()
    {
        var runtime = CreateRuntime();

        var targets = runtime.ListTargets();

        Assert.Contains(targets, t => t.Target == "current_rebuildability");
    }

    [Fact]
    public void HubRegistration_ListTargets_ContainsRegistryContinuity()
    {
        var runtime = CreateRuntime();

        var targets = runtime.ListTargets();

        Assert.Contains(targets, t => t.Target == "registry_continuity");
    }

    [Fact]
    public void HubRegistration_ListTargets_AllTargetsHaveNonEmptyName()
    {
        var runtime = CreateRuntime();

        var targets = runtime.ListTargets();

        Assert.All(targets, t => Assert.False(string.IsNullOrWhiteSpace(t.Target)));
    }

    // ─── SSOT reader connection ───────────────────────────────────────────────

    [Fact]
    public void HubRegistration_SsotReader_CompletionConditions_ContainsPhase1Keys()
    {
        // The SSOT reader connects this test file to docs/system-roadmap.yaml.
        // If a completion_condition key is renamed or deleted in the roadmap, this test breaks.
        var conditions = SsotYamlContractReader.RoadmapCompletionConditions("system_ci.hub_registration");

        Assert.Contains("hub_id_empty_produces_blocking_finding", conditions);
        Assert.Contains("unknown_target_throws_not_silent_fallback", conditions);
        Assert.Contains("inspected_at_and_inspection_kind_are_set_in_every_result", conditions);
    }

    [Fact]
    public void HubRegistration_SsotReader_CompletionConditions_ContainsPhase4Keys()
    {
        var conditions = SsotYamlContractReader.RoadmapCompletionConditions("system_ci.hub_registration");

        Assert.Contains("hub_id_empty_classification_is_missing_required", conditions);
        Assert.Contains("candidate_id_empty_classification_is_missing_required", conditions);
        Assert.Contains("has_evidence_false_classification_is_not_covered", conditions);
        Assert.Contains("non_finite_score_ema_classification_is_runtime_failure", conditions);
        Assert.Contains("ssot_reader_connects_hub_registration_completion_conditions", conditions);
    }

    // ─── Classification: MissingRequired for hub/candidate identity ───────────

    [Fact]
    public void HubRegistration_HubIdEmpty_ClassificationIsMissingRequired()
    {
        // hub_id empty → Classification=MissingRequired (SSOT-defined identity invariant).
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord() with { HubId = Guid.Empty };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        var finding = result.Findings.Single(f => f.CheckName == "HUB_ID_EMPTY");
        Assert.Equal(SystemCiFindingClassification.MissingRequired, finding.Classification);
    }

    [Fact]
    public void HubRegistration_CandidateIdEmpty_ProducesBlockingFinding()
    {
        // candidate_id empty → Blocking status and CANDIDATE_ID_EMPTY finding.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord() with { CandidateId = Guid.Empty };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings,
            f => f.CheckName == "CANDIDATE_ID_EMPTY" && f.Status == SystemCiStatus.Blocking);
    }

    [Fact]
    public void HubRegistration_CandidateIdEmpty_ClassificationIsMissingRequired()
    {
        // candidate_id empty → Classification=MissingRequired.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord() with { CandidateId = Guid.Empty };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        var finding = result.Findings.Single(f => f.CheckName == "CANDIDATE_ID_EMPTY");
        Assert.Equal(SystemCiFindingClassification.MissingRequired, finding.Classification);
    }

    // ─── Classification: NotCovered for HasEvidence=false ─────────────────────

    [Fact]
    public async Task HubRegistration_HasEvidenceFalse_OverallStatusIsGap()
    {
        // HasEvidence=false is an evidence coverage gap, not a blocking invariant violation.
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
    }

    [Fact]
    public async Task HubRegistration_HasEvidenceFalse_ClassificationIsNotCovered()
    {
        // HasEvidence=false → Classification=NotCovered (evidence observation is absent).
        var stub = new StubHubAttentionCiRepository(
        [
            new HubAttentionCiSummary(
                Guid.NewGuid(), Guid.NewGuid(), 1000,
                AttentionScore: 0.8f, EmaFast: 0.5f, EmaSlow: 0.3f,
                HasEvidence: false, UpdatedAt: DateTimeOffset.UtcNow)
        ], eventCount: 5);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectHubAttentionContinuityAsync();

        var finding = result.Findings.Single(f => f.CheckName == "CRON_EVIDENCE_MISSING");
        Assert.Equal(SystemCiFindingClassification.NotCovered, finding.Classification);
    }

    // ─── Classification: RuntimeFailure for non-finite score/EMA ─────────────

    [Fact]
    public void HubRegistration_NonFiniteAttentionScore_ClassificationIsRuntimeFailure()
    {
        // Non-finite attention_score → Classification=RuntimeFailure (computation invariant violated).
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord() with { AttentionScore = float.NaN };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        var finding = result.Findings.Single(f => f.CheckName == "ATTENTION_SCORE_NOT_FINITE");
        Assert.Equal(SystemCiFindingClassification.RuntimeFailure, finding.Classification);
    }

    [Fact]
    public void HubRegistration_NonFiniteEmaFast_ClassificationIsRuntimeFailure()
    {
        // Non-finite ema_fast → Classification=RuntimeFailure.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord() with { EmaFast = float.PositiveInfinity };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        var finding = result.Findings.Single(f => f.CheckName == "EMA_FAST_NOT_FINITE");
        Assert.Equal(SystemCiFindingClassification.RuntimeFailure, finding.Classification);
    }

    [Fact]
    public void HubRegistration_NonFiniteEmaSlow_ClassificationIsRuntimeFailure()
    {
        // Non-finite ema_slow → Classification=RuntimeFailure.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord() with { EmaSlow = float.NegativeInfinity };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        var finding = result.Findings.Single(f => f.CheckName == "EMA_SLOW_NOT_FINITE");
        Assert.Equal(SystemCiFindingClassification.RuntimeFailure, finding.Classification);
    }

    // ─── SQL Attention auto-mutation prohibition (return-type boundary) ────────

    [Fact]
    public void HubRegistration_InspectHubAttentionAfterUpdate_ReturnTypeIsReadOnlyDiagnosticResult()
    {
        // InspectHubAttentionAfterUpdate returns SystemCiDiagnosticResult (read-only struct).
        // The return type structurally prohibits DB write or auto-mutation side effects:
        // callers observe the result but cannot use it to trigger hub state changes.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.IsType<SystemCiDiagnosticResult>(result);
        Assert.NotNull(result.Findings);
        Assert.NotNull(result.InspectionTarget);
    }

    [Fact]
    public async Task HubRegistration_InspectAsync_ReturnTypeIsReadOnlyDiagnosticResult()
    {
        // InspectAsync (cron surface) also returns SystemCiDiagnosticResult.
        // All inspection paths share the same read-only return type boundary.
        var runtime = CreateRuntime();

        var result = await runtime.InspectAsync("hub_attention_continuity");

        Assert.IsType<SystemCiDiagnosticResult>(result);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static HubAttentionCurrentRecord MakeValidHubRecord() =>
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
            AttentionScore:       0.5f,
            Rank:                 1,
            EvidenceJson:         "[]",
            MlpFeatureJson:       "[]",
            UpdatedAt:            DateTimeOffset.UtcNow
        );
}
