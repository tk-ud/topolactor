using System.Runtime.CompilerServices;
using System.Text;

namespace Topolactor.Tests.Shared;

/// <summary>
/// Schema-composed proof-chain continuity round 2 (2026-09-07): extracted from
/// Topolactor.Runtime.Tests.LayoutSchemaStructuralCompositionTests' own private RepoRoot/
/// ExtractSqlJsonLiteral helpers, which Topolactor.Integration.Tests.
/// TeamDashboardUiBuilderCanonicalApplyPipelineLiveDbTests then re-typed verbatim (same repo-root
/// discovery walk, same single-quoted-SQL-string-literal extraction with the SAME ''-escaping
/// rule) because the two test assemblies carry no ProjectReference between them and cannot share
/// a private per-class method. Compiled into BOTH test projects via each csproj's own existing
/// `&lt;Compile Include&gt; Link=...` mechanism (the SAME linked-compile pattern each csproj
/// already uses to pull in shared PRODUCTION source from backend/schema, backend/runtime, etc.) --
/// one physical implementation, not two independently-maintained copies of the same parsing rule.
/// </summary>
internal static class SqlSeedLiteralTestSupport
{
    /// <summary>
    /// Walks up from the calling file's own directory (falling back to the current working
    /// directory, then AppContext.BaseDirectory) until db/seed_empty.sql is found. Callers must
    /// NOT pass sourceFile explicitly -- the compiler supplies it via [CallerFilePath], which
    /// resolves to the CALLING file's own path (not this shared file's), so the walk starts from
    /// wherever the test class that invoked this actually lives.
    /// </summary>
    public static string RepoRoot([CallerFilePath] string sourceFile = "")
    {
        var fromSource = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "..", ".."));
        if (File.Exists(Path.Combine(fromSource, "db", "seed_empty.sql"))) return fromSource;
        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "db", "seed_empty.sql"))) return cwd;
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "db", "seed_empty.sql")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    /// <summary>
    /// Extracts a single-quoted SQL string literal's own content starting at `quoteStart` (the
    /// index of its OPENING quote character), honoring SQL's '' -&gt; ' escaping and tracking
    /// brace depth so a `}` inside the JSON payload never causes a false-early stop before the
    /// literal's own real closing quote.
    /// </summary>
    public static string ExtractSqlJsonLiteral(string sql, int quoteStart)
    {
        var i = quoteStart + 1;
        var sb = new StringBuilder();
        var depth = 0;
        var started = false;
        while (i < sql.Length)
        {
            var ch = sql[i];
            if (ch == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
            {
                sb.Append('\'');
                i += 2;
                continue;
            }
            if (ch == '\'') break;
            if (ch == '{') { depth++; started = true; }
            else if (ch == '}') depth--;
            sb.Append(ch);
            i++;
            if (started && depth == 0) break;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extracts a Postgres dollar-quoted ($$...$$) literal's own content, starting the search for
    /// the opening $$ at or after `afterIdx`. Distinct quoting style from ExtractSqlJsonLiteral
    /// above (db/seed_empty.sql uses this for its own tensor rows, chosen there for the
    /// embedded double-quote-heavy JSON) -- a genuinely different extraction rule, not a
    /// duplicate of the single-quote one.
    /// </summary>
    public static string ExtractSqlDollarQuotedJsonLiteral(string sql, int afterIdx)
    {
        var dollarStart = sql.IndexOf("$$", afterIdx, StringComparison.Ordinal);
        if (dollarStart < 0) throw new InvalidOperationException("expected a $$-quoted literal after the given index");
        var contentStart = dollarStart + 2;
        var dollarEnd = sql.IndexOf("$$", contentStart, StringComparison.Ordinal);
        if (dollarEnd < 0) throw new InvalidOperationException("expected a closing $$ for the dollar-quoted literal");
        return sql.Substring(contentStart, dollarEnd - contentStart).Trim();
    }
}
