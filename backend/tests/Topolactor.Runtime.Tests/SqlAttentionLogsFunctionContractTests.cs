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
}
