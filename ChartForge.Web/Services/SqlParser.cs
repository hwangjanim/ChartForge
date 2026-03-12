using System.Text.RegularExpressions;

namespace ChartForge.Web.Services;

public static class SqlParser
{
    private static readonly Regex SqlTagPattern = new Regex(@"<SQL>(.*?)<\/SQL>", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? ExtractSql(string sql)
    {
        if (string.IsNullOrEmpty(sql))
            return null;

        var match = SqlTagPattern.Match(sql);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}