using System.Text.RegularExpressions;

namespace ChartForge.Web.Services;

public static class SqlParser
{
    private static readonly Regex SqlTagPattern = new Regex(@"<SQL>(.*?)<\/SQL>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Fallback: matches ```sql ... ``` when the LLM uses backtick fences instead of <SQL> tags
    private static readonly Regex BacktickSqlPattern = new(
        @"```sql\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? ExtractSql(string sql)
    {
        if (string.IsNullOrEmpty(sql))
            return null;

        var match = SqlTagPattern.Match(sql);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Fallback: try backtick-fenced SQL
        var backtickMatch = BacktickSqlPattern.Match(sql);
        return backtickMatch.Success ? backtickMatch.Groups[1].Value.Trim() : null;
    }
}