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
    public void Step4GenerationLine_HasExplicitResolverLineageColumnsAndMigration()
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
        var migration = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../../db/migrations/sql_attention_phase_generation_line.sql"));
        Assert.Contains("ALTER TABLE logs.attention ALTER COLUMN hub_current_id DROP NOT NULL", migration);
        Assert.Contains("fk_logs_attention_source_attention", migration);
        Assert.Contains("CREATE OR REPLACE FUNCTION logs.resolve_related_topology_manifests", migration);
        Assert.DoesNotContain("UPDATE hubs.hub_relations", migration);
    }

}
