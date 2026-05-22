using System.IO;
using Xunit;

public class SqlAttentionLogsFunctionContractTests
{
    private static string LoadSql()
    {
        var path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../../db/sql_attention_logs_tables.sql"));
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
        Assert.Contains("WHERE rs.reason IS DISTINCT FROM 'no_change'", sql);
    }

    [Fact]
    public void RefreshFunction_ComputesL2NormAndDoesNotWriteAttentionOrPhaseOrExploration()
    {
        var sql = LoadSql();

        Assert.Contains("sqrt(power(a.count_total::DOUBLE PRECISION, 2.0) + power(a.recordcount_total::DOUBLE PRECISION, 2.0))", sql);
        Assert.DoesNotContain("INSERT INTO logs.attention", sql);
        Assert.DoesNotContain("phase_vector", sql);
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
}
