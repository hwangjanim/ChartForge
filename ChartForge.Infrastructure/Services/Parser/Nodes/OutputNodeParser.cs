using System.Text.Json;
using System.Text.RegularExpressions;
using ChartForge.Core.Models;

namespace ChartForge.Infrastructure.Services;

public class OutputNodeParser : INodeParser
{
    private const string OPENING_THOUGHT_TAG = "<THOUGHT>";
    private const string CLOSING_THOUGHT_TAG = "</THOUGHT>";
    private const string OPENING_SQL_TAG = "<SQL>";
    private const string CLOSING_SQL_TAG = "</SQL>";

    // Fallback: matches ```sql ... ``` when the LLM uses backtick fences instead of <SQL> tags
    private static readonly Regex BacktickSqlPattern = new(
        @"```sql\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IEnumerable<StreamResult> Parse(string content, StreamParseState state)
    {
        if (string.IsNullOrEmpty(content))
            yield break;

        state.OutputBuffer.Append(content);
        var buffered = state.OutputBuffer.ToString();

        if (!state.OutputThoughtClosed)
        {
            if (buffered.StartsWith(OPENING_THOUGHT_TAG, StringComparison.OrdinalIgnoreCase))
            {
                // OutputNode also has a thought block — buffer until it closes.
                int thoughtEnd = buffered.IndexOf(CLOSING_THOUGHT_TAG, StringComparison.OrdinalIgnoreCase);
                if (thoughtEnd < 0) 
                    yield break;

                var afterThought = buffered[(thoughtEnd + CLOSING_THOUGHT_TAG.Length)..].TrimStart('\n', '\r');

                // After thought, SQL may follow — keep buffering until we know.
                if (afterThought.Length == 0)
                    yield break; // More content needed to determine what follows.

                if (afterThought.StartsWith(OPENING_SQL_TAG, StringComparison.OrdinalIgnoreCase))
                {
                    int sqlEnd = afterThought.IndexOf(CLOSING_SQL_TAG, StringComparison.OrdinalIgnoreCase);
                    if (sqlEnd < 0)
                        yield break; // SQL tag opened but not closed — keep buffering.

                    var sqlContent = afterThought[OPENING_SQL_TAG.Length..sqlEnd].Trim();
                    yield return new StreamResult { SqlQuery = sqlContent };
                    state.SqlClosed = true;
                    afterThought = afterThought[(sqlEnd + CLOSING_SQL_TAG.Length)..].TrimStart('\n', '\r');
                }
                else
                {
                    // Fallback: try ```sql ... ``` backtick fences
                    var backtickMatch = BacktickSqlPattern.Match(afterThought);
                    if (backtickMatch.Success)
                    {
                        yield return new StreamResult { SqlQuery = backtickMatch.Groups[1].Value.Trim() };
                        state.SqlClosed = true;
                        afterThought = afterThought[(backtickMatch.Index + backtickMatch.Length)..].TrimStart('\n', '\r');
                    }
                }

                state.OutputThoughtClosed = true;
                foreach (var r in ProcessOutputContent(afterThought, state))
                    yield return r;
            }
            else if (buffered.StartsWith(OPENING_SQL_TAG, StringComparison.OrdinalIgnoreCase))
            {
                int sqlEnd = buffered.IndexOf(CLOSING_SQL_TAG, StringComparison.OrdinalIgnoreCase);
                if (sqlEnd < 0) 
                    yield break;

                // Extract and yield the SQL query for execution by the caller.
                var sqlContent = buffered[OPENING_SQL_TAG.Length..sqlEnd].Trim();
                yield return new StreamResult { SqlQuery = sqlContent };

                state.SqlClosed = true;
                var afterSql = buffered[(sqlEnd + CLOSING_SQL_TAG.Length)..].TrimStart('\n', '\r');
                foreach (var r in ProcessOutputContent(afterSql, state))
                    yield return r;
            }
            else
            {
                // No THOUGHT block — check for backtick-fenced SQL before treating as content.
                var backtickMatch = BacktickSqlPattern.Match(buffered);
                if (backtickMatch.Success)
                {
                    yield return new StreamResult { SqlQuery = backtickMatch.Groups[1].Value.Trim() };
                    state.SqlClosed = true;
                    var afterSql = buffered[(backtickMatch.Index + backtickMatch.Length)..].TrimStart('\n', '\r');
                    state.OutputThoughtClosed = true;
                    foreach (var r in ProcessOutputContent(afterSql, state))
                        yield return r;
                }
                else
                {
                    state.OutputThoughtClosed = true;
                    foreach (var r in ProcessOutputContent(buffered, state))
                        yield return r;
                }
            }
            state.OutputBuffer.Clear();
        }

        if (!state.SqlClosed)
        {
            if (buffered.StartsWith(OPENING_SQL_TAG, StringComparison.OrdinalIgnoreCase))
            {
                int sqlEnd = buffered.IndexOf(CLOSING_SQL_TAG, StringComparison.OrdinalIgnoreCase);
                if (sqlEnd < 0)
                    yield break;

                var sqlContent = buffered[OPENING_SQL_TAG.Length..sqlEnd].Trim();
                yield return new StreamResult { SqlQuery = sqlContent };

                state.SqlClosed = true;
                var afterSql = buffered[(sqlEnd + CLOSING_SQL_TAG.Length)..].TrimStart('\n', '\r');
                foreach (var r in ProcessOutputContent(afterSql, state))
                    yield return r;
            }
            else
            {
                // Fallback: try backtick-fenced SQL
                var backtickMatch = BacktickSqlPattern.Match(buffered);
                if (backtickMatch.Success)
                {
                    yield return new StreamResult { SqlQuery = backtickMatch.Groups[1].Value.Trim() };
                    state.SqlClosed = true;
                    var afterSql = buffered[(backtickMatch.Index + backtickMatch.Length)..].TrimStart('\n', '\r');
                    foreach (var r in ProcessOutputContent(afterSql, state))
                        yield return r;
                }
            }
        }
    }

    private static IEnumerable<StreamResult> ProcessOutputContent(string text, StreamParseState state)
    {
        const string openTag = "<DATA>";
        const string closeTag = "</DATA>";
        int pos = 0;

        while (pos <= text.Length)
        {
            if (state.DataBlockActive)
            {
                int closeIdx = text.IndexOf(closeTag, pos, StringComparison.OrdinalIgnoreCase);
                if (closeIdx < 0)
                {
                    // Still inside the DATA block — buffer the rest of this chunk.
                    state.DataBuffer.Append(text[pos..]);
                    yield break;
                }

                // Closing tag found — emit the complete data payload.
                state.DataBuffer.Append(text[pos..closeIdx]);
                state.DataBlockActive = false;
                yield return new StreamResult { FinalData = state.DataBuffer.ToString().Trim() };
                state.DataBuffer.Clear();
                pos = closeIdx + closeTag.Length;
            }
            else
            {
                int openIdx = text.IndexOf(openTag, pos, StringComparison.OrdinalIgnoreCase);
                if (openIdx < 0)
                {
                    // No DATA block in this chunk — pass remainder to the code-fence filter.
                    foreach (var r in FilterCodeBlocks(text[pos..], state))
                        yield return r;
                    yield break;
                }

                // Yield text before the opening tag as normal assistant content.
                if (openIdx > pos)
                    foreach (var r in FilterCodeBlocks(text[pos..openIdx], state))
                        yield return r;

                state.DataBlockActive = true;
                pos = openIdx + openTag.Length;
            }
        }
    }

    /// <summary>
    /// Yields only the non-code portions of <paramref name="text"/>, splitting on ``` fences.
    /// Correctly handles fences mid-chunk and preserves <see cref="StreamParseState.IsInsideCodeBlock"/>
    /// across calls so multi-chunk streaming code blocks are suppressed end-to-end.
    /// </summary>
    private static IEnumerable<StreamResult> FilterCodeBlocks(string text, StreamParseState state)
    {
        const string fence = "```";
        const string startingHtmlTag = "<!DOCTYPE html>";
        const string endingHtmlTag = "</html>";
        int pos = 0;
        
        if (text.IndexOf(startingHtmlTag, StringComparison.OrdinalIgnoreCase) >= 0)
        {

            while (pos < text.Length)
            {
                if (!state.IsInsideCodeBlock)
                {
                    int startIdx = text.IndexOf(startingHtmlTag, pos, StringComparison.OrdinalIgnoreCase);

                    if (startIdx < 0)
                    {
                        yield return new StreamResult { AssistantChunk = text[pos..] };
                        yield break;
                    }

                    if (startIdx > pos)
                    {
                        yield return new StreamResult { AssistantChunk = text[pos..startIdx] };
                    }

                    state.IsInsideCodeBlock = true;
                    pos = startIdx;
                }
                else
                {
                    int endIdx = text.IndexOf(endingHtmlTag, pos, StringComparison.OrdinalIgnoreCase);

                    if (endIdx < 0)
                    {
                        yield break;
                    }

                    int htmlEnd = endIdx + endingHtmlTag.Length;

                    state.IsInsideCodeBlock = false;
                    pos = htmlEnd;

                }
            }

        } 
        else
        {
            while (pos < text.Length)
            {
                int fenceIdx = text.IndexOf(fence, pos, StringComparison.Ordinal);

                if (fenceIdx < 0)
                {
                    // No more fences — yield the remainder if outside a code block.
                    if (!state.IsInsideCodeBlock && pos < text.Length)
                        yield return new StreamResult { AssistantChunk = text[pos..] };
                    break;
                }

                // Yield text before the fence if outside a code block.
                if (!state.IsInsideCodeBlock && fenceIdx > pos)
                    yield return new StreamResult { AssistantChunk = text[pos..fenceIdx] };

                state.IsInsideCodeBlock = !state.IsInsideCodeBlock;
                pos = fenceIdx + fence.Length;
            }
        }

    }
}