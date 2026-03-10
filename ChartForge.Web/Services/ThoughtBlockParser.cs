using System.Text.RegularExpressions;

namespace ChartForge.Web.Services;

public static class ThoughtBlockParser
{
    private static readonly Regex ThoughtBlockRegex =
        new(@"<THOUGHT>.*?</THOUGHT>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex TitleRegex =
        new(@"<TITLE>(.*?)</TITLE>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>
    /// Strips the &lt;THOUGHT&gt;...&lt;/THOUGHT&gt; block from raw AI content and
    /// extracts the optional &lt;TITLE&gt; contained within it.
    /// Returns the cleaned content and the title (null if not present).
    /// </summary>
    public static (string CleanContent, string? Title) Parse(string raw)
    {
        var thoughtMatch = ThoughtBlockRegex.Match(raw);

        string? title = null;
        if (thoughtMatch.Success)
        {
            var titleMatch = TitleRegex.Match(thoughtMatch.Value);
            if (titleMatch.Success)
                title = titleMatch.Groups[1].Value.Trim();
        }

        var cleaned = ThoughtBlockRegex.Replace(raw, "").Trim();
        return (cleaned, title);
    }
}
