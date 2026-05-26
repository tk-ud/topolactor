using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// CI lane: diagnostics / evidence / eligibility vs persistence boundary audit.
//
// SSOT contract (system-roadmap.yaml, docs/design/system-ci-admin-runtime-callable-surface.yaml):
//   - SystemCiDiagnosticResult.OverallStatus is the worst finding status (Blocking > Gap > Pass).
//   - Pass result has zero findings.
//   - Gap result has at least one Gap finding and no Blocking findings.
//   - Blocking result has at least one Blocking finding.
//   - InspectionKind is always set (EventDriven or CronContinuity, never default).
//   - InspectedAt is set to a real timestamp in every result.
//   - HasEvidence=false leads to Gap (eligibility judgment), not Blocking or Pass.
//   - DiagnosticResult is a read-only struct — callers cannot use it as a persistence trigger.
//   - Findings and OverallStatus are consistent (no orphaned findings at wrong severity).
//
// Out of scope: diagnostics persistence / audit DB write. These tests verify eligibility
// and evidence judgment only. The persistence boundary is separate from the CI check boundary.
public class SsotWiringAuditDiagnosticsEligibilityTests
{
    private static SystemOperationCiRuntime CreateRuntime(
        ContextRouteRepository? repo = null)
    {
        repo ??= new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "dummy");
        return new SystemOperationCiRuntime(
            NullLogger<SystemOperationCiRuntime>.Instance, repo);
    }

    // ─── OverallStatus consistency ────────────────────────────────────────────

    [Fact]
    public void DiagnosticsEligibility_SsotCompletionConditionKey_Exists()
    {
        var keys = SsotYamlContractReader.RoadmapCompletionConditions("system_ci.dotnet_ssot_wiring_audit_tests");

        Assert.Contains("initial_phase_returns_diagnostics_evidence_eligibility_only", keys);
    }

    [Fact]
    public void DiagnosticsEligibility_PassResult_HasZeroFindings()
    {
        // Pass result must have empty Findings list.
        // A Pass with non-empty Findings is inconsistent — indicates aggregation bug.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        if (result.OverallStatus == SystemCiStatus.Pass)
            Assert.Empty(result.Findings);
    }

    [Fact]
    public void DiagnosticsEligibility_OverallStatus_Blocking_RequiresBlockingFinding()
    {
        // Blocking OverallStatus requires at least one Blocking finding.
        // No findings at Blocking severity means OverallStatus must not be Blocking.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord() with { HubId = Guid.Empty }; // triggers HUB_ID_EMPTY Blocking

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.Status == SystemCiStatus.Blocking);
    }

    [Fact]
    public void DiagnosticsEligibility_OverallStatus_Gap_RequiresGapFindingNoBlocking()
    {
        // Gap OverallStatus requires at least one Gap finding and zero Blocking findings.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        // Passing an invalid JSON array for evidence triggers EVIDENCE_JSON_NOT_PARSEABLE (Gap)
        var result = runtime.InspectHubAttentionAfterUpdate(record, "not-valid-json", "[]");

        if (result.OverallStatus == SystemCiStatus.Gap)
        {
            Assert.Contains(result.Findings, f => f.Status == SystemCiStatus.Gap);
            Assert.DoesNotContain(result.Findings, f => f.Status == SystemCiStatus.Blocking);
        }
    }

    [Fact]
    public void DiagnosticsEligibility_NoFindings_OverallStatusIsPass()
    {
        // When no findings are produced, OverallStatus must be Pass.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void DiagnosticsEligibility_BlockingFindingDominatesGap()
    {
        // When both Blocking and Gap findings exist, OverallStatus must be Blocking.
        // The worst finding status wins; Gap is not reported as OverallStatus when Blocking exists.
        var runtime = CreateRuntime();
        // Empty hub_id (Blocking) + invalid evidence JSON (Gap) = Blocking overall
        var record = MakeValidHubRecord() with { HubId = Guid.Empty };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "not-valid-json", "[]");

        Assert.Equal(SystemCiStatus.Blocking, result.OverallStatus);
        Assert.Contains(result.Findings, f => f.Status == SystemCiStatus.Blocking);
        Assert.Contains(result.Findings, f => f.Status == SystemCiStatus.Gap);
    }

    // ─── Evidence eligibility: HasEvidence=false → Gap (not Blocking, not Pass) ─

    [Fact]
    public async Task DiagnosticsEligibility_MissingEvidence_ReturnsGapNotBlocking()
    {
        // HasEvidence=false is an evidence eligibility gap, not a blocking invariant violation.
        // The eligibility/evidence boundary: missing evidence is observable but not system-breaking.
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
        Assert.DoesNotContain(result.Findings, f => f.Status == SystemCiStatus.Blocking);
        Assert.Contains(result.Findings, f =>
            f.CheckName == "CRON_EVIDENCE_MISSING" &&
            f.Classification == SystemCiFindingClassification.NotCovered &&
            f.Status == SystemCiStatus.Gap);
    }

    [Fact]
    public async Task DiagnosticsEligibility_MissingEvidence_HasCronEvidenceMissingCheckName()
    {
        // The specific check name CRON_EVIDENCE_MISSING identifies the evidence eligibility gap.
        var stub = new StubHubAttentionCiRepository(
        [
            new HubAttentionCiSummary(
                Guid.NewGuid(), Guid.NewGuid(), 1000,
                AttentionScore: 0.8f, EmaFast: 0.5f, EmaSlow: 0.3f,
                HasEvidence: false, UpdatedAt: DateTimeOffset.UtcNow)
        ], eventCount: 5);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectHubAttentionContinuityAsync();

        Assert.Contains(result.Findings, f => f.CheckName == "CRON_EVIDENCE_MISSING");
    }

    // ─── InspectionKind: always set ───────────────────────────────────────────

    [Fact]
    public void DiagnosticsEligibility_EventDrivenInspection_InspectionKindIsEventDriven()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.Equal(SystemCiInspectionKind.EventDriven, result.InspectionKind);
    }

    [Fact]
    public async Task DiagnosticsEligibility_CronInspection_InspectionKindIsCronContinuity()
    {
        var runtime = CreateRuntime();

        var result = await runtime.InspectHubAttentionContinuityAsync();

        Assert.Equal(SystemCiInspectionKind.CronContinuity, result.InspectionKind);
    }

    [Fact]
    public void DiagnosticsEligibility_EvidenceIntegrityInspection_InspectionKindIsEventDriven()
    {
        var runtime = CreateRuntime();

        var result = runtime.InspectEvidenceIntegrity(null, []);

        Assert.Equal(SystemCiInspectionKind.EventDriven, result.InspectionKind);
    }

    // ─── InspectedAt: always set ──────────────────────────────────────────────

    [Fact]
    public void DiagnosticsEligibility_EventDrivenResult_InspectedAtIsSet()
    {
        // InspectedAt must not be default(DateTimeOffset) — it must be a real timestamp.
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.True(result.InspectedAt > before,
            "InspectedAt must be set to a timestamp at or after the inspection call.");
        Assert.False(string.IsNullOrWhiteSpace(result.InspectionTarget));
    }

    [Fact]
    public async Task DiagnosticsEligibility_CronResult_InspectedAtIsSet()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var runtime = CreateRuntime();

        var result = await runtime.InspectHubAttentionContinuityAsync();

        Assert.True(result.InspectedAt > before);
        Assert.False(string.IsNullOrWhiteSpace(result.InspectionTarget));
    }

    [Fact]
    public void DiagnosticsEligibility_InvalidEvidenceShape_UsesInvalidShapeClassification()
    {
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "not-json", "[]");

        var finding = Assert.Single(result.Findings.Where(f => f.CheckName == "EVIDENCE_JSON_NOT_PARSEABLE"));
        Assert.Equal(SystemCiFindingClassification.InvalidShape, finding.Classification);
        Assert.Equal(SystemCiStatus.Gap, finding.Status);
    }

    // ─── Persistence boundary: diagnostic result is not void or trigger ───────

    [Fact]
    public void DiagnosticsEligibility_InspectHubAttentionAfterUpdate_ReturnsResultNotVoid()
    {
        // Return type SystemCiDiagnosticResult enforces the read-only diagnostic boundary.
        // There is no way to call this method for side effects without observing its result.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord();

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        Assert.NotNull(result);
        Assert.NotNull(result.InspectionTarget);
        Assert.NotNull(result.Findings);
    }

    [Fact]
    public void DiagnosticsEligibility_DiagnosticResultShape_ContainsNoMutationCommands()
    {
        var propNames = typeof(SystemCiDiagnosticResult).GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain("Persist", propNames);
        Assert.DoesNotContain("Write", propNames);
        Assert.DoesNotContain("Repair", propNames);
        Assert.DoesNotContain("Promote", propNames);
        Assert.DoesNotContain("Mutate", propNames);
        Assert.DoesNotContain("Recommendation", propNames);
    }

    [Fact]
    public void DiagnosticsEligibility_InspectEvidenceIntegrity_ReturnsResultNotVoid()
    {
        var runtime = CreateRuntime();

        var result = runtime.InspectEvidenceIntegrity("op:view", []);

        Assert.NotNull(result);
        Assert.Equal("evidence_integrity", result.InspectionTarget);
    }

    [Fact]
    public void DiagnosticsEligibility_InspectFeedbackEvents_ReturnsResultNotVoid()
    {
        var runtime = CreateRuntime();

        var result = runtime.InspectFeedbackEvents([]);

        Assert.NotNull(result);
        Assert.Equal(SystemCiStatus.Pass, result.OverallStatus);
    }

    // ─── Findings consistency: each finding status matches OverallStatus rules ─

    [Fact]
    public async Task DiagnosticsEligibility_RegistryContinuity_OrphanedTokens_GapFindingHasGapStatus()
    {
        // Each Gap finding must have Status=Gap, not Blocking or Pass.
        var stub = new StubRegistryCiRepository(
            totalActiveTokens: 10, unreferencedTokenCount: 3);
        var runtime = CreateRuntime(stub);

        var result = await runtime.InspectRegistryContinuityAsync();

        Assert.Equal(SystemCiStatus.Gap, result.OverallStatus);
        Assert.All(result.Findings, f => Assert.Equal(SystemCiStatus.Gap, f.Status));
    }

    [Fact]
    public void DiagnosticsEligibility_HubIdEmpty_BlockingFindingHasBlockingStatus()
    {
        // Each Blocking finding must have Status=Blocking.
        var runtime = CreateRuntime();
        var record = MakeValidHubRecord() with { HubId = Guid.Empty };

        var result = runtime.InspectHubAttentionAfterUpdate(record, "[]", "[]");

        var blockingFindings = result.Findings.Where(f => f.CheckName == "HUB_ID_EMPTY").ToList();
        Assert.NotEmpty(blockingFindings);
        Assert.All(blockingFindings, f => Assert.Equal(SystemCiStatus.Blocking, f.Status));
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
