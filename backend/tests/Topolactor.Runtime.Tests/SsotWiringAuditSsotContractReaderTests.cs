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

        Assert.Contains("sh_checks_are_kept_as_ai_agent_operational_guard", keys);
        Assert.Contains("dotnet_ci_audits_application_wiring_against_ssot_contracts", keys);
        Assert.Contains("topology_registration_ci_lane_defined", keys);
        Assert.Contains("hub_registration_ci_lane_defined", keys);
        Assert.Contains("scheduler_runtime_ci_lane_defined", keys);
        Assert.Contains("component_registration_ci_lane_defined", keys);
        Assert.Contains("initial_phase_returns_diagnostics_evidence_eligibility_only", keys);
    }

    [Fact]
    public void Roadmap_SystemCiChildLanes_CompletionConditionKeys_MustExist()
    {
        var topology = SsotYamlContractReader.RoadmapCompletionConditions("system_ci.topology_registration");
        Assert.NotEmpty(topology);
        Assert.Contains("broken_package_reference_returns_package_resolve_failed_not_silence", topology);
        Assert.Contains("successful_pipeline_preserves_all_four_topology_identity_fields", topology);

        var hub = SsotYamlContractReader.RoadmapCompletionConditions("system_ci.hub_registration");
        Assert.NotEmpty(hub);
        Assert.Contains("inspect_async_routes_to_correct_target_by_name", hub);
        Assert.Contains("unknown_target_throws_not_silent_fallback", hub);

        var scheduler = SsotYamlContractReader.RoadmapCompletionConditions("system_ci.scheduler_runtime");
        Assert.NotEmpty(scheduler);
        Assert.Contains("canonical_route_scheduler_dispatcher_executor_preserves_topology_ids", scheduler);
        Assert.Contains("queue_overflow_returns_explicit_scheduler_queue_full_not_silent_drop", scheduler);

        var component = SsotYamlContractReader.RoadmapCompletionConditions("system_ci.component_registration");
        Assert.NotEmpty(component);
        Assert.Contains("generate_transitions_to_packaging_without_issuing_ids", component);
        Assert.Contains("promote_issues_all_five_ids_non_empty", component);
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
