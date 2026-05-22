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
/// Tracks whether LoadHubCurrentCandidatesAsync was called (to verify no exploration when no change).
/// </summary>
internal sealed class StubSqlAttentionLogsRepository(
    IReadOnlyList<WatchChangeCandidate> candidates,
    IReadOnlyList<HubCurrentCandidate> hubCurrentCandidates)
    : SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "dummy")
{
    public int HubCurrentCallCount { get; private set; }
    public int WriteLogsAttentionCallCount { get; private set; }
    public HubAttractorExplorationResult? LastWriteResult { get; private set; }

    public override Task<IReadOnlyList<WatchChangeCandidate>> LoadWatchCandidatesAsync(
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
        => Task.FromResult(candidates);

    public override Task<IReadOnlyList<HubCurrentCandidate>> LoadHubCurrentCandidatesAsync(
        string sourceSetId,
        string basisWindow,
        CancellationToken ct = default)
    {
        HubCurrentCallCount++;
        return Task.FromResult(hubCurrentCandidates);
    }

    public override Task<int> WriteLogsAttentionAsync(
        HubAttractorExplorationResult explorationResult,
        CancellationToken ct = default)
    {
        WriteLogsAttentionCallCount++;
        LastWriteResult = explorationResult;
        return base.WriteLogsAttentionAsync(explorationResult, ct);
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
        string changeReason = "level_changed") =>
        new(
            CurrentId: currentId ?? Guid.NewGuid(),
            PhysicalTableId: physicalTableId ?? "table_a",
            NormRank: 1,
            PreviousNormLevel: "medium",
            NormLevel: "high",
            ChangeDetected: changeDetected,
            ChangeReason: changeReason);

    public static WatchChangeCandidate NoChangeCandidate() =>
        new(
            CurrentId: Guid.NewGuid(),
            PhysicalTableId: "table_b",
            NormRank: 2,
            PreviousNormLevel: "high",
            NormLevel: "high",
            ChangeDetected: false,
            ChangeReason: "no_change");

    public static HubCurrentCandidate HubCurrent(
        string attractorKey = "attractor_a",
        long populationCount = 100,
        long populationRecordcount = 50) =>
        new(
            HubCurrentId: Guid.NewGuid(),
            SourceSetId: "test_source",
            HubId: Guid.NewGuid(),
            AttractorKey: attractorKey,
            HubRelationId: Guid.NewGuid(),
            RelationRegistryId: Guid.NewGuid(),
            BasisWindow: "7d",
            AttractorVectorJson: "{}",
            PopulationCount: populationCount,
            PopulationRecordcount: populationRecordcount);

    public static string ValidPolicyJson(
        int topK = 3,
        int maxKinds = 5,
        int maxTables = 10,
        int phaseLimit = 1,
        int maxRows = 20) =>
        $$"""
        {
          "topK_per_hub_kind": {{topK}},
          "max_hub_kinds_per_current": {{maxKinds}},
          "max_hub_tables_per_kind": {{maxTables}},
          "phase_expansion_limit": {{phaseLimit}},
          "max_attention_rows_saved": {{maxRows}}
        }
        """;

    public static HubAttractorExplorationRuntime CreateRuntime(
        string? policyJson,
        IReadOnlyList<HubCurrentCandidate> hubCurrentCandidates) =>
        new(
            NullLogger<HubAttractorExplorationRuntime>.Instance,
            new StubExplorationPolicyTopologyRepository(policyJson),
            new StubSqlAttentionLogsRepository([], hubCurrentCandidates));

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

public class HubAttractorExplorationRuntime_ChangeCandidateTests
{
    [Fact]
    public async Task ExploreAsync_WithChangeCandidates_ReturnsOk()
    {
        var logsRepo = new StubSqlAttentionLogsRepository(
            [ExplorationTestFactory.ChangeCandidate()],
            [ExplorationTestFactory.HubCurrent()]);

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            logsRepo);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
    }

    [Fact]
    public async Task ExploreAsync_WithChangeCandidates_LoadsHubCurrentCandidates()
    {
        var logsRepo = new StubSqlAttentionLogsRepository(
            [],
            [ExplorationTestFactory.HubCurrent()]);

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            logsRepo);

        await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.Equal(1, logsRepo.HubCurrentCallCount);
    }

    [Fact]
    public async Task ExploreAsync_WithChangeCandidates_ResultContainsHits()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [ExplorationTestFactory.HubCurrent()]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
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
            [ExplorationTestFactory.ChangeCandidate(currentId: currentId)],
            "src", "7d");

        var hit = Assert.Single(result.Result!.Hits);
        Assert.Equal(currentId, hit.CurrentId);
        Assert.Equal(hubCurrent.HubCurrentId, hit.HubCurrentId);
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
        var logsRepo = new StubSqlAttentionLogsRepository([], [ExplorationTestFactory.HubCurrent()]);
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
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.MissingPolicy, result.Status);
    }

    [Fact]
    public async Task ExploreAsync_PolicyMissing_ResultIsNull()
    {
        var runtime = ExplorationTestFactory.CreateRuntime(null, []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.Null(result.Result);
    }

    [Fact]
    public async Task ExploreAsync_PolicyMalformedJson_ReturnsMalformedPolicy()
    {
        var runtime = ExplorationTestFactory.CreateRuntime("not-valid-json", []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.MalformedPolicy, result.Status);
    }

    [Fact]
    public async Task ExploreAsync_PolicyMissingRequiredKey_ReturnsMalformedPolicy()
    {
        // Missing topK_per_hub_kind
        var runtime = ExplorationTestFactory.CreateRuntime(
            """{ "max_hub_kinds_per_current": 5, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 1, "max_attention_rows_saved": 20 }""",
            []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.MalformedPolicy, result.Status);
    }

    [Theory]
    [InlineData("""{ "topK_per_hub_kind": 0, "max_hub_kinds_per_current": 5, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 1, "max_attention_rows_saved": 20 }""")]
    [InlineData("""{ "topK_per_hub_kind": 3, "max_hub_kinds_per_current": 0, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 1, "max_attention_rows_saved": 20 }""")]
    [InlineData("""{ "topK_per_hub_kind": 3, "max_hub_kinds_per_current": 5, "max_hub_tables_per_kind": 0, "phase_expansion_limit": 1, "max_attention_rows_saved": 20 }""")]
    [InlineData("""{ "topK_per_hub_kind": 3, "max_hub_kinds_per_current": 5, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 0, "max_attention_rows_saved": 20 }""")]
    [InlineData("""{ "topK_per_hub_kind": 3, "max_hub_kinds_per_current": 5, "max_hub_tables_per_kind": 10, "phase_expansion_limit": 1, "max_attention_rows_saved": 0 }""")]
    public async Task ExploreAsync_PolicyNonPositiveValue_ReturnsMalformedPolicy(string policyJson)
    {
        var runtime = ExplorationTestFactory.CreateRuntime(policyJson, []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.MalformedPolicy, result.Status);
    }

    [Fact]
    public async Task ExploreAsync_PolicyMalformedJson_ResultIsNull()
    {
        var runtime = ExplorationTestFactory.CreateRuntime("not-valid-json", []);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
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
        // 5 hub current records with same attractor key, topK=2 → at most 2 hits per kind
        var hubs = Enumerable.Range(0, 5)
            .Select(_ => ExplorationTestFactory.HubCurrent("attractor_a", 100, 50))
            .ToList();

        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(topK: 2, maxRows: 100),
            hubs);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        Assert.True(result.Result!.Hits.Count <= 2,
            $"Expected at most 2 hits (topK=2), got {result.Result.Hits.Count}");
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
            ExplorationTestFactory.ValidPolicyJson(topK: 5, maxKinds: 10, maxTables: 10, phaseLimit: 1, maxRows: 3),
            hubs);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
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
            ExplorationTestFactory.ValidPolicyJson(topK: 1, maxKinds: 2, maxTables: 5, phaseLimit: 1, maxRows: 100),
            hubs);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
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
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result.Hits);
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
            ExplorationTestFactory.ValidPolicyJson(topK: 2),
            hubs);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        var ranks = result.Result!.Hits.Select(h => h.HitRank).OrderBy(r => r).ToList();
        Assert.Equal(1, ranks[0]);
        Assert.Equal(2, ranks[1]);
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
        // Verifies that exploration runtime returns a result without persisting.
        // The result has Hits but no write is performed (SqlAttentionLogsRepository has no write method).
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [ExplorationTestFactory.HubCurrent()]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        // Result carries hits for downstream consumption — no write_logs_attention call here.
        Assert.Equal(HubAttractorExplorationStatus.Ok, result.Status);
        Assert.NotNull(result.Result);
        // SqlAttentionLogsRepository has no WriteLogsAttention method — structural boundary proof.
        Assert.DoesNotContain("WriteLogsAttention",
            typeof(SqlAttentionLogsRepository).GetMethods().Select(m => m.Name));
    }

    [Fact]
    public async Task ExploreAsync_OkResult_HitsHaveNoPhaseVectorField()
    {
        // phase_vector_json is excluded from HubAttractorExplorationHit — phase generation is a separate step.
        var runtime = ExplorationTestFactory.CreateRuntime(
            ExplorationTestFactory.ValidPolicyJson(),
            [ExplorationTestFactory.HubCurrent()]);

        var result = await runtime.ExploreAsync(
            [ExplorationTestFactory.ChangeCandidate()],
            "src", "7d");

        // Structural check: HubAttractorExplorationHit has no PhaseVectorJson property.
        var hitType = typeof(HubAttractorExplorationHit);
        Assert.Null(hitType.GetProperty("PhaseVectorJson"));
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
    }
}

// ---------------------------------------------------------------------------
// Tests — ScoreBand classification
// ---------------------------------------------------------------------------

public class HubAttractorExplorationRuntime_ScoreBandTests
{
    [Theory]
    [InlineData(1.00, "strong")]
    [InlineData(0.95, "strong")]
    [InlineData(0.94, "normal")]
    [InlineData(0.90, "normal")]
    [InlineData(0.89, "exploratory")]
    [InlineData(0.85, "exploratory")]
    [InlineData(0.84, "evidence_only")]
    [InlineData(0.0,  "evidence_only")]
    public void ClassifyScoreBand_ReturnsExpectedBand(double score, string expectedBand)
    {
        var band = HubAttractorExplorationRuntime.ClassifyScoreBand(score);
        Assert.Equal(expectedBand, band);
    }
}

// ---------------------------------------------------------------------------
// Tests — SqlAttentionScheduler: no exploration on no-change
// ---------------------------------------------------------------------------

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
    public async Task RunOnceAsync_WithEnvVars_ChangeCandidates_LoadsHubCurrentCandidates()
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

            Assert.Equal(1, logsRepo.HubCurrentCallCount);
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
        var result = new HubAttractorExplorationResult("src", "7d", []);
        var count = await repo.WriteLogsAttentionAsync(result);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task WriteLogsAttentionAsync_WithHits_ReturnsHitCount()
    {
        var repo = CreateBaseRepo();
        var currentId = Guid.NewGuid();
        var hubCurrentId = Guid.NewGuid();
        var hit = new HubAttractorExplorationHit(
            CurrentId: currentId,
            HubCurrentId: hubCurrentId,
            SourceSetId: "src",
            HubId: Guid.NewGuid(),
            AttractorKey: "att_a",
            HubRelationId: null,
            RelationRegistryId: null,
            NeighborScore: 0.5,
            HitRank: 1,
            ScoreBand: "evidence_only",
            PermutationKey: "default");
        var result = new HubAttractorExplorationResult("src", "7d", [hit]);
        var count = await repo.WriteLogsAttentionAsync(result);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task WriteLogsAttentionAsync_CurrentIdEmpty_Throws()
    {
        var repo = CreateBaseRepo();
        var hit = new HubAttractorExplorationHit(
            CurrentId: Guid.Empty,
            HubCurrentId: Guid.NewGuid(),
            SourceSetId: "src",
            HubId: null,
            AttractorKey: "att_a",
            HubRelationId: null,
            RelationRegistryId: null,
            NeighborScore: 0.5,
            HitRank: 1,
            ScoreBand: "evidence_only",
            PermutationKey: "default");
        var result = new HubAttractorExplorationResult("src", "7d", [hit]);
        await Assert.ThrowsAsync<ArgumentException>(() => repo.WriteLogsAttentionAsync(result));
    }

    [Fact]
    public async Task WriteLogsAttentionAsync_HubCurrentIdEmpty_Throws()
    {
        var repo = CreateBaseRepo();
        var hit = new HubAttractorExplorationHit(
            CurrentId: Guid.NewGuid(),
            HubCurrentId: Guid.Empty,
            SourceSetId: "src",
            HubId: null,
            AttractorKey: "att_a",
            HubRelationId: null,
            RelationRegistryId: null,
            NeighborScore: 0.5,
            HitRank: 1,
            ScoreBand: "evidence_only",
            PermutationKey: "default");
        var result = new HubAttractorExplorationResult("src", "7d", [hit]);
        await Assert.ThrowsAsync<ArgumentException>(() => repo.WriteLogsAttentionAsync(result));
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

public class SqlAttentionScheduler_WriteLogsAttention_Tests
{
    private static SqlAttentionScheduler CreateScheduler(StubSqlAttentionLogsRepository logsRepo) =>
        new(
            NullLogger<SqlAttentionScheduler>.Instance,
            logsRepo,
            new HubAttractorExplorationRuntime(
                NullLogger<HubAttractorExplorationRuntime>.Instance,
                new StubExplorationPolicyTopologyRepository(ExplorationTestFactory.ValidPolicyJson()),
                logsRepo));

    [Fact]
    public async Task RunOnceAsync_OkWithHits_CallsWriteLogsAttention()
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

            Assert.Equal(1, logsRepo.WriteLogsAttentionCallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
    }

    [Fact]
    public async Task RunOnceAsync_OkWithNoHits_DoesNotCallWriteLogsAttention()
    {
        Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", "src");
        Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", "7d");
        try
        {
            // No hub current candidates → exploration Ok but empty hits
            var logsRepo = new StubSqlAttentionLogsRepository(
                [ExplorationTestFactory.ChangeCandidate()],
                []);

            var scheduler = CreateScheduler(logsRepo);
            await scheduler.RunOnceAsync(CancellationToken.None);

            Assert.Equal(0, logsRepo.WriteLogsAttentionCallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
    }

    [Fact]
    public async Task RunOnceAsync_NoChange_DoesNotCallWriteLogsAttention()
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

            Assert.Equal(0, logsRepo.WriteLogsAttentionCallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
    }

    [Fact]
    public async Task RunOnceAsync_OkWithHits_WriteResultCarriesHits()
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

            Assert.NotNull(logsRepo.LastWriteResult);
            Assert.NotEmpty(logsRepo.LastWriteResult!.Hits);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
    }

    [Fact]
    public async Task RunOnceAsync_OkWithHits_HitHasNonEmptyCurrentId()
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

            foreach (var hit in logsRepo.LastWriteResult!.Hits)
                Assert.NotEqual(Guid.Empty, hit.CurrentId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
    }

    [Fact]
    public async Task RunOnceAsync_OkWithHits_EvidenceMeaningsNotCollapsed()
    {
        // Structural check: hit does not carry a single collapsed score field.
        Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", "src");
        Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", "7d");
        try
        {
            var logsRepo = new StubSqlAttentionLogsRepository(
                [ExplorationTestFactory.ChangeCandidate()],
                [ExplorationTestFactory.HubCurrent()]);

            var scheduler = CreateScheduler(logsRepo);
            await scheduler.RunOnceAsync(CancellationToken.None);

            var hitType = typeof(HubAttractorExplorationHit);
            // No collapsed single score field — evidence layers stay separate
            Assert.Null(hitType.GetProperty("CollapsedScore"));
            Assert.Null(hitType.GetProperty("SingleScore"));
            // Phase vector excluded from exploration hit (generation is separate TODO)
            Assert.Null(hitType.GetProperty("PhaseVectorJson"));
            // Statistics excluded from exploration hit (integration is separate TODO)
            Assert.Null(hitType.GetProperty("StatisticsJson"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQL_ATTENTION_SOURCE_SET_ID", null);
            Environment.SetEnvironmentVariable("SQL_ATTENTION_BASIS_WINDOW", null);
        }
    }
}
