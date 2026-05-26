using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

// CI lane: component vocabulary → catalog implementation → seed/bootstrap row → runtime adapter/renderer
// identity continuity audit.
//
// SSOT contract (docs/design/component-catalog-classification-ssot.yaml, system-roadmap.yaml):
//   - generate (bucketed → packaging) transitions lifecycle status WITHOUT issuing IDs.
//   - promote (packaging → promoted) issues all five IDs: tensorId/componentId/packageId/layoutId/wiringId.
//   - All five IDs are non-empty (non-null Guid) after a successful promote.
//   - A broken or incomplete promote result returns an explicit error code, not partial IDs.
//   - componentId, packageId, layoutId, wiringId are all present in the promote result payload.
//
// The generate → promote pipeline is the registration continuity chain:
//   ui_component_bucket → package_generator (generate/promote)
//   → componentId / packageId / layoutId / wiringId → ui_topology_tensor
//
// Test doubles isolate the UiTopologyRepository boundary. No DB access.
public class SsotWiringAuditComponentRegistrationTests
{
    [Fact]
    public void ComponentRegistrationLane_CompletionConditionKeys_MustExistInRoadmapSsot()
    {
        var keys = SsotYamlContractReader.RoadmapCompletionConditions("system_ci.component_registration");

        Assert.NotEmpty(keys);
        Assert.Contains("generate_transitions_to_packaging_without_issuing_ids", keys);
        Assert.Contains("promote_issues_all_five_ids_non_empty", keys);
        Assert.Contains("broken_promote_returns_explicit_error_not_partial_ids", keys);
    }

    [Fact]
    public void ComponentRegistrationLane_ComponentClassificationVocabulary_MustBeLoadedFromSsot()
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
    }

    [Fact]
    public void ComponentRegistrationLane_ComponentCatalogValues_MustBeSubsetOfSsotVocabulary()
    {
        var catalog = SsotYamlContractReader.ReadDoc("frontend/components/catalog.ts");
        var familyAllowed = SsotYamlContractReader.ReadInlineArray("docs/design/component-catalog-classification-ssot.yaml", "component_family").ToHashSet();
        var semanticAllowed = SsotYamlContractReader.ReadInlineArray("docs/design/component-catalog-classification-ssot.yaml", "semantic_role").ToHashSet();
        var visualAllowed = SsotYamlContractReader.ReadInlineArray("docs/design/component-catalog-classification-ssot.yaml", "visual_role").ToHashSet();
        var lifecycleAllowed = SsotYamlContractReader.ReadInlineArray("docs/design/component-catalog-classification-ssot.yaml", "lifecycle_status").ToHashSet();
        var tagAllowed = SsotYamlContractReader.ReadInlineArray("docs/design/component-catalog-classification-ssot.yaml", "capability_tags").ToHashSet();

        foreach (Match m in Regex.Matches(catalog, @"componentFamily:\s*""([^""]+)"""))
            Assert.Contains(m.Groups[1].Value, familyAllowed);
        foreach (Match m in Regex.Matches(catalog, @"semanticRole:\s*""([^""]+)"""))
            Assert.Contains(m.Groups[1].Value, semanticAllowed);
        foreach (Match m in Regex.Matches(catalog, @"visualRole:\s*""([^""]+)"""))
            Assert.Contains(m.Groups[1].Value, visualAllowed);
        foreach (Match m in Regex.Matches(catalog, @"lifecycleStatus:\s*""([^""]+)"""))
            Assert.Contains(m.Groups[1].Value, lifecycleAllowed);
        foreach (Match m in Regex.Matches(catalog, @"capabilityTags:\s*\[([^\]]*)\]"))
        {
            foreach (Match tag in Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\""))
                Assert.Contains(tag.Groups[1].Value, tagAllowed);
        }
    }

    [Fact]
    public void ComponentRegistrationLane_RuntimeConnectedKinds_MustBeSupportedByFactoryRegistryBoundary()
    {
        var catalog = SsotYamlContractReader.ReadDoc("frontend/components/catalog.ts");
        var adapter = SsotYamlContractReader.ReadDoc("frontend/runtime/runtimeComponentAdapter.ts");
        var registry = SsotYamlContractReader.ReadDoc("frontend/runtime/runtimeComponentRegistry.ts");
        var factory = SsotYamlContractReader.ReadDoc("frontend/runtime/runtimeComponentFactory.ts");
        var renderer = SsotYamlContractReader.ReadDoc("frontend/runtime/runtimePrimitiveRenderer.ts");

        // Parse entry-by-entry so runtimeConnected:true in one entry does not
        // bleed into an adjacent entry that has runtimeConnected:false.
        var connectedKinds = ExtractCatalogEntries(catalog)
            .Where(entry => Regex.IsMatch(entry, @"runtimeConnected:\s*true"))
            .Select(entry => Regex.Match(entry, @"componentKind:\s*""([^""]+)"""))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(connectedKinds);
        Assert.Contains("hasRuntimeComponentFactory", adapter);
        Assert.Contains("resolveRuntimeComponentFactory", registry);
        Assert.Contains("RUNTIME_COMPONENT_FACTORIES", factory);
        Assert.DoesNotContain("switch (spec.componentType)", renderer);
        foreach (var kind in connectedKinds)
        {
            Assert.True(
                factory.Contains($"\"{kind}\"", StringComparison.Ordinal),
                $"runtimeConnected componentKind must be declared in runtime component factory componentKinds: {kind}");
        }
    }

    [Fact]
    public void ComponentRegistrationLane_EventBindingRequiredComponents_MustDeclareFactoryBindingSurface()
    {
        var catalog = SsotYamlContractReader.ReadDoc("frontend/components/catalog.ts");
        var factory = SsotYamlContractReader.ReadDoc("frontend/runtime/runtimeComponentFactory.ts");
        var renderer = SsotYamlContractReader.ReadDoc("frontend/runtime/runtimePrimitiveRenderer.ts");

        // Parse entry-by-entry: only collect kinds where BOTH requires_event_binding
        // AND runtimeConnected:true appear within the same catalog entry object.
        var requiringBindingKinds = ExtractCatalogEntries(catalog)
            .Where(entry =>
                Regex.IsMatch(entry, @"runtimeConnected:\s*true") &&
                Regex.IsMatch(entry, @"capabilityTags:\s*\[[^\]]*""requires_event_binding"""))
            .Select(entry => Regex.Match(entry, @"componentKind:\s*""([^""]+)"""))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(requiringBindingKinds);
        Assert.Contains("emitBoundEvent", factory);
        Assert.Contains("requireBinding", factory);
        Assert.DoesNotContain("emitBoundEvent", renderer);
        foreach (var kind in requiringBindingKinds)
        {
            Assert.True(
                factory.Contains($"\"{kind}\"", StringComparison.Ordinal),
                $"event-binding-required runtimeConnected kind must be factory componentKinds reachable: {kind}");
        }
    }

    // Extracts each top-level { ... } entry from COMPONENT_CATALOG_ENTRIES by tracking
    // balanced braces. This ensures per-entry field reads do not cross object boundaries.
    private static IEnumerable<string> ExtractCatalogEntries(string catalog)
    {
        var idx = catalog.IndexOf("COMPONENT_CATALOG_ENTRIES", StringComparison.Ordinal);
        if (idx < 0) return Enumerable.Empty<string>();

        // Use '= [' to locate the array initializer, skipping the type annotation '[]'.
        var eqBracket = catalog.IndexOf("= [", idx, StringComparison.Ordinal);
        if (eqBracket < 0) return Enumerable.Empty<string>();

        var arrayStart = eqBracket + 2; // position of '['
        var entries = new List<string>();
        var depth = 0;
        var entryStart = -1;

        for (var i = arrayStart; i < catalog.Length; i++)
        {
            var ch = catalog[i];
            if (ch == '{')
            {
                if (depth == 0) entryStart = i;
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
                if (depth == 0 && entryStart >= 0)
                {
                    entries.Add(catalog.Substring(entryStart, i - entryStart + 1));
                    entryStart = -1;
                }
            }
            else if (ch == ']' && depth == 0)
            {
                break; // reached end of COMPONENT_CATALOG_ENTRIES array
            }
        }

        return entries;
    }

    // ─── Generate: lifecycle transitions to packaging without issuing IDs ─────

    [Fact]
    public async Task ComponentRegistration_Generate_TransitionsToPackagingStatus()
    {
        // generate must move the bucket item to "packaging" lifecycle status.
        // No topology IDs are issued at this stage.
        var runtime = CreateRuntime(new ComponentStubUiRepository(
            generateResult: new PackageGenerateResult(
                PackageGenerateCode.Success,
                null, null, null, null, null)));
        var vector = new OperationVector(
            "admin", "package_generator", "generate", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                bucketItemId = Guid.NewGuid().ToString(),
                routeKey = "admin:ui-builder"
            }), null);

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("packaging", data!.Value.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ComponentRegistration_Generate_DoesNotIssueComponentId()
    {
        // componentId is not issued during generate (only during promote).
        var runtime = CreateRuntime(new ComponentStubUiRepository(
            generateResult: new PackageGenerateResult(
                PackageGenerateCode.Success,
                null, null, null, null, null)));
        var vector = new OperationVector(
            "admin", "package_generator", "generate", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                bucketItemId = Guid.NewGuid().ToString(),
                routeKey = "admin:ui-builder"
            }), null);

        var (data, _) = await runtime.ExecuteDataAsync(vector);

        Assert.False(data!.Value.TryGetProperty("componentId", out _),
            "componentId must not appear in generate result — it is only issued on promote.");
    }

    [Fact]
    public async Task ComponentRegistration_Generate_DoesNotIssueLayoutIdOrWiringId()
    {
        var runtime = CreateRuntime(new ComponentStubUiRepository(
            generateResult: new PackageGenerateResult(
                PackageGenerateCode.Success,
                null, null, null, null, null)));
        var vector = new OperationVector(
            "admin", "package_generator", "generate", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                bucketItemId = Guid.NewGuid().ToString(),
                routeKey = "admin:ui-builder"
            }), null);

        var (data, _) = await runtime.ExecuteDataAsync(vector);

        Assert.False(data!.Value.TryGetProperty("layoutId", out _),
            "layoutId must not appear in generate result.");
        Assert.False(data!.Value.TryGetProperty("wiringId", out _),
            "wiringId must not appear in generate result.");
    }

    // ─── Promote: all five IDs issued and non-empty ───────────────────────────

    [Fact]
    public async Task ComponentRegistration_Promote_ComponentIdIsNonEmpty()
    {
        // componentId must be non-empty after a successful promote.
        // An empty or null componentId means the registration chain is broken.
        var componentId = Guid.NewGuid();
        var runtime = CreateRuntime(new ComponentStubUiRepository(
            promoteResult: new PackageGenerateResult(
                PackageGenerateCode.Success,
                Guid.NewGuid(), componentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())));
        var vector = MakePromoteVector();

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.Equal(componentId.ToString(), data!.Value.GetProperty("componentId").GetString());
    }

    [Fact]
    public async Task ComponentRegistration_Promote_PackageIdIsNonEmpty()
    {
        var packageId = Guid.NewGuid();
        var runtime = CreateRuntime(new ComponentStubUiRepository(
            promoteResult: new PackageGenerateResult(
                PackageGenerateCode.Success,
                Guid.NewGuid(), Guid.NewGuid(), packageId, Guid.NewGuid(), Guid.NewGuid())));
        var vector = MakePromoteVector();

        var (data, _) = await runtime.ExecuteDataAsync(vector);

        Assert.Equal(packageId.ToString(), data!.Value.GetProperty("packageId").GetString());
    }

    [Fact]
    public async Task ComponentRegistration_Promote_LayoutIdIsNonEmpty()
    {
        // layoutId must be issued during promote and must be non-empty.
        // An empty layoutId means ui_layout_registry did not receive a row.
        var layoutId = Guid.NewGuid();
        var runtime = CreateRuntime(new ComponentStubUiRepository(
            promoteResult: new PackageGenerateResult(
                PackageGenerateCode.Success,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), layoutId, Guid.NewGuid())));
        var vector = MakePromoteVector();

        var (data, _) = await runtime.ExecuteDataAsync(vector);

        Assert.Equal(layoutId.ToString(), data!.Value.GetProperty("layoutId").GetString());
    }

    [Fact]
    public async Task ComponentRegistration_Promote_WiringIdIsNonEmpty()
    {
        // wiringId must be issued during promote and must be non-empty.
        // An empty wiringId means ui_wiring_registry did not receive a row.
        var wiringId = Guid.NewGuid();
        var runtime = CreateRuntime(new ComponentStubUiRepository(
            promoteResult: new PackageGenerateResult(
                PackageGenerateCode.Success,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), wiringId)));
        var vector = MakePromoteVector();

        var (data, _) = await runtime.ExecuteDataAsync(vector);

        Assert.Equal(wiringId.ToString(), data!.Value.GetProperty("wiringId").GetString());
    }

    [Fact]
    public async Task ComponentRegistration_Promote_AllFiveIdsArePresentInResult()
    {
        // All five IDs (tensorId/componentId/packageId/layoutId/wiringId) must be present
        // in a successful promote result. Missing any ID means the registration chain is broken.
        var runtime = CreateRuntime(new ComponentStubUiRepository(
            promoteResult: new PackageGenerateResult(
                PackageGenerateCode.Success,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())));
        var vector = MakePromoteVector();

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(error);
        Assert.True(data!.Value.TryGetProperty("tensorId", out _));
        Assert.True(data!.Value.TryGetProperty("componentId", out _));
        Assert.True(data!.Value.TryGetProperty("packageId", out _));
        Assert.True(data!.Value.TryGetProperty("layoutId", out _));
        Assert.True(data!.Value.TryGetProperty("wiringId", out _));
    }

    // ─── Promote failure: explicit error (not partial IDs) ───────────────────

    [Fact]
    public async Task ComponentRegistration_Promote_NotBucketed_ReturnsExplicitError()
    {
        // If the bucket item is not in packaging status, promote must return an explicit error.
        // Partial ID issuance (some IDs set, others null) is not permitted.
        var runtime = CreateRuntime(new ComponentStubUiRepository(
            promoteResult: new PackageGenerateResult(
                PackageGenerateCode.NotBucketed,
                null, null, null, null, null,
                "NOT_PACKAGING",
                "bucket item is not in packaging status")));
        var vector = MakePromoteVector();

        var (data, error) = await runtime.ExecuteDataAsync(vector);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal("PACKAGE_NOT_BUCKETED", error!.Code);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AdminRuntime CreateRuntime(UiTopologyRepository uiRepo)
    {
        var ctxRepo = new ContextRouteRepository(
            NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(
            NullLogger<TopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(
            NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(
            NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var pkg = new PackageGeneratorRuntime(
            NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance, ctxRepo, registrar, pkg, uiRepo, null);
    }

    private static OperationVector MakePromoteVector() =>
        new OperationVector(
            "admin", "package_generator", "promote", null, "admin",
            JsonSerializer.SerializeToElement(new
            {
                bucketItemId = Guid.NewGuid().ToString(),
                routeKey = "admin:ui-builder"
            }), null);
}

// Stub UiTopologyRepository for component registration CI lane tests.
// Allows separate generate and promote results to test each stage independently.
internal sealed class ComponentStubUiRepository : UiTopologyRepository
{
    private readonly PackageGenerateResult? _generateResult;
    private readonly PackageGenerateResult? _promoteResult;

    public ComponentStubUiRepository(
        PackageGenerateResult? generateResult = null,
        PackageGenerateResult? promoteResult = null)
        : base(NullLogger<UiTopologyRepository>.Instance, "test-double")
    {
        _generateResult = generateResult;
        _promoteResult = promoteResult;
    }

    public override Task<PackageGenerateResult> GenerateFromBucketAsync(
        Guid bucketItemId, string routeKey, CancellationToken ct = default)
        => Task.FromResult(_generateResult
            ?? new PackageGenerateResult(PackageGenerateCode.Success, null, null, null, null, null));

    public override Task<PackageGenerateResult> PromoteBucketItemAsync(
        Guid bucketItemId, string routeKey, CancellationToken ct = default)
        => Task.FromResult(_promoteResult
            ?? new PackageGenerateResult(PackageGenerateCode.Success, null, null, null, null, null));
}
