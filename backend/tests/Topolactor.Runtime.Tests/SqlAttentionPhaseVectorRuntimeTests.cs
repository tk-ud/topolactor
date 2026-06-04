using System.Text.Json;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class SqlAttentionPhaseVectorRuntimeTests
{
    [Fact]
    public void GeneratePhaseVector_UsesHubsExplorationIds_NotManifestOrPolicyCapScalars()
    {
        var hitRelationId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var expandedRelationId = Guid.NewGuid();
        var expandedManifestId = Guid.NewGuid();
        var expandedHubId = Guid.NewGuid();

        var json = SqlAttentionPhaseVectorRuntime.GeneratePhaseVector(new PhaseVectorGenerationRequest(
            WL2Norm: 4.2,
            HitHubRelationId: hitRelationId,
            TopologyManifestId: manifestId,
            HubId: hubId,
            SourceTopologyManifestIds: [manifestId],
            ExpandedHubRelationIds: [expandedRelationId],
            ExpandedTopologyManifestIds: [expandedManifestId],
            ExpandedHubIds: [expandedHubId],
            ExplorationBudgetTierLabel: "mid",
            ExplorationSearchMode: "normal_topK"));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("phaseAT", root.GetProperty("q_kind").GetString());
        Assert.False(root.GetProperty("q_is_draft").GetBoolean());
        Assert.True(root.GetProperty("phase_movement_is_not_manifest_or_policy_cap_derived").GetBoolean());
        Assert.Equal(hitRelationId.ToString(), root.GetProperty("x_hit_hub_relation_id").GetString());
        Assert.Equal(manifestId.ToString(), root.GetProperty("y_topology_manifest_id").GetString());
        Assert.Equal(hubId.ToString(), root.GetProperty("z_hub_id").GetString());
        Assert.Contains(expandedRelationId.ToString(), root.GetProperty("i_expanded_hub_relation_ids").EnumerateArray().Select(e => e.GetString()));
        Assert.False(root.TryGetProperty("legacy_support_cache_statistics", out _));
        Assert.False(root.TryGetProperty("topology_manifests_count", out _));
    }

    [Fact]
    public void GeneratePhaseVector_RequiresHitHubRelationId()
    {
        Assert.Throws<ArgumentException>(() =>
            SqlAttentionPhaseVectorRuntime.GeneratePhaseVector(new PhaseVectorGenerationRequest(
                WL2Norm: 1.0,
                HitHubRelationId: Guid.Empty,
                TopologyManifestId: Guid.NewGuid(),
                HubId: Guid.NewGuid(),
                SourceTopologyManifestIds: [],
                ExpandedHubRelationIds: [],
                ExpandedTopologyManifestIds: [],
                ExpandedHubIds: [],
                ExplorationBudgetTierLabel: "weak",
                ExplorationSearchMode: "near_narrow_topK")));
    }

    [Fact]
    public void BuildPhaseVectorJson_RemainsDeprecatedSupportCacheDiagnosticsShape()
    {
        var candidate = new WatchChangeCandidate(
            CurrentId: Guid.NewGuid(),
            PhysicalTableId: "table_a",
            NormRank: 1,
            PreviousNormLevel: "medium",
            NormLevel: "high",
            ChangeDetected: true,
            ChangeReason: "level_changed",
            L2Norm: 9.5,
            BasisVectorJson: "{}");
        var hub = new HubCurrentCandidate(
            HubCurrentId: Guid.NewGuid(),
            SourceSetId: "test_source",
            HubId: Guid.NewGuid(),
            AttractorKey: "hub",
            HubRelationId: Guid.NewGuid(),
            RelationRegistryId: Guid.NewGuid(),
            BasisWindow: "7d",
            AttractorVectorJson: "{}",
            PopulationCount: 100,
            PopulationRecordcount: 50,
            AxisPopulationJson: """{"hub_relations_count":3,"hub_count":2,"topology_manifests_count":1}""",
            AxisZScoreJson: """{"i":0.1,"j":0.2,"k":0.3}""",
            PhaseBasisJson: """{"basis":"hub_current"}""");
        var tierLimits = new ExplorationBudgetTierLimits(5, 2, 3, "normal_topK");

        var json = SqlAttentionPhaseVectorRuntime.BuildPhaseVectorJson(
            candidate,
            hub,
            """{"diff_count": 10}""",
            ExplorationBudgetTier.Mid,
            tierLimits);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("deprecated_support_cache_diagnostics_only", root.GetProperty("generation_status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("x_hit_hub_relation_id").ValueKind);
        Assert.True(root.TryGetProperty("legacy_support_cache_statistics", out _));
    }
}
