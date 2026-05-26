using System.Text.RegularExpressions;

namespace Topolactor.Runtime.Tests;

internal static class SsotYamlContractReader
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Repository root (AGENTS.md) not found.");
        }
    }

    internal static string ReadDoc(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    internal static IReadOnlyList<string> ReadInlineArray(string relativePath, string key)
    {
        var text = ReadDoc(relativePath);
        var m = Regex.Match(text, $@"^{Regex.Escape(key)}:\s*\[(.*?)\]\s*$", RegexOptions.Multiline);
        if (!m.Success) return [];
        return m.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.Trim())
            .ToArray();
    }

    internal static bool ContainsLiteral(string relativePath, string literal) =>
        ReadDoc(relativePath).Contains(literal, StringComparison.Ordinal);

    internal static bool RoadmapEntryExists(string entryKey)
    {
        var text = ReadDoc("docs/system-roadmap.yaml");
        return Regex.IsMatch(text, @"^\s{4}" + Regex.Escape(entryKey) + @":\s*$", RegexOptions.Multiline);
    }

    internal static IReadOnlyList<string> RoadmapCompletionConditions(string entryKey)
    {
        var lines = ReadDoc("docs/system-roadmap.yaml").Split('\n');
        var entryLine = Array.FindIndex(lines, l => Regex.IsMatch(l, @"^\s{4}" + Regex.Escape(entryKey) + @":\s*$"));
        if (entryLine < 0) return [];
        var ccLine = -1;
        for (var i = entryLine + 1; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"^\s{4}\S")) break;
            if (Regex.IsMatch(lines[i], @"^\s{6}completion_condition:\s*$"))
            {
                ccLine = i;
                break;
            }
        }
        if (ccLine < 0) return [];

        var values = new List<string>();
        for (var i = ccLine + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (Regex.IsMatch(line, @"^\s{6}\S")) break;
            var m = Regex.Match(line, @"^\s{8}-\s+(.+?)\s*$");
            if (m.Success) values.Add(m.Groups[1].Value.Trim());
        }
        return values;
    }
}
