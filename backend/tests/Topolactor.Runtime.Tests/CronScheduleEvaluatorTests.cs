using Topolactor.Scheduler;
using Xunit;

namespace Topolactor.Runtime.Tests;

// ---------------------------------------------------------------------------
// CronScheduleEvaluator unit tests.
//
// Proves:
//   1. * * * * * matches any time.
//   2. Specific minute/hour/day/month/dow values match exactly.
//   3. Range, list, and step syntax are evaluated correctly.
//   4. Non-matching expressions return false.
//   5. Invalid / malformed expressions return false (skip, not throw).
//   6. Day-of-week 7 is accepted as Sunday alias (=0).
// ---------------------------------------------------------------------------

public class CronScheduleEvaluatorTests
{
    // Reference time: 2026-06-20 14:35:00 UTC — Saturday (DayOfWeek=6), month=6, day=20
    private static readonly DateTimeOffset Ref = new(2026, 6, 20, 14, 35, 0, TimeSpan.Zero);

    [Fact]
    public void WildcardAll_AlwaysMatches()
    {
        Assert.True(CronScheduleEvaluator.IsDue("* * * * *", Ref));
    }

    [Fact]
    public void ExactMatch_AllFields_ReturnsTrue()
    {
        // 35 14 20 6 6 — minute=35, hour=14, day=20, month=6, saturday(6)
        Assert.True(CronScheduleEvaluator.IsDue("35 14 20 6 6", Ref));
    }

    [Fact]
    public void MinuteMismatch_ReturnsFalse()
    {
        Assert.False(CronScheduleEvaluator.IsDue("30 14 20 6 6", Ref));
    }

    [Fact]
    public void HourMismatch_ReturnsFalse()
    {
        Assert.False(CronScheduleEvaluator.IsDue("35 10 20 6 6", Ref));
    }

    [Fact]
    public void MonthMismatch_ReturnsFalse()
    {
        Assert.False(CronScheduleEvaluator.IsDue("35 14 20 1 6", Ref));
    }

    [Fact]
    public void DayOfMonthMismatch_ReturnsFalse()
    {
        Assert.False(CronScheduleEvaluator.IsDue("35 14 1 6 6", Ref));
    }

    [Fact]
    public void DayOfWeekMismatch_ReturnsFalse()
    {
        // Ref is Saturday (6); dow=0 (Sunday) — no match
        Assert.False(CronScheduleEvaluator.IsDue("35 14 20 6 0", Ref));
    }

    [Fact]
    public void DayOfWeek_7_AcceptedAsSundayAlias()
    {
        var sunday = new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero); // Sunday
        Assert.True(CronScheduleEvaluator.IsDue("0 0 21 6 7", sunday));
        Assert.True(CronScheduleEvaluator.IsDue("0 0 21 6 0", sunday));
    }

    [Fact]
    public void List_MinuteList_MatchesAny()
    {
        // minute=30,35,40 — ref minute=35 → match
        Assert.True(CronScheduleEvaluator.IsDue("30,35,40 * * * *", Ref));
    }

    [Fact]
    public void List_MinuteList_NoMatch()
    {
        Assert.False(CronScheduleEvaluator.IsDue("10,20,30 * * * *", Ref));
    }

    [Fact]
    public void Range_HourRange_Matches()
    {
        // hour=12-16 — ref hour=14 → match
        Assert.True(CronScheduleEvaluator.IsDue("* 12-16 * * *", Ref));
    }

    [Fact]
    public void Range_HourRange_NoMatch()
    {
        Assert.False(CronScheduleEvaluator.IsDue("* 0-10 * * *", Ref));
    }

    [Fact]
    public void Step_StarSlashStep_Every15Minutes()
    {
        // */15 matches 0,15,30,45 — ref minute=35 → no match
        Assert.False(CronScheduleEvaluator.IsDue("*/15 * * * *", Ref));
        // minute=30 → match
        var at30 = new DateTimeOffset(2026, 6, 20, 14, 30, 0, TimeSpan.Zero);
        Assert.True(CronScheduleEvaluator.IsDue("*/15 * * * *", at30));
    }

    [Fact]
    public void Step_RangeWithStep_Matches()
    {
        // 0-59/5 matches 0,5,10,...,35,...,55 — ref minute=35 → match
        Assert.True(CronScheduleEvaluator.IsDue("0-59/5 * * * *", Ref));
    }

    [Fact]
    public void Step_RangeWithStep_NoMatch()
    {
        // 0-59/5 does not match 37
        var at37 = new DateTimeOffset(2026, 6, 20, 14, 37, 0, TimeSpan.Zero);
        Assert.False(CronScheduleEvaluator.IsDue("0-59/5 * * * *", at37));
    }

    // ─── Invalid / unsupported expressions — all must return false, not throw ───

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("* * * *")]           // only 4 fields
    [InlineData("* * * * * *")]       // 6 fields
    [InlineData("invalid")]
    [InlineData("60 * * * *")]        // minute out of range
    [InlineData("* 24 * * *")]        // hour out of range
    [InlineData("* * 0 * *")]         // day-of-month out of range (must be 1–31)
    [InlineData("* * * 13 *")]        // month out of range
    [InlineData("* * * * 8")]         // dow out of range
    [InlineData("abc * * * *")]       // non-numeric field
    [InlineData("*/0 * * * *")]       // step of 0
    [InlineData("10-5 * * * *")]      // inverted range
    [InlineData("1- * * * *")]        // malformed range
    [InlineData("-1 * * * *")]        // negative value
    public void InvalidExpression_ReturnsFalse(string expr)
    {
        Assert.False(CronScheduleEvaluator.IsDue(expr, Ref));
    }
}
