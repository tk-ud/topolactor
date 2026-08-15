using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB E2E proof for the credential-management hub relation / navigation / ui_projection
/// bundle, per runtime-orchestration-ssot.yaml dispatcher_contract.
/// ui_projection_render_reachability_contract.test_proof_contract: dispatch through the REAL
/// ManifestDispatcher.DispatchAsync entry point (not a hardcoded layoutId constant fed directly
/// into TopologyRepository.LoadLayoutNodesAsync), using real Npgsql repositories, walking the
/// full relation-vector -> scalar-Emission chain from an explicit
/// relation_uuid / hub_ids[] / package_ids[] test input:
///
///   relation_uuid (hubs.hub_relations.hub_relation_id)
///   -> source topology_manifest (manifest 092, via hub_relations.topology_manifest_id)
///   -> hub_ids[] (hubs.hub_relations.related_hub_id, ordered by sequence_position — the
///      manifest-scoped relation vector / route vector, not a global hub-to-hub graph)
///   -> package_ids[] (manifest.topology[ui_projection].packageIds, read from the manifest
///      topology just loaded from the DB, not a test constant)
///   -> topology.components_package_design / topology.components_layout_design /
///      topology.ui_wiring_registry / topology.ui_topology_tensor
///      (TopologyRepository.LoadLayoutNodesAsync)
///   -> backend dispatch (ManifestDispatcher.DispatchAsync)
///   -> scalar Emission.ManifestId / NavigationSequence / PackageId / LayoutId / LayoutNodes,
///      with NavigationSequence[].RelatedHubId observably corresponding to hub_ids[] and
///      NavigationSequence[].TargetManifestId resolved via the exactly-one-ACTIVE-manifest rule
///
/// The admin_runtime leg uses a REAL AdminRuntime (minimal constructor, same pattern as
/// AdminRuntimeDbProjectionScenarioTests.StartAdminDispatchRouteAsync) so the
/// ADMIN_OPERATION_NOT_FOUND -> structural-render-only-entry fallback in ManifestDispatcher is
/// exercised against AdminRuntime's actual ExecuteDataAsync switch statement, not a stub that
/// merely assumes that behavior.
///
/// The frontend counterpart of this round-trip (NavigationSequence[].TargetManifestId ->
/// resolveHubNavigationLinks -> ?manifest=<uuid> -> parseProjectionEntrySelection ->
/// resolveProjectionEntryAxes -> payload.target_ref) is proven in
/// frontend/tests/projectionEntry.test.ts ("hub navigation round-trip: ...").
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set. Requires
/// db/seed_empty.sql applied to the target database (manifest 092 + its ui_projection rows).
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class CredentialManagementHubRelationUiProjectionLiveDbTests
{
    private static readonly Guid CredentialManagementManifestId =
        new("00000000-0000-0000-0000-000000000092");
    private static readonly Guid CredentialManagementPackageId =
        new("00000000-0000-0000-0000-0000000cd005");

    // Dispatcher construction and the relation-vector -> NavigationSequence resolution
    // assertion are manifest-agnostic and shared via HubRelationUiProjectionResolutionChainProof
    // (runtime-orchestration-ssot.yaml test_proof_contract.test_input_shape) — not duplicated
    // here, so a future target manifest's own live-DB proof (e.g. enum-dictionary /
    // team-dashboard / scheduler-settings, once each has seed content) can reuse the same
    // resolution-chain assertion instead of re-deriving it against a new manifest constant.

    [Fact]
    public async Task DispatchAsync_CredentialManagementManifest_E2E_RelationVectorToScalarEmission()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        // Explicit relation-vector test input, per runtime-orchestration-ssot.yaml
        // dispatcher_contract.ui_projection_render_reachability_contract.test_proof_contract.
        // test_input_shape: relation_uuid / hub_ids[] / package_ids[].
        var relationUuid = Guid.NewGuid();
        var relatedHubId = Guid.NewGuid();
        var relatedManifestId = Guid.NewGuid();
        var packageIds = new[] { CredentialManagementPackageId };
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            // hub_relation(relation_uuid) whose source topology_manifest is manifest 092, whose
            // related hub has exactly one active topology_manifest — the relation vector this
            // proof walks: relation_uuid -> hub_ids[] -> package_ids[] -> scalar Emission.
            await ExecAsync(
                "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)",
                ("id", relatedHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) " +
                "VALUES (@mid, @hid, @key, 'active')",
                ("mid", relatedManifestId), ("hid", relatedHubId), ("key", $"live-db-relation-vector-{suffix}"));
            await ExecAsync(
                "INSERT INTO hubs.hub_relations (hub_relation_id, topology_manifest_id, related_hub_id, sequence_position, status) " +
                "VALUES (@rid, @mid, @hid, 9201, 'active')",
                ("rid", relationUuid), ("mid", CredentialManagementManifestId), ("hid", relatedHubId));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // Same target_ref shape frontend/runtime/projectionEntry.ts resolveProjectionEntryAxes
            // produces for ?manifest=<uuid> selection, and the same screen_list/Search axes the
            // default projection entry dispatches — this is the real production entry shape, not
            // a test-only shortcut.
            var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{CredentialManagementManifestId}:projection_entry",
            });
            var request = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

            var response = await dispatcher.DispatchAsync(request);

            Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.NotNull(response.Emission);
            var emission = response.Emission!;

            // package_ids[] connects through manifest.topology[ui_projection].packageIds to
            // topology.components_package_design (the manifest-facing package authority).
            // Content-specific to credential-management/092 — not part of the generic
            // relation-vector resolution chain asserted below.
            Assert.NotNull(emission.PackageId);
            Assert.Contains(emission.PackageId!.Value, packageIds);

            // ui_projection refs resolved from the manifest topology just loaded from the DB (not
            // a hardcoded layoutId test constant) -> real topology.ui_topology_tensor /
            // topology.ui_wiring_registry rows -> real LayoutNodes.
            Assert.NotNull(emission.LayoutId);
            Assert.NotNull(emission.LayoutNodes);
            Assert.Contains(emission.LayoutNodes!, n => n.NodeId == "instance_settings_import_form");
            Assert.Contains(emission.LayoutNodes!, n => n.WiringKind == "admin_runtime");

            // "current topology phase" identity, and hub_ids[] (manifest-scoped relation vector,
            // hubs.hub_relations.sequence_position) resolving through to Emission.NavigationSequence
            // with the related hub's sole active topology_manifest as TargetManifestId — the same
            // identity the frontend round-trip (?manifest=<TargetManifestId>) would dispatch next.
            // Generic resolution-chain assertion, shared with any other source manifest's own
            // live-DB proof — see HubRelationUiProjectionResolutionChainProof.
            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                emission,
                CredentialManagementManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(relatedHubId, 9201, relatedManifestId)]);

            // No errors — the ADMIN_OPERATION_NOT_FOUND real-AdminRuntime routing gap for this
            // screen-read axes combination was converted to a structural success, exactly the
            // admin_runtime_structural_read_fallback contract in runtime-orchestration-ssot.yaml.
            Assert.Empty(emission.Errors);
        }
        finally
        {
            await ExecAsync("DELETE FROM hubs.hub_relations WHERE hub_relation_id = @rid", ("rid", relationUuid));
            await ExecAsync(
                "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @mid", ("mid", relatedManifestId));
            await ExecAsync("DELETE FROM hubs.hub WHERE hub_id = @hid", ("hid", relatedHubId));
        }
    }

    [Fact]
    public async Task DispatchAsync_HubNavigationCreate_RealAuthoringPath_SourceManifestDispatchReflectsRelationInNavigationSequence_AndFailClosesOnZeroActiveTarget()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        // Proves the REAL admin authoring path end to end — the exact axes
        // frontend/api/adminApi.ts createHubRelation() sends via frontend/islands/
        // HubNavigationAdmin.tsx (role=admin target=admin layer=hub_navigation action=create,
        // already seeded and active in db/seed_empty.sql), through the real ManifestDispatcher ->
        // AdminRuntime.HubNavigationCreateAsync -> NpgsqlContentBundleRepository.
        // CreateHubRelationAsync — never a raw SQL insert standing in for the authoring path
        // itself. The SOURCE manifest here is an ordinary, already-existing topology_manifest —
        // representative of any manifest an admin could select via /admin/manifests
        // (ManifestsAdmin.tsx's manifest list + HubNavigationAdmin.tsx's selector). It has no
        // relation whatsoever to /admin's own landing page — nothing about a "/admin landing
        // manifest" is required anywhere in this path (see CreateHubRelationAsync, which only
        // validates the given topologyManifestId/relatedHubId exist and are not a self-loop).
        var sourceHubId = Guid.NewGuid();
        var sourceManifestId = Guid.NewGuid();
        var targetHubId = Guid.NewGuid();
        var targetManifestId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        Guid? createdHubRelationId = null;
        try
        {
            await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", sourceHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", sourceManifestId), ("hid", sourceHubId), ("key", $"live-db-hub-nav-create-source-{suffix}"));
            // The RUNTIME manifest registry row (`manifest` table) is a separate concept from
            // hubs.topology_manifests (the hub-facing navigation registry) — dispatching the
            // source manifest by target_ref resolves against THIS table
            // (ManifestRepository.LoadByIdAsync), not hubs.topology_manifests. A minimal
            // runtime_mapping entry (no dispatcher_mapping, no ui_projection — this is
            // deliberately NOT topology UI seed content) is all that's required to route the
            // dispatch to admin_runtime.
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY['{\"type\":\"runtime_mapping\",\"runtime_destination\":\"admin_runtime\"}'::jsonb], 'active')",
                ("mid", sourceManifestId));
            await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", targetHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", targetManifestId), ("hid", targetHubId), ("key", $"live-db-hub-nav-create-target-{suffix}"));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // STEP 1: author the relation via the REAL hub_navigation:create dispatch action.
            var createPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = sourceManifestId.ToString(),
                relatedHubId = targetHubId.ToString(),
                sequencePosition = 1,
            });
            var createRequest = new EndpointRequestDto(
                OperationType: "HubNavigationAdminScenario",
                Target: "admin",
                Layer: "hub_navigation",
                Action: "create",
                IdOrHubId: null, Payload: createPayload, Context: null, TriggerKind: "client", Role: "admin");
            var createResponse = await dispatcher.DispatchAsync(createRequest);

            Assert.True(
                createResponse.Success,
                string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            // STEP 2: readback through the repository read path — the created row is really
            // persisted, not merely echoed in the create response.
            var contentBundleRepo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
            var relations = await contentBundleRepo.ListHubRelationsByManifestAsync(sourceManifestId);
            var created = Assert.Single(relations, r => r.RelatedHubId == targetHubId.ToString());
            createdHubRelationId = Guid.Parse(created.HubRelationId);
            Assert.Equal(1, created.SequencePosition);
            Assert.Equal("active", created.Status);

            // STEP 3: dispatch the SOURCE manifest via target_ref (the same manifest:<uuid>:<key>
            // shape frontend/runtime/projectionEntry.ts produces for an explicit ?manifest=
            // selection), routed to a REAL registered admin_runtime action
            // (hub_navigation:get_hub_relations, not the ui_projection structural-render
            // fallback — this source manifest deliberately has no ui_projection, since building
            // one would be topology UI seed content this remediation must not add), and confirm
            // Emission.NavigationSequence reflects the relation just authored through the real
            // create action, with the target resolving via the exactly-one-active-manifest rule.
            // EnrichWithHubNavigationAsync runs for ANY successful admin_runtime response
            // (ManifestDispatcher.cs), not only ui_projection-backed manifests.
            var sourcePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{sourceManifestId}:hub_relations_read",
                topologyManifestId = sourceManifestId.ToString(),
            });
            var sourceRequest = new EndpointRequestDto(
                OperationType: "HubNavigationAdminScenario",
                Target: "admin",
                Layer: "hub_navigation",
                Action: "get_hub_relations",
                IdOrHubId: null, Payload: sourcePayload, Context: null, TriggerKind: "client", Role: "admin");
            var sourceResponse = await dispatcher.DispatchAsync(sourceRequest);

            Assert.True(
                sourceResponse.Success,
                string.Join(";", sourceResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.NotNull(sourceResponse.Emission);
            // Generic resolution-chain assertion — this source manifest is a synthetic,
            // non-credential-management manifest, so this call already proves the shared helper
            // is not accidentally coupled to manifest 092.
            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                sourceResponse.Emission!,
                sourceManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(targetHubId, 1, targetManifestId)]);

            // STEP 4: fail-close — deprecate the sole active target manifest and re-dispatch the
            // same source manifest. Zero active target manifests under the related hub must
            // resolve to null, never a stale/fallback value (mirrors the
            // no_implicit_join_nullable_fallback_or_oldest_manifest_fallback invariant already
            // covered for the repository method directly in
            // LoadHubNavigationSequenceAsync_TargetManifestId_ResolvesOnlyExactlyOneActiveManifestPerHub —
            // this proves the SAME fail-close through the full dispatch path instead).
            await ExecAsync(
                "UPDATE hubs.topology_manifests SET status = 'deprecated' WHERE topology_manifest_id = @mid",
                ("mid", targetManifestId));
            var afterDeprecateResponse = await dispatcher.DispatchAsync(sourceRequest);
            Assert.True(afterDeprecateResponse.Success);
            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                afterDeprecateResponse.Emission!,
                sourceManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(targetHubId, 1, null)]);
        }
        finally
        {
            if (createdHubRelationId is not null)
                await ExecAsync("DELETE FROM hubs.hub_relations WHERE hub_relation_id = @rid", ("rid", createdHubRelationId.Value));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(
                "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id IN (@m1, @m2)",
                ("m1", sourceManifestId), ("m2", targetManifestId));
            await ExecAsync(
                "DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2)", ("h1", sourceHubId), ("h2", targetHubId));
        }
    }

    /// <summary>
    /// Closes admin-normal-surface-projection-seed-ssot.yaml
    /// design_blocking.target_surface_manifest_readiness.navigation_binding_authoring_and_verification
    /// (subbundle_status.credential-management), whose 2026-07-22 gap detail is: the prior proof set
    /// split the hub_navigation:create authoring-dispatch path (proven only against a fully synthetic,
    /// non-092 TARGET manifest above) and the resolution-chain-to-092 assertion (proven only via a
    /// directly-SQL-inserted hub_relations row, see
    /// DispatchAsync_CredentialManagementManifest_E2E_RelationVectorToScalarEmission) across two
    /// separate tests instead of combining them. This single test: (1) authors a hub_relations row via
    /// the REAL hub_navigation:create dispatch action with manifest 092's OWN existing hub
    /// ('...a1', external_port_substrate -- the hub HubRelations_Manifest092_HasCanonicalSequencePosition1Relation_SeedOnly
    /// confirms carries exactly manifest 092 as its sole active topology_manifest) as the
    /// relatedHubId/target, then (2) walks that SAME authored relation's resolved TargetManifestId
    /// onward through manifest 092's real package/layout/wiring/tensor rows to a scalar Emission --
    /// never a synthetic target, never a direct SQL relation insert standing in for authoring.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_HubNavigationCreate_AuthorsRelationTargetingManifest092_ResolutionChainReachesScalarEmission()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var sourceHubId = Guid.NewGuid();
        var sourceManifestId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var credentialManagementHubId = new Guid("00000000-0000-0000-0000-0000000000a1");

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        Guid? createdHubRelationId = null;
        try
        {
            // SOURCE manifest: an ordinary, already-existing topology_manifest an admin could select
            // via /admin/manifests -- same setup pattern as
            // DispatchAsync_HubNavigationCreate_RealAuthoringPath_... above. Manifest 092 itself is
            // the TARGET here, reached through its own existing hub/seed rows -- nothing about 092 is
            // created or modified by this test.
            await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", sourceHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", sourceManifestId), ("hid", sourceHubId), ("key", $"live-db-hub-nav-create-source-to-092-{suffix}"));
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY['{\"type\":\"runtime_mapping\",\"runtime_destination\":\"admin_runtime\"}'::jsonb], 'active')",
                ("mid", sourceManifestId));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // STEP 1: author the relation via the REAL hub_navigation:create dispatch action
            // (frontend/api/adminApi.ts createHubRelation() -> HubNavigationAdmin.tsx -> real
            // ManifestDispatcher -> AdminRuntime.HubNavigationCreateAsync ->
            // NpgsqlContentBundleRepository.CreateHubRelationAsync), targeting manifest 092's OWN
            // existing hub as relatedHubId -- never a raw SQL insert standing in for authoring.
            var createPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = sourceManifestId.ToString(),
                relatedHubId = credentialManagementHubId.ToString(),
                sequencePosition = 1,
            });
            var createRequest = new EndpointRequestDto(
                OperationType: "HubNavigationAdminScenario",
                Target: "admin",
                Layer: "hub_navigation",
                Action: "create",
                IdOrHubId: null, Payload: createPayload, Context: null, TriggerKind: "client", Role: "admin");
            var createResponse = await dispatcher.DispatchAsync(createRequest);

            Assert.True(
                createResponse.Success,
                string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            // STEP 2: readback through the repository read path -- the created row is really
            // persisted, not merely echoed in the create response.
            var contentBundleRepo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
            var relations = await contentBundleRepo.ListHubRelationsByManifestAsync(sourceManifestId);
            var created = Assert.Single(relations, r => r.RelatedHubId == credentialManagementHubId.ToString());
            createdHubRelationId = Guid.Parse(created.HubRelationId);
            Assert.Equal(1, created.SequencePosition);
            Assert.Equal("active", created.Status);

            // STEP 3: dispatch the SOURCE manifest and confirm THIS SAME authored relation resolves
            // Emission.NavigationSequence[].TargetManifestId to manifest 092 itself, via the
            // exactly-one-active-manifest rule against hub '...a1' (never an implicit fallback).
            var sourcePayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{sourceManifestId}:hub_relations_read",
                topologyManifestId = sourceManifestId.ToString(),
            });
            var sourceRequest = new EndpointRequestDto(
                OperationType: "HubNavigationAdminScenario",
                Target: "admin",
                Layer: "hub_navigation",
                Action: "get_hub_relations",
                IdOrHubId: null, Payload: sourcePayload, Context: null, TriggerKind: "client", Role: "admin");
            var sourceResponse = await dispatcher.DispatchAsync(sourceRequest);

            Assert.True(
                sourceResponse.Success,
                string.Join(";", sourceResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.NotNull(sourceResponse.Emission);
            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                sourceResponse.Emission!,
                sourceManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(
                    credentialManagementHubId, 1, CredentialManagementManifestId)]);

            // STEP 4: walk onward from that SAME relation's resolved target -- dispatch manifest 092
            // itself (the exact TargetManifestId STEP 3 just resolved, asserted above) and confirm the
            // full package/layout/wiring/tensor resolution chain reaches a real scalar Emission:
            // LayoutId/PackageId populated, LayoutNodes containing the instance_settings_import_form
            // leaf and the instance_settings_action_bundle wiring, zero unresolved catalog leaves, zero
            // errors. This is the same target_ref shape frontend/runtime/projectionEntry.ts produces
            // once resolveHubNavigationLinks turns this NavigationSequence entry into a
            // ?manifest=<TargetManifestId> selection.
            var targetPayload = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                target_ref = $"manifest:{CredentialManagementManifestId}:projection_entry",
            });
            var targetRequest = new EndpointRequestDto(
                "Search", "default", "screen_list", "Search",
                IdOrHubId: null, Payload: targetPayload, Context: null, TriggerKind: "client", Role: "admin");
            var targetResponse = await dispatcher.DispatchAsync(targetRequest);

            Assert.True(
                targetResponse.Success,
                string.Join(";", targetResponse.Errors.Select(e => e.Code + ":" + e.Message)));
            Assert.NotNull(targetResponse.Emission);
            var targetEmission = targetResponse.Emission!;
            Assert.Equal(CredentialManagementManifestId.ToString(), targetEmission.ManifestId);
            Assert.NotNull(targetEmission.LayoutId);
            Assert.NotNull(targetEmission.PackageId);
            Assert.NotNull(targetEmission.LayoutNodes);
            Assert.Contains(targetEmission.LayoutNodes!, n => n.NodeId == "instance_settings_import_form");
            Assert.Contains(targetEmission.LayoutNodes!, n => n.WiringKind == "admin_runtime");

            var unresolvedLeaves = targetEmission.LayoutNodes!
                .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null)
                .ToList();
            Assert.Empty(unresolvedLeaves);
            Assert.Empty(targetEmission.Errors);
        }
        finally
        {
            if (createdHubRelationId is not null)
                await ExecAsync("DELETE FROM hubs.hub_relations WHERE hub_relation_id = @rid", ("rid", createdHubRelationId.Value));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync("DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync("DELETE FROM hubs.hub WHERE hub_id = @hid", ("hid", sourceHubId));
        }
    }

    [Fact]
    public async Task DispatchAsync_CredentialManagementManifest_LayoutNodes_IncludeStructuralCategorySectionWrappers_NotOnlyFlatFormNodes()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{CredentialManagementManifestId}:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Emission);
        var nodes = response.Emission!.LayoutNodes;
        Assert.NotNull(nodes);

        // Structural authority (components_layout_design.layout_schema_json.records[]) is read —
        // Category/Section wrapper nodes exist, sourced from the schema tree, not only the four
        // flat tensor form nodes. Structural nodes never carry a componentId/componentKind.
        // Round 6: this category's own key/categoryKey was renamed from the legacy "user_auth" to
        // the owning SSOT's canonical category identifier "users" (admin-normal-surface-projection-
        // seed-ssot.yaml surface_axes.admin.surfaces.credentials.categories.users) -- its child
        // section key "user_auth_section" is unchanged (never itself a category identity).
        var usersCategory = Assert.Single(nodes!, n => n.NodeId == "users");
        Assert.Equal("structural_node", usersCategory.NodeKind);
        Assert.Equal("topology_ui_category", usersCategory.RecordType);
        Assert.Null(usersCategory.ComponentId);

        var instanceSettingsSection = Assert.Single(nodes!, n => n.NodeId == "instance_settings_section");
        Assert.Equal("structural_node", instanceSettingsSection.NodeKind);
        Assert.Equal("topology_ui_section", instanceSettingsSection.RecordType);
        Assert.Null(instanceSettingsSection.ComponentId);

        // Field/Action leaves resolve componentId/componentKind from the existing
        // ui_component_registry preset catalog — never left silently unresolved. The seed's
        // "approval_status" field key is reused in two branches (user_auth_section and
        // instance_operation_approval_form) — an authoring collision the composer disambiguates
        // via parent-scoped NodeId rather than colliding or silently dropping one.
        var approvalStatusFields = nodes!.Where(n => n.NodeId.EndsWith("::approval_status")).ToList();
        Assert.Equal(2, approvalStatusFields.Count);
        foreach (var approvalStatusField in approvalStatusFields)
        {
            Assert.Equal("catalog_component", approvalStatusField.NodeKind);
            Assert.NotNull(approvalStatusField.ComponentId);
            Assert.Equal("form_input/select", approvalStatusField.ComponentKind);
        }

        var validateAction = Assert.Single(nodes!, n => n.NodeId == "validate");
        Assert.Equal("catalog_component", validateAction.NodeKind);
        Assert.NotNull(validateAction.ComponentId);
        Assert.Equal("action/button", validateAction.ComponentKind);
        // The tensor's own runtimeInteractions for this leaf survive the composition merge.
        Assert.NotNull(validateAction.RuntimeInteractions);

        // render completion proof: no leaf is left without a componentId (which would otherwise
        // surface as an explicit CATALOG_COMPONENT_KIND_REQUIRED error component on the frontend).
        var unresolvedLeaves = nodes!.Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null).ToList();
        Assert.Empty(unresolvedLeaves);
    }

    /// <summary>
    /// Closes the "backend-callable-only" gap in
    /// external_api_credential.consumer_reference_binding: proves the FULL production path
    /// 092 projection -> user-intent leaf -> dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger
    /// candidate -> real dispatcher -> admin_runtime write, not merely that
    /// credential_management:configure_scheduler_job_credential_or_port_binding is reachable via a
    /// hand-authored EndpointRequestDto. Step 1 dispatches manifest 092 itself and asserts the
    /// resolved LayoutNode for "configure_scheduler_job_credential_or_port_binding_button" carries
    /// EXACTLY the dispatch candidate the seed authored (manifest cd006 target_ref + the
    /// schedulerJobId/credentialRequirementRef/externalPortRef/dryRun payloadFrom map) -- this is
    /// what a real click on this leaf would actually send, not what the seed merely intends. Step 2
    /// takes that SAME resolved target_ref (never a re-typed constant) and dispatches it for real,
    /// proving the candidate the projection exposes is itself live-dispatchable end to end.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_CredentialManagementManifest_SchedulerBindingButton_ResolvedDispatchCandidateIsLiveDispatchable()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            target_ref = $"manifest:{CredentialManagementManifestId}:projection_entry",
        });
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: payload, Context: null, TriggerKind: "client", Role: "admin");

        var response = await dispatcher.DispatchAsync(request);
        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        var nodes = response.Emission!.LayoutNodes;
        Assert.NotNull(nodes);

        // STEP 1: the button leaf's resolved dispatch candidate is exactly what the seed authored --
        // real componentId (catalog resolution succeeded), admin_runtime wiring, and a
        // dispatchTargetRefByTrigger/dispatchPayloadFromByTrigger the frontend's own
        // buildAdminRuntimeTargetRefOverrideByTrigger / buildAdminRuntimePayloadFromByTrigger would
        // consume verbatim (docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
        // lane_storage_boundary.admin_runtime_payload_binding_contract).
        var button = Assert.Single(nodes!, n => n.NodeId == "configure_scheduler_job_credential_or_port_binding_button");
        Assert.Equal("catalog_component", button.NodeKind);
        Assert.NotNull(button.ComponentId);
        Assert.Equal("action/button", button.ComponentKind);
        Assert.Equal("admin_runtime", button.WiringKind);
        Assert.NotNull(button.DispatchTargetRefByTrigger);
        Assert.NotNull(button.DispatchPayloadFromByTrigger);

        var resolvedTargetRef = button.DispatchTargetRefByTrigger!.Value.GetProperty("click").GetString();
        Assert.Equal(
            "manifest:00000000-0000-0000-0000-0000000cd006:credential_management:configure_scheduler_job_credential_or_port_binding",
            resolvedTargetRef);

        var payloadFromClick = button.DispatchPayloadFromByTrigger!.Value.GetProperty("click");
        Assert.Equal("node:scheduler_job_id_input.value", payloadFromClick.GetProperty("schedulerJobId").GetString());
        Assert.Equal(
            "node:scheduler_credential_requirement_ref_input.value",
            payloadFromClick.GetProperty("credentialRequirementRef").GetString());
        Assert.Equal(
            "node:scheduler_external_port_ref_input.value",
            payloadFromClick.GetProperty("externalPortRef").GetString());
        Assert.Equal("literal:true", payloadFromClick.GetProperty("dryRun").GetString());

        // The confirm button (inside the confirm modal) resolves the identical target_ref with
        // confirmed:true instead of dryRun:true -- the preview/confirm pairing the
        // mutation_confirmation_contract requires is structurally present, not just the preview half.
        var confirmButton = Assert.Single(
            nodes!, n => n.NodeId == "configure_scheduler_job_credential_or_port_binding_confirm_button");
        Assert.Equal(
            resolvedTargetRef,
            confirmButton.DispatchTargetRefByTrigger!.Value.GetProperty("click").GetString());
        Assert.Equal(
            "literal:true",
            confirmButton.DispatchPayloadFromByTrigger!.Value.GetProperty("click").GetProperty("confirmed").GetString());

        // STEP 2: the EXACT resolved target_ref from Step 1 (not a re-typed constant) is itself
        // live-dispatchable through the SAME real dispatcher -- the candidate the projection exposes
        // is not a dead reference.
        var schedulerJobId = Guid.NewGuid();
        var dispatchResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "CredentialManagementScenario", "admin", "credential_management",
            "configure_scheduler_job_credential_or_port_binding",
            IdOrHubId: null,
            Payload: System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                schedulerJobId = schedulerJobId.ToString(),
                dryRun = true,
            }),
            Context: null, TriggerKind: "client", Role: "admin"));

        // A non-existent scheduler_job_id fails closed with a real validation error (not
        // MANIFEST_NOT_FOUND/ADMIN_OPERATION_NOT_FOUND) -- proof the target_ref actually reaches
        // AdminRuntime.ExecuteDataAsync's credential_management case, not a resolution dead end.
        Assert.False(dispatchResponse.Success);
        Assert.Contains(dispatchResponse.Errors, e => e.Code == "SCHEDULER_JOB_NOT_FOUND");
    }

    [Fact]
    public async Task DispatchAsync_BareDefaultEntry_NoTargetRef_ResolvesManifest0092ViaCanonicalDefaultEntryRelation()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        // The EXACT axes frontend/runtime/projectionEntry.ts resolveProjectionEntryAxes({})
        // sends for a bare "/" with no ?route=, no ?manifest=, and therefore no pre-injected
        // payload.target_ref. This is not the ?manifest=092 explicit-selection path (that is
        // proven separately above) — this proves the UNSET entry itself reaches manifest 092
        // via the canonical_default_entry hubs.hub_relations row (seed_empty.sql), the means
        // described in ContentBundleRepository.ResolveCanonicalDefaultEntryManifestIdAsync.
        var request = new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null, Payload: null, Context: null, TriggerKind: "client", Role: "admin");

        var response = await dispatcher.DispatchAsync(request);

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.NotNull(response.Emission);
        Assert.Equal(CredentialManagementManifestId.ToString(), response.Emission!.ManifestId);
        Assert.NotNull(response.Emission.LayoutNodes);
        Assert.NotEmpty(response.Emission.LayoutNodes!);

        // render completion, not merely reachability: zero unresolved catalog leaves means the
        // frontend renderEmission() proof (layoutSchemaStructuralRender.test.ts) also holds for
        // exactly this bare-entry-resolved emission shape.
        var unresolvedLeaves = response.Emission.LayoutNodes!
            .Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null)
            .ToList();
        Assert.Empty(unresolvedLeaves);

        // Same representative scenario as the frontend proof: frontend/tests/fixtures/
        // manifest_0092_bare_entry_layout_nodes.json is a checked-in snapshot of THIS EXACT
        // dispatch's Emission.LayoutNodes (camelCase-serialized, the same shape the frontend
        // receives over HTTP — see backend/Program.cs JsonNamingPolicy.CamelCase), consumed by
        // frontend/tests/layoutSchemaStructuralRender.test.ts through the real renderEmission().
        // This assertion is the link: if manifest 092's seed data ever changes, this test fails
        // here rather than the frontend fixture silently going stale against real data.
        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../../frontend/tests/fixtures/manifest_0092_bare_entry_layout_nodes.json"));
        var expectedJson = await File.ReadAllTextAsync(fixturePath);
        // Options must mirror backend/Program.cs's actual wire serialization exactly (including
        // DefaultIgnoreCondition) — the fixture is a snapshot of what the frontend really
        // receives over HTTP, not an arbitrary debug dump. A field-shape drift (e.g. a new
        // LayoutNode field, or a null-handling change) must fail HERE, not silently pass while
        // the checked-in fixture and the real DTO diverge.
        var actualJson = System.Text.Json.JsonSerializer.Serialize(
            response.Emission.LayoutNodes,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
            });
        Assert.Equal(expectedJson.Trim(), actualJson.Trim());
    }

    [Fact]
    public async Task HubRelations_Manifest092_HasCanonicalSequencePosition1Relation_SeedOnly()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT related_hub_id::text, status FROM hubs.hub_relations " +
            "WHERE topology_manifest_id = @id AND sequence_position = 1";
        cmd.Parameters.AddWithValue("id", CredentialManagementManifestId);
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.True(
            await reader.ReadAsync(),
            "manifest 092 must have a canonical (seed_empty.sql) hubs.hub_relations row at sequence_position=1");
        // Self-referencing: related_hub_id is 092's own existing hub (external_port_substrate,
        // '...a1') — no dedicated hub was introduced for this relation.
        Assert.Equal("00000000-0000-0000-0000-0000000000a1", reader.GetString(0));
        Assert.Equal("active", reader.GetString(1));
    }

    [Fact]
    public async Task Manifest092_IsRegisteredInTopologyManifestsAndPhysicalTableBindings()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT hub_id::text, manifest_key, status FROM hubs.topology_manifests " +
                "WHERE topology_manifest_id = @id";
            cmd.Parameters.AddWithValue("id", CredentialManagementManifestId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "manifest 092 must be registered in hubs.topology_manifests");
            Assert.Equal("auth.external.credential_management.projection", reader.GetString(1));
            Assert.Equal("active", reader.GetString(2));
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT COUNT(*)::int FROM topology.physical_table_manifest_bindings " +
                "WHERE topology_manifest_id = @id AND active = true";
            cmd.Parameters.AddWithValue("id", CredentialManagementManifestId);
            var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            Assert.True(count > 0, "manifest 092 must have active physical_table_manifest_bindings rows");
        }
    }

    [Fact]
    public async Task LoadHubNavigationSequenceAsync_TargetManifestId_ResolvesOnlyExactlyOneActiveManifestPerHub()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        // Four related-hub scenarios in one hub_relations sequence, per reviewer requirement:
        //   1. exactly one ACTIVE manifest (+ zero deprecated siblings)              -> resolves
        //   2. one ACTIVE + one DEPRECATED manifest under the same hub               -> resolves (active)
        //   3. only a DEPRECATED manifest, no active one                            -> null
        //   4. two ACTIVE manifests under the same hub (genuinely ambiguous)         -> null
        var singleActiveHubId = Guid.NewGuid();
        var singleActiveManifestId = Guid.NewGuid();

        var activePlusDeprecatedHubId = Guid.NewGuid();
        var activeAmongDeprecatedManifestId = Guid.NewGuid();
        var deprecatedSiblingManifestId = Guid.NewGuid();

        var deprecatedOnlyHubId = Guid.NewGuid();
        var deprecatedOnlyManifestId = Guid.NewGuid();

        var twoActiveHubId = Guid.NewGuid();
        var twoActiveManifestIdA = Guid.NewGuid();
        var twoActiveManifestIdB = Guid.NewGuid();

        var suffix = Guid.NewGuid().ToString("N")[..8];

        // LoadHubNavigationSequenceAsync opens its own connection, so setup/teardown must be
        // committed (not left in an uncommitted transaction) for it to observe the rows.
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        async Task InsertHubAsync(Guid hubId) =>
            await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", hubId));

        async Task InsertManifestAsync(Guid manifestId, Guid hubId, string key, string status) =>
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) " +
                "VALUES (@mid, @hid, @key, @status)",
                ("mid", manifestId), ("hid", hubId), ("key", key), ("status", status));

        async Task InsertHubRelationAsync(Guid relatedHubId, int sequencePosition) =>
            await ExecAsync(
                "INSERT INTO hubs.hub_relations (topology_manifest_id, related_hub_id, sequence_position, status) " +
                "VALUES (@mid, @hid, @seq, 'active')",
                ("mid", CredentialManagementManifestId), ("hid", relatedHubId), ("seq", sequencePosition));

        try
        {
            await InsertHubAsync(singleActiveHubId);
            await InsertManifestAsync(singleActiveManifestId, singleActiveHubId, $"live-db-single-active-{suffix}", "active");

            await InsertHubAsync(activePlusDeprecatedHubId);
            await InsertManifestAsync(activeAmongDeprecatedManifestId, activePlusDeprecatedHubId, $"live-db-active-{suffix}", "active");
            await InsertManifestAsync(deprecatedSiblingManifestId, activePlusDeprecatedHubId, $"live-db-deprecated-sibling-{suffix}", "deprecated");

            await InsertHubAsync(deprecatedOnlyHubId);
            await InsertManifestAsync(deprecatedOnlyManifestId, deprecatedOnlyHubId, $"live-db-deprecated-only-{suffix}", "deprecated");

            await InsertHubAsync(twoActiveHubId);
            await InsertManifestAsync(twoActiveManifestIdA, twoActiveHubId, $"live-db-two-active-a-{suffix}", "active");
            await InsertManifestAsync(twoActiveManifestIdB, twoActiveHubId, $"live-db-two-active-b-{suffix}", "active");

            await InsertHubRelationAsync(singleActiveHubId, 9001);
            await InsertHubRelationAsync(activePlusDeprecatedHubId, 9002);
            await InsertHubRelationAsync(deprecatedOnlyHubId, 9003);
            await InsertHubRelationAsync(twoActiveHubId, 9004);

            var repo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
            var items = await repo.LoadHubNavigationSequenceAsync(CredentialManagementManifestId);

            var singleActiveItem = Assert.Single(items, i => i.RelatedHubId == singleActiveHubId.ToString());
            Assert.Equal(singleActiveManifestId.ToString(), singleActiveItem.TargetManifestId);

            var activePlusDeprecatedItem = Assert.Single(items, i => i.RelatedHubId == activePlusDeprecatedHubId.ToString());
            Assert.Equal(activeAmongDeprecatedManifestId.ToString(), activePlusDeprecatedItem.TargetManifestId);

            var deprecatedOnlyItem = Assert.Single(items, i => i.RelatedHubId == deprecatedOnlyHubId.ToString());
            Assert.Null(deprecatedOnlyItem.TargetManifestId);

            var twoActiveItem = Assert.Single(items, i => i.RelatedHubId == twoActiveHubId.ToString());
            Assert.Null(twoActiveItem.TargetManifestId);
        }
        finally
        {
            await ExecAsync(
                "DELETE FROM hubs.hub_relations WHERE topology_manifest_id = @mid AND related_hub_id IN (@h1, @h2, @h3, @h4)",
                ("mid", CredentialManagementManifestId),
                ("h1", singleActiveHubId), ("h2", activePlusDeprecatedHubId),
                ("h3", deprecatedOnlyHubId), ("h4", twoActiveHubId));
            await ExecAsync(
                "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id IN (@m1, @m2, @m3, @m4, @m5, @m6)",
                ("m1", singleActiveManifestId), ("m2", activeAmongDeprecatedManifestId), ("m3", deprecatedSiblingManifestId),
                ("m4", deprecatedOnlyManifestId), ("m5", twoActiveManifestIdA), ("m6", twoActiveManifestIdB));
            await ExecAsync(
                "DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2, @h3, @h4)",
                ("h1", singleActiveHubId), ("h2", activePlusDeprecatedHubId),
                ("h3", deprecatedOnlyHubId), ("h4", twoActiveHubId));
        }
    }

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();

    // admin-surface-topology-seed-conversion: a prior revision of this file added
    // AdminLandingHub_HubRelation_ResolvesToCredentialManagementManifest092_SeedOnly, which
    // asserted against a fabricated admin-landing manifest/hub/hub_relation
    // (00000000-0000-0000-0000-0000000ad100/ad101/ad102) created solely to give /admin its own
    // outbound hub relation. That construct has been removed from db/seed_empty.sql (owner
    // correction, PR #584 review comments): creating a manifest/hub only to host a hub_relations
    // row is the "empty/fake topology manifest for hub relation connection purposes" pattern now
    // explicitly prohibited. This test is removed with it rather than left asserting against seed
    // rows that no longer exist. It was never structurally necessary in the first place: the real
    // hub relation source is whichever EXISTING topology_manifest an admin selects via
    // /admin/manifests (see DispatchAsync_HubNavigationCreate_RealAuthoringPath_... above and
    // docs/design/admin-console-workflow-ssot.yaml admin_hub_relation_navigation_contract.authoring),
    // never required to be /admin's own landing page.
}
