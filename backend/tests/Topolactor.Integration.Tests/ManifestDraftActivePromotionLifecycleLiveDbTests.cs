using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proof that public.manifest and hubs.topology_manifests never diverge in lifecycle
/// phase for the same Manifest identity, and that draft -> active promotion
/// (NpgsqlManifestRepository.PromoteAsync) is the canonical completion boundary for
/// production_projection_connectivity_invariant (docs/design/db-schema.yaml
/// hub_relations.minimum_cardinality_completion_invariant) -- fail-closing on zero,
/// deprecated-only, or unresolved-only-target hub_relations, while a draft manifest legitimately
/// has zero hub_relations during authoring (that is not itself a violation; only promoting with
/// zero resolvable ones is). Round 11's separate hub_navigation:deprecate orphan guard (an
/// already-active manifest may never lose its last active relation) is unmodified and already
/// proven by HubRelationOrphanInvariantLiveDbTests.cs -- both of its tests still pass unchanged
/// after this round's promotion realignment (proof item 8), so it is not re-proven here.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set.
/// </summary>
public class ManifestDraftActivePromotionLifecycleLiveDbTests
{
    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();

    private static async Task ExecAsync(string cs, string sql, params (string Name, object Value)[] parms)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarStringAsync(string cs, string sql, params (string Name, object Value)[] parms)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    private static (NpgsqlContentBundleRepository ContentBundleRepo, NpgsqlManifestRepository ManifestRepo) BuildRepos(string cs)
    {
        var contentBundleRepo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
        var manifestRepo = new NpgsqlManifestRepository(NullLogger<NpgsqlManifestRepository>.Instance, cs, contentBundleRepo);
        return (contentBundleRepo, manifestRepo);
    }

    private static readonly IReadOnlySet<string> AllowedRuntimeDestinations =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin_runtime" };

    private static IReadOnlyList<JsonElement> BuildSourceTopology(Guid hubId, string manifestKey, string suffix)
    {
        var topology = ManifestTopologyValidator.BuildTopology(
            role: "admin",
            target: "admin",
            layer: $"manifest_lifecycle_proof_{suffix}",
            action: "list",
            runtimeDestination: "admin_runtime",
            projectionDefinition: null);

        var hubGroupingEntry = JsonSerializer.SerializeToElement(new
        {
            type = ManifestCanonicalProjection.HubGroupingEntryType,
            hubId,
            manifestKey,
        });
        return ManifestCanonicalProjection.MergeTopologyEntry(
            topology, ManifestCanonicalProjection.HubGroupingEntryType, hubGroupingEntry);
    }

    [Fact]
    public async Task CreateDraftAsync_ProjectsDraftTopologyManifest_WithZeroHubRelations_DuringAuthoring()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var hubId = Guid.NewGuid();

        try
        {
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", hubId));

            var (_, manifestRepo) = BuildRepos(cs);
            var topology = BuildSourceTopology(hubId, $"lifecycle-proof-source-{suffix}", suffix);
            var (manifest, error) = await manifestRepo.CreateDraftAsync(relationRegistryId: null, topology);
            Assert.Null(error);
            Assert.NotNull(manifest);
            // Proof item 1: draft create -> canonical topology side is also draft.
            Assert.Equal("draft", manifest!.Status);

            var topologyManifestStatus = await ScalarStringAsync(cs,
                "SELECT status FROM hubs.topology_manifests WHERE topology_manifest_id = @id",
                ("id", manifest.ManifestId));
            Assert.Equal("draft", topologyManifestStatus);

            // Proof item 2: draft N=0 hub_relations is valid/existing during authoring -- no
            // error was raised by CreateDraftAsync above despite zero relations existing.
            var relationCount = await ScalarStringAsync(cs,
                "SELECT COUNT(*)::text FROM hubs.hub_relations WHERE topology_manifest_id = @id",
                ("id", manifest.ManifestId));
            Assert.Equal("0", relationCount);
        }
        finally
        {
            await ExecAsync(cs, "DELETE FROM hubs.hub_relations WHERE topology_manifest_id IN (SELECT topology_manifest_id FROM hubs.topology_manifests WHERE hub_id = @hid)", ("hid", hubId));
            await ExecAsync(cs, "DELETE FROM manifest WHERE manifest_id IN (SELECT topology_manifest_id FROM hubs.topology_manifests WHERE hub_id = @hid)", ("hid", hubId));
            await ExecAsync(cs, "DELETE FROM hubs.topology_manifests WHERE hub_id = @hid", ("hid", hubId));
            await ExecAsync(cs, "DELETE FROM hubs.hub WHERE hub_id = @hid", ("hid", hubId));
        }
    }

    [Fact]
    public async Task PromoteAsync_ZeroHubRelations_FailsClosed_LeavesDraftStateTransactionallyIntact()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var hubId = Guid.NewGuid();

        try
        {
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", hubId));

            var (_, manifestRepo) = BuildRepos(cs);
            var topology = BuildSourceTopology(hubId, $"lifecycle-proof-zero-rel-{suffix}", suffix);
            var (manifest, createError) = await manifestRepo.CreateDraftAsync(relationRegistryId: null, topology);
            Assert.Null(createError);

            // Proof item 4: zero-relation promotion fails closed.
            var (promoted, promoteError) = await manifestRepo.PromoteAsync(manifest!.ManifestId, AllowedRuntimeDestinations);
            Assert.Null(promoted);
            Assert.NotNull(promoteError);
            Assert.Equal("HUB_RELATION_REQUIRED_FOR_PROMOTION", promoteError!.Code);

            // Proof item 7: promotion failure leaves draft state transactionally intact -- no
            // partial active projection on either table.
            var manifestStatus = await ScalarStringAsync(cs,
                "SELECT status FROM manifest WHERE manifest_id = @id", ("id", manifest.ManifestId));
            Assert.Equal("draft", manifestStatus);
            var topologyManifestStatus = await ScalarStringAsync(cs,
                "SELECT status FROM hubs.topology_manifests WHERE topology_manifest_id = @id", ("id", manifest.ManifestId));
            Assert.Equal("draft", topologyManifestStatus);
        }
        finally
        {
            await ExecAsync(cs, "DELETE FROM hubs.hub_relations WHERE topology_manifest_id IN (SELECT topology_manifest_id FROM hubs.topology_manifests WHERE hub_id = @hid)", ("hid", hubId));
            await ExecAsync(cs, "DELETE FROM manifest WHERE manifest_id IN (SELECT topology_manifest_id FROM hubs.topology_manifests WHERE hub_id = @hid)", ("hid", hubId));
            await ExecAsync(cs, "DELETE FROM hubs.topology_manifests WHERE hub_id = @hid", ("hid", hubId));
            await ExecAsync(cs, "DELETE FROM hubs.hub WHERE hub_id = @hid", ("hid", hubId));
        }
    }

    [Fact]
    public async Task PromoteAsync_DeprecatedOnlyHubRelation_FailsClosed_LeavesDraftStateIntact()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var sourceHubId = Guid.NewGuid();
        var targetHubId = Guid.NewGuid();
        var targetManifestId = Guid.NewGuid();
        Guid sourceManifestId = Guid.Empty;

        try
        {
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", sourceHubId));
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", targetHubId));
            await ExecAsync(cs,
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", targetManifestId), ("hid", targetHubId), ("key", $"lifecycle-proof-dep-only-target-{suffix}"));

            var (_, manifestRepo) = BuildRepos(cs);
            var topology = BuildSourceTopology(sourceHubId, $"lifecycle-proof-dep-only-source-{suffix}", suffix);
            var (manifest, createError) = await manifestRepo.CreateDraftAsync(relationRegistryId: null, topology);
            Assert.Null(createError);
            sourceManifestId = manifest!.ManifestId;

            // Seed a hub_relations row that is deprecated-only for this manifest -- proving the
            // promotion-time check in isolation, distinct from Round 11's mutation-time
            // hub_navigation:deprecate guard (which would refuse to deprecate a manifest's only
            // active relation and so cannot itself be used to reach this state).
            await ExecAsync(cs,
                "INSERT INTO hubs.hub_relations (topology_manifest_id, related_hub_id, sequence_position, status) " +
                "VALUES (@mid, @hid, 1, 'deprecated')",
                ("mid", sourceManifestId), ("hid", targetHubId));

            // Proof item 5: deprecated-only-relation promotion fails closed.
            var (promoted, promoteError) = await manifestRepo.PromoteAsync(sourceManifestId, AllowedRuntimeDestinations);
            Assert.Null(promoted);
            Assert.NotNull(promoteError);
            Assert.Equal("HUB_RELATION_REQUIRED_FOR_PROMOTION", promoteError!.Code);

            var manifestStatus = await ScalarStringAsync(cs,
                "SELECT status FROM manifest WHERE manifest_id = @id", ("id", sourceManifestId));
            Assert.Equal("draft", manifestStatus);
            var topologyManifestStatus = await ScalarStringAsync(cs,
                "SELECT status FROM hubs.topology_manifests WHERE topology_manifest_id = @id", ("id", sourceManifestId));
            Assert.Equal("draft", topologyManifestStatus);
        }
        finally
        {
            await ExecAsync(cs, "DELETE FROM hubs.hub_relations WHERE topology_manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(cs, "DELETE FROM manifest WHERE manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(cs, "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id IN (@m1, @m2)",
                ("m1", sourceManifestId), ("m2", targetManifestId));
            await ExecAsync(cs, "DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2)", ("h1", sourceHubId), ("h2", targetHubId));
        }
    }

    [Fact]
    public async Task PromoteAsync_UnresolvedOnlyHubRelation_TargetTopologyManifestStillDraft_FailsClosed()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var sourceHubId = Guid.NewGuid();
        var targetHubId = Guid.NewGuid();
        Guid sourceManifestId = Guid.Empty;
        Guid targetManifestId = Guid.Empty;

        try
        {
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", sourceHubId));
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", targetHubId));

            var (contentBundleRepo, manifestRepo) = BuildRepos(cs);

            var sourceTopology = BuildSourceTopology(sourceHubId, $"lifecycle-proof-unresolved-source-{suffix}", suffix);
            var (sourceManifest, sourceCreateError) = await manifestRepo.CreateDraftAsync(relationRegistryId: null, sourceTopology);
            Assert.Null(sourceCreateError);
            sourceManifestId = sourceManifest!.ManifestId;

            // Target manifest is created but deliberately left in draft (never promoted) -- its
            // hubs.topology_manifests row is therefore status='draft', so LoadHubNavigationSequenceAsync's
            // tm2.status='active' resolution correlated subquery yields no row for it.
            var targetTopology = BuildSourceTopology(targetHubId, $"lifecycle-proof-unresolved-target-{suffix}", suffix + "t");
            var (targetManifest, targetCreateError) = await manifestRepo.CreateDraftAsync(relationRegistryId: null, targetTopology);
            Assert.Null(targetCreateError);
            targetManifestId = targetManifest!.ManifestId;

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
            var createPayload = JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = sourceManifestId.ToString(),
                relatedHubId = targetHubId.ToString(),
                sequencePosition = 1,
            });
            var createResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                OperationType: "ManifestLifecycleProofScenario", Target: "admin", Layer: "hub_navigation",
                Action: "create", IdOrHubId: null, Payload: createPayload, Context: null,
                TriggerKind: "client", Role: "admin"));
            Assert.True(createResponse.Success, string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            // Sanity: the relation is status='active' (authoring succeeded), but unresolvable
            // because the target's topology_manifest is still draft.
            var resolvable = await contentBundleRepo.HasResolvableActiveHubRelationAsync(sourceManifestId);
            Assert.False(resolvable);

            // Proof item 6: unresolved-only-target-manifest promotion fails closed.
            var (promoted, promoteError) = await manifestRepo.PromoteAsync(sourceManifestId, AllowedRuntimeDestinations);
            Assert.Null(promoted);
            Assert.NotNull(promoteError);
            Assert.Equal("HUB_RELATION_REQUIRED_FOR_PROMOTION", promoteError!.Code);

            var manifestStatus = await ScalarStringAsync(cs,
                "SELECT status FROM manifest WHERE manifest_id = @id", ("id", sourceManifestId));
            Assert.Equal("draft", manifestStatus);
            var topologyManifestStatus = await ScalarStringAsync(cs,
                "SELECT status FROM hubs.topology_manifests WHERE topology_manifest_id = @id", ("id", sourceManifestId));
            Assert.Equal("draft", topologyManifestStatus);
        }
        finally
        {
            await ExecAsync(cs, "DELETE FROM hubs.hub_relations WHERE topology_manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(cs, "DELETE FROM manifest WHERE manifest_id IN (@m1, @m2)", ("m1", sourceManifestId), ("m2", targetManifestId));
            await ExecAsync(cs, "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id IN (@m1, @m2)",
                ("m1", sourceManifestId), ("m2", targetManifestId));
            await ExecAsync(cs, "DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2)", ("h1", sourceHubId), ("h2", targetHubId));
        }
    }

    [Fact]
    public async Task PromoteAsync_ResolvableActiveHubRelation_Succeeds_BothManifestAndTopologyManifestBecomeActive()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var sourceHubId = Guid.NewGuid();
        var targetHubId = Guid.NewGuid();
        var targetManifestId = Guid.NewGuid();
        Guid sourceManifestId = Guid.Empty;

        try
        {
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", sourceHubId));
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", targetHubId));
            await ExecAsync(cs,
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", targetManifestId), ("hid", targetHubId), ("key", $"lifecycle-proof-resolvable-target-{suffix}"));

            var (_, manifestRepo) = BuildRepos(cs);
            var sourceTopology = BuildSourceTopology(sourceHubId, $"lifecycle-proof-resolvable-source-{suffix}", suffix);
            var (sourceManifest, createError) = await manifestRepo.CreateDraftAsync(relationRegistryId: null, sourceTopology);
            Assert.Null(createError);
            sourceManifestId = sourceManifest!.ManifestId;

            // Proof item 3 (authoring half): hub relation authoring after draft creation, via the
            // real hub_navigation:create dispatch, on a draft topology_manifest.
            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
            var createPayload = JsonSerializer.SerializeToElement(new
            {
                topologyManifestId = sourceManifestId.ToString(),
                relatedHubId = targetHubId.ToString(),
                sequencePosition = 1,
            });
            var createResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                OperationType: "ManifestLifecycleProofScenario", Target: "admin", Layer: "hub_navigation",
                Action: "create", IdOrHubId: null, Payload: createPayload, Context: null,
                TriggerKind: "client", Role: "admin"));
            Assert.True(createResponse.Success, string.Join(";", createResponse.Errors.Select(e => e.Code + ":" + e.Message)));

            // Proof item 3 (promotion half): canonically-resolvable relation -> promotion
            // succeeds and both public.manifest and hubs.topology_manifests become active.
            var (promoted, promoteError) = await manifestRepo.PromoteAsync(sourceManifestId, AllowedRuntimeDestinations);
            Assert.Null(promoteError);
            Assert.NotNull(promoted);
            Assert.Equal("active", promoted!.Status);

            var topologyManifestStatus = await ScalarStringAsync(cs,
                "SELECT status FROM hubs.topology_manifests WHERE topology_manifest_id = @id", ("id", sourceManifestId));
            Assert.Equal("active", topologyManifestStatus);
        }
        finally
        {
            await ExecAsync(cs, "DELETE FROM hubs.hub_relations WHERE topology_manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(cs, "DELETE FROM manifest WHERE manifest_id = @mid", ("mid", sourceManifestId));
            await ExecAsync(cs, "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id IN (@m1, @m2)",
                ("m1", sourceManifestId), ("m2", targetManifestId));
            await ExecAsync(cs, "DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2)", ("h1", sourceHubId), ("h2", targetHubId));
        }
    }

    [Fact]
    public async Task DeprecateAsync_ActiveManifest_MirrorsStatusToTopologyManifests_TargetBecomesUnresolvable()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var upstreamHubId = Guid.NewGuid();
        var upstreamManifestId = Guid.NewGuid();
        var targetHubId = Guid.NewGuid();
        Guid targetManifestId = Guid.Empty;
        var farHubId = Guid.NewGuid();
        var farManifestId = Guid.NewGuid();

        try
        {
            // An upstream manifest whose sole relation points at the manifest under test -- used
            // to observe, via the real resolution query, that a deprecated topology_manifest can
            // no longer resolve as a navigable target.
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", upstreamHubId));
            await ExecAsync(cs,
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", upstreamManifestId), ("hid", upstreamHubId), ("key", $"lifecycle-proof-deprecate-upstream-{suffix}"));
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", targetHubId));

            var (contentBundleRepo, manifestRepo) = BuildRepos(cs);
            var targetTopology = BuildSourceTopology(targetHubId, $"lifecycle-proof-deprecate-target-{suffix}", suffix);
            var (targetManifest, createError) = await manifestRepo.CreateDraftAsync(relationRegistryId: null, targetTopology);
            Assert.Null(createError);
            targetManifestId = targetManifest!.ManifestId;

            // Give the target its own resolvable relation so it can be promoted to active.
            await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", farHubId));
            await ExecAsync(cs,
                "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                ("mid", farManifestId), ("hid", farHubId), ("key", $"lifecycle-proof-deprecate-far-{suffix}"));
            await ExecAsync(cs,
                "INSERT INTO hubs.hub_relations (topology_manifest_id, related_hub_id, sequence_position, status) VALUES (@mid, @hid, 1, 'active')",
                ("mid", targetManifestId), ("hid", farHubId));

            var (promotedTarget, promoteError) = await manifestRepo.PromoteAsync(targetManifestId, AllowedRuntimeDestinations);
            Assert.Null(promoteError);
            Assert.Equal("active", promotedTarget!.Status);

            await ExecAsync(cs,
                "INSERT INTO hubs.hub_relations (topology_manifest_id, related_hub_id, sequence_position, status) VALUES (@mid, @hid, 1, 'active')",
                ("mid", upstreamManifestId), ("hid", targetHubId));

            Assert.True(await contentBundleRepo.HasResolvableActiveHubRelationAsync(upstreamManifestId));

            var (deprecated, deprecateError) = await manifestRepo.DeprecateAsync(targetManifestId);
            Assert.Null(deprecateError);
            Assert.Equal("deprecated", deprecated!.Status);

            var topologyManifestStatus = await ScalarStringAsync(cs,
                "SELECT status FROM hubs.topology_manifests WHERE topology_manifest_id = @id", ("id", targetManifestId));
            Assert.Equal("deprecated", topologyManifestStatus);

            // Once the target's topology_manifests row mirrors 'deprecated', the upstream
            // manifest's relation to it must stop resolving (same rule that makes an
            // active-but-unresolvable-target relation fail promotion in the earlier test).
            Assert.False(await contentBundleRepo.HasResolvableActiveHubRelationAsync(upstreamManifestId));
        }
        finally
        {
            await ExecAsync(cs, "DELETE FROM hubs.hub_relations WHERE topology_manifest_id IN (@m1, @m2)",
                ("m1", upstreamManifestId), ("m2", targetManifestId));
            await ExecAsync(cs, "DELETE FROM manifest WHERE manifest_id = @mid", ("mid", targetManifestId));
            await ExecAsync(cs, "DELETE FROM hubs.topology_manifests WHERE topology_manifest_id IN (@m1, @m2, @m3)",
                ("m1", upstreamManifestId), ("m2", targetManifestId), ("m3", farManifestId));
            await ExecAsync(cs, "DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2, @h3)",
                ("h1", upstreamHubId), ("h2", targetHubId), ("h3", farHubId));
        }
    }
}
