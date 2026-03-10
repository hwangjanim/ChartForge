using System.Text.RegularExpressions;

namespace ChartForge.Web.Services;

public static class ThoughtBlockParser
{
    private static readonly Regex ThoughtBlockRegex =
        new(@"<THOUGHT>.*?</THOUGHT>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex TitleRegex =
        new(@"<TITLE>(.*?)</TITLE>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex DataBlockRegex =
        new(@"<DATA>(.*?)</DATA>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>
    /// Strips &lt;THOUGHT&gt; and &lt;DATA&gt; blocks from raw AI content, extracting
    /// the optional &lt;TITLE&gt; from within the thought block and the CSV from the data block.
    /// Acts as a post-processing safety net after streaming-layer filtering.
    /// </summary>
    public static (string CleanContent, string? Title, string? Data) Parse(string raw)
    {
        // Extract <TITLE> from inside the <THOUGHT> block.
        var thoughtMatch = ThoughtBlockRegex.Match(raw);
        string? title = null;
        if (thoughtMatch.Success)
        {
            var titleMatch = TitleRegex.Match(thoughtMatch.Value);
            if (titleMatch.Success)
                title = titleMatch.Groups[1].Value.Trim();
        }

        // Strip the <THOUGHT> block.
        var cleaned = ThoughtBlockRegex.Replace(raw, "").Trim();

        // Extract and strip the <DATA> block.
        var dataMatch = DataBlockRegex.Match(cleaned);
        string? data = null;
        if (dataMatch.Success)
        {
            data = dataMatch.Groups[1].Value.Trim();
            cleaned = DataBlockRegex.Replace(cleaned, "").Trim();
        }

        return (cleaned, title, data);
    }
}
