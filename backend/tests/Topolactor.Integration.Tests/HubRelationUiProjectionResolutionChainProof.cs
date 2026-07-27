using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Reusable live-DB proof helpers for the relation_uuid -> hub_ids[] -> Emission resolution
/// chain declared generically by runtime-orchestration-ssot.yaml
/// ui_projection_render_reachability_contract.test_proof_contract.test_input_shape
/// (relation_uuid / hub_ids[] / package_ids[]). Extracted so any source topology_manifest —
/// not only credential-management/manifest 092 — can reuse the same real-dispatcher
/// construction and NavigationSequence resolution assertions once it has its own live seed
/// data. Neither method assumes manifest 092 or any other specific manifest; callers supply
/// the source/related manifest identities explicitly.
/// </summary>
internal static class HubRelationUiProjectionResolutionChainProof
{
    /// <summary>
    /// Builds a real ManifestDispatcher wired to real Npgsql repositories and a real
    /// AdminRuntime (same pattern as AdminRuntimeDbProjectionScenarioTests.StartAdminDispatchRouteAsync),
    /// so the ADMIN_OPERATION_NOT_FOUND -> structural-render-only-entry fallback in
    /// ManifestDispatcher is exercised against AdminRuntime's actual ExecuteDataAsync switch
    /// statement, not a stub. Manifest-agnostic — reusable for any source manifest under proof.
    /// </summary>
    public static async Task<ManifestDispatcher> BuildRealDispatcherAsync(string connectionString)
    {
        var uiRepo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, connectionString);
        var topoRepo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, connectionString);
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, connectionString);
        var topoVector = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, topoVector);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);
        var manifestRepo = new NpgsqlManifestRepository(NullLogger<NpgsqlManifestRepository>.Instance, connectionString);
        var contentBundleRepo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, connectionString);
        var hubNavResolver = new HubNavigationResolver(contentBundleRepo, manifestRepo);
        var adminRuntime = new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo,
            registrar,
            pkg,
            uiRepo,
            topologyRepository: topoRepo,
            // Real hub_navigation:* authoring path (frontend/islands/HubNavigationAdmin.tsx ->
            // hub_navigation:create/update/deprecate/reorder) needs this repository wired, not
            // just the read-only HubNavigationResolver above.
            contentBundleRepository: contentBundleRepo,
            // Real enum_dictionary:* admin_runtime actions (admin-runtime-operation-dispatch-lane-
            // determination read-circuit proof) need this repository wired, or every
            // enum_dictionary:* case in AdminRuntime.ExecuteDataAsync fails closed with
            // ENUM_DICTIONARY_NOT_AVAILABLE regardless of manifest/wiring resolution.
            enumDictionaryRepository: new NpgsqlEnumDictionaryRepository(connectionString),
            // AdminMasterRosterAudit.AppendAsync (diff_log stage of mutation_confirmation_contract)
            // silently no-ops when this is null (backend/runtime/AdminMasterRosterAudit.cs) --
            // without it, no consumer of this manifest-agnostic helper (admin-enum, admin-dashboard,
            // credential-management) can prove diff_log persistence, only that the write itself
            // succeeded. Wiring the existing, unmodified NpgsqlSqlAttentionLogsRepository here does
            // not change behavior for the other two consumers (grep-confirmed neither references
            // sqlAttentionLogsRepository or a *_NOT_AVAILABLE code gated by its absence).
            sqlAttentionLogsRepository: new NpgsqlSqlAttentionLogsRepository(
                NullLogger<NpgsqlSqlAttentionLogsRepository>.Instance, connectionString));
        var adminAdapter = new AdminRuntimeDispatchAdapter(adminRuntime, new OperationVectorResolver());

        var targetOverride = new TargetDispatchOverride(NullLogger<TargetDispatchOverride>.Instance, adminRuntime);

        return await Task.FromResult(new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            new Dictionary<string, IDispatchableRuntime>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin_runtime"] = adminAdapter,
            },
            new OperationVectorResolver(),
            targetOverride,
            manifestRepo,
            errorAppender: null,
            topologyRepository: topoRepo,
            hubNavigationResolver: hubNavResolver));
    }

    /// <summary>
    /// One relation-vector entry's expectation: a related hub, its sequence_position, and the
    /// TargetManifestId it must resolve to (null when the exactly-one-active-manifest rule is
    /// not met for that hub — see db-schema.yaml no_implicit_join_nullable_fallback_or_oldest_manifest_fallback).
    /// </summary>
    public sealed record ExpectedHubVectorEntry(Guid RelatedHubId, int SequencePosition, Guid? ExpectedTargetManifestId);

    /// <summary>
    /// Asserts Emission.ManifestId / Emission.NavigationSequence resolve the given source
    /// manifest's relation vector exactly, per
    /// ui_projection_render_reachability_contract.hub_navigation_resolution and
    /// .test_proof_contract.resolution_chain. Manifest-identity-agnostic: callers supply the
    /// expected source manifest id and hub vector explicitly rather than this helper assuming
    /// credential-management/092 or any other specific manifest.
    /// </summary>
    public static void AssertNavigationSequenceResolvesHubVector(
        Emission emission,
        Guid expectedSourceManifestId,
        IReadOnlyList<ExpectedHubVectorEntry> expectedHubVector)
    {
        Assert.Equal(expectedSourceManifestId.ToString(), emission.ManifestId);
        Assert.NotNull(emission.NavigationSequence);

        foreach (var expected in expectedHubVector)
        {
            var navItem = Assert.Single(
                emission.NavigationSequence!, i => i.RelatedHubId == expected.RelatedHubId.ToString());
            Assert.Equal(expected.SequencePosition, navItem.SequencePosition);
            Assert.Equal(expected.ExpectedTargetManifestId?.ToString(), navItem.TargetManifestId);
        }
    }
}
