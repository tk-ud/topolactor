using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Composed tier_2 proof (docs/design/pipeline-continuity-ssot.yaml test_tier_policy.tier_2_scenario_harness,
/// required_when backend_runtime_result_affects_frontend_final_state) for the SQL Attention circulation
/// path PR #602 round 9/10 audit found only single-tier-proven: real DB evidence generation
/// (SqlAttentionLiveDbEndToEndTests) never reached AdminRuntime, and the AdminRuntime dispatch
/// (AdminRuntimeSqlAttentionProjectionTests) never touched a real database.
///
/// This composes both halves against ONE real PostgreSQL database and ONE real sourceSetId reference,
/// matching the confirmed architecture (RecommendNavigationProjectionSpec/SqlAttentionProjectionChildSpec
/// carry ONLY the sourceSetId reference, never candidate payload data — backend/schema/ContextRouteContracts.cs
/// — and must stay that way; the real candidate data is fetched by a separate reference-keyed query, per
/// Owner decision recorded in .agent/tasks/todo.md):
///   1. Real DB evidence generation (same generation line as SqlAttentionLiveDbEndToEndTests): logs.diff
///      append -> watch candidates -> HubAttractorExplorationRuntime.ExploreAsync -> logs.attention append,
///      all keyed by one sourceSetId.
///   2. A REAL AdminRuntime.ExecuteDataAsync("sql_attention:list_projection", {sourceSetId}) dispatch —
///      the exact same AbstractFunctionExecutor / SqlAttentionProjectionPrimitiveAdapter /
///      SqlAttentionTopologyProjectionRuntime.ProjectAsync production path AdminRuntimeSqlAttentionProjectionTests
///      exercises, but backed by NpgsqlSqlAttentionLogsRepository/NpgsqlTopologyRepository against the SAME
///      real database the evidence was written to in step 1 (not a test-double repository).
///   3. Assertion that the dispatch response's candidate is the SAME evidence row seeded in step 1 (same
///      hub/attractor identity, cross-checked against a direct repository read) — proving the sourceSetId
///      reference genuinely selects real, freshly-generated evidence through the real admin dispatch
///      boundary end to end, not a coincidental shape match.
///
/// The response this test asserts is also checked in as
/// frontend/tests/fixtures/sql_attention_projection_real_candidate_response.json, consumed by
/// frontend/tests/sqlAttentionCirculationFinalStateProof.test.ts — completing the backend-truth ->
/// checked-in-fixture -> frontend-final-state-consumption composition pattern already established by
/// CredentialManagementHubRelationUiProjectionLiveDbTests / manifest_0092_bare_entry_layout_nodes.json
/// (frontend/tests/layoutSchemaStructuralRender.test.ts) — reused here, not invented.
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset means explicit local skip. Set
/// TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI.
/// </summary>
public class SqlAttentionCirculationComposedProofTests
{
    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task ListProjection_RealAdminRuntimeDispatch_ReturnsSameRealDbGeneratedEvidence_ForSameSourceSetId()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException("TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var sourceSetId = $"test-sqla-circulation-{suffix}";
        var basisWindow = $"test-window-{suffix}";
        var tableRef = $"test_table_{suffix}";
        var hubId = Guid.NewGuid();
        var relatedHubId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var relationId = Guid.NewGuid();
        long physicalTableId = 0;

        var logsRepo = new NpgsqlSqlAttentionLogsRepository(NullLogger<NpgsqlSqlAttentionLogsRepository>.Instance, cs);
        var topologyRepo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);
        var explorationRuntime = new HubAttractorExplorationRuntime(NullLogger<HubAttractorExplorationRuntime>.Instance, topologyRepo, logsRepo);

        try
        {
            // ---- Step 1: real DB evidence generation (same shape as SqlAttentionLiveDbEndToEndTests). ----
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
INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key) VALUES (@manifest_id, @hub_id, @manifest_key);
INSERT INTO hubs.hub_relations (hub_relation_id, topology_manifest_id, related_hub_id, sequence_position, relation_config)
VALUES (@relation_id, @manifest_id, @related_hub_id, 1, '{""sql_attention_score"":0.93}'::jsonb);
INSERT INTO topology.physical_tables (table_ref) VALUES (@table_ref) RETURNING physical_table_id", conn))
                {
                    cmd.Parameters.AddWithValue("hub_id", hubId);
                    cmd.Parameters.AddWithValue("related_hub_id", relatedHubId);
                    cmd.Parameters.AddWithValue("manifest_id", manifestId);
                    cmd.Parameters.AddWithValue("manifest_key", $"manifest-{suffix}");
                    cmd.Parameters.AddWithValue("relation_id", relationId);
                    cmd.Parameters.AddWithValue("table_ref", tableRef);
                    physicalTableId = Convert.ToInt64((await cmd.ExecuteScalarAsync())!);
                }
                await using var binding = new NpgsqlCommand(@"
INSERT INTO topology.physical_table_manifest_bindings (physical_table_id, topology_manifest_id, binding_evidence_json)
VALUES (@physical_table_id, @manifest_id, '{""seed"":""SqlAttentionCirculationComposedProofTests""}'::jsonb)", conn);
                binding.Parameters.AddWithValue("physical_table_id", physicalTableId);
                binding.Parameters.AddWithValue("manifest_id", manifestId);
                await binding.ExecuteNonQueryAsync();
            }

            await logsRepo.AppendLogsDiffAsync(new LogsDiffAppendRequest(
                sourceSetId, basisWindow, physicalTableId.ToString(), tableRef, $"rec-{suffix}", "create",
                "{}", $@"{{""id"":""{suffix}""}}", DateTimeOffset.UtcNow, "SqlAttentionCirculationComposedProofTests", "required"));
            var watchCandidates = await logsRepo.LoadWatchCandidatesAsync(sourceSetId, basisWindow);
            Assert.Contains(watchCandidates, c => c.ChangeDetected);

            var exploreResult = await explorationRuntime.ExploreAsync(watchCandidates, sourceSetId, basisWindow);
            Assert.Equal(HubAttractorExplorationStatus.Ok, exploreResult.Status);
            var hit = Assert.Single(exploreResult.Result!.Hits);
            Assert.Equal(relationId, hit.HubRelationId);

            await logsRepo.AppendAttentionGenerationAsync(new AttentionGenerationAppendRequest(
                hit.GenerationLineId, hit.CurrentId, hit.SourceSetId, hit.TopologyManifestId!.Value,
                hit.HubRelationId!.Value, hit.HubId!.Value, hit.NeighborScore, hit.HitRank, hit.ScoreBand,
                hit.L2Norm, hit.PhaseVectorJson, hit.EvidenceJson, hit.SourceTopologyManifestIds!,
                hit.ExpandedHubRelationIds!, hit.ExpandedTopologyManifestIds!, hit.ExpandedHubIds!));

            // ---- Step 2: REAL AdminRuntime dispatch (production sql_attention:list_projection path),
            //      backed by the SAME real database — not a test-double repository. ----
            var adminRuntime = BuildRealAdminRuntime(cs);
            var vector = new OperationVector("admin", "sql_attention", "list_projection", null, "admin",
                JsonSerializer.SerializeToElement(new { sourceSetId }), null);

            var (data, error) = await adminRuntime.ExecuteDataAsync(vector);

            // ---- Step 3: composed assertion — the dispatch response is the SAME real evidence,
            //      cross-checked against a direct repository read, not merely "some candidate came back". ----
            Assert.Null(error);
            Assert.NotNull(data);
            Assert.Equal("ok", data!.Value.GetProperty("status").GetString());

            var candidates = data.Value.GetProperty("candidates");
            Assert.True(candidates.GetArrayLength() >= 1);
            var candidate = candidates[0];
            Assert.Equal("hubs.hub_relations", candidate.GetProperty("attractorKey").GetString());
            var candidateHubId = candidate.GetProperty("hubId").GetGuid();
            Assert.Equal(hit.HubId!.Value, candidateHubId);

            var directEvidence = await logsRepo.LoadAttentionEvidenceForProjectionAsync(sourceSetId, 10, 0.0, 30);
            Assert.NotEmpty(directEvidence);
            Assert.Equal(directEvidence[0].HubId!.Value, candidateHubId);

            // ---- Fixture: checked-in real response shape for the frontend-final-state composition. ----
            var fixturePath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
                "frontend", "tests", "fixtures", "sql_attention_projection_real_candidate_response.json");
            var normalized = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                status = "ok",
                candidates = new[]
                {
                    new
                    {
                        attractorKey = "hubs.hub_relations",
                        scoreBand = candidate.GetProperty("scoreBand").GetString(),
                        hitRank = candidate.GetProperty("hitRank").GetInt32(),
                        lane = candidate.GetProperty("lane").GetString(),
                    },
                },
            }, new JsonSerializerOptions { WriteIndented = true }));
            Assert.True(File.Exists(fixturePath), $"checked-in fixture missing at resolved path: {fixturePath}");
            var checkedIn = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
            var checkedInCandidate = checkedIn.RootElement.GetProperty("candidates")[0];
            var normalizedCandidate = normalized.RootElement.GetProperty("candidates")[0];
            Assert.Equal(
                checkedIn.RootElement.GetProperty("status").GetString(),
                normalized.RootElement.GetProperty("status").GetString());
            Assert.Equal(
                checkedInCandidate.GetProperty("attractorKey").GetString(),
                normalizedCandidate.GetProperty("attractorKey").GetString());
            Assert.Equal(
                checkedInCandidate.GetProperty("scoreBand").GetString(),
                normalizedCandidate.GetProperty("scoreBand").GetString());
            Assert.Equal(
                checkedInCandidate.GetProperty("hitRank").GetInt32(),
                normalizedCandidate.GetProperty("hitRank").GetInt32());
            Assert.Equal(
                checkedInCandidate.GetProperty("lane").GetString(),
                normalizedCandidate.GetProperty("lane").GetString());
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

    private static AdminRuntime BuildRealAdminRuntime(string cs)
    {
        var ctxRepo = new ContextRouteRepository(NullLogger<ContextRouteRepository>.Instance, "test-double");
        var topoRepo = new TopologyRepository(NullLogger<TopologyRepository>.Instance, "test-double");
        var uiRepo = new UiTopologyRepository(NullLogger<UiTopologyRepository>.Instance, "test-double");
        var vecRuntime = new TopologyVectorRuntime(NullLogger<TopologyVectorRuntime>.Instance, ctxRepo);
        var registrar = new RegistrarValidationService(NullLogger<RegistrarValidationService>.Instance, topoRepo, vecRuntime);
        var pkg = new PackageGeneratorRuntime(NullLogger<PackageGeneratorRuntime>.Instance, uiRepo);

        var realLogsRepo = new NpgsqlSqlAttentionLogsRepository(NullLogger<NpgsqlSqlAttentionLogsRepository>.Instance, cs);
        var realTopologyRepo = new NpgsqlTopologyRepository(NullLogger<NpgsqlTopologyRepository>.Instance, cs);
        var adapter = new SqlAttentionProjectionPrimitiveAdapter(
            NullLogger<SqlAttentionProjectionPrimitiveAdapter>.Instance, realLogsRepo, realTopologyRepo);
        var manifest = CreateSeedManifest();
        var abstractFunctionExecutor = new AbstractFunctionExecutor(
            new StaticManifestRepository(manifest),
            new IAbstractFunctionPrimitiveAdapter[] { adapter });

        return new AdminRuntime(
            NullLogger<AdminRuntime>.Instance,
            ctxRepo, registrar, pkg, uiRepo,
            abstractFunctionExecutor: abstractFunctionExecutor);
    }

    private static AbstractFunctionManifest CreateSeedManifest()
    {
        var step = new AbstractFunctionStep(
            AbstractFunctionStepId: Guid.Parse("00000000-0000-0000-0000-00000000bf11"),
            StepOrder: 1,
            PrimitiveKey: "sql_attention",
            StepConfig: new Dictionary<string, string>
            {
                ["function_name"] = SqlAttentionTopologyProjectionRuntime.ProjectionFunctionName,
                ["parameter_key"] = SqlAttentionTopologyProjectionRuntime.ProjectionPolicyKey
            },
            InputBindings: new[]
            {
                new AbstractFunctionInputBinding("source_set_id", "payload", "sourceSetId", true, false)
            },
            ResultContextKey: "projection_result",
            Active: true);

        return new AbstractFunctionManifest(
            AbstractFunctionId: Guid.Parse("00000000-0000-0000-0000-00000000af09"),
            FunctionKey: "sql_attention.list_projection",
            RuntimeLane: "admin_runtime",
            AuthorityScope: "admin_sql_attention",
            Steps: new[] { step },
            DeniedProjectionKeys: Array.Empty<string>(),
            Active: true,
            AuthorityBindings: new AbstractFunctionAuthorityBinding[]
            {
                new("policy", "admin_sql_attention_projection", true),
                new("table", "logs.attention", true)
            },
            OutputShape: new Dictionary<string, string> { ["sql_attention_result"] = "projection_result" });
    }

    private sealed class StaticManifestRepository : IAbstractFunctionManifestRepository
    {
        private readonly AbstractFunctionManifest? _manifest;
        public StaticManifestRepository(AbstractFunctionManifest? manifest) => _manifest = manifest;
        public Task<AbstractFunctionManifest?> LoadAsync(string functionKey, CancellationToken ct = default) =>
            Task.FromResult(_manifest);
    }
}
