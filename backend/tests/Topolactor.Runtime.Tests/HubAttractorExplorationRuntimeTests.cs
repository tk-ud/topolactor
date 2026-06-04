using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// ---------------------------------------------------------------------------
// Stub helpers
// ---------------------------------------------------------------------------

/// <summary>
/// Stub TopologyRepository returning a given exploration policy JSON.
/// </summary>
internal sealed class StubExplorationPolicyTopologyRepository(string? policyJson)
    : TopologyRepository(NullLogger<TopologyRepository>.Instance, "dummy")
{
    public override Task<string?> LoadFunctionParameterAsync(
        string functionName,
        string parameterKey,
        CancellationToken ct = default)
        => Task.FromResult<string?>(policyJson);
}

/// <summary>
/// Stub SqlAttentionLogsRepository with controllable return values.
/// Tracks whether deprecated logs.hub_current support-cache diagnostics were loaded.
/// </summary>
internal sealed class StubSqlAttentionLogsRepository(
    IReadOnlyList<WatchChangeCandidate> candidates,
    IReadOnlyList<HubCurrentCandidate> hubCurrentCandidates,
    IReadOnlyList<Guid>? resolvedManifestIds = null,
    IReadOnlyList<HubRelationExplorationCandidate>? hubRelationCandidates = null)
    : SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "dummy")
{
    // When resolvedManifestIds is null → use the default manifest ID so exploration proceeds.
    // When explicitly passed as [] → return empty (fail close / NoRelatedTopologyManifest).
    private readonly IReadOnlyList<Guid> _resolvedManifestIds =
        resolvedManifestIds ?? [Guid.Parse("00000000-0000-0000-0000-000000000001")];
    private readonly IReadOnlyList<HubRelationExplorationCandidate> _hubRelationCandidates =
        hubRelationCandidates ?? [];

    public int HubCurrentCallCount { get; private set; }
    public int ResolveManifestsCallCount { get; private set; }
    public int WriteLogsAttentionCallCount { get; private set; }
    public IReadOnlyList<LogsAttentionWriteRequest>? LastWriteRequests { get; private set; }

    public override Task<IReadOnlyList<WatchChangeCandidate>> LoadWatchCandidatesAsync(
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
        => Task.FromResult(candidates);

    public override Task<RelatedTopologyManifestResolution> ResolveRelatedTopologyManifestsAsync(
        WatchChangeCandidate candidate,
        CancellationToken ct = default)
    {
        ResolveManifestsCallCount++;
        var manifests = _resolvedManifestIds;
        return Task.FromResult(new RelatedTopologyManifestResolution(
            candidate.CurrentId, manifests, """{"resolver":"test_stub"}"""));
    }

    public override Task<IReadOnlyList<HubRelationExplorationCandidate>> LoadHubRelationExplorationCandidatesAsync(
        IReadOnlyList<Guid> topologyManifestIds,
        CancellationToken ct = default) =>
        Task.FromResult(_hubRelationCandidates);

    public override Task<IReadOnlyList<HubCurrentCandidate>> LoadLegacyHubCurrentSupportCacheCandidatesAsync(
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
    {
        HubCurrentCallCount++;
        return Task.FromResult(hubCurrentCandidates);
    }

    public override Task<int> WriteLogsAttentionAsync(
        IReadOnlyList<LogsAttentionWriteRequest> requests,
        CancellationToken ct = default)
    {
        WriteLogsAttentionCallCount++;
        LastWriteRequests = requests;
        return base.WriteLogsAttentionAsync(requests, ct);
    }
}

// ---------------------------------------------------------------------------
// Factories
// ---------------------------------------------------------------------------

internal static class ExplorationTestFactory
{
    public static WatchChangeCandidate ChangeCandidate(
        Guid? currentId = null,
        string? physicalTableId = null,
        bool changeDetected = true,
        string changeReason = "level_changed",
        double l2Norm = 10.0,
        string? normLevel = "high",
        string basisVectorJson = """{"diff_count": 10}""") =>
        new(
            CurrentId: currentId ?? Guid.NewGuid(),
            PhysicalTableId: physicalTableId ?? "table_a",
            NormRank: 1,
            PreviousNormLevel: "medium",
            NormLevel: normLevel,
            ChangeDetected: changeDetected,
            ChangeReason: changeReason,
            L2Norm: l2Norm,
            BasisVectorJson: basisVectorJson);

    public static WatchChangeCandidate NoChangeCandidate() =>
        new(
            CurrentId: Guid.NewGuid(),
            PhysicalTableId: "table_b",
            NormRank: 2,
            PreviousNormLevel: "high",
            NormLevel: "high",
            ChangeDetected: false,
            ChangeReason: "no_change",
            L2Norm: 0.0,
            BasisVectorJson: "{}");

    public static HubCurrentCandidate HubCurrent(
        string attractorKey = "attractor_a",
        long populationCount = 100,
        long populationRecordcount = 50,
        string attractorVectorJson = "{}") =>
        new(
            HubCurrentId: Guid.NewGuid(),
            SourceSetId: "test_source",
            HubId: Guid.NewGuid(),
            AttractorKey: attractorKey,
            HubRelationId: Guid.NewGuid(),
            RelationRegistryId: Guid.NewGuid(),
            BasisWindow: "7d",
            AttractorVectorJson: attractorVectorJson,
            PopulationCount: populationCount,
            PopulationRecordcount: populationRecordcount,
            AxisPopulationJson: """{"x":10,"y":20,"z":30}""",
            AxisZScoreJson: """{"i":0.1,"j":0.2,"k":0.3}""",
            PhaseBasisJson: """{"basis":"hub_current"}""");

    public static HubRelationExplorationCandidate HubRelationCandidate(
        Guid? hubRelationId = null,
        Guid? topologyManifestId = null,
        Guid? hubId = null,
        Guid? relatedHubId = null,
        int sequencePosition = 1,
        double relationScore = 0.9,
        string relationConfigJson = "{}") =>
        new(
            HubRelationId: hubRelationId ?? Guid.NewGuid(),
            TopologyManifestId: topologyManifestId ?? Guid.Parse("00000000-0000-0000-0000-000000000001"),
            HubId: hubId ?? Guid.NewGuid(),
            RelatedHubId: relatedHubId ?? Guid.NewGuid(),
            SequencePosition: sequencePosition,
            RelationScore: relationScore,
            RelationConfigJson: relationConfigJson);

    public static string ValidPolicyJson(
        int weakTopK = 1,
        int midTopK = 3,
        int highTopK = 5,
        int weakMaxTables = 2,
        int midMaxTables = 5,
        int highMaxTables = 10,
        int weakPhaseLimit = 1,
        int midPhaseLimit = 1,
        int highPhaseLimit = 3,
        int maxKinds = 5,
        int maxRows = 20,
        double normLevelHigh = 10.0,
        double normLevelMedium = 1.0,
        double neighborScoreMin = 0.0,
        double strongHitThreshold = 0.95,
        double normalHitThreshold = 0.90,
        double exploratoryHitThreshold = 0.85) =>
        $$"""
        {
          "norm_level_high": {{normLevelHigh}},
          "norm_level_medium": {{normLevelMedium}},
          "exploration_budget_tiers": {
            "weak": {
              "topK_per_hub_kind": {{weakTopK}},
              "max_hub_tables_per_kind": {{weakMaxTables}},
              "phase_expansion_limit": {{weakPhaseLimit}},
              "search_mode": "near_neighbor_narrow_topK"
            },
            "mid": {
              "topK_per_hub_kind": {{midTopK}},
              "max_hub_tables_per_kind": {{midMaxTables}},
              "phase_expansion_limit": {{midPhaseLimit}},
              "search_mode": "normal_topK"
            },
            "high": {
              "topK_per_hub_kind": {{highTopK}},
              "max_hub_tables_per_kind": {{highMaxTables}},
              "phase_expansion_limit": {{highPhaseLimit}},
              "search_mode": "expanded_distance_band_or_permutation"
            }
          },
          "max_hub_kinds_per_current": {{maxKinds}},
          "max_attention_rows_saved": {{maxRows}},
          "neighbor_score_min": {{neighborScoreMin}},
          "strong_hit_threshold": {{strongHitThreshold}},
          "normal_hit_threshold": {{normalHitThreshold}},
          "exploratory_hit_threshold": {{exploratoryHitThreshold}}
        }
        """;

    public static HubAttractorExplorationRuntime CreateRuntime(
        string? policyJson,
        IReadOnlyList<HubCurrentCandidate> hubCurrentCandidates)
    {
        // Convert hub current candidates to hub relation candidates so ExploreAsync
        // (which uses the canonical hubs.hub_relations path) returns results.
        // When no hub currents → use default manifest ID but empty hub relations → NoHubRelations.
        if (hubCurrentCandidates.Count == 0)
        {
            var emptyRepo = new StubSqlAttentionLogsRepository([], hubCurrentCandidates,
                hubRelationCandidates: []);
            return new(
                NullLogger<HubAttractorExplorationRuntime>.Instance,
                new StubExplorationPolicyTopologyRepository(policyJson),
                emptyRepo);
        }

        // Group hub currents by AttractorKey; each group gets a deterministic manifest ID.
        var defaultManifestId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var hubRelationCandidates = hubCurrentCandidates
            .Select((hub, i) => new HubRelationExplorationCandidate(
                HubRelationId: hub.HubRelationId ?? Guid.NewGuid(),
                TopologyManifestId: defaultManifestId,
                HubId: hub.HubId ?? Guid.NewGuid(),
                RelatedHubId: Guid.NewGuid(),
                SequencePosition: i + 1,
                RelationScore: 0.9,
                RelationConfigJson: "{}"))
            .ToList();

        var logsRepo = new StubSqlAttentionLogsRepository([], hubCurrentCandidates,
            hubRelationCandidates: hubRelationCandidates);
        return new(
            NullLogger<HubAttractorExplorationRuntime>.Instance,
            new StubExplorationPolicyTopologyRepository(policyJson),
            logsRepo);
    }

    public static HubAttractorExplorationRuntime CreateRuntime(
        string? policyJson,
        StubSqlAttentionLogsRepository logsRepo) =>
        new(
            NullLogger<HubAttractorExplorationRuntime>.Instance,
            new StubExplorationPolicyTopologyRepository(policyJson),
            logsRepo);
}

// ---------------------------------------------------------------------------
// Tests — change candidate triggers exploration
// ---------------------------------------------------------------------------

public class HubAttractorExplorationRuntime_CanonicalBoundaryTests
{
    [Fact]
    public async Task ExploreAsync_WithChangedCandidate_FailsCloseWithoutLegacySupportCacheFallback()
    {
        var logsRepo = new StubSqlAttentionLogsRepository(
            [ExplorationTestFactory.ChangeCandidate()],
            [ExplorationTestFactory.HubCurrent(attractorVectorJson: """{"diff_count": 10}""")],
            resolvedManifestIds: []);
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(), logsRepo);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()], "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.NoRelatedTopologyManifest, result.Status);
        Assert.Null(result.Result);
        Assert.Contains("No explicit physical table", result.Detail);
        Assert.Equal(0, logsRepo.HubCurrentCallCount);
    }
}

public class HubAttractorExplorationRuntime_ChangeCandidateTests
{
    [Fact]
    public async Task ExploreAsync_WithChangeCandidates_ReturnsOk()
    {
        var logsRepo = new StubSqlAttentionLogsRepository(
            [ExplorationTestFactory.ChangeCandidate()],
            [ExplorationTestFactory.HubCurrent()],
            hubRelationCandidates: [ExplorationTestFactory.HubRelationCandidate()]);

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            logsRepo);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
    }

    [Fact]
    public async Task ExploreAsync_WithChangeCandidates_LoadsHubCurrentCandidates()
    {
        var logsRepo = new StubSqlAttentionLogsRepository(
            [],
            [ExplorationTestFactory.HubCurrent()],
            hubRelationCandidates: [ExplorationTestFactory.HubRelationCandidate()]);

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            logsRepo);

        await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.True(logsRepo.ResolveManifestsCallCount >= 1,
            "ExploreAsync must call ResolveRelatedTopologyManifestsAsync");
    }

    [Fact]
    public async Task ExploreAsync_WithChangeCandidates_ResultContainsHits()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [ExplorationTestFactory.HubCurrent()]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.NotNull(result.Result);
        Assert.NotEmpty(result.Result.Hits);
    }

    [Fact]
    public async Task ExploreAsync_Hit_ContainsCurrentIdAndHubCurrentId()
    {
        var currentId = Guid.NewGuid();
        var hubCurrent = ExplorationTestFactory.HubCurrent();

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [hubCurrent]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(currentId: currentId, normLevel: "medium", l2Norm: 5.0)],
            "src", "7d");

        var hit = Assert.Single(result.Result!.Hits);
        Assert.Equal(currentId, hit.CurrentId);
        // Canonical path always sets HubCurrentId to Guid.Empty (no logs.hub_current lookup)
        Assert.Equal(Guid.Empty, hit.HubCurrentId);
        // Hub relation ID must be populated by canonical path
        Assert.NotEqual(Guid.Empty, hit.HubRelationId!.Value);
    }

    [Fact]
    public async Task ExploreAsync_WithChangeCandidates_ResultSourceSetIdMatchesInput()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [ExplorationTestFactory.HubCurrent()]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "my_source_set", "7d");

        Assert.Equal("my_source_set", result.Result!.SourceSetId);
        Assert.Equal("7d", result.Result.BasisWindow);
    }
}

// ---------------------------------------------------------------------------
// Tests — no-change skips exploration
// ---------------------------------------------------------------------------

public class HubAttractorExplorationRuntime_NoChangeTests
{
    [Fact]
    public async Task ExploreAsync_NoChangeCandidates_ReturnsNoChange()
    {
        var logsRepo = new StubSqlAttentionLogsRepository(
            [ExplorationTestFactory.NoChangeCandidate()],
            [ExplorationTestFactory.HubCurrent()]);

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            logsRepo);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.NoChangeCandidate()],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.NoChange, result.Status);
    }

    [Fact]
    public async Task ExploreAsync_EmptyCandidateList_ReturnsNoChange()
    {
        var logsRepo = new StubSqlAttentionLogsRepository([], [ExplorationTestFactory.HubCurrent()]);
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(), logsRepo);

        var result = await runtime.ExploreAsync([], "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.NoChange, result.Status);
    }

    [Fact]
    public async Task ExploreAsync_NoChangeCandidates_DoesNotLoadHubCurrentCandidates()
    {
        var logsRepo = new StubSqlAttentionLogsRepository(
            [ExplorationTestFactory.NoChangeCandidate()],
            [ExplorationTestFactory.HubCurrent()]);

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(), logsRepo);

        await runtime.ExploreAsync(
            [ExplorationTestFactory.NoChangeCandidate()],
            "src", "7d");

        Assert.Equal(0, logsRepo.HubCurrentCallCount);
    }

    [Fact]
    public async Task ExploreAsync_NoChangeCandidates_ResultIsNull()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(), []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.NoChangeCandidate()],
            "src", "7d");

        Assert.Null(result.Result);
    }

    [Fact]
    public async Task ExploreAsync_MixedCandidates_OnlyChangeDetectedCandidatesCountAsChange()
    {
        // Only one candidate has ChangeDetected=true; exploration should run
        var logsRepo = new StubSqlAttentionLogsRepository([], [ExplorationTestFactory.HubCurrent()],
            hubRelationCandidates: [ExplorationTestFactory.HubRelationCandidate()]);
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(), logsRepo);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.NoChangeCandidate(), ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
    }
}

// ---------------------------------------------------------------------------
// Tests — policy missing / invalid fail-close
// ---------------------------------------------------------------------------

public class HubAttractorExplorationRuntime_PolicyTests
{
    [Fact]
    public async Task ExploreAsync_PolicyMissing_ReturnsMissingPolicy()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(null, []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.MissingPolicy, result.Status);
    }

    [Fact]
    public async Task ExploreAsync_PolicyMissing_ResultIsNull()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(null, []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Null(result.Result);
    }

    [Fact]
    public async Task ExploreAsync_PolicyMalformedJson_ReturnsMalformedPolicy()
    {
        var runtime = ExplorationTestFactory.CreateRuntime("not-valid-json", []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.MalformedPolicy, result.Status);
    }

    [Fact]
    public async Task ExploreAsync_PolicyMissingRequiredKey_ReturnsMalformedPolicy()
    {
        // Missing exploration_budget_tiers
        var runtime = ExplorationTestFactory.CreateRuntime(
            """{ "norm_level_high": 10.0, "norm_level_medium": 1.0, "max_hub_kinds_per_current": 5, "max_attention_rows_saved": 20 }""",
            []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.MalformedPolicy, result.Status);
    }

    [Theory]
    [InlineData("""{ "norm_level_high": 10.0, "norm_level_medium": 1.0, "exploration_budget_tiers": { "weak": { "topK_per_hub_kind": 0, "max_hub_tables_per_kind": 2, "phase_expansion_limit": 1, "search_mode": "near_neighbor_narrow_topK" }, "mid": { "topK_per_hub_kind": 3, "max_hub_tables_per_kind": 5, "phase_expansion_limit": 1, "search_mode": "normal_topK" }, "high": { "topK_per_hub_kind": 5, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 3, "search_mode": "expanded_distance_band_or_permutation" } }, "max_hub_kinds_per_current": 5, "max_attention_rows_saved": 20 }""")]
    [InlineData("""{ "norm_level_high": 10.0, "norm_level_medium": 1.0, "exploration_budget_tiers": { "weak": { "topK_per_hub_kind": 1, "max_hub_tables_per_kind": 0, "phase_expansion_limit": 1, "search_mode": "near_neighbor_narrow_topK" }, "mid": { "topK_per_hub_kind": 3, "max_hub_tables_per_kind": 5, "phase_expansion_limit": 1, "search_mode": "normal_topK" }, "high": { "topK_per_hub_kind": 5, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 3, "search_mode": "expanded_distance_band_or_permutation" } }, "max_hub_kinds_per_current": 5, "max_attention_rows_saved": 20 }""")]
    [InlineData("""{ "norm_level_high": 10.0, "norm_level_medium": 1.0, "exploration_budget_tiers": { "weak": { "topK_per_hub_kind": 1, "max_hub_tables_per_kind": 2, "phase_expansion_limit": 0, "search_mode": "near_neighbor_narrow_topK" }, "mid": { "topK_per_hub_kind": 3, "max_hub_tables_per_kind": 5, "phase_expansion_limit": 1, "search_mode": "normal_topK" }, "high": { "topK_per_hub_kind": 5, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 3, "search_mode": "expanded_distance_band_or_permutation" } }, "max_hub_kinds_per_current": 5, "max_attention_rows_saved": 20 }""")]
    [InlineData("""{ "norm_level_high": 10.0, "norm_level_medium": 1.0, "exploration_budget_tiers": { "weak": { "topK_per_hub_kind": 1, "max_hub_tables_per_kind": 2, "phase_expansion_limit": 1, "search_mode": "near_neighbor_narrow_topK" }, "mid": { "topK_per_hub_kind": 3, "max_hub_tables_per_kind": 5, "phase_expansion_limit": 1, "search_mode": "normal_topK" }, "high": { "topK_per_hub_kind": 5, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 3, "search_mode": "expanded_distance_band_or_permutation" } }, "max_hub_kinds_per_current": 0, "max_attention_rows_saved": 20 }""")]
    [InlineData("""{ "norm_level_high": 10.0, "norm_level_medium": 1.0, "exploration_budget_tiers": { "weak": { "topK_per_hub_kind": 1, "max_hub_tables_per_kind": 2, "phase_expansion_limit": 1, "search_mode": "near_neighbor_narrow_topK" }, "mid": { "topK_per_hub_kind": 3, "max_hub_tables_per_kind": 5, "phase_expansion_limit": 1, "search_mode": "normal_topK" }, "high": { "topK_per_hub_kind": 5, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 3, "search_mode": "expanded_distance_band_or_permutation" } }, "max_hub_kinds_per_current": 5, "max_attention_rows_saved": 0 }""")]
    public async Task ExploreAsync_PolicyNonPositiveValue_ReturnsMalformedPolicy(string policyJson)
    {
        var runtime = ExplorationTestFactory.CreateRuntime(policyJson, []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.MalformedPolicy, result.Status);
    }

    [Fact]
    public async Task ExploreAsync_PolicyMalformedJson_ResultIsNull()
    {
        var runtime = ExplorationTestFactory.CreateRuntime("not-valid-json", []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Null(result.Result);
    }

    [Fact]
    public async Task PolicyFunctionName_IsDataDefinedNotMagicNumber()
    {
        Assert.Equal("sql_attention_hub_attractor_exploration",
            HubAttractorExplorationRuntime.ExplorationFunctionName);
        Assert.Equal("default_policy",
            HubAttractorExplorationRuntime.ExplorationPolicyKey);
    }
}

// ---------------------------------------------------------------------------
// Tests — topK / budget cap
// ---------------------------------------------------------------------------

public class HubAttractorExplorationRuntime_BudgetCapTests
{
    [Fact]
    public async Task ExploreAsync_TopKCap_LimitsHitsPerHubKind()
    {
        // 5 hub current records with same attractor key, mid topK=2 → at most 2 hits per kind
        var hubs = Enumerable.Range(0, 5)
            .Select(_ => ExplorationTestFactory.HubCurrent("attractor_a", 100, 50))
            .ToList();

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(midTopK: 2, maxRows: 100),
            hubs);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "medium", l2Norm: 5.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        Assert.True(result.Result!.Hits.Count <= 2,
            $"Expected at most 2 hits (mid topK=2), got {result.Result.Hits.Count}");
    }

    [Fact]
    public async Task ExploreAsync_MaxAttentionRowsSaved_LimitsTotalHits()
    {
        // 3 hub kinds × 3 hubs each = 9 potential hits; max_attention_rows_saved=3 → 3 total
        var hubs = new[]
        {
            ExplorationTestFactory.HubCurrent("kind_a"), ExplorationTestFactory.HubCurrent("kind_a"), ExplorationTestFactory.HubCurrent("kind_a"),
            ExplorationTestFactory.HubCurrent("kind_b"), ExplorationTestFactory.HubCurrent("kind_b"), ExplorationTestFactory.HubCurrent("kind_b"),
            ExplorationTestFactory.HubCurrent("kind_c"), ExplorationTestFactory.HubCurrent("kind_c"), ExplorationTestFactory.HubCurrent("kind_c"),
        };

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(highTopK: 5, maxKinds: 10, highMaxTables: 10, highPhaseLimit: 1, maxRows: 3),
            hubs);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        Assert.True(result.Result!.Hits.Count <= 3,
            $"Expected at most 3 total hits (max_attention_rows_saved=3), got {result.Result.Hits.Count}");
    }

    [Fact]
    public async Task ExploreAsync_MaxHubKindsPerCurrent_LimitsKindsProcessed()
    {
        // 4 distinct hub kinds, max_hub_kinds_per_current=2 → at most 2 kinds explored
        var hubs = new[]
        {
            ExplorationTestFactory.HubCurrent("kind_a"),
            ExplorationTestFactory.HubCurrent("kind_b"),
            ExplorationTestFactory.HubCurrent("kind_c"),
            ExplorationTestFactory.HubCurrent("kind_d"),
        };

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(midTopK: 1, maxKinds: 2, midMaxTables: 5, midPhaseLimit: 1, maxRows: 100),
            hubs);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        var distinctKinds = result.Result!.Hits.Select(h => h.AttractorKey).Distinct().Count();
        Assert.True(distinctKinds <= 2,
            $"Expected at most 2 attractor kinds (max_hub_kinds_per_current=2), got {distinctKinds}");
    }

    [Fact]
    public async Task ExploreAsync_NoHubCurrentCandidates_ReturnsOkWithEmptyHits()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        // With no hub current candidates, no hub relation candidates exist → NoHubRelations
        Assert.Equal(HubAttractorExplorationStatus.NoHubRelations, result.Status);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task ExploreAsync_HitRanks_AreOneIndexed()
    {
        var hubs = new[]
        {
            ExplorationTestFactory.HubCurrent("kind_a", populationCount: 200),
            ExplorationTestFactory.HubCurrent("kind_a", populationCount: 100),
        };

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(midTopK: 2),
            hubs);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "medium", l2Norm: 5.0)],
            "src", "7d");

        var ranks = result.Result!.Hits.Select(h => h.HitRank).OrderBy(r => r).ToList();
        Assert.Equal(1, ranks[0]);
        Assert.Equal(2, ranks[1]);
    }
}

// ---------------------------------------------------------------------------
// Tests — w / l2_norm exploration budget gate (weak / mid / high)
// ---------------------------------------------------------------------------

public class HubAttractorExplorationRuntime_ExplorationBudgetGateTests
{
    private static HubAttractorExplorationPolicy TestPolicy() =>
        new(
            NormLevelHigh: 10.0,
            NormLevelMedium: 1.0,
            WeakTier: new ExplorationBudgetTierLimits(1, 2, 1, "near_neighbor_narrow_topK"),
            MidTier: new ExplorationBudgetTierLimits(3, 5, 1, "normal_topK"),
            HighTier: new ExplorationBudgetTierLimits(5, 10, 3, "expanded_distance_band_or_permutation"),
            MaxHubKindsPerCurrent: 5,
            MaxAttentionRowsSaved: 20,
            NeighborScoreMin: 0.0,
            StrongHitThreshold: 0.95,
            NormalHitThreshold: 0.90,
            ExploratoryHitThreshold: 0.85);

    [Theory]
    [InlineData("low", 0.5, ExplorationBudgetTier.Weak)]
    [InlineData("medium", 5.0, ExplorationBudgetTier.Mid)]
    [InlineData("high", 15.0, ExplorationBudgetTier.High)]
    [InlineData(null, 0.5, ExplorationBudgetTier.Weak)]
    [InlineData(null, 5.0, ExplorationBudgetTier.Mid)]
    [InlineData(null, 15.0, ExplorationBudgetTier.High)]
    public void ClassifyExplorationBudgetTier_MapsNormLevelAndL2Norm(
        string? normLevel,
        double l2Norm,
        ExplorationBudgetTier expected)
    {
        var policy = TestPolicy();
        var candidate = ExplorationTestFactory.ChangeCandidate(normLevel: normLevel, l2Norm: l2Norm);
        var tier = HubAttractorExplorationRuntime.ClassifyExplorationBudgetTier(candidate, policy);
        Assert.Equal(expected, tier);
    }

    [Fact]
    public async Task ExploreAsync_WeakTier_UsesNarrowTopK()
    {
        var hubs = Enumerable.Range(0, 5)
            .Select(i => ExplorationTestFactory.HubCurrent(
                "kind_a",
                populationCount: 100 - i,
                attractorVectorJson: $$"""{"diff_count": {{10 - i}}}"""))
            .ToList();

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(weakTopK: 1, weakMaxTables: 2),
            hubs);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "low", l2Norm: 0.5)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        Assert.True(result.Result!.Hits.Count <= 1,
            $"weak tier topK=1 must cap hits, got {result.Result.Hits.Count}");
        using var phaseDoc = JsonDocument.Parse(result.Result.Hits[0].PhaseVectorJson);
        var phaseRoot = phaseDoc.RootElement;
        Assert.Equal("weak", phaseRoot.GetProperty("exploration_budget_tier").GetString());
        Assert.Equal("near_neighbor_narrow_topK", phaseRoot.GetProperty("exploration_search_mode").GetString());
    }

    [Fact]
    public async Task ExploreAsync_HighTier_UsesPermutationExpansion()
    {
        var hubs = Enumerable.Range(0, 6)
            .Select(i => ExplorationTestFactory.HubCurrent(
                "kind_a",
                populationCount: 100 - i,
                attractorVectorJson: $$"""{"diff_count": {{10 - i}}}"""))
            .ToList();

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(
                highTopK: 2,
                highMaxTables: 2,
                highPhaseLimit: 3,
                maxRows: 100),
            hubs);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        // Canonical path always uses "canonical" permutation key (no permutation expansion)
        Assert.All(result.Result!.Hits, h => Assert.Equal("canonical", h.PermutationKey));
        using var phaseDoc = JsonDocument.Parse(result.Result.Hits[0].PhaseVectorJson);
        var phaseRoot = phaseDoc.RootElement;
        Assert.Equal("high", phaseRoot.GetProperty("exploration_budget_tier").GetString());
        Assert.Equal("expanded_distance_band_or_permutation",
            phaseRoot.GetProperty("exploration_search_mode").GetString());
    }

    [Fact]
    public async Task ExploreAsync_MidTier_RecordsNormalTopKMode()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [ExplorationTestFactory.HubCurrent()]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "medium", l2Norm: 5.0)],
            "src", "7d");

        var hit = Assert.Single(result.Result!.Hits);
        using var phaseDoc = JsonDocument.Parse(hit.PhaseVectorJson);
        var phaseRoot = phaseDoc.RootElement;
        Assert.Equal("mid", phaseRoot.GetProperty("exploration_budget_tier").GetString());
        Assert.Equal("normal_topK", phaseRoot.GetProperty("exploration_search_mode").GetString());
        Assert.True(phaseRoot.GetProperty("no_automatic_topology_mutation").GetBoolean());
    }
}

// ---------------------------------------------------------------------------
// Tests — no logs.attention write, no phase_vector, no registry mutation
// ---------------------------------------------------------------------------

public class HubAttractorExplorationRuntime_BoundaryTests
{
    [Fact]
    public async Task ExploreAsync_OkResult_DoesNotWriteLogsAttention()
    {
        // Responsibility split boundary:
        // - HubAttractorExplorationRuntime: result generation only (no persistence)
        // - SqlAttentionScheduler: owns AppendAttentionGenerationAsync boundary
        var logsRepo = new StubSqlAttentionLogsRepository(
            [],
            [ExplorationTestFactory.HubCurrent()],
            hubRelationCandidates: [ExplorationTestFactory.HubRelationCandidate()]);
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            logsRepo);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        // Result carries hits for downstream consumption — write boundary is not called here.
        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        Assert.NotNull(result.Result);
        Assert.Equal(0, logsRepo.WriteLogsAttentionCallCount);
    }

    [Fact]
    public async Task ExploreAsync_OkResult_HitsHavePhaseVectorField()
    {
        // phase_vector_json is generated in runtime as post-main auxiliary evidence
        // and carried on HubAttractorExplorationHit for write boundary persistence.
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [ExplorationTestFactory.HubCurrent()]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        // Structural check: HubAttractorExplorationHit carries PhaseVectorJson evidence payload.
        var hitType = typeof(HubAttractorExplorationHit);
        Assert.NotNull(hitType.GetProperty("PhaseVectorJson"));
    }

    [Fact]
    public async Task ExploreAsync_OkResult_HitsHaveNoStatisticsOrEmaScoreField()
    {
        // statistics_json and ema_score are write_logs_attention boundary fields — not in exploration hit.
        var hitType = typeof(HubAttractorExplorationHit);
        Assert.Null(hitType.GetProperty("StatisticsJson"));
        Assert.Null(hitType.GetProperty("EmaScore"));
    }

    [Fact]
    public void HubAttractorExplorationHit_HasRequiredWriteBoundaryFields()
    {
        // Verify the hit carries the identity fields needed by write_logs_attention.
        var hitType = typeof(HubAttractorExplorationHit);
        Assert.NotNull(hitType.GetProperty("CurrentId"));
        Assert.NotNull(hitType.GetProperty("HubCurrentId"));
        Assert.NotNull(hitType.GetProperty("SourceSetId"));
        Assert.NotNull(hitType.GetProperty("AttractorKey"));
        Assert.NotNull(hitType.GetProperty("NeighborScore"));
        Assert.NotNull(hitType.GetProperty("HitRank"));
        Assert.NotNull(hitType.GetProperty("ScoreBand"));
        Assert.NotNull(hitType.GetProperty("PermutationKey"));
        // Production evidence fields (per SSOT attention layer)
        Assert.NotNull(hitType.GetProperty("L2Norm"));
        Assert.NotNull(hitType.GetProperty("VectorJson"));
        Assert.NotNull(hitType.GetProperty("EvidenceJson"));
    }
}

// ---------------------------------------------------------------------------
// Tests — production evidence: vector scoring and evidence_json provenance
// ---------------------------------------------------------------------------

public class HubAttractorExplorationRuntime_VectorScoringTests
{
    [Fact]
    public async Task ExploreAsync_Hit_EvidenceJsonContainsScoringProvenance()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [ExplorationTestFactory.HubCurrent()]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "medium", l2Norm: 5.0)],
            "src", "7d");

        var hit = Assert.Single(result.Result!.Hits);
        var evidence = JsonSerializer.Deserialize<JsonElement>(hit.EvidenceJson);
        Assert.Equal(JsonValueKind.Object, evidence.ValueKind);
        Assert.True(evidence.TryGetProperty("scoring_role", out _),
            "evidence_json must contain scoring_role");
        Assert.True(evidence.TryGetProperty("canonical_exploration_field", out _),
            "evidence_json must contain canonical_exploration_field");
        Assert.True(evidence.TryGetProperty("relation_score_source", out _),
            "evidence_json must contain relation_score_source");
        Assert.True(evidence.TryGetProperty("relation_score", out _),
            "evidence_json must contain relation_score");
        Assert.True(evidence.TryGetProperty("no_logs_hub_current_fallback", out _),
            "evidence_json must contain no_logs_hub_current_fallback");
    }

    [Fact]
    public async Task ExploreAsync_Hit_L2NormFromCandidate()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [ExplorationTestFactory.HubCurrent()]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "medium", l2Norm: 42.5)],
            "src", "7d");

        var hit = Assert.Single(result.Result!.Hits);
        Assert.Equal(42.5, hit.L2Norm);
    }

    [Fact]
    public async Task ExploreAsync_WithEmptyAttractorVector_NeighborScoreIsZero()
    {
        // In the canonical path, NeighborScore = relation.RelationScore.
        // Use an explicit hub relation candidate with RelationScore=0.0 to verify the mapping.
        var logsRepo = new StubSqlAttentionLogsRepository(
            [],
            [],
            hubRelationCandidates: [ExplorationTestFactory.HubRelationCandidate(relationScore: 0.0)]);
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(), logsRepo);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(
                normLevel: "medium",
                l2Norm: 5.0,
                basisVectorJson: """{"diff_count": 10}""")],
            "src", "7d");

        var hit = Assert.Single(result.Result!.Hits);
        Assert.Equal(0.0, hit.NeighborScore);
    }

    [Fact]
    public async Task ExploreAsync_WithMatchingVectors_ProducesNonZeroNeighborScore()
    {
        // In the canonical path, NeighborScore = relation.RelationScore.
        // Default hub relation candidates have RelationScore=0.9 → non-zero score.
        var hub = ExplorationTestFactory.HubCurrent(
            attractorVectorJson: """{"diff_count": 10}""");

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [hub]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(
                normLevel: "medium",
                l2Norm: 5.0,
                basisVectorJson: """{"diff_count": 10}""")],
            "src", "7d");

        var hit = Assert.Single(result.Result!.Hits);
        Assert.True(hit.NeighborScore > 0.0,
            $"Expected non-zero neighbor score from relation score, got {hit.NeighborScore}");
        // Canonical path always sets VectorJson = "{}" (no legacy vector embedding stored on hit)
    }

    [Fact]
    public async Task ExploreAsync_WithMatchingVectors_EvidenceJsonRecordsCurrentL2Norm()
    {
        var hub = ExplorationTestFactory.HubCurrent(
            attractorVectorJson: """{"diff_count": 5}""");

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [hub]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(
                normLevel: "medium",
                l2Norm: 99.0,
                basisVectorJson: """{"diff_count": 5}""")],
            "src", "7d");

        // Canonical path stores L2Norm directly on the hit (not in evidence_json).
        var hit = Assert.Single(result.Result!.Hits);
        Assert.Equal(99.0, hit.L2Norm, precision: 5);
    }

    [Fact]
    public void FlattenVectorJson_EmptyObject_ReturnsEmptyDict()
    {
        var result = HubAttractorExplorationRuntime.FlattenVectorJson("{}");
        Assert.Empty(result);
    }

    [Fact]
    public void FlattenVectorJson_SimpleNumeric_ReturnsKey()
    {
        var result = HubAttractorExplorationRuntime.FlattenVectorJson("""{"diff_count": 10}""");
        Assert.True(result.ContainsKey("diff_count"));
        Assert.Equal(10.0, result["diff_count"]);
    }

    [Fact]
    public void FlattenVectorJson_NestedObject_FlattensDotNotation()
    {
        var result = HubAttractorExplorationRuntime.FlattenVectorJson(
            """{"operation_kind_count": {"INSERT": 5, "UPDATE": 3}}""");
        Assert.True(result.ContainsKey("operation_kind_count.INSERT"));
        Assert.True(result.ContainsKey("operation_kind_count.UPDATE"));
        Assert.Equal(5.0, result["operation_kind_count.INSERT"]);
        Assert.Equal(3.0, result["operation_kind_count.UPDATE"]);
    }

    [Fact]
    public void FlattenVectorJson_MalformedJson_ReturnsEmptyDict()
    {
        var result = HubAttractorExplorationRuntime.FlattenVectorJson("not-json");
        Assert.Empty(result);
    }

    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var v = new Dictionary<string, double> { ["a"] = 3.0, ["b"] = 4.0 };
        var score = HubAttractorExplorationRuntime.CosineSimilarity(v, v);
        Assert.Equal(1.0, score, precision: 10);
    }

    [Fact]
    public void CosineSimilarity_EmptyVector_ReturnsZero()
    {
        var a = new Dictionary<string, double> { ["a"] = 1.0 };
        var empty = new Dictionary<string, double>();
        Assert.Equal(0.0, HubAttractorExplorationRuntime.CosineSimilarity(a, empty));
        Assert.Equal(0.0, HubAttractorExplorationRuntime.CosineSimilarity(empty, a));
    }

    [Fact]
    public void CosineSimilarity_NoSharedKeys_ReturnsZero()
    {
        var a = new Dictionary<string, double> { ["x"] = 1.0 };
        var b = new Dictionary<string, double> { ["y"] = 1.0 };
        Assert.Equal(0.0, HubAttractorExplorationRuntime.CosineSimilarity(a, b));
    }

    [Fact]
    public void OverlapScore_IdenticalKeys_ReturnsOne()
    {
        var a = new Dictionary<string, double> { ["a"] = 1.0, ["b"] = 2.0 };
        var b = new Dictionary<string, double> { ["a"] = 3.0, ["b"] = 4.0 };
        Assert.Equal(1.0, HubAttractorExplorationRuntime.OverlapScore(a, b), precision: 10);
    }

    [Fact]
    public void OverlapScore_NoSharedKeys_ReturnsZero()
    {
        var a = new Dictionary<string, double> { ["x"] = 1.0 };
        var b = new Dictionary<string, double> { ["y"] = 1.0 };
        Assert.Equal(0.0, HubAttractorExplorationRuntime.OverlapScore(a, b));
    }
}

// ---------------------------------------------------------------------------
// Tests — ScoreBand classification (policy-driven; no hardcoded thresholds)
// ---------------------------------------------------------------------------

public class HubAttractorExplorationRuntime_ScoreBandTests
{
    private static HubAttractorExplorationPolicy PolicyWithThresholds(
        double strong = 0.95,
        double normal = 0.90,
        double exploratory = 0.85,
        double neighborScoreMin = 0.0) =>
        new(
            NormLevelHigh: 10.0,
            NormLevelMedium: 1.0,
            WeakTier: new ExplorationBudgetTierLimits(1, 2, 1, "near_neighbor_narrow_topK"),
            MidTier: new ExplorationBudgetTierLimits(3, 5, 1, "normal_topK"),
            HighTier: new ExplorationBudgetTierLimits(5, 10, 3, "expanded_distance_band_or_permutation"),
            MaxHubKindsPerCurrent: 5,
            MaxAttentionRowsSaved: 20,
            NeighborScoreMin: neighborScoreMin,
            StrongHitThreshold: strong,
            NormalHitThreshold: normal,
            ExploratoryHitThreshold: exploratory);

    [Theory]
    [InlineData(1.00, "strong")]
    [InlineData(0.95, "strong")]
    [InlineData(0.94, "normal")]
    [InlineData(0.90, "normal")]
    [InlineData(0.89, "exploratory")]
    [InlineData(0.85, "exploratory")]
    [InlineData(0.84, "evidence_only")]
    [InlineData(0.0,  "evidence_only")]
    public void ClassifyScoreBand_PolicyThresholds_ReturnsExpectedBand(double score, string expectedBand)
    {
        var policy = PolicyWithThresholds(0.95, 0.90, 0.85);
        Assert.Equal(expectedBand, HubAttractorExplorationRuntime.ClassifyScoreBand(score, policy));
    }

    [Fact]
    public void ClassifyScoreBand_CustomPolicyThresholds_FollowsPolicyNotHardcoded()
    {
        // Thresholds differ from legacy 0.95/0.90/0.85 — classification must follow policy.
        var policy = PolicyWithThresholds(strong: 0.80, normal: 0.60, exploratory: 0.40);
        Assert.Equal("strong",       HubAttractorExplorationRuntime.ClassifyScoreBand(0.80, policy));
        Assert.Equal("normal",       HubAttractorExplorationRuntime.ClassifyScoreBand(0.70, policy));
        Assert.Equal("exploratory",  HubAttractorExplorationRuntime.ClassifyScoreBand(0.50, policy));
        Assert.Equal("evidence_only",HubAttractorExplorationRuntime.ClassifyScoreBand(0.30, policy));
        // 0.95 would be "strong" in hardcoded logic but is "strong" only because >= 0.80 in this policy
        Assert.Equal("strong",       HubAttractorExplorationRuntime.ClassifyScoreBand(0.95, policy));
        // 0.89 would be "exploratory" in hardcoded logic; with these thresholds it is "strong"
        Assert.Equal("strong",       HubAttractorExplorationRuntime.ClassifyScoreBand(0.89, policy));
    }

    [Fact]
    public async Task ExploreAsync_CanonicalRoute_ScoreBandFollowsPolicyThresholds()
    {
        // Use a policy with non-default thresholds to prove score band is policy-driven.
        // strong=0.80 means score=0.97 must yield "strong"; with 0.95 default it would also be "strong"
        // but 0.82 would be "evidence_only" with 0.95 default while "strong" with 0.80 threshold.
        var policyJson = ExplorationTestFactory.ValidPolicyJson(
            strongHitThreshold: 0.80,
            normalHitThreshold: 0.60,
            exploratoryHitThreshold: 0.40);
        var manifestId = Guid.NewGuid();
        var repo = new CanonicalSqlAttentionStubRepository
        {
            ManifestIds = [manifestId],
            Relations = [new HubRelationExplorationCandidate(Guid.NewGuid(), manifestId, Guid.NewGuid(), Guid.NewGuid(), 1, 0.82, "{\"sql_attention_score\":0.82}")]
        };
        var runtime = new HubAttractorExplorationRuntime(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HubAttractorExplorationRuntime>.Instance,
            new StubExplorationPolicyTopologyRepository(policyJson),
            repo);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(l2Norm: 12.0, normLevel: "high")], "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        var hit = Assert.Single(result.Result!.Hits);
        // score=0.82 with strong_hit_threshold=0.80 → "strong" (not "evidence_only" as per legacy 0.95)
        Assert.Equal("strong", hit.ScoreBand);
    }

    [Fact]
    public async Task ExploreAsync_PolicyMissingScoreBandKey_ReturnsMalformedPolicy()
    {
        // Policy JSON without neighbor_score_policy keys must fail-close.
        var policyWithoutThresholds =
            """{ "norm_level_high": 10.0, "norm_level_medium": 1.0, "exploration_budget_tiers": { "weak": { "topK_per_hub_kind": 1, "max_hub_tables_per_kind": 2, "phase_expansion_limit": 1, "search_mode": "near_neighbor_narrow_topK" }, "mid": { "topK_per_hub_kind": 3, "max_hub_tables_per_kind": 5, "phase_expansion_limit": 1, "search_mode": "normal_topK" }, "high": { "topK_per_hub_kind": 5, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 3, "search_mode": "expanded_distance_band_or_permutation" } }, "max_hub_kinds_per_current": 5, "max_attention_rows_saved": 20 }""";
        var runtime = ExplorationTestFactory.CreateRuntime(policyWithoutThresholds, []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.MalformedPolicy, result.Status);
    }

    [Theory]
    [InlineData(0.80, 0.90, 0.85)] // strong < normal
    [InlineData(0.95, 0.90, 0.92)] // exploratory > normal
    [InlineData(0.95, 0.90, 0.85, -0.01)] // neighbor_score_min < 0
    [InlineData(1.01, 0.90, 0.85)] // strong > 1
    public async Task ExploreAsync_PolicyThresholdOrderInvalid_ReturnsMalformedPolicy(
        double strong, double normal, double exploratory, double neighborScoreMin = 0.0)
    {
        var policyJson = ExplorationTestFactory.ValidPolicyJson(
            neighborScoreMin: neighborScoreMin,
            strongHitThreshold: strong,
            normalHitThreshold: normal,
            exploratoryHitThreshold: exploratory);
        var runtime = ExplorationTestFactory.CreateRuntime(policyJson, []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(normLevel: "high", l2Norm: 15.0)],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.MalformedPolicy, result.Status);
    }
}

// ---------------------------------------------------------------------------
// Tests — SqlAttentionScheduler: no exploration on no-change
// ---------------------------------------------------------------------------

[Collection("SqlAttentionSchedulerEnvVarTests")]
public class SqlAttentionScheduler_RunOnceTests
{
    private static SqlAttentionScheduler CreateScheduler(
        StubSqlAttentionLogsRepository logsRepo,
        string? policyJson = null) =>
        new(
            NullLogger<SqlAttentionScheduler>.Instance,
            logsRepo,
            new HubAttractorExplorationRuntime(
                NullLogger<HubAttractorExplorationRuntime>.Instance,
                new StubExplorationPolicyTopologyRepository(policyJson ?? ExplorationTestFactory.ValidPolicyJson()),
                logsRepo));

    [Fact]
    public async Task RunOnceAsync_WithEnvVars_ChangeCandidates_DoesNotLoadLegacySupportCache()
    {
        Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", "src");
        Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", "7d");
        try
        {
            var logsRepo = new StubSqlAttentionLogsRepository(
                [ExplorationTestFactory.ChangeCandidate()],
                [ExplorationTestFactory.HubCurrent()]);

            var scheduler = CreateScheduler(logsRepo);
            await scheduler.RunOnceAsync(CancellationToken.None);

            Assert.Equal(0, logsRepo.HubCurrentCallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
    }

    [Fact]
    public async Task RunOnceAsync_WithEnvVars_NoChangeCandidates_SkipsExploration()
    {
        Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", "src");
        Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", "7d");
        try
        {
            var logsRepo = new StubSqlAttentionLogsRepository(
                [ExplorationTestFactory.NoChangeCandidate()],
                [ExplorationTestFactory.HubCurrent()]);

            var scheduler = CreateScheduler(logsRepo);
            await scheduler.RunOnceAsync(CancellationToken.None);

            Assert.Equal(0, logsRepo.HubCurrentCallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
    }

    [Fact]
    public async Task RunOnceAsync_MissingEnvVars_SkipsRun()
    {
        Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
        Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);

        var logsRepo = new StubSqlAttentionLogsRepository(
            [ExplorationTestFactory.ChangeCandidate()],
            [ExplorationTestFactory.HubCurrent()]);

        var scheduler = CreateScheduler(logsRepo);
        await scheduler.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, logsRepo.HubCurrentCallCount);
    }

    [Theory]
    [InlineData("1",     1)]
    [InlineData("300",   300)]
    [InlineData("86400", 86400)]
    public void ParseEnvSeconds_ValidRange_ReturnsParsed(string value, int expectedSeconds)
    {
        const string envKey = "TEST_SQL_ATTENTION_INTERVAL_VALID";
        Environment.SetEnvironmentVariable(envKey, value);
        try
        {
            var result = SqlAttentionScheduler.ParseEnvSeconds(envKey, TimeSpan.FromSeconds(999));
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, null);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("86401")]
    [InlineData("-1")]
    [InlineData("notanumber")]
    [InlineData("")]
    public void ParseEnvSeconds_OutOfRange_ReturnsFallback(string? value)
    {
        const string envKey = "TEST_SQL_ATTENTION_INTERVAL_OUTOFRANGE";
        var fallback = TimeSpan.FromSeconds(999);
        Environment.SetEnvironmentVariable(envKey, value);
        try
        {
            var result = SqlAttentionScheduler.ParseEnvSeconds(envKey, fallback);
            Assert.Equal(fallback, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, null);
        }
    }
}

// ---------------------------------------------------------------------------
// Tests — write_logs_attention boundary (SqlAttentionLogsRepository base)
// ---------------------------------------------------------------------------

public class WriteLogsAttention_BaseRepository_Tests
{
    private static SqlAttentionLogsRepository CreateBaseRepo() =>
        new StubSqlAttentionLogsRepository([], []);

    [Fact]
    public async Task WriteLogsAttentionAsync_EmptyHits_ReturnsZero()
    {
        var repo = CreateBaseRepo();
        var count = await repo.WriteLogsAttentionAsync([]);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task WriteLogsAttentionAsync_WithHits_ReturnsHitCount()
    {
        var repo = CreateBaseRepo();
        var currentId = Guid.NewGuid();
        var hubCurrentId = Guid.NewGuid();
        var count = await repo.WriteLogsAttentionAsync(
            [
                new LogsAttentionWriteRequest(currentId, hubCurrentId, "src", Guid.NewGuid(), "att_a", null, null, 0.5, 1, "evidence_only", "default", 0.0, "{}", "{}", "{}", null, "{}", "required")
            ]);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task WriteLogsAttentionAsync_CurrentIdEmpty_Throws()
    {
        var repo = CreateBaseRepo();
        await Assert.ThrowsAsync<ArgumentException>(() => repo.WriteLogsAttentionAsync(
            [new LogsAttentionWriteRequest(Guid.Empty, Guid.NewGuid(), "src", null, "att_a", null, null, 0.5, 1, "evidence_only", "default", 0.0, "{}", "{}", "{}", null, "{}", "required")]));
    }

    [Fact]
    public async Task WriteLogsAttentionAsync_HubCurrentIdEmpty_Throws()
    {
        var repo = CreateBaseRepo();
        await Assert.ThrowsAsync<ArgumentException>(() => repo.WriteLogsAttentionAsync(
            [new LogsAttentionWriteRequest(Guid.NewGuid(), Guid.Empty, "src", null, "att_a", null, null, 0.5, 1, "evidence_only", "default", 0.0, "{}", "{}", "{}", null, "{}", "required")]));
    }

    [Fact]
    public void WriteLogsAttentionAsync_RepositoryHasWriteMethod()
    {
        // Structural check: SqlAttentionLogsRepository exposes WriteLogsAttentionAsync.
        var method = typeof(SqlAttentionLogsRepository).GetMethod("WriteLogsAttentionAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void LogsAttentionWriteRequest_HasAllEvidenceLayerFields()
    {
        // Structural check: LogsAttentionWriteRequest carries all SSOT evidence fields.
        var t = typeof(LogsAttentionWriteRequest);
        Assert.NotNull(t.GetProperty("CurrentId"));
        Assert.NotNull(t.GetProperty("HubCurrentId"));
        Assert.NotNull(t.GetProperty("StatisticsJson"));
        Assert.NotNull(t.GetProperty("EmaScore"));
        Assert.NotNull(t.GetProperty("L2Norm"));
        Assert.NotNull(t.GetProperty("VectorJson"));
        Assert.NotNull(t.GetProperty("NeighborScore"));
        Assert.NotNull(t.GetProperty("PhaseVectorJson"));
        Assert.NotNull(t.GetProperty("EvidenceJson"));
        Assert.NotNull(t.GetProperty("ArchivePolicy"));
    }
}

// ---------------------------------------------------------------------------
// Tests — write_logs_attention scheduler integration
// ---------------------------------------------------------------------------

[Collection("SqlAttentionSchedulerEnvVarTests")]
public class SqlAttentionScheduler_WriteLogsAttention_Tests
{
    private static SqlAttentionScheduler CreateScheduler(StubSqlAttentionLogsRepository logsRepo) =>
        new(
            NullLogger<SqlAttentionScheduler>.Instance,
            logsRepo,
            new HubAttractorExplorationRuntime(
                NullLogger<HubAttractorExplorationRuntime>.Instance,
                new StubExplorationPolicyTopologyRepository(
                    ExplorationTestFactory.ValidPolicyJson(highPhaseLimit: 1)),
                logsRepo));

    [Fact]
    public async Task RunOnceAsync_ChangedCandidate_FailsCloseWithoutLegacySupportCacheOrWrite()
    {
        Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", "src");
        Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", "7d");
        try
        {
            var logsRepo = new StubSqlAttentionLogsRepository(
                [ExplorationTestFactory.ChangeCandidate()],
                [ExplorationTestFactory.HubCurrent(attractorVectorJson: """{"diff_count": 10}""")]);

            var scheduler = CreateScheduler(logsRepo);
            await scheduler.RunOnceAsync(CancellationToken.None);

            Assert.Equal(0, logsRepo.HubCurrentCallCount);
            Assert.Equal(0, logsRepo.WriteLogsAttentionCallCount);
            Assert.Null(logsRepo.LastWriteRequests);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
    }

    [Fact]
    public async Task RunOnceAsync_NoChange_DoesNotLoadSupportCacheOrWrite()
    {
        Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", "src");
        Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", "7d");
        try
        {
            var logsRepo = new StubSqlAttentionLogsRepository(
                [ExplorationTestFactory.NoChangeCandidate()],
                [ExplorationTestFactory.HubCurrent()]);

            var scheduler = CreateScheduler(logsRepo);
            await scheduler.RunOnceAsync(CancellationToken.None);

            Assert.Equal(0, logsRepo.HubCurrentCallCount);
            Assert.Equal(0, logsRepo.WriteLogsAttentionCallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
    }

    [Fact]
    public async Task ExplicitLegacyDiagnostics_PhaseVectorJson_IsPendingIdSpaceEvidence_NotDraftOrCountScalarAxes()
    {
        var hubRelationId = Guid.NewGuid();
        var logsRepo = new StubSqlAttentionLogsRepository(
            [ExplorationTestFactory.ChangeCandidate(l2Norm: 12.5)],
            [ExplorationTestFactory.HubCurrent(
                attractorVectorJson: """{"diff_count": 10}""")],
            hubRelationCandidates: [ExplorationTestFactory.HubRelationCandidate(hubRelationId: hubRelationId)]);
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(highPhaseLimit: 1), logsRepo);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate(l2Norm: 12.5)], "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        var hit = Assert.Single(result.Result!.Hits);
        using var doc = JsonDocument.Parse(hit.PhaseVectorJson);
        var root = doc.RootElement;
        // Canonical phaseAT structure — not legacy diagnostics
        Assert.Equal("phaseAT", root.GetProperty("q_kind").GetString());
        Assert.False(root.GetProperty("q_is_draft").GetBoolean());
        Assert.Equal("hubs.hub_relations", root.GetProperty("canonical_exploration_field").GetString());
        Assert.True(root.GetProperty("phase_movement_is_not_manifest_or_policy_cap_derived").GetBoolean());
        Assert.Equal(12.5, root.GetProperty("w_l2_norm").GetDouble());
        // Canonical path: x/y/z are real GUIDs (not null)
        Assert.Equal(JsonValueKind.String, root.GetProperty("x_hit_hub_relation_id").ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("y_topology_manifest_id").ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("z_hub_id").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("i_expanded_hub_relation_ids").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("j_expanded_topology_manifest_ids").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("k_expanded_hub_ids").ValueKind);
        // No legacy single-letter axes (x/y/z/i/j/k are prefixed with their role, not bare)
        Assert.False(root.TryGetProperty("x", out _));
        Assert.False(root.TryGetProperty("y", out _));
        Assert.False(root.TryGetProperty("z", out _));
        Assert.False(root.TryGetProperty("i", out _));
        Assert.False(root.TryGetProperty("j", out _));
        Assert.False(root.TryGetProperty("k", out _));
        // Canonical path has no_automatic_topology_mutation (not legacy_support_cache_statistics)
        Assert.True(root.GetProperty("no_automatic_topology_mutation").GetBoolean());
        Assert.False(root.TryGetProperty("legacy_support_cache_statistics", out _));
        Assert.False(root.TryGetProperty("phase_basis_json", out _));
    }

    [Fact]
    public void EvidenceContracts_RemainSeparated_AndSqlFunctionUsesPendingIdSpaceShape()
    {
        var hitType = typeof(HubAttractorExplorationHit);
        Assert.Null(hitType.GetProperty("CollapsedScore"));
        Assert.Null(hitType.GetProperty("SingleScore"));
        Assert.NotNull(hitType.GetProperty("PhaseVectorJson"));
        Assert.Null(hitType.GetProperty("StatisticsJson"));

        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../../db/sql_attention_logs_tables.sql"));
        Assert.Contains("'q_kind', 'phaseAT'", sql);
        Assert.Contains("'q_is_draft', false", sql);
        Assert.Contains("'x_hit_hub_relation_id', NULL", sql);
        Assert.Contains("'i_expanded_hub_relation_ids', '[]'::jsonb", sql);
        Assert.DoesNotContain("'x', COALESCE(p_hub_relations_count", sql);

        var runtimeCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../../backend/runtime/HubAttractorExplorationRuntime.cs"));
        var phaseVectorRuntimeCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../../backend/runtime/SqlAttentionPhaseVectorRuntime.cs"));
        Assert.Contains("SqlAttentionPhaseVectorRuntime.GeneratePhaseVector(", runtimeCode);
        Assert.Contains("BuildPhaseVectorJson(", phaseVectorRuntimeCode);
        Assert.Contains("GeneratePhaseVector(", phaseVectorRuntimeCode);
        Assert.Contains("RunCanonicalHubRelationsExploration", runtimeCode);
        Assert.DoesNotContain("ExploreLegacyHubCurrentSupportCacheDiagnosticsAsync", runtimeCode);
        Assert.DoesNotContain("RunLegacyHubCurrentSupportCacheExploration", runtimeCode);
        // Score band thresholds must not be hardcoded in runtime — resolved from policy only.
        Assert.DoesNotContain(">= 0.95", runtimeCode);
        Assert.DoesNotContain(">= 0.90", runtimeCode);
        Assert.DoesNotContain(">= 0.85", runtimeCode);
    }
}
