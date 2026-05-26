using Xunit;

namespace Topolactor.Runtime.Tests;

public class SsotWiringAuditSsotContractReaderTests
{
    [Fact]
    public void Roadmap_SystemCiBundleAndChildLaneKeys_MustExist()
    {
        Assert.True(SsotYamlContractReader.RoadmapEntryExists("system_ci.dotnet_ssot_wiring_audit_tests"));
        Assert.True(SsotYamlContractReader.RoadmapEntryExists("system_ci.topology_registration"));
        Assert.True(SsotYamlContractReader.RoadmapEntryExists("system_ci.hub_registration"));
        Assert.True(SsotYamlContractReader.RoadmapEntryExists("system_ci.scheduler_runtime"));
        Assert.True(SsotYamlContractReader.RoadmapEntryExists("system_ci.component_registration"));
    }

    [Fact]
    public void Roadmap_SystemCiBundle_CompletionConditionKeys_MustExist()
    {
        var keys = SsotYamlContractReader.RoadmapCompletionConditions("system_ci.dotnet_ssot_wiring_audit_tests");

        Assert.Contains("dotnet_ci_audits_application_wiring_against_ssot_contracts", keys);
        Assert.Contains("topology_registration_ci_lane_defined", keys);
        Assert.Contains("hub_registration_ci_lane_defined", keys);
        Assert.Contains("scheduler_runtime_ci_lane_defined", keys);
        Assert.Contains("component_registration_ci_lane_defined", keys);
    }

    [Fact]
    public void CiContractSsot_MustDeclareLoaderAndNoSecondaryExpectedVocabularyPolicy()
    {
        Assert.True(SsotYamlContractReader.ContainsLiteral("docs/design/ci-contract-ssot.yaml", "CI_LANE_CONTRACT_LOADER"));
        Assert.True(SsotYamlContractReader.ContainsLiteral("docs/design/ci-contract-ssot.yaml", "CI_SSOT_VOCABULARY_MEMBERSHIP_ASSERTION"));
        Assert.True(SsotYamlContractReader.ContainsLiteral("docs/design/ci-contract-ssot.yaml", "tests は expected vocabulary を secondary SSOT として再定義してはならない"));
    }

    [Fact]
    public void ComponentCatalogClassification_AllowedValues_AreLoadedFromYaml()
    {
        var family = SsotYamlContractReader.ReadInlineArray("docs/design/component-catalog-classification-ssot.yaml", "component_family");
        var semantic = SsotYamlContractReader.ReadInlineArray("docs/design/component-catalog-classification-ssot.yaml", "semantic_role");
        var visual = SsotYamlContractReader.ReadInlineArray("docs/design/component-catalog-classification-ssot.yaml", "visual_role");
        var lifecycle = SsotYamlContractReader.ReadInlineArray("docs/design/component-catalog-classification-ssot.yaml", "lifecycle_status");
        var tags = SsotYamlContractReader.ReadInlineArray("docs/design/component-catalog-classification-ssot.yaml", "capability_tags");

        Assert.NotEmpty(family);
        Assert.NotEmpty(semantic);
        Assert.NotEmpty(visual);
        Assert.NotEmpty(lifecycle);
        Assert.NotEmpty(tags);
        Assert.Contains("promoted", lifecycle);
        Assert.Contains("requires_event_binding", tags);
    }
}

