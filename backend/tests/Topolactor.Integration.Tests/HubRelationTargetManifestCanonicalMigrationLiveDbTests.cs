using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live-DB proofs for Bundle hub-relation-target-manifest-canonical-migration (.agent/tasks/todo.md):
///
/// Goal 1 -- Admin Credential (manifest 092) reaches Admin Enum (manifest ae200) through the existing
/// source-scoped, forward-only fixed navigation: the 092 -> ae200 row is a canonical bootstrap row of
/// db/seed_empty.sql (never created by this test), the real ManifestDispatcher surfaces it in 092's
/// Emission.NavigationSequence, and its resolved TargetManifestId dispatches ae200's real projection.
/// The serialized NavigationSequence is pinned to frontend/tests/fixtures/
/// manifest_0092_navigation_sequence.json, which frontend/tests/projectionShellHubNavigationRenderProof.test.ts
/// renders through the real ProjectionShell nav bar and clicks.
///
/// Goal 2 -- docs/design/db-schema.yaml hub_relations.target_reference_canonical_contract:
/// mirror_freshness_boundary through the real replacement-merge flow, and
/// legacy_ambiguous_row_migration_disposition through
/// db/legacy_utils/hub_relations_related_hub_id_to_target_topology_manifest_id.sql.
///
/// Skipped (no-op) when TOPOLACTOR_TEST_DB_CONNECTION is not set.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class HubRelationTargetManifestCanonicalMigrationLiveDbTests
{
    private static readonly Guid AdminCredentialManifestId = new("00000000-0000-0000-0000-000000000092");
    private static readonly Guid AdminCredentialHubId = new("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid AdminEnumManifestId = new("00000000-0000-0000-0000-0000000ae200");
    private static readonly Guid AdminEnumHubId = new("00000000-0000-0000-0000-0000000ae201");
    private static readonly Guid AdminCredentialToAdminEnumRelationId = new("00000000-0000-0000-0000-0000000ae2b1");

    private static readonly IReadOnlySet<string> AllowedRuntimeDestinations =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin_runtime" };

    private static string? GetConnectionString() => AggregateTriggerRepositoryLiveDbTests.GetConnectionString();

    private static string RepoPath(string relative) => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../../../", relative));

    private static async Task ExecAsync(string cs, string sql, params (string Name, object Value)[] parms)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── Goal 1 ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CanonicalSeed_Manifest092_OwnsOutboundRelationToAe200_CoexistingWithItsSelfReferencingRow()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT hub_relation_id, sequence_position, target_topology_manifest_id, related_hub_id, status, " +
            "       relation_config->>'transition' " +
            "FROM hubs.hub_relations WHERE topology_manifest_id = @mid AND sequence_position IN (1, 2) " +
            "ORDER BY sequence_position";
        cmd.Parameters.AddWithValue("mid", AdminCredentialManifestId);
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), "092's canonical self-referencing sequence_position=1 row must remain");
        Assert.Equal(1, reader.GetInt32(1));
        Assert.Equal(AdminCredentialManifestId, reader.GetGuid(2));
        Assert.Equal(AdminCredentialHubId, reader.GetGuid(3));
        Assert.Equal("active", reader.GetString(4));
        Assert.Equal("canonical_default_entry", reader.GetString(5));

        // The row db/seed_empty.sql authors (fixed id): present in the canonically bootstrapped DB,
        // not created by any test.
        Assert.True(await reader.ReadAsync(), "db/seed_empty.sql must author 092's outbound row to ae200 at sequence_position=2");
        Assert.Equal(AdminCredentialToAdminEnumRelationId, reader.GetGuid(0));
        Assert.Equal(2, reader.GetInt32(1));
        Assert.Equal(AdminEnumManifestId, reader.GetGuid(2));
        // related_hub_id is ae200's own hub_id -- the derived legacy mirror.
        Assert.Equal(AdminEnumHubId, reader.GetGuid(3));
        Assert.Equal("active", reader.GetString(4));
        Assert.True(reader.IsDBNull(5), "an ordinary outbound relation, not a canonical_default_entry marker");
    }

    [Fact]
    public async Task DispatchAsync_Manifest092_NavigationSequenceResolvesAe200_AndThatTargetDispatchesAe200Projection()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);

        // Exactly what ProjectionShell sends for ?manifest=<092> (projectionEntry.ts
        // resolveProjectionEntryAxes) -- the production entry shape, not a test shortcut.
        var sourceResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null,
            Payload: JsonSerializer.SerializeToElement(new { target_ref = $"manifest:{AdminCredentialManifestId}:projection_entry" }),
            Context: null, TriggerKind: "client", Role: "admin"));

        Assert.True(sourceResponse.Success, string.Join(";", sourceResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        var sourceEmission = sourceResponse.Emission!;
        HubRelationUiProjectionResolutionChainProof.AssertNavigationSequenceResolvesHubVector(
            sourceEmission,
            AdminCredentialManifestId,
            [
                new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(AdminCredentialHubId, 1, AdminCredentialManifestId),
                new HubRelationUiProjectionResolutionChainProof.ExpectedHubVectorEntry(AdminEnumHubId, 2, AdminEnumManifestId),
            ]);

        var enumLink = Assert.Single(sourceEmission.NavigationSequence!, i => i.SequencePosition == 2);
        Assert.Equal(AdminCredentialToAdminEnumRelationId.ToString(), enumLink.HubRelationId);
        // Human-readable nav-bar text through the same relation_registry label mechanism 092's own
        // hub uses -- never the raw hub_id UUID.
        Assert.Equal("Enum dictionary management", enumLink.RelatedHubLabel);

        // The frontend nav-bar proof consumes exactly this serialized NavigationSequence (same
        // snapshot-link pattern as manifest_0092_bare_entry_layout_nodes.json): a seed change fails
        // here rather than leaving the frontend fixture silently stale.
        var expectedJson = await File.ReadAllTextAsync(RepoPath("frontend/tests/fixtures/manifest_0092_navigation_sequence.json"));
        var actualJson = JsonSerializer.Serialize(
            sourceEmission.NavigationSequence,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
            });
        Assert.Equal(expectedJson.Trim(), actualJson.Trim());

        // One click later: the ?manifest=<TargetManifestId> the nav bar links to dispatches ae200's
        // own real projection (package/layout/wiring/tensor rows -> LayoutNodes).
        var targetResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "Search", "default", "screen_list", "Search",
            IdOrHubId: null,
            Payload: JsonSerializer.SerializeToElement(new { target_ref = $"manifest:{enumLink.TargetManifestId}:projection_entry" }),
            Context: null, TriggerKind: "client", Role: "admin"));
        Assert.True(targetResponse.Success, string.Join(";", targetResponse.Errors.Select(e => e.Code + ":" + e.Message)));
        var targetEmission = targetResponse.Emission!;
        Assert.Equal(AdminEnumManifestId.ToString(), targetEmission.ManifestId);
        Assert.NotNull(targetEmission.LayoutId);
        Assert.NotNull(targetEmission.LayoutNodes);
        Assert.Contains(targetEmission.LayoutNodes!, n => n.NodeId == "enum_table");
        Assert.Empty(targetEmission.LayoutNodes!.Where(n => n.NodeKind == "catalog_component" && n.ComponentId is null));
    }

    // ─── Goal 2: mirror_freshness_boundary ──────────────────────────────────────────────────

    [Fact]
    public async Task ReplacementMerge_MovesTargetManifestHubId_ResyncsRelatedHubIdMirror_OnEveryTargetingRowRegardlessOfStatus()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var originalHubId = Guid.NewGuid();
        var movedHubId = Guid.NewGuid();
        var farHubId = Guid.NewGuid();
        var farManifestId = Guid.NewGuid();
        var sourceHubId = Guid.NewGuid();
        var sourceManifestId = Guid.NewGuid();
        var otherSourceHubId = Guid.NewGuid();
        var otherSourceManifestId = Guid.NewGuid();
        var legacyNullTargetRelationId = Guid.NewGuid();
        // Multi-instance leakage guard: another active manifest on the SAME original hub. Rows that
        // target it must keep mirroring originalHubId -- the resync is keyed on the moved manifest's
        // own target_topology_manifest_id, never on the shared hub.
        var siblingManifestId = Guid.NewGuid();
        Guid targetManifestId = Guid.Empty;

        var contentBundleRepo = new NpgsqlContentBundleRepository(NullLogger<NpgsqlContentBundleRepository>.Instance, cs);
        var manifestRepo = new NpgsqlManifestRepository(NullLogger<NpgsqlManifestRepository>.Instance, cs, contentBundleRepo);

        try
        {
            foreach (var hubId in new[] { originalHubId, movedHubId, farHubId, sourceHubId, otherSourceHubId })
                await ExecAsync(cs, "INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", hubId));
            foreach (var (manifestId, hubId, key) in new[]
                     {
                         (farManifestId, farHubId, "far"), (sourceManifestId, sourceHubId, "source"),
                         (siblingManifestId, originalHubId, "sibling-on-original-hub"),
                         (otherSourceManifestId, otherSourceHubId, "other-source"),
                     })
            {
                await ExecAsync(cs,
                    "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, 'active')",
                    ("mid", manifestId), ("hid", hubId), ("key", $"mirror-freshness-{key}-{suffix}"));
            }

            // The TARGET manifest goes through the real authoring lifecycle so it can later be
            // replaced by a real replacement-clone merge: draft (hub_grouping.hubId=originalHubId) ->
            // its own outbound relation -> promote to active.
            var (target, createError) = await manifestRepo.CreateDraftAsync(
                relationRegistryId: null, BuildTopology(originalHubId, $"mirror-freshness-target-{suffix}", suffix));
            Assert.Null(createError);
            targetManifestId = target!.ManifestId;

            var dispatcher = await HubRelationUiProjectionResolutionChainProof.BuildRealDispatcherAsync(cs);
            async Task<Guid> CreateAsync(Guid source, Guid targetManifest, int seq)
            {
                var response = await dispatcher.DispatchAsync(new EndpointRequestDto(
                    OperationType: "MirrorFreshnessScenario", Target: "admin", Layer: "hub_navigation", Action: "create",
                    IdOrHubId: null,
                    Payload: JsonSerializer.SerializeToElement(new
                    {
                        topologyManifestId = source.ToString(),
                        targetTopologyManifestId = targetManifest.ToString(),
                        sequencePosition = seq,
                    }),
                    Context: null, TriggerKind: "client", Role: "admin"));
                Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
                var data = response.Emission!.Data!.Value;
                Assert.True(data.GetProperty("ok").GetBoolean(), data.GetRawText());
                return Guid.Parse(data.GetProperty("hubRelationId").GetString()!);
            }

            await CreateAsync(targetManifestId, farManifestId, 1);
            var (promoted, promoteError) = await manifestRepo.PromoteAsync(targetManifestId, AllowedRuntimeDestinations);
            Assert.Null(promoteError);
            Assert.Equal("active", promoted!.Status);

            // Rows targeting the manifest: an active one, and a deprecated one (deprecated via the
            // real hub_navigation:deprecate action, keeping another active row on its source).
            var activeRelationId = await CreateAsync(sourceManifestId, targetManifestId, 1);
            await CreateAsync(sourceManifestId, farManifestId, 2);
            var siblingRelationId = await CreateAsync(sourceManifestId, siblingManifestId, 3);
            var deprecatedRelationId = await CreateAsync(otherSourceManifestId, targetManifestId, 1);
            await CreateAsync(otherSourceManifestId, farManifestId, 2);
            var deprecateResponse = await dispatcher.DispatchAsync(new EndpointRequestDto(
                OperationType: "MirrorFreshnessScenario", Target: "admin", Layer: "hub_navigation", Action: "deprecate",
                IdOrHubId: null,
                Payload: JsonSerializer.SerializeToElement(new { hubRelationId = deprecatedRelationId.ToString() }),
                Context: null, TriggerKind: "client", Role: "admin"));
            Assert.True(deprecateResponse.Emission!.Data!.Value.GetProperty("ok").GetBoolean());

            // A transition-window legacy row (NULL target, deprecated so the active-target CHECK
            // allows it) that still mirrors the original hub: it has no target authority, so the
            // resync must leave it alone.
            await ExecAsync(cs,
                "INSERT INTO hubs.hub_relations (hub_relation_id, topology_manifest_id, related_hub_id, sequence_position, status) " +
                "VALUES (@rid, @mid, @hid, 3, 'deprecated')",
                ("rid", legacyNullTargetRelationId), ("mid", otherSourceManifestId), ("hid", originalHubId));

            Assert.Equal(originalHubId, await RelatedHubIdAsync(cs, activeRelationId));
            Assert.Equal(originalHubId, await RelatedHubIdAsync(cs, deprecatedRelationId));
            Assert.Equal(originalHubId, await RelatedHubIdAsync(cs, siblingRelationId));

            // Real replacement clone of the active target whose hub_grouping moves it to movedHubId,
            // merged back into the SAME active topology_manifest_id (ProjectOnPromoteAsync upsert).
            var (draft, cloneError) = await manifestRepo.CreateCloneReplacementDraftFromActiveAsync(targetManifestId);
            Assert.Null(cloneError);
            var movedTopology = ManifestCanonicalProjection.MergeTopologyEntry(
                draft!.Topology,
                ManifestCanonicalProjection.HubGroupingEntryType,
                JsonSerializer.SerializeToElement(new
                {
                    type = ManifestCanonicalProjection.HubGroupingEntryType,
                    hubId = movedHubId,
                    manifestKey = $"mirror-freshness-target-{suffix}",
                }));
            var (_, updateError) = await manifestRepo.UpdateDraftAsync(draft.ManifestId, draft.RelationRegistryId, movedTopology);
            Assert.Null(updateError);
            var merge = await manifestRepo.MergeCloneReplacementDraftToActiveAsync(draft.ManifestId, AllowedRuntimeDestinations, "mirror-freshness-proof");
            Assert.True(merge.Ok, merge.Error?.Code + ":" + merge.Error?.Message);
            Assert.Equal(targetManifestId, merge.ActiveManifestId);

            Assert.Equal(movedHubId, await ScalarGuidAsync(cs,
                "SELECT hub_id FROM hubs.topology_manifests WHERE topology_manifest_id = @id", ("id", targetManifestId)));

            // Every row whose target_topology_manifest_id is this manifest now mirrors the new hub,
            // active and deprecated alike; the NULL-target legacy row is untouched.
            Assert.Equal(movedHubId, await RelatedHubIdAsync(cs, activeRelationId));
            Assert.Equal(movedHubId, await RelatedHubIdAsync(cs, deprecatedRelationId));
            Assert.Equal(originalHubId, await RelatedHubIdAsync(cs, legacyNullTargetRelationId));
            // No leakage onto a different manifest that merely shared the old hub.
            Assert.Equal(originalHubId, await RelatedHubIdAsync(cs, siblingRelationId));

            // The existing /admin/manifests listing (all statuses) exposes the fresh mirror.
            var listed = await contentBundleRepo.ListHubRelationsByManifestAsync(otherSourceManifestId);
            var listedDeprecated = Assert.Single(listed, r => r.HubRelationId == deprecatedRelationId.ToString());
            Assert.Equal("deprecated", listedDeprecated.Status);
            Assert.Equal(movedHubId.ToString(), listedDeprecated.RelatedHubId);

            // Target resolution never depended on the mirror: still the same manifest.
            var sequence = await contentBundleRepo.LoadHubNavigationSequenceAsync(sourceManifestId);
            var resolved = Assert.Single(sequence, i => i.HubRelationId == activeRelationId.ToString());
            Assert.Equal(targetManifestId.ToString(), resolved.TargetManifestId);
            Assert.Equal(movedHubId.ToString(), resolved.RelatedHubId);
        }
        finally
        {
            await ExecAsync(cs,
                "DELETE FROM manifest WHERE manifest_id = @tid OR manifest_id IN " +
                "(SELECT topology_manifest_id FROM hubs.topology_manifests WHERE hub_id IN (@h1, @h2))",
                ("tid", targetManifestId), ("h1", originalHubId), ("h2", movedHubId));
            await ExecAsync(cs,
                "DELETE FROM hubs.hub WHERE hub_id IN (@h1, @h2, @h3, @h4, @h5)",
                ("h1", originalHubId), ("h2", movedHubId), ("h3", farHubId), ("h4", sourceHubId), ("h5", otherSourceHubId));
        }
    }

    // ─── Goal 2: legacy_ambiguous_row_migration_disposition ─────────────────────────────────

    [Fact]
    public async Task LegacyUtility_BackfillsExactlyOneOnly_LeavesZeroAndMultipleNullWithStatusUntouched_ReportsAndDefersConstraint()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        var script = await File.ReadAllTextAsync(
            RepoPath("db/legacy_utils/hub_relations_related_hub_id_to_target_topology_manifest_id.sql"));

        var singleHubId = Guid.NewGuid();
        var singleManifestId = Guid.NewGuid();
        var zeroHubId = Guid.NewGuid();
        var zeroHubDeprecatedManifestId = Guid.NewGuid();
        var twoHubId = Guid.NewGuid();
        var twoManifestA = Guid.NewGuid();
        var twoManifestB = Guid.NewGuid();
        var okSourceHubId = Guid.NewGuid();
        var okSourceManifestId = Guid.NewGuid();
        var orphanSourceHubId = Guid.NewGuid();
        var orphanSourceManifestId = Guid.NewGuid();

        var resolvableRow = Guid.NewGuid();
        var resolvableDeprecatedRow = Guid.NewGuid();
        var zeroRow = Guid.NewGuid();
        var multipleRow = Guid.NewGuid();

        var notices = new List<string>();
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        conn.Notice += (_, e) => notices.Add(e.Notice.MessageText);

        // Everything below runs inside one transaction that is always rolled back: the canonical
        // table is temporarily reshaped back to its pre-migration (no target_topology_manifest_id)
        // form so the utility can be exercised against real legacy rows without leaving any trace.
        await using var tx = await conn.BeginTransactionAsync();
        async Task SqlAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }
        async Task<object?> ScalarAsync(string sql, params (string Name, object Value)[] parms)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
            return await cmd.ExecuteScalarAsync();
        }

        try
        {
            await SqlAsync(
                "ALTER TABLE hubs.hub_relations DROP CONSTRAINT hub_relations_active_target_topology_manifest_required; " +
                "ALTER TABLE hubs.hub_relations DROP COLUMN target_topology_manifest_id;");

            foreach (var hubId in new[] { singleHubId, zeroHubId, twoHubId, okSourceHubId, orphanSourceHubId })
                await SqlAsync("INSERT INTO hubs.hub (hub_id, relation) VALUES (@id, '{}'::jsonb)", ("id", hubId));
            foreach (var (manifestId, hubId, status) in new[]
                     {
                         (singleManifestId, singleHubId, "active"),
                         (zeroHubDeprecatedManifestId, zeroHubId, "deprecated"),
                         (twoManifestA, twoHubId, "active"), (twoManifestB, twoHubId, "active"),
                         (okSourceManifestId, okSourceHubId, "active"), (orphanSourceManifestId, orphanSourceHubId, "active"),
                     })
            {
                await SqlAsync(
                    "INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@mid, @hid, @key, @status)",
                    ("mid", manifestId), ("hid", hubId), ("key", $"legacy-disposition-{manifestId:N}"), ("status", status));
            }

            // Legacy-shaped rows (related_hub_id only):
            //   okSource:     seq1 -> singleHub (exactly one active)          active
            //                 seq2 -> singleHub                                 deprecated
            //   orphanSource: seq1 -> zeroHub (only a deprecated manifest)     active
            //                 seq2 -> twoHub (two active manifests)            active
            foreach (var (rowId, source, hub, seq, status) in new[]
                     {
                         (resolvableRow, okSourceManifestId, singleHubId, 1, "active"),
                         (resolvableDeprecatedRow, okSourceManifestId, singleHubId, 2, "deprecated"),
                         (zeroRow, orphanSourceManifestId, zeroHubId, 1, "active"),
                         (multipleRow, orphanSourceManifestId, twoHubId, 2, "active"),
                     })
            {
                await SqlAsync(
                    "INSERT INTO hubs.hub_relations (hub_relation_id, topology_manifest_id, related_hub_id, sequence_position, status) " +
                    "VALUES (@rid, @mid, @hid, @seq, @status)",
                    ("rid", rowId), ("mid", source), ("hid", hub), ("seq", seq), ("status", status));
            }

            await SqlAsync(script);

            async Task<(Guid? Target, Guid RelatedHub, string Status)> RowAsync(Guid rowId)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText =
                    "SELECT target_topology_manifest_id, related_hub_id, status FROM hubs.hub_relations WHERE hub_relation_id = @id";
                cmd.Parameters.AddWithValue("id", rowId);
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                return (reader.IsDBNull(0) ? null : reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2));
            }

            // (1) exactly one active target manifest -> backfilled, status untouched (both statuses).
            Assert.Equal((singleManifestId, singleHubId, "active"), await RowAsync(resolvableRow));
            Assert.Equal((singleManifestId, singleHubId, "deprecated"), await RowAsync(resolvableDeprecatedRow));
            // (2) zero / multiple -> NOT fabricated: NULL target, status still active, mirror untouched.
            Assert.Equal(((Guid?)null, zeroHubId, "active"), await RowAsync(zeroRow));
            Assert.Equal(((Guid?)null, twoHubId, "active"), await RowAsync(multipleRow));

            // Canonical seed rows that were stripped to legacy shape are recovered exactly.
            Assert.Equal(AdminEnumManifestId, await ScalarAsync(
                "SELECT target_topology_manifest_id FROM hubs.hub_relations WHERE hub_relation_id = @id",
                ("id", AdminCredentialToAdminEnumRelationId)));

            // Classification surface names the reason per unresolved row.
            async Task<string?> ClassificationAsync(Guid rowId) => (string?)await ScalarAsync(
                "SELECT classification FROM hubs.hub_relations_target_manifest_classification() WHERE hub_relation_id = @id",
                ("id", rowId));
            Assert.Equal("zero_active_target_manifest", await ClassificationAsync(zeroRow));
            Assert.Equal("multiple_active_target_manifests", await ClassificationAsync(multipleRow));
            Assert.Null(await ClassificationAsync(resolvableRow));

            // (5) a source whose active rows are ALL unresolved is reported as a navigation orphan.
            Assert.Equal(2L, await ScalarAsync(
                "SELECT unresolved_active_row_count FROM hubs.hub_relations_unresolved_active_sources() WHERE topology_manifest_id = @id",
                ("id", orphanSourceManifestId)));
            Assert.Null(await ScalarAsync(
                "SELECT unresolved_active_row_count FROM hubs.hub_relations_unresolved_active_sources() WHERE topology_manifest_id = @id",
                ("id", okSourceManifestId)));

            // (3) the active-target CHECK is deferred while active NULL rows remain.
            Assert.Equal(0L, await ScalarAsync(
                "SELECT COUNT(*) FROM pg_constraint WHERE conrelid = 'hubs.hub_relations'::regclass " +
                "AND conname = 'hub_relations_active_target_topology_manifest_required'"));

            Assert.Contains(notices, n => n.Contains($"hub_relation_id={zeroRow}") && n.Contains("zero_active_target_manifest"));
            Assert.Contains(notices, n => n.Contains($"hub_relation_id={multipleRow}") && n.Contains("multiple_active_target_manifests"));
            Assert.Contains(notices, n => n.Contains("navigation orphan source") && n.Contains(orphanSourceManifestId.ToString()));
            Assert.Contains(notices, n => n.Contains("DEFERRED"));

            // Idempotent re-run: nothing guessed on the second pass either.
            await SqlAsync(script);
            Assert.Equal(((Guid?)null, zeroHubId, "active"), await RowAsync(zeroRow));
            Assert.Equal(((Guid?)null, twoHubId, "active"), await RowAsync(multipleRow));

            // (4) after explicit admin remediation (here: re-point one row to a chosen manifest and
            // deliberately deprecate the other), the constraint can be enabled and is then enforced.
            await SqlAsync(
                "UPDATE hubs.hub_relations SET target_topology_manifest_id = @tid, related_hub_id = @hid WHERE hub_relation_id = @id",
                ("tid", twoManifestA), ("hid", twoHubId), ("id", multipleRow));
            await SqlAsync("UPDATE hubs.hub_relations SET status = 'deprecated' WHERE hub_relation_id = @id", ("id", zeroRow));
            Assert.Equal(true, await ScalarAsync("SELECT hubs.hub_relations_try_enable_active_target_required()"));

            await SqlAsync("SAVEPOINT enforce_check");
            var violation = await Assert.ThrowsAsync<PostgresException>(() => SqlAsync(
                "UPDATE hubs.hub_relations SET status = 'active' WHERE hub_relation_id = @id", ("id", zeroRow)));
            Assert.Equal(PostgresErrorCodes.CheckViolation, violation.SqlState);
            await SqlAsync("ROLLBACK TO SAVEPOINT enforce_check");
        }
        finally
        {
            await tx.RollbackAsync();
        }

        // Rolled back: canonical shape is intact.
        Assert.Equal(1L, await ScalarOutsideAsync(cs,
            "SELECT COUNT(*) FROM pg_constraint WHERE conrelid = 'hubs.hub_relations'::regclass " +
            "AND conname = 'hub_relations_active_target_topology_manifest_required'"));
    }

    [Fact]
    public async Task ActiveRowWithoutTarget_IsRejectedByCanonicalSchema()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO hubs.hub_relations (topology_manifest_id, related_hub_id, sequence_position, status) " +
            "VALUES (@mid, @hid, 99001, 'active')";
        cmd.Parameters.AddWithValue("mid", AdminCredentialManifestId);
        cmd.Parameters.AddWithValue("hid", AdminEnumHubId);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("hub_relations_active_target_topology_manifest_required", ex.ConstraintName);
        await tx.RollbackAsync();
    }

    private static IReadOnlyList<JsonElement> BuildTopology(Guid hubId, string manifestKey, string suffix)
    {
        var topology = ManifestTopologyValidator.BuildTopology(
            role: "admin",
            target: "admin",
            layer: $"mirror_freshness_proof_{suffix}",
            action: "list",
            runtimeDestination: "admin_runtime",
            projectionDefinition: null);
        return ManifestCanonicalProjection.MergeTopologyEntry(
            topology,
            ManifestCanonicalProjection.HubGroupingEntryType,
            JsonSerializer.SerializeToElement(new
            {
                type = ManifestCanonicalProjection.HubGroupingEntryType,
                hubId,
                manifestKey,
            }));
    }

    private static async Task<Guid> RelatedHubIdAsync(string cs, Guid hubRelationId) =>
        await ScalarGuidAsync(cs, "SELECT related_hub_id FROM hubs.hub_relations WHERE hub_relation_id = @id", ("id", hubRelationId));

    private static async Task<Guid> ScalarGuidAsync(string cs, string sql, params (string Name, object Value)[] parms) =>
        (Guid)(await ScalarOutsideAsync(cs, sql, parms))!;

    private static async Task<object?> ScalarOutsideAsync(string cs, string sql, params (string Name, object Value)[] parms)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parms) cmd.Parameters.AddWithValue(name, value);
        return await cmd.ExecuteScalarAsync();
    }
}
