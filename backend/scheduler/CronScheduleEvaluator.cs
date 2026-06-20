namespace Topolactor.Scheduler;

/// <summary>
/// Minimal 5-field cron expression evaluator.
/// Fields: minute hour day-of-month month day-of-week (standard Vixie cron order).
/// Supported per field: * (any), n (value), n,m (list), n-m (range), */s (step), n-m/s (range+step).
/// Day-of-week: 0–6 (Sunday=0); 7 is accepted as Sunday alias.
/// Invalid or unsupported expression → returns false (skip, not fail-close).
/// </summary>
internal static class CronScheduleEvaluator
{
    internal static bool IsDue(string cronExpression, DateTimeOffset now)
    {
        var parts = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return false;

        var minute     = ExpandField(parts[0], 0, 59);
        var hour       = ExpandField(parts[1], 0, 23);
        var dayOfMonth = ExpandField(parts[2], 1, 31);
        var month      = ExpandField(parts[3], 1, 12);
        var dayOfWeek  = ExpandField(parts[4], 0, 7); // 7 accepted as Sunday alias

        if (minute is null || hour is null || dayOfMonth is null || month is null || dayOfWeek is null)
            return false;

        // Normalise 7→0 for day-of-week to align with DayOfWeek enum.
        if (dayOfWeek.Remove(7)) dayOfWeek.Add(0);

        return minute.Contains(now.Minute)
            && hour.Contains(now.Hour)
            && dayOfMonth.Contains(now.Day)
            && month.Contains(now.Month)
            && dayOfWeek.Contains((int)now.DayOfWeek);
    }

    private static HashSet<int>? ExpandField(string field, int min, int max)
    {
        var result = new HashSet<int>();
        foreach (var part in field.Split(','))
        {
            if (!ExpandPart(part.Trim(), min, max, result))
                return null;
        }
        return result;
    }

    private static bool ExpandPart(string part, int min, int max, HashSet<int> result)
    {
        if (part == "*")
        {
            for (var i = min; i <= max; i++) result.Add(i);
            return true;
        }

        if (part.StartsWith("*/", StringComparison.Ordinal))
        {
            if (!int.TryParse(part[2..], out var step) || step <= 0) return false;
            for (var i = min; i <= max; i += step) result.Add(i);
            return true;
        }

        var slashIdx = part.IndexOf('/');
        var rangeStr = slashIdx > 0 ? part[..slashIdx] : part;
        var stepSize = 1;

        if (slashIdx > 0)
        {
            if (!int.TryParse(part[(slashIdx + 1)..], out stepSize) || stepSize <= 0) return false;
        }

        var dashIdx = rangeStr.IndexOf('-');
        if (dashIdx > 0)
        {
            if (!int.TryParse(rangeStr[..dashIdx], out var lo) ||
                !int.TryParse(rangeStr[(dashIdx + 1)..], out var hi)) return false;
            if (lo < min || hi > max || lo > hi) return false;
            for (var i = lo; i <= hi; i += stepSize) result.Add(i);
            return true;
        }

        if (!int.TryParse(rangeStr, out var val)) return false;
        if (val < min || val > max) return false;
        result.Add(val);
        return true;
    }
}
