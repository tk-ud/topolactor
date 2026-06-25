using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

/// <summary>
/// Live PostgreSQL proof for the manifest_topology_key_expansion draft lane.
///
/// Covers:
///   - logs.sql_attention_jsonb_leaves scalar-safe execution on real adversarial JSONB
///     (scalars at multiple depths, arrays of scalars, booleans/numbers, top-level scalars),
///     proving the recursive walker never raises on scalar leaves regardless of planner ordering.
///   - End-to-end logs.run_sql_attention_manifest_topology_key_expansion_draft_lane: SQLAT
///     evidence -> Key extraction -> full-space expansion -> insert-only draft candidate JSONB
///     + Markdown projection + structured DB NOTIFY (event_type / candidate_id / candidate_lane /
///     markdown_projection_id).
///
/// TOPOLACTOR_TEST_DB_CONNECTION unset means explicit local skip. Set
/// TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1 to require a live database in CI.
/// </summary>
public class SqlAttentionDraftLaneLiveDbTests
{
    private static string? ResolveConnectionOrSkip()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException("TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }
        return cs;
    }

    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task JsonbLeaves_IsScalarSafe_OnAdversarialAndPathologicalInput()
    {
        var cs = ResolveConnectionOrSkip();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        // Nested objects, arrays of scalars, an array-of-object, booleans, numbers, deep nesting.
        const string adversarial =
            """{"a":"open","b":{"c":1,"d":true},"e":["x","y",{"f":"z"}],"g":3.14,"h":{"i":{"j":{"k":"deep"}}}}""";

        await using (var cmd = new NpgsqlCommand(
            "SELECT key_name, key_value, value_kind FROM logs.sql_attention_jsonb_leaves(@doc::jsonb) ORDER BY key_name, key_value", conn))
        {
            cmd.Parameters.AddWithValue("doc", adversarial);
            var leaves = new List<(string Name, string Value, string Kind)>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                leaves.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));

            // Scalar leaves under keys are returned; container nodes and keyless array scalars are not.
            Assert.Contains(("a", "open", "string"), leaves);
            Assert.Contains(("c", "1", "number"), leaves);
            Assert.Contains(("d", "true", "boolean"), leaves);
            Assert.Contains(("f", "z", "string"), leaves);
            Assert.Contains(("g", "3.14", "number"), leaves);
            Assert.Contains(("k", "deep", "string"), leaves);
        }

        // Pathological inputs that re-enter the recursion as scalars / empties must NOT raise.
        foreach (var doc in new[] { "\"justscalar\"", "123", "true", "null", "{}", "[]", "[[1,2],[\"a\"]]" })
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT count(*) FROM logs.sql_attention_jsonb_leaves(@doc::jsonb)", conn);
            cmd.Parameters.AddWithValue("doc", doc);
            // Executes without raising; keyless / scalar-root inputs simply yield zero keyed leaves.
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.True(count >= 0);
        }
    }

    [Fact]
    [Trait("Category", "RequiresDatabase")]
    public async Task RunDraftLane_EndToEnd_InsertsCandidateMarkdownAndNotifies()
    {
        var cs = ResolveConnectionOrSkip();
        if (cs is null) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var sourceSetId = $"test-draftlane-{suffix}";
        var basisWindow = $"test-window-{suffix}";
        var hubId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var relationId = Guid.NewGuid();
        var currentId = Guid.NewGuid();

        try
        {
            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.OpenAsync();
                await using var seed = new NpgsqlCommand(@"
INSERT INTO topology.function_parameters (function_name, parameter_key, parameter_value, active)
VALUES ('sql_attention_manifest_topology_key_expansion', 'default_policy',
  '{""max_keys"":25,""min_key_pressure"":1,""max_candidates"":50,""min_candidate_score"":0.1,""discrete_key_name_patterns"":[""status"",""category"",""type"",""kind"",""state""],""generic_column_names"":[""name"",""title"",""description""],""id_column_suffixes"":[""_id"",""_ref""],""routine_value_manifest_fraction_dampen"":0.6,""routine_value_dampen_factor"":0.4,""generic_column_dampen_factor"":0.5,""lift_min"":1.0,""score_weights"":{""same_name_axis"":1.0,""same_type_axis"":0.4,""common_axis"":1.5,""enum_group_match"":1.2,""value_overlap"":0.8,""logs_diff_pressure"":0.5,""logs_attention_pressure"":0.6,""table_ref_reuse"":0.5,""manifest_reuse"":0.7,""id_axis"":0.9,""lift"":0.8}}'::jsonb, true)
ON CONFLICT (function_name, parameter_key) DO UPDATE SET parameter_value = EXCLUDED.parameter_value, active = true;

INSERT INTO hubs.hub (hub_id, relation) VALUES (@hub_id, '{}'::jsonb);
INSERT INTO hubs.topology_manifests (topology_manifest_id, hub_id, manifest_key, status, topology_jsonb)
VALUES (@manifest_id, @hub_id, @manifest_key, 'active',
  '{""fields"":{""status"":""open"",""category"":""sales"",""order_id"":""x""}}'::jsonb);
INSERT INTO hubs.hub_relations (hub_relation_id, topology_manifest_id, related_hub_id, sequence_position, status, relation_config)
VALUES (@relation_id, @manifest_id, @hub_id, 1, 'active', '{""status"":""open"",""kind"":""sale""}'::jsonb);

INSERT INTO logs.current (current_id, source_set_id, basis_window, physical_table_id, physical_table_name, l2_norm)
VALUES (@current_id, @sid, @bw, '1', @manifest_key, 5.0);
INSERT INTO logs.attention (current_id, source_current_id, source_set_id, evidence_kind, attractor_key,
  source_topology_manifest_ids, hit_hub_relation_ids, neighbor_score, hit_rank, l2_norm)
VALUES (@current_id, @current_id, @sid, 'sql_attention_hit', 'hubs.hub_relations',
  ARRAY[@manifest_id]::uuid[], ARRAY[@relation_id]::uuid[], 0.9, 1, 5.0);", conn);
                seed.Parameters.AddWithValue("hub_id", hubId);
                seed.Parameters.AddWithValue("manifest_id", manifestId);
                seed.Parameters.AddWithValue("manifest_key", $"manifest-{suffix}");
                seed.Parameters.AddWithValue("relation_id", relationId);
                seed.Parameters.AddWithValue("current_id", currentId);
                seed.Parameters.AddWithValue("sid", sourceSetId);
                seed.Parameters.AddWithValue("bw", basisWindow);
                await seed.ExecuteNonQueryAsync();
            }

            // LISTEN for the structured DB NOTIFY emitted by the AFTER INSERT trigger.
            var notifications = new List<string>();
            await using var listenConn = new NpgsqlConnection(cs);
            await listenConn.OpenAsync();
            listenConn.Notification += (_, e) =>
            {
                if (e.Channel == "sql_attention_draft_candidate") notifications.Add(e.Payload);
            };
            await using (var listen = new NpgsqlCommand("LISTEN sql_attention_draft_candidate", listenConn))
                await listen.ExecuteNonQueryAsync();

            // Run the SQL-only draft lane through the repository bridge (no C# candidate inference).
            var repo = new NpgsqlSqlAttentionLogsRepository(NullLogger<NpgsqlSqlAttentionLogsRepository>.Instance, cs);
            var drafts = await repo.CompileManifestTopologyDraftCandidatesAsync(sourceSetId, basisWindow);
            Assert.NotEmpty(drafts);
            Assert.All(drafts, d =>
            {
                Assert.Equal("sql_attention", d.Source);
                Assert.Equal("manifest_topology_key_expansion_draft_lane", d.CandidateLane);
                Assert.Equal("draft", d.Status);
                Assert.NotEqual(Guid.Empty, d.CandidateId);
                Assert.NotEqual(Guid.Empty, d.MarkdownProjectionId);
            });

            // Drain notifications (delivered at the lane's autocommit).
            await listenConn.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotEmpty(notifications);
            Assert.Contains(notifications, p =>
                p.Contains("sql_attention_draft_candidate_created")
                && p.Contains("manifest_topology_key_expansion_draft_lane")
                && p.Contains("markdown_projection_id"));

            // Verify insert-only authority record + Markdown projection, with authority on the JSONB.
            await using var verify = new NpgsqlConnection(cs);
            await verify.OpenAsync();
            await using (var cmd = new NpgsqlCommand(@"
SELECT c.source, c.candidate_lane, c.status, c.candidate_type, c.score,
       c.high_pressure_key->>'key_name' AS key_name,
       jsonb_array_length(c.source_evidence_refs) AS evidence_count,
       m.rendered_markdown
  FROM logs.sql_attention_draft_candidate c
  JOIN logs.sql_attention_draft_markdown_projection m ON m.candidate_id = c.candidate_id
 WHERE c.source_set_id = @sid
 ORDER BY c.score DESC", verify))
            {
                cmd.Parameters.AddWithValue("sid", sourceSetId);
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("sql_attention", reader.GetString(0));
                Assert.Equal("manifest_topology_key_expansion_draft_lane", reader.GetString(1));
                Assert.Equal("draft", reader.GetString(2));
                Assert.False(reader.IsDBNull(3));
                Assert.True(reader.GetDouble(4) > 0);
                Assert.False(reader.IsDBNull(5));        // high_pressure_key.key_name present
                Assert.True(reader.GetInt32(6) >= 1);    // source evidence refs preserved
                var md = reader.GetString(7);
                Assert.Contains("SQL Attention draft candidate", md);
                Assert.Contains("Authority is the draft candidate JSONB record", md);
                Assert.DoesNotContain("<script", md);
                Assert.DoesNotContain("class=", md);
            }
        }
        finally
        {
            try
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand(@"
DELETE FROM logs.sql_attention_draft_markdown_projection WHERE source_set_id = @sid;
DELETE FROM logs.sql_attention_draft_candidate WHERE source_set_id = @sid;
DELETE FROM logs.attention WHERE source_set_id = @sid;
DELETE FROM logs.current WHERE source_set_id = @sid;
DELETE FROM hubs.hub_relations WHERE hub_relation_id = @relation_id;
DELETE FROM hubs.topology_manifests WHERE topology_manifest_id = @manifest_id;
DELETE FROM hubs.hub WHERE hub_id = @hub_id;", conn);
                cmd.Parameters.AddWithValue("sid", sourceSetId);
                cmd.Parameters.AddWithValue("relation_id", relationId);
                cmd.Parameters.AddWithValue("manifest_id", manifestId);
                cmd.Parameters.AddWithValue("hub_id", hubId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Cleanup failure does not replace the test result.
            }
        }
    }
}
