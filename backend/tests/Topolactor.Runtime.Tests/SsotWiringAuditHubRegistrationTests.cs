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
