using System.Text.Json;
using Npgsql;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof for production_projection_connectivity_invariant (docs/design/db-schema.yaml
/// manifest_hub_chain, docs/design/runtime-orchestration-ssot.yaml
/// production_projection_connectivity_invariant): every hubs.topology_manifests row must retain
/// at least one active hub_relations row. The canonical fail-close boundary for this invariant is
/// hub_navigation:deprecate (NpgsqlContentBundleRepository.DeprecateHubRelationAsync) — the only
/// existing mutation that can reduce a topology_manifest's active hub_relations count
/// (hub_navigation:update replaces relatedHubId in place; hub_navigation:reorder never changes
/// status). This file proves the guard through the REAL hub_navigation:deprecate dispatch path
/// against a real database, and that the resolution chain (Emission.NavigationSequence) reflects
/// the DB state correctly in both the blocked and successful cases — not merely that the dispatch
/// call returns the expected error code in isolation.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set.
/// </summary>
public class HubRelationOrphanInvariantLiveDbTests
{
    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();

    [Fact]
    public async Task DispatchAsync_HubNavigationDeprecate_LastActiveRelationForManifest_FailsClosed_RelationRemainsActiveInResolutionChain()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var sourceHubId = Guid.NewGuid();
        var sourceManifestId = Guid.NewGuid();
        var targetHubId = Guid.NewGuid();
        var targetManifestId = Guid.NewGuid();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", sourceHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", sourceManifestId), ("hid", sourceHubId), ("key", $"live-db-orphan-guard-source-{suffix}"));
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY['{\"type\":\"runtime_mapping\",\"runtime_destination\":\"admin_runtime\"}'::jsonb], 'active')",
                ("mid", sourceManifestId));
            await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", targetHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", targetManifestId), ("hid", targetHubId), ("key", $"live-db-orphan-guard-target-{suffix}"));

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            // Author the manifest's sole active hub_relations row via the real hub_navigation:create
            // dispatch (never a raw SQL insert standing in for authoring).
            var createPayload = JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = sourceManifestId.ToString(),
                relatedHubId = targetHubId.ToString(),
                sequencePosition = 1,
            });
            var createResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                OperationType: "HubNavigationOrphanGuardScenario", Target: "admin", Layer: "hub_navigation",
                Action: "create", IdOrHubId: null, Payload: createPayload, Context: null,
                TriggerKind: "client", Role: "admin"));
            Assert.True(createResponse.Success, string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            var createdData = createResponse.Emission?.Data;
            Assert.NotNull(createdData);
            var createdHubRelationId = Guid.Parse(createdData!.Value.GetProperty("hubRelationId").GetString()!);

            // STEP 1: real hub_navigation:deprecate dispatch on the manifest's ONLY active relation
            // must fail closed — never silently orphan the manifest.
            var deprecatePayload = JsonSerializer.SerializeToElement(new { hubRelationId = createdHubRelationId.ToString() });
            var deprecateResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                OperationType: "HubNavigationOrphanGuardScenario", Target: "admin", Layer: "hub_navigation",
                Action: "deprecate", IdOrHubId: null, Payload: deprecatePayload, Context: null,
                TriggerKind: "client", Role: "admin"));
            Assert.True(deprecateResponse.Success, "the dispatch itself must succeed even though the guard rejects the mutation");
            var deprecateData = deprecateResponse.Emission?.Data;
            Assert.NotNull(deprecateData);
            Assert.False(deprecateData!.Value.GetProperty("ok").GetBoolean());
            Assert.Equal("HUB_RELATION_LAST_ACTIVE_FOR_MANIFEST", deprecateData.Value.GetProperty("errorCode").GetString());

            // STEP 2: real DB round trip -- the relation must STILL be active, and the resolution
            // chain must still resolve it, proving the guard actually prevented the write rather
            // than only returning an error while the row silently changed underneath it.
            var rereadRequest = new EndpointRequestDto(
                OperationType: "HubNavigationOrphanGuardScenario", Target: "admin", Layer: "hub_navigation",
                Action: "get_hub_relations",
                IdOrHubId: null,
                Payload: JsonSerializer.SerializeToElement(new
                {
                    target_ref = $"manifest:{sourceManifestId}:hub_relations_read",
                    topologyManifestId = sourceManifestId.ToString(),
                }),
                Context: null, TriggerKind: "client", Role: "admin");
            var rereadResponse = await dispatcher.DispatchAsync(rereadRequest);
            Assert.True(rereadResponse.Success);
            Assert.NotNull(rereadResponse.Emission);
            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                rereadResponse.Emission!,
                sourceManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(targetHubId, 1, targetManifestId)]);
        }
        finally
        {
            await ExecAsync("DELETE FROM hubs.hub_relations WHERE topology_manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(
                "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id IN (@m1, @m2)",
                ("m1", sourceManifestId), ("m2", targetManifestId));
            await ExecAsync("DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2)", ("h1", sourceHubId), ("h2", targetHubId));
        }
    }

    [Fact]
    public async Task DispatchAsync_HubNavigationDeprecate_OneOfTwoActiveRelations_Succeeds_ResolutionChainReflectsOnlyTheRemainingOne()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var sourceHubId = Guid.NewGuid();
        var sourceManifestId = Guid.NewGuid();
        var targetHubId1 = Guid.NewGuid();
        var targetManifestId1 = Guid.NewGuid();
        var targetHubId2 = Guid.NewGuid();
        var targetManifestId2 = Guid.NewGuid();

        async Task ExecAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", sourceHubId));
            await ExecAsync(
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", sourceManifestId), ("hid", sourceHubId), ("key", $"live-db-orphan-guard-2rel-source-{suffix}"));
            await ExecAsync(
                "INSERT INTO manifest (manifest_id, relation_registry_id, topology, status) VALUES " +
                "(@mid, NULL, ARRAY['{\"type\":\"runtime_mapping\",\"runtime_destination\":\"admin_runtime\"}'::jsonb], 'active')",
                ("mid", sourceManifestId));
            foreach (var (hubId, manifestId, label) in new[] { (targetHubId1, targetManifestId1, "a"), (targetHubId2, targetManifestId2, "b") })
            {
                await ExecAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", hubId));
                await ExecAsync(
                    "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                    ("mid", manifestId), ("hid", hubId), ("key", $"live-db-orphan-guard-2rel-target-{label}-{suffix}"));
            }

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

            async Task<Guid> CreateRelationAsync(Guid relatedHubId, int seq)
            {
                var payload = JsonSerializer.SerializeToElement(new
                {
                    topologyManifestId = sourceManifestId.ToString(),
                    relatedHubId = relatedHubId.ToString(),
                    sequencePosition = seq,
                });
                var response = await dispatcher.DispatchAsync(new EndpointRequestDto(
                    OperationType: "HubNavigationOrphanGuardScenario", Target: "admin", Layer: "hub_navigation",
                    Action: "create", IdOrHubId: null, Payload: payload, Context: null,
                    TriggerKind: "client", Role: "admin"));
                Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
                return Guid.Parse(response.Emission!.Data!.Value.GetProperty("hubRelationId").GetString()!);
            }

            var relationId1 = await CreateRelationAsync(targetHubId1, 1);
            _ = await CreateRelationAsync(targetHubId2, 2);

            // Deprecating ONE of two active relations must succeed -- the manifest still has an
            // active relation remaining.
            var deprecateResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                OperationType: "HubNavigationOrphanGuardScenario", Target: "admin", Layer: "hub_navigation",
                Action: "deprecate", IdOrHubId: null,
                Payload: JsonSerializer.SerializeToElement(new { hubRelationId = relationId1.ToString() }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(deprecateResponse.Success);
            var deprecateData = deprecateResponse.Emission?.Data;
            Assert.NotNull(deprecateData);
            Assert.True(deprecateData!.Value.GetProperty("ok").GetBoolean());

            var rereadResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                OperationType: "HubNavigationOrphanGuardScenario", Target: "admin", Layer: "hub_navigation",
                Action: "get_hub_relations",
                IdOrHubId: null,
                Payload: JsonSerializer.SerializeToElement(new
                {
                    target_ref = $"manifest:{sourceManifestId}:hub_relations_read",
                    topologyManifestId = sourceManifestId.ToString(),
                }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(rereadResponse.Success);
            Assert.NotNull(rereadResponse.Emission);
            // Only the surviving relation (targetHubId2) is in the resolution chain now -- the
            // deprecated one (targetHubId1) must not appear.
            Assert.DoesNotContain(
                rereadResponse.Emission!.NavigationSequence!, i => i.RelatedHubId == targetHubId1.ToString());
            HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
                rereadResponse.Emission!,
                sourceManifestId,
                [new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(targetHubId2, 2, targetManifestId2)]);
        }
        finally
        {
            await ExecAsync("DELETE FROM hubs.hub_relations WHERE topology_manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync("DELETE FROM manifest WHERE manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(
                "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id IN (@m1, @m2, @m3)",
                ("m1", sourceManifestId), ("m2", targetManifestId1), ("m3", targetManifestId2));
            await ExecAsync(
                "DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2, @h3)",
                ("h1", sourceHubId), ("h2", targetHubId1), ("h3", targetHubId2));
        }
    }
}
