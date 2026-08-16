using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

/// <summary>
/// Live PostgreSQL end-to-end proof for the canonical SQL Attention generation line:
/// logs.diff -> logs.current watch -> explicit physical-table manifest resolver ->
/// hubs.hub_relations exploration -> append-only SQLAT hit / phaseAT evidence ->
/// explicit Draft/adoption/rejection lifecycle evidence.
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset means explicit local skip. Set
/// TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI.
/// </summary>
public class SqlAttentionLiveDbEndToEndTests
{
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task ClosedLoop_Resolver_RelationExploration_GenerationLine_ExplicitLifecycle()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException("TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var sourceSetId = $"test-sqla-e2e-{suffix}";
        var basisWindow = $"test-window-{suffix}";
        var tableRef = $"test_table_{suffix}";
        var hubId = Guid.NewGuid();
        var relatedHubId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var relationId = Guid.NewGuid();
        long physicalTableId = 0;

        var repo = new NpgsqlSqlAttentionLogsRepository(NullLogger<NpgsqlSqlAttentionLogsRepository>.Instance, cs);
        var topologyRepo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);
        var explorationRuntime = new HubAttractorExplorationRuntime(NullLogger<HubAttractorExplorationRuntime>.Instance, topologyRepo, repo);
        var promotionRuntime = new SqlAttentionEvidencePromotionRuntime(repo);

        try
        {
            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.OpenAsync();
                await using (var cmd = new NpgsqlCommand(@"
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES ('sql_attention_logs_watch', 'default_policy',
        '{""top_n"":3,""delta_threshold"":0.0,""norm_level_high"":10.0,""norm_level_medium"":1.0}'::jsonb, true)
ON CONFLICT (function_name, parameter_key) DO NOTHING;
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES ('sql_attention_hub_attractor_exploration', 'default_policy',
        '{""norm_level_high"":10.0,""norm_level_medium"":1.0,""exploration_budget_tiers"":{""weak"":{""topK_per_hub_kind"":1,""max_hub_tables_per_kind"":2,""phase_expansion_limit"":1,""search_mode"":""near_neighbor_narrow_topK""},""mid"":{""topK_per_hub_kind"":3,""max_hub_tables_per_kind"":5,""phase_expansion_limit"":1,""search_mode"":""normal_topK""},""high"":{""topK_per_hub_kind"":5,""max_hub_tables_per_kind"":10,""phase_expansion_limit"":3,""search_mode"":""expanded_distance_band_or_permutation""}},""max_hub_kinds_per_current"":5,""max_attention_rows_saved"":10,""neighbor_score_min"":0.0,""strong_hit_threshold"":0.95,""normal_hit_threshold"":0.90,""exploratory_hit_threshold"":0.85}'::jsonb, true)
ON CONFLICT (function_name, parameter_key) DO NOTHING;
INSERT INTO hubs.hub (hub_id, relation) VALUES (@hub_id, '{}'::jsonb), (@related_hub_id, '{}'::jsonb);
INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status) VALUES (@manifest_id, @hub_id, @manifest_key, 'active');
INSERT INTO hubs.hub_relations (hub_relation_id, topology_manifest_id, related_hub_id, sequence_position, relation_config)
VALUES (@relation_id, @manifest_id, @related_hub_id, 1, '{""sql_attention_score"":0.8}'::jsonb);
INSERT INTO topology.physical_tables (table_ref) VALUES (@table_ref) RETURNING physical_table_id", conn))
                {
                    cmd.Parameters.AddWithValue("hub_id", hubId);
                    cmd.Parameters.AddWithValue("related_hub_id", relatedHubId);
                    cmd.Parameters.AddWithValue("manifest_id", manifestId);
                    cmd.Parameters.AddWithValue("manifest_key", $"manifest-{suffix}");
                    cmd.Parameters.AddWithValue("relation_id", relationId);
                    cmd.Parameters.AddWithValue("table_ref", tableRef);
                    physicalTableId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                }
                await using var binding = new NpgsqlCommand(@"
INSERT INTO topology.physical_table_manifest_bindings (physical_table_id, topology_manifest_id, binding_evidence_json)
VALUES (@physical_table_id, @manifest_id, '{""seed"":""SqlAttentionLiveDbEndToEndTests""}'::jsonb)", conn);
                binding.Parameters.AddWithValue("physical_table_id", physicalTableId);
                binding.Parameters.AddWithValue("manifest_id", manifestId);
                await binding.ExecuteNonQueryAsync();
            }

            await repo.AppendLogsDiffAsync(new LogsDiffAppendRequest(sourceSetId, basisWindow, physicalTableId.ToString(), tableRef, $"rec-{suffix}", "create", "{}", $@"{{""id"":""{suffix}""}}", DateTimeOffset.UtcNow, "SqlAttentionLiveDbEndToEndTests", "required"));
            var candidates = await repo.LoadWatchCandidatesAsync(sourceSetId, basisWindow);
            Assert.Contains(candidates, candidate => candidate.ChangeDetected);

            var exploreResult = await explorationRuntime.ExploreAsync(candidates, sourceSetId, basisWindow);
            Assert.Equal(HubAttractorExplorationStatus.Ok, exploreResult.Status);
            var hit = Assert.Single(exploreResult.Result!.Hits);
            Assert.Equal(relationId, hit.HubRelationId);
            Assert.Equal(manifestId, hit.TopologyManifestId);
            Assert.Equal("hubs.hub_relations", hit.AttractorKey);

            var generation = await repo.AppendAttentionGenerationAsync(new AttentionGenerationAppendRequest(hit.GenerationLineId, hit.CurrentId, hit.SourceSetId, hit.TopologyManifestId!.Value, hit.HubRelationId!.Value, hit.HubId!.Value, hit.NeighborScore, hit.HitRank, hit.ScoreBand, hit.L2Norm, hit.PhaseVectorJson, hit.EvidenceJson, hit.SourceTopologyManifestIds!, hit.ExpandedHubRelationIds!, hit.ExpandedTopologyManifestIds!, hit.ExpandedHubIds!));
            var draft = await promotionRuntime.ExecuteAsync(new AttentionLifecycleCommand(generation.PhaseAtAttentionId, AttentionLifecycleOperation.CreateDraft, "e2e-user", $"draft-{suffix}"));
            Assert.True(draft.Succeeded);
            var adopted = await promotionRuntime.ExecuteAsync(new AttentionLifecycleCommand(draft.AttentionId!.Value, AttentionLifecycleOperation.AdoptDraft, "e2e-user", $"adopt-{suffix}"));
            Assert.True(adopted.Succeeded);
            var rejected = await promotionRuntime.ExecuteAsync(new AttentionLifecycleCommand(generation.PhaseAtAttentionId, AttentionLifecycleOperation.Reject, "e2e-user", $"reject-{suffix}"));
            Assert.True(rejected.Succeeded);

            await using var verifyConn = new NpgsqlConnection(cs);
            await verifyConn.OpenAsync();
            await using (var cmd = new NpgsqlCommand(@"
SELECT evidence_kind, generation_line_id, source_attention_id, phase_vector_json::text
FROM logs.attention WHERE generation_line_id = @generation_line_id ORDER BY created_at, attention_id", verifyConn))
            {
                cmd.Parameters.AddWithValue("generation_line_id", generation.GenerationLineId);
                await using var reader = await cmd.ExecuteReaderAsync();
                var kinds = new List<string>();
                string? phaseJson = null;
                while (await reader.ReadAsync())
                {
                    kinds.Add(reader.GetString(0));
                    Assert.Equal(generation.GenerationLineId, reader.GetGuid(1));
                    if (reader.GetString(0) == "phaseAT") phaseJson = reader.GetString(3);
                }
                Assert.Contains("sql_attention_hit", kinds);
                Assert.Contains("phaseAT", kinds);
                Assert.Contains("draft_projection", kinds);
                Assert.Contains("adoption_result", kinds);
                Assert.Contains("rejection_result", kinds);
                using var phaseDoc = JsonDocument.Parse(phaseJson!);
                var root = phaseDoc.RootElement;
                Assert.Equal("phaseAT", root.GetProperty("q_kind").GetString());
                Assert.False(root.GetProperty("q_is_draft").GetBoolean());
                Assert.Equal(relationId, root.GetProperty("x_hit_hub_relation_id").GetGuid());
                Assert.Equal(manifestId, root.GetProperty("y_topology_manifest_id").GetGuid());
                Assert.True(root.TryGetProperty("k_expanded_hub_ids", out _));
            }

            var evidence = await repo.LoadAttentionEvidenceForProjectionAsync(sourceSetId, 10, 0.0, 30);
            Assert.Equal(2, evidence.Count);
        }
        finally
        {
            try
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand(@"
DELETE FROM logs.attention WHERE source_set_id = @sid;
DELETE FROM logs.current WHERE source_set_id = @sid;
DELETE FROM logs.diff WHERE source_set_id = @sid;
DELETE FROM topology.physical_table_manifest_bindings WHERE topology_manifest_id = @manifest_id;
DELETE FROM hubs.hub_relations WHERE hub_relation_id = @relation_id;
DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @manifest_id;
DELETE FROM hubs.hub WHERE hub_id IN (@hub_id, @related_hub_id);
DELETE FROM topology.physical_tables WHERE physical_table_id = @physical_table_id", conn);
                cmd.Parameters.AddWithValue("sid", sourceSetId);
                cmd.Parameters.AddWithValue("manifest_id", manifestId);
                cmd.Parameters.AddWithValue("relation_id", relationId);
                cmd.Parameters.AddWithValue("hub_id", hubId);
                cmd.Parameters.AddWithValue("related_hub_id", relatedHubId);
                cmd.Parameters.AddWithValue("physical_table_id", physicalTableId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Cleanup failure does not replace the test result.
            }
        }
    }
}
