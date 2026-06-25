using System.IO;
using Xunit;

public class SqlAttentionLogsFunctionContractTests
{
    private static string LoadSql()
    {
        var cwd = Directory.GetCurrentDirectory();
        var candidate = new DirectoryInfo(cwd);
        string? repoRoot = null;
        while (candidate != null)
        {
            var hasGit = Directory.Exists(Path.Combine(candidate.FullName, ".git"));
            var hasDb = File.Exists(Path.Combine(candidate.FullName, "db", "sql_attention_logs_tables.sql"));
            if (hasGit && hasDb)
            {
                repoRoot = candidate.FullName;
                break;
            }

            candidate = candidate.Parent;
        }

        Assert.False(string.IsNullOrWhiteSpace(repoRoot), $"Repository root not found from cwd={cwd}");
        var path = Path.Combine(repoRoot!, "db", "sql_attention_logs_tables.sql");
        return File.ReadAllText(path);
    }

    [Fact]
    public void RefreshFunction_IncludesTopNMembershipOrderLevelAndDeltaWatch()
    {
        var sql = LoadSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION logs.refresh_logs_current_watch", sql);
        Assert.Contains("membership_entered_topn", sql);
        Assert.Contains("membership_left_topn", sql);
        Assert.Contains("order_changed", sql);
        Assert.Contains("level_changed", sql);
        Assert.Contains("delta_threshold_exceeded", sql);
        Assert.Contains("COALESCE(a.current_id, b.current_id)", sql);
        Assert.Contains("rs.reason IS NOT NULL", sql);
        Assert.Contains("rs.reason <> 'no_change'", sql);
    }

    [Fact]
    public void RefreshFunction_ComputesL2NormAndDoesNotWriteAttentionOrPhaseOrExploration()
    {
        var sql = LoadSql();

        Assert.Contains("sqrt(power(a.count_total::DOUBLE PRECISION, 2.0) + power(a.recordcount_total::DOUBLE PRECISION, 2.0))", sql);
        Assert.DoesNotContain("INSERT INTO logs.attention", sql);
        Assert.DoesNotContain("generate_phase_vector", sql);
    }

    [Fact]
    public void PolicyResolver_FailCloses_WhenPolicyMissingOrIncomplete()
    {
        var sql = LoadSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION logs.resolve_sql_attention_watch_policy", sql);
        Assert.Contains("SQL Attention watch policy missing", sql);
        Assert.Contains("SQL Attention watch policy keys missing", sql);
        Assert.DoesNotContain("COALESCE(v_policy", sql);
    }

    [Fact]
    public void RefreshFunction_UsesSsotPhysicalIdentityColumns_AndNoSiblingDoubleWriteCte()
    {
        var sql = LoadSql();
        Assert.Contains("CREATE TABLE IF NOT EXISTS logs.diff", sql);
        Assert.Contains("physical_table_id", sql);
        Assert.Contains("physical_table_name", sql);
        Assert.Contains("record_id", sql);
        Assert.Contains("operation_kind", sql);
        Assert.DoesNotContain("d.table_id", sql);
        Assert.DoesNotContain("d.primary_key", sql);

        Assert.Contains("WITH aggregated AS", sql);
        Assert.Contains("INSERT INTO logs.current", sql);
        Assert.Contains("WITH ranked AS", sql);
        Assert.Contains("UPDATE logs.current c", sql);
    }

    [Fact]
    public void RefreshHubCurrent_UsesManifestScopedHubRelationsCount()
    {
        var sql = LoadSql();
        Assert.Contains("JOIN hubs.topology_manifests tm ON tm.topology_manifest_id = hr.topology_manifest_id", sql);
        Assert.DoesNotContain("FROM hubs.hub_relations hr WHERE hr.hub_id", sql);
    }

    [Fact]
    public void RefreshHubCurrent_UpdatesProjectionBoundaries_AndAvoidsMutation()
    {
        var sql = LoadSql();
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.refresh_hub_current", sql);
        Assert.Contains("population_count", sql);
        Assert.Contains("population_recordcount", sql);
        Assert.Contains("axis_population_json", sql);
        Assert.Contains("axis_z_score_json", sql);
        Assert.Contains("phase_basis_json", sql);
        Assert.Contains("attractor_vector_json", sql);
        Assert.Contains("'i', 0", sql);
        Assert.DoesNotContain("INSERT INTO topologys", sql);
        Assert.DoesNotContain("UPDATE topologys", sql);
    }

    [Fact]
    public void GenerateAttentionPhaseVector_ReturnsPendingIdSpaceEvidence_NotCountScalarAxes()
    {
        var sql = LoadSql();
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.generate_attention_phase_vector", sql);
        Assert.Contains("'q_kind', 'phaseAT'", sql);
        Assert.Contains("'q_is_draft', false", sql);
        Assert.Contains("'canonical_exploration_field', 'hubs.hub_relations'", sql);
        Assert.Contains("'x_hit_hub_relation_id', NULL", sql);
        Assert.Contains("'y_topology_manifest_id', NULL", sql);
        Assert.Contains("'z_hub_id', NULL", sql);
        Assert.Contains("'i_expanded_hub_relation_ids', '[]'::jsonb", sql);
        Assert.Contains("'legacy_support_cache_statistics'", sql);
        Assert.DoesNotContain("'x', COALESCE(p_hub_relations_count", sql);
        Assert.DoesNotContain("'y', COALESCE(p_hub_count", sql);
        Assert.DoesNotContain("'z', COALESCE(p_topology_manifests_count", sql);
        Assert.Contains("no_automatic_topology_mutation", sql);
    }
    [Fact]
    public void Step4GenerationLine_HasExplicitResolverLineageColumnsAndNoHubRelationMutation()
    {
        var sql = LoadSql();
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.resolve_related_topology_manifests", sql);
        Assert.Contains("topology.physical_table_manifest_bindings", sql);
        Assert.Contains("AMBIGUOUS_PHYSICAL_TABLE_IDENTITY", sql);
        Assert.Contains("generation_line_id", sql);
        Assert.Contains("source_attention_id", sql);
        Assert.Contains("source_topology_manifest_ids", sql);
        Assert.Contains("expanded_hub_relation_ids", sql);
        Assert.Contains("evidence_kind IN ('sql_attention_hit', 'phaseAT', 'draft_projection', 'adoption_result', 'rejection_result')", sql);
        // hub_current_id is nullable in canonical CREATE TABLE (migration retired; ALTER DROP NOT NULL integrated)
        Assert.DoesNotContain("hub_current_id        UUID        NOT NULL", sql);
        // source_attention_id FK to logs.attention is declared inline in canonical CREATE TABLE
        Assert.Contains("source_attention_id   UUID        REFERENCES logs.attention(attention_id)", sql);
        // canonical DDL must not mutate hubs.hub_relations
        Assert.DoesNotContain("UPDATE hubs.hub_relations", sql);
    }

    // =========================================================================
    // manifest_topology_key_expansion_draft_lane contract
    // =========================================================================

    [Fact]
    public void DraftLane_JsonbLeavesWalker_FeedsGuardedInputsToSetReturningFunctions()
    {
        var sql = LoadSql();
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.sql_attention_jsonb_leaves", sql);
        // Scalar-safe: jsonb_each / jsonb_array_elements must receive a CASE-guarded container,
        // never the raw recursive node (which raises on scalar leaves regardless of planner ordering).
        Assert.Contains("jsonb_each(", sql);
        Assert.Contains("CASE WHEN jsonb_typeof(walk.v) = 'object' THEN walk.v ELSE '{}'::jsonb END", sql);
        Assert.Contains("CASE WHEN jsonb_typeof(walk.v) = 'array' THEN walk.v ELSE '[]'::jsonb END", sql);
        // The fragile form (raw node fed to the SRF, guarded only by a WHERE qual) must not return.
        Assert.DoesNotContain("FROM jsonb_each(walk.v)", sql);
        Assert.DoesNotContain("FROM jsonb_array_elements(walk.v)", sql);
    }

    [Fact]
    public void DraftLane_DefinesInsertOnlyAuthorityAndMarkdownProjectionTables()
    {
        var sql = LoadSql();
        Assert.Contains("CREATE TABLE IF NOT EXISTS logs.sql_attention_draft_candidate", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS logs.sql_attention_draft_markdown_projection", sql);
        // authority constants enforced by CHECK constraints
        Assert.Contains("CHECK (source = 'sql_attention')", sql);
        Assert.Contains("CHECK (candidate_lane = 'manifest_topology_key_expansion_draft_lane')", sql);
        Assert.Contains("CHECK (status = 'draft')", sql);
        Assert.Contains("CHECK (candidate_type IN ('relationship_axis_candidate', 'meaning_projection_candidate', 'aggregate_projection_candidate'))", sql);
        // required draft payload fields
        foreach (var field in new[]
        {
            "source_evidence_refs", "high_pressure_key", "hit_manifest_refs", "hit_table_refs",
            "common_axis_candidates", "candidate_columns", "score", "candidate_payload_json"
        })
        {
            Assert.Contains(field, sql);
        }
    }

    [Fact]
    public void DraftLane_IsInsertOnly_NoUpdateOrDeleteOnDraftOrAttentionTables()
    {
        var sql = LoadSql();
        Assert.DoesNotContain("UPDATE logs.sql_attention_draft_candidate", sql);
        Assert.DoesNotContain("DELETE FROM logs.sql_attention_draft_candidate", sql);
        Assert.DoesNotContain("UPDATE logs.sql_attention_draft_markdown_projection", sql);
        Assert.DoesNotContain("DELETE FROM logs.sql_attention_draft_markdown_projection", sql);
        // the draft lane never mutates the SQLAT evidence it consumes
        Assert.DoesNotContain("UPDATE logs.attention", sql);
        Assert.DoesNotContain("DELETE FROM logs.attention", sql);
    }

    [Fact]
    public void DraftLane_KeyExtraction_DependsOnSqlAttentionEvidence_NotFullSpaceMining()
    {
        var sql = LoadSql();
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.extract_sql_attention_high_pressure_keys", sql);
        // extraction reads SQLAT evidence rows (no SQLAT evidence => no keys => no candidates)
        Assert.Contains("FROM logs.attention a", sql);
        Assert.Contains("a.evidence_kind = 'sql_attention_hit'", sql);
        // neighborhood (hub_relation) is the key-extraction space
        Assert.Contains("hit_hub_relation_ids", sql);
        Assert.Contains("JOIN hubs.hub_relations hr ON hr.hub_relation_id = rel", sql);
        // discrete key patterns are data-defined policy, not hidden literals
        Assert.Contains("v_policy->'discrete_key_name_patterns'", sql);
    }

    [Fact]
    public void DraftLane_Compile_ExpandsAcrossFullManifestTopologySpace_NotNeighborhoodOnly()
    {
        var sql = LoadSql();
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.compile_sql_attention_manifest_topology_draft_candidates", sql);
        // full registered manifest topology space (not limited to the SQLAT neighborhood)
        Assert.Contains("FROM hubs.topology_manifests tm", sql);
        Assert.Contains("WHERE tm.status = 'active'", sql);
        // logical tables/columns + enum metadata + physical bindings searched
        Assert.Contains("topology.schema_registry", sql);
        Assert.Contains("topology.physical_table_manifest_bindings", sql);
        // enum metadata is searched "when available" via a to_regclass-guarded helper, so a
        // database without the optional enum dictionary does not hard-fail the lane.
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.sql_attention_enum_group_matches", sql);
        Assert.Contains("to_regclass('enum.groups') IS NULL", sql);
        Assert.Contains("logs.sql_attention_enum_group_matches(k.key_name, k.key_value)", sql);
        Assert.Contains("enum.groups", sql);
        // re-aggregation axes
        foreach (var axis in new[]
        {
            "same_name_axis", "same_type_axis", "same_name_and_same_type_common_axis",
            "enum_group_match", "value_overlap", "logs_diff_pressure", "logs_attention_pressure",
            "table_ref_reuse", "manifest_reuse"
        })
        {
            Assert.Contains(axis, sql);
        }
    }

    [Fact]
    public void DraftLane_Scoring_IsNotRawCount_HasDampeningLiftAndIdHandling()
    {
        var sql = LoadSql();
        // weights, dampening, and lift are data-defined policy
        Assert.Contains("logs.resolve_sql_attention_key_expansion_policy", sql);
        Assert.Contains("routine_value_dampen_factor", sql);
        Assert.Contains("generic_column_dampen_factor", sql);
        Assert.Contains("v_lift_min", sql);
        Assert.Contains("'raw_count_only', false", sql);
        // ID columns are axis/dimension candidates, not primary display text
        Assert.Contains("'relationship_axis_candidate'", sql);
        Assert.Contains("id_axis_treated_as_dimension", sql);
        // policy fail-close (no hidden magic numbers / silent fallback)
        Assert.Contains("SQL Attention key expansion policy missing", sql);
        Assert.Contains("SQL Attention key expansion policy keys missing", sql);
    }

    [Fact]
    public void DraftLane_MarkdownRender_HasNoHtmlIslandCssScriptOrPromotionAuthority()
    {
        var sql = LoadSql();
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.render_sql_attention_draft_candidate_markdown", sql);
        // body is plain markdown only — no markup/script/css-class/placement authority generated by SQL
        Assert.DoesNotContain("<script", sql);
        Assert.DoesNotContain("<div", sql);
        Assert.DoesNotContain("<span", sql);
        Assert.DoesNotContain("class=", sql);
        Assert.DoesNotContain("<iframe", sql);
        // authority disclaimer present in rendered body
        Assert.Contains("Authority is the draft candidate JSONB record", sql);
    }

    [Fact]
    public void DraftLane_Notify_EmitsStructuredJsonPayload_NoUiPlacement()
    {
        var sql = LoadSql();
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.notify_sql_attention_draft_candidate_created", sql);
        Assert.Contains("AFTER INSERT ON logs.sql_attention_draft_markdown_projection", sql);
        Assert.Contains("'event_type', 'sql_attention_draft_candidate_created'", sql);
        Assert.Contains("'source', 'sql_attention'", sql);
        Assert.Contains("'candidate_id', NEW.candidate_id", sql);
        Assert.Contains("'candidate_lane', NEW.candidate_lane", sql);
        Assert.Contains("'markdown_projection_id', NEW.markdown_projection_id", sql);
        Assert.Contains("pg_notify('sql_attention_draft_candidate'", sql);
    }

    [Fact]
    public void DraftLane_HasNoAutoApplyAutoPromoteOrActiveTopologyMutation()
    {
        var sql = LoadSql();
        // the draft lane functions must not mutate active manifests / topology registry / hub relations / routes
        Assert.DoesNotContain("UPDATE hubs.topology_manifests", sql);
        Assert.DoesNotContain("UPDATE hubs.hub_relations", sql);
        Assert.DoesNotContain("UPDATE topology.structure_maps", sql);
        Assert.DoesNotContain("INSERT INTO topology.structure_maps", sql);
        // run-lane orchestrator only compiles + inserts draft candidates
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.run_sql_attention_manifest_topology_key_expansion_draft_lane", sql);
        Assert.Contains("logs.insert_sql_attention_draft_candidate", sql);
    }

}
