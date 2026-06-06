using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class RecommendNavigationProjectionSpecTests
{
    [Fact]
    public void FromRecommendation_SeparatesHubLocalLanesAndDoesNotMixSqlAttention()
    {
        var result = new ContextRouteRecommendationResult(
            NextOperations:
            [
                new RecommendationCandidate("Search", 0.9f, 0.8f, ["operation transition"], RecommendationPressureLanes.UiPressure)
            ],
            NextTokens: [],
            NextEnumItems:
            [
                new RecommendationCandidate("Approved", 0.7f, 0.6f, ["enum transition"], RecommendationPressureLanes.StatePressure)
            ],
            NearestPrefixSessionIds: [],
            ContributingTokens: [],
            Status: RecommendationStatus.Ok,
            StatusDetail: "ready");

        var spec = RecommendNavigationProjectionSpec.FromRecommendation(result, "source_set_1");

        Assert.Equal("main_projection_island", spec.MainProjectionIslandId);
        Assert.Equal("recommend_navigation_child_island", spec.ChildIslandId);
        Assert.Contains(spec.HubLocalSections, section => section.Lane == RecommendationPressureLanes.UiPressure && section.Candidates.Single().Value == "Search");
        Assert.Contains(spec.HubLocalSections, section => section.Lane == RecommendationPressureLanes.StatePressure && section.Candidates.Single().Value == "Approved");
        Assert.DoesNotContain(spec.HubLocalSections.SelectMany(section => section.Candidates), candidate => candidate.Lane == RecommendationPressureLanes.SqlAttentionProjection);
        Assert.Equal(RecommendationPressureLanes.SqlAttentionProjection, spec.SqlAttentionProjection.Lane);
        Assert.Equal("next_hub_projection_candidate", spec.SqlAttentionProjection.CandidateKind);
    }

    [Fact]
    public void FromRecommendation_MissingSourceSetIsExplicitUnavailable()
    {
        var spec = RecommendNavigationProjectionSpec.FromRecommendation(null, null);

        Assert.Equal(RecommendProjectionStatus.ExplicitUnavailable, spec.SqlAttentionProjection.Status);
        Assert.Null(spec.SqlAttentionProjection.SourceSetId);
        Assert.Contains("sql_attention_source_set_id", spec.SqlAttentionProjection.StatusDetail);
    }

    [Fact]
    public void ProjectionCandidateWithoutBackendRuntimeDispatchSpecIsNonExecutable()
    {
        var result = new ContextRouteRecommendationResult(
            NextOperations:
            [
                new RecommendationCandidate("Create", 0.5f, null, ["neighbor"], RecommendationPressureLanes.UiPressure)
            ],
            NextTokens: [],
            NextEnumItems: [],
            NearestPrefixSessionIds: [],
            ContributingTokens: [],
            Status: RecommendationStatus.Ok,
            StatusDetail: null);

        var spec = RecommendNavigationProjectionSpec.FromRecommendation(result, "source_set_1");
        var candidate = spec.HubLocalSections.Single(section => section.Lane == RecommendationPressureLanes.UiPressure).Candidates.Single();

        Assert.Null(candidate.RuntimeDispatchSpec);
    }
}
