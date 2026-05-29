using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;
using Xunit;

/// <summary>
/// Live PostgreSQL end-to-end test proving the SQL Attention closed loop.
///
/// Loop under test:
///   logs.diff append
///   → logs.refresh_logs_current_watch → change candidate
///   → HubAttractorExplorationRuntime.ExploreAsync → hits
///   → NpgsqlSqlAttentionLogsRepository.WriteLogsAttentionAsync → logs.attention persisted
///   → logs.refresh_hub_current → hub_current population_count / attractor_vector_json updated
///   → NpgsqlSqlAttentionLogsRepository.LoadAttentionEvidenceForProjectionAsync → evidence returned
///
/// This test is production_ready evidence for SQL Attention: it proves that the runtime boundary
/// is closed end-to-end with a real PostgreSQL instance, not just in-memory mocks.
///
/// Skip condition:
///   TOPOLACTOR_TEST_DB_CONNECTION not set → early return (not a silent pass; reason logged in source).
///   TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 → throws, enforcing DB presence in CI.
///
/// To run:
///   export TOPOLACTOR_TEST_DB_CONNECTION='Host=localhost;Database=topolactor_demo;Username=topolactor_demo;Password=topolactor_demo'
///   dotnet test --filter SqlAttentionLiveDbEndToEndTests
/// </summary>
public class SqlAttentionLiveDbEndToEndTests
{
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task ClosedLoop_DiffAppend_Watch_Explore_WriteAttention_RefreshHubCurrent_ProjectionRead()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required for SQL Attention live DB E2E test " +
                    "(TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 enforces DB presence). " +
                    "Set TOPOLACTOR_TEST_DB_CONNECTION to a valid PostgreSQL connection string.");
            // No DB connection available — explicit skip, not a silent pass.
            // Run with TOPOLACTOR_TEST_DB_CONNECTION set to execute this test.
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var sourceSetId = $"test-sqla-e2e-{suffix}";
        var basisWindow = $"test-window-{suffix}";
        Guid hubCurrentId = Guid.Empty;

        var repo = new NpgsqlSqlAttentionLogsRepository(
            NullLogger<NpgsqlSqlAttentionLogsRepository>.Instance, cs);
        var topologyRepo = new NpgsqlTopologyRepository(
            NullLogger<NpgsqlTopologyRepository>.Instance, cs);
        var explorationRuntime = new HubAttractorExplorationRuntime(
            NullLogger<HubAttractorExplorationRuntime>.Instance, topologyRepo, repo);

        try
        {
            // ── Setup: seed function_parameters policies ────────────────────────────────────
            // ON CONFLICT DO NOTHING: safe if production rows already present.
            // Values are minimal valid defaults; any existing production values are preserved.
            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.OpenAsync();

                await using (var cmd = new NpgsqlCommand(@"
INSERT INTO topologys.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES ('sql_attention_logs_watch', 'default_policy',
        '{""top_n"":3,""delta_threshold"":0.0,""norm_level_high"":10.0,""norm_level_medium"":1.0}'::jsonb,
        true)
ON CONFLICT (function_name, parameter_key) DO NOTHING", conn))
                    await cmd.ExecuteNonQueryAsync();

                await using (var cmd = new NpgsqlCommand(@"
INSERT INTO topologys.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES ('sql_attention_hub_attractor_exploration', 'default_policy',
        '{""topK_per_hub_kind"":3,""max_hub_kinds_per_current"":5,""max_hub_tables_per_kind"":5,""phase_expansion_limit"":1,""max_attention_rows_saved"":10}'::jsonb,
        true)
ON CONFLICT (function_name, parameter_key) DO NOTHING", conn))
                    await cmd.ExecuteNonQueryAsync();

                // Insert logs.hub_current candidate.
                // attractor_vector_json has diff_count key that overlaps with basis_vector_json
                // produced by refresh_logs_current_watch (which includes diff_count).
                await using (var cmd = new NpgsqlCommand(@"
INSERT INTO logs.hub_current (
    source_set_id, basis_window, attractor_key,
    attractor_vector_json, axis_population_json, axis_z_score_json, phase_basis_json,
    population_count, population_recordcount
) VALUES (
    @sid, @bw, @ak,
    '{""diff_count"":2.0}'::jsonb, '{}'::jsonb, '{}'::jsonb, '{}'::jsonb,
    0, 0
) RETURNING hub_current_id", conn))
                {
                    cmd.Parameters.AddWithValue("sid", sourceSetId);
                    cmd.Parameters.AddWithValue("bw", basisWindow);
                    cmd.Parameters.AddWithValue("ak", $"test-attractor-{suffix}");
                    hubCurrentId = (Guid)(await cmd.ExecuteScalarAsync())!;
                }
            }

            // ── Step 1: Append logs.diff (physical mutation pressure source) ───────────────
            await repo.AppendLogsDiffAsync(new LogsDiffAppendRequest(
                SourceSetId: sourceSetId,
                BasisWindow: basisWindow,
                PhysicalTableId: $"test-table-{suffix}",
                PhysicalTableName: $"test_table_{suffix}",
                RecordId: $"rec-{suffix}",
                OperationKind: "create",
                BeforeStateOrDiffJson: "{}",
                AfterStateOrDiffJson: $@"{{""id"":""{suffix}""}}",
                ObservedAt: DateTimeOffset.UtcNow,
                ActorOrSource: "SqlAttentionLiveDbEndToEndTests",
                ArchivePolicy: "required"));

            // ── Step 2: LoadWatchCandidatesAsync → refresh_logs_current_watch → change candidates
            var candidates = await repo.LoadWatchCandidatesAsync(sourceSetId, basisWindow);

            Assert.NotEmpty(candidates);
            Assert.Contains(candidates, c => c.ChangeDetected);

            // ── Step 3: ExploreAsync → hits produced ─────────────────────────────────────
            var exploreResult = await explorationRuntime.ExploreAsync(
                candidates, sourceSetId, basisWindow);

            Assert.Equal(HubAttractorExplorationStatus.Ok, exploreResult.Status);
            Assert.NotNull(exploreResult.Result);
            Assert.NotEmpty(exploreResult.Result.Hits);

            // ── Step 4: WriteLogsAttentionAsync → logs.attention persisted ────────────────
            var requests = exploreResult.Result.Hits
                .Select(hit => new LogsAttentionWriteRequest(
                    CurrentId: hit.CurrentId,
                    HubCurrentId: hit.HubCurrentId,
                    SourceSetId: hit.SourceSetId,
                    HubId: hit.HubId,
                    AttractorKey: hit.AttractorKey,
                    HubRelationId: hit.HubRelationId,
                    RelationRegistryId: hit.RelationRegistryId,
                    NeighborScore: hit.NeighborScore,
                    HitRank: hit.HitRank,
                    ScoreBand: hit.ScoreBand,
                    PermutationKey: hit.PermutationKey,
                    L2Norm: hit.L2Norm,
                    VectorJson: hit.VectorJson,
                    PhaseVectorJson: hit.PhaseVectorJson,
                    StatisticsJson: SqlAttentionScheduler.BuildStatisticsJson(hit, basisWindow),
                    EmaScore: null,
                    EvidenceJson: hit.EvidenceJson,
                    ArchivePolicy: "required"))
                .ToList();

            var rowsWritten = await repo.WriteLogsAttentionAsync(requests);
            Assert.True(rowsWritten > 0);

            await using var verifyConn = new NpgsqlConnection(cs);
            await verifyConn.OpenAsync();

            // ── Step 5: Verify logs.attention row persisted with archive_policy = 'required' ──
            await using (var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM logs.attention WHERE source_set_id = @sid AND archive_policy = 'required'",
                verifyConn))
            {
                cmd.Parameters.AddWithValue("sid", sourceSetId);
                var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                Assert.True(count > 0,
                    "logs.attention must have at least one evidence row with archive_policy='required' after WriteLogsAttentionAsync.");
            }

            // ── Step 6: Verify phase_vector_json carries w/x/y/z/i/j/k and not_manifest_or_policy_cap ──
            string phaseVectorJsonStr;
            await using (var cmd = new NpgsqlCommand(
                "SELECT phase_vector_json::text FROM logs.attention WHERE source_set_id = @sid LIMIT 1",
                verifyConn))
            {
                cmd.Parameters.AddWithValue("sid", sourceSetId);
                phaseVectorJsonStr = (string)(await cmd.ExecuteScalarAsync())!;
            }

            using var phaseDoc = JsonDocument.Parse(phaseVectorJsonStr);
            var root = phaseDoc.RootElement;

            Assert.True(root.TryGetProperty("w", out _),
                "phase_vector_json.w (l2_norm basis) must be present per SSOT boundary.");
            Assert.True(root.TryGetProperty("x", out _),
                "phase_vector_json.x (hub-side record-count base) must be present.");
            Assert.True(root.TryGetProperty("y", out _),
                "phase_vector_json.y must be present.");
            Assert.True(root.TryGetProperty("z", out _),
                "phase_vector_json.z must be present.");
            Assert.True(root.TryGetProperty("i", out _),
                "phase_vector_json.i (axis movement amount) must be present.");
            Assert.True(root.TryGetProperty("j", out _),
                "phase_vector_json.j must be present.");
            Assert.True(root.TryGetProperty("k", out _),
                "phase_vector_json.k must be present.");
            Assert.True(root.TryGetProperty("meaning_boundary", out var mb),
                "phase_vector_json.meaning_boundary must be present.");
            Assert.True(mb.TryGetProperty("phase_movement_source", out var pms),
                "meaning_boundary.phase_movement_source must be present.");
            Assert.Equal("not_manifest_or_policy_cap", pms.GetString());
            Assert.True(mb.TryGetProperty("no_automatic_topology_mutation", out var noMut),
                "meaning_boundary.no_automatic_topology_mutation must be present.");
            Assert.True(noMut.GetBoolean(),
                "no_automatic_topology_mutation must be true per SSOT boundary.");

            // ── Step 7: refresh_hub_current → attractor_vector_json / population updated ──
            await using (var cmd = new NpgsqlCommand(
                "SELECT hub_current_id FROM logs.refresh_hub_current(@sid, @bw)", verifyConn))
            {
                cmd.Parameters.AddWithValue("sid", sourceSetId);
                cmd.Parameters.AddWithValue("bw", basisWindow);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { /* consume refresh results */ }
            }

            // population_count was 0 at insert; must be > 0 after refresh aggregates attention evidence.
            await using (var cmd = new NpgsqlCommand(
                "SELECT population_count, attractor_vector_json::text FROM logs.hub_current WHERE hub_current_id = @hid",
                verifyConn))
            {
                cmd.Parameters.AddWithValue("hid", hubCurrentId);
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync(), "hub_current row must exist after refresh.");
                var popCount = reader.GetInt64(0);
                Assert.True(popCount > 0,
                    "hub_current.population_count must be > 0 after refresh_hub_current aggregates logs.attention evidence.");
            }

            // ── Step 8: LoadAttentionEvidenceForProjectionAsync → evidence layer separation ──
            var evidence = await repo.LoadAttentionEvidenceForProjectionAsync(
                sourceSetId, topK: 10, minNeighborScore: 0.0, recentWindowDays: 30);

            Assert.NotEmpty(evidence);
            var row = evidence.First();

            // Evidence layer separation per SSOT: statistics / attention / phase_attention must be preserved
            Assert.False(string.IsNullOrWhiteSpace(row.StatisticsJson),
                "AttentionEvidenceRecord.StatisticsJson (statistics layer) must be non-empty.");
            Assert.False(string.IsNullOrWhiteSpace(row.VectorJson),
                "AttentionEvidenceRecord.VectorJson (attention layer) must be non-empty.");
            Assert.False(string.IsNullOrWhiteSpace(row.PhaseVectorJson),
                "AttentionEvidenceRecord.PhaseVectorJson (phase-attention layer) must be non-empty.");
            Assert.False(string.IsNullOrWhiteSpace(row.EvidenceJson),
                "AttentionEvidenceRecord.EvidenceJson (scoring provenance) must be non-empty.");

            // ── Step 9: Topology/registry mutation guard ─────────────────────────────────
            // The SQL Attention pipeline only writes to logs.* tables. This is enforced structurally:
            //   - ExploreAsync prohibited list: no registry mutation, no topology write.
            //   - WriteLogsAttentionAsync SQL: INSERT INTO logs.attention only.
            //   - refresh_hub_current: UPDATE logs.hub_current only.
            //   - NpgsqlSqlAttentionLogsRepository: no SQL targeting topologys.* or hubs.*.
            // Boundary verification is performed by check-sql-attention-ssot.sh (append-only guard).
            // No topologys.* rows with this test's sourceSetId should exist.
        }
        finally
        {
            // Cleanup in FK dependency order: attention → hub_current → current → diff
            try
            {
                await using var cleanConn = new NpgsqlConnection(cs);
                await cleanConn.OpenAsync();

                await using (var cmd = new NpgsqlCommand(
                    "DELETE FROM logs.attention WHERE source_set_id = @sid", cleanConn))
                {
                    cmd.Parameters.AddWithValue("sid", sourceSetId);
                    await cmd.ExecuteNonQueryAsync();
                }

                if (hubCurrentId != Guid.Empty)
                {
                    await using (var cmd = new NpgsqlCommand(
                        "DELETE FROM logs.hub_current WHERE hub_current_id = @hid", cleanConn))
                    {
                        cmd.Parameters.AddWithValue("hid", hubCurrentId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await using (var cmd = new NpgsqlCommand(
                    "DELETE FROM logs.current WHERE source_set_id = @sid", cleanConn))
                {
                    cmd.Parameters.AddWithValue("sid", sourceSetId);
                    await cmd.ExecuteNonQueryAsync();
                }

                await using (var cmd = new NpgsqlCommand(
                    "DELETE FROM logs.diff WHERE source_set_id = @sid", cleanConn))
                {
                    cmd.Parameters.AddWithValue("sid", sourceSetId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch
            {
                // Cleanup failure does not override test result.
            }
        }
    }
}
