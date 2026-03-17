using System.Text.Json;
using System.Text.RegularExpressions;
using ChartForge.Core.Models;

namespace ChartForge.Infrastructure.Services;

public class MainAgentParser : INodeParser
{
    private const string CLOSING_THOUGHT_TAG = "</THOUGHT>";
    private static readonly Regex TitleRegex =
        new(@"<TITLE>(.*?)</TITLE>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    
    private static readonly Regex SqlTagPattern = new Regex(@"<SQL>(.*?)</SQL>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Fallback: matches ```sql ... ``` when the LLM uses backtick fences instead of <SQL> tags
    private static readonly Regex BacktickSqlPattern = new(
        @"```sql\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IEnumerable<StreamResult> Parse(string content, StreamParseState state)
    {
        if (string.IsNullOrEmpty(content))
            yield break;

        // MainAgent events are used solely for <TITLE> extraction.
        // All content (pre-tool intermediate text and post-tool response) is suppressed here;
        // the OutputNode is the authoritative source for conversational content.
        if (!state.TitleExtracted)
        {
            state.ThoughtBuffer.Append(content);
            var buffered = state.ThoughtBuffer.ToString();

            int thoughtEnd = buffered.IndexOf(CLOSING_THOUGHT_TAG, StringComparison.OrdinalIgnoreCase);
            if (thoughtEnd < 0)
                yield break; // Still buffering — keep going.

            // Extract SQL from the thought buffer if present (<SQL> tags first, backtick fallback).
            var sqlMatch = SqlTagPattern.Match(buffered);
            if (sqlMatch.Success)
                yield return new StreamResult { SqlQuery = sqlMatch.Groups[1].Value.Trim() };
            else
            {
                var backtickMatch = BacktickSqlPattern.Match(buffered);
                if (backtickMatch.Success)
                    yield return new StreamResult { SqlQuery = backtickMatch.Groups[1].Value.Trim() };
            }



            var titleMatch = TitleRegex.Match(buffered[..thoughtEnd]);
            if (titleMatch.Success)
                yield return new StreamResult { ConversationTitle = titleMatch.Groups[1].Value.Trim() };

            state.TitleExtracted = true;
        }
        state.ThoughtBuffer.Clear();
        yield break;
    }
}