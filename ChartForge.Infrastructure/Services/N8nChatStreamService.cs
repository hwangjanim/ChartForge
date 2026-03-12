using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChartForge.Core.Interfaces;
using ChartForge.Core.Models;

namespace ChartForge.Infrastructure.Services;

public class N8nChatStreamService : IChatStreamService
{
    private readonly HttpClient _httpClient;

    private const string OPENING_THOUGHT_TAG = "<THOUGHT>";
    private const string CLOSING_THOUGHT_TAG = "</THOUGHT>";
    private const string OPENING_SQL_TAG = "<SQL>";
    private const string CLOSING_SQL_TAG = "</SQL>";

    public N8nChatStreamService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<StreamResult> StreamChatAsync(
        ChatRequest chatRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var payload = new
        {
            chatInput = chatRequest.UserPrompt,
            currentCode = chatRequest.CurrentChartCode,
            chatHistory = chatRequest.History,
            dataSchema = chatRequest.CurrentData ?? "",
            dbSchema = chatRequest.DataSchema ?? ""
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, "") { Content = content };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var state = new StreamParseState();
        var jsonBuffer = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            // Blank line = SSE event boundary; attempt to flush accumulated buffer.
            if (string.IsNullOrWhiteSpace(line))
            {
                if (jsonBuffer.Length > 0)
                {
                    var buffered = jsonBuffer.ToString();
                    jsonBuffer.Clear();

                    foreach (var result in ParseAndProcess(buffered, state))
                        yield return result;
                }
                continue;
            }

            string chunk = line.StartsWith("data: ", StringComparison.Ordinal)
                ? line["data: ".Length..]
                : line;

            if (chunk == "[DONE]")
                break;

            Console.WriteLine("RAW: " + chunk);

            if (TryParseJsonDocument(chunk, out var doc))
            {
                using (doc)
                    foreach (var result in ProcessDocument(doc!.RootElement, state))
                        yield return result;
            }
            else
            {
                Console.WriteLine("Added chunk to buffer: ", chunk);
                jsonBuffer.Append(chunk);

                if (TryParseJsonDocument(jsonBuffer.ToString(), out var bufferedDoc))
                {
                    jsonBuffer.Clear();
                    using (bufferedDoc)
                        foreach (var result in ProcessDocument(bufferedDoc!.RootElement, state))
                            yield return result;
                }
            }
        }

        if (state.ChartCodeBuilder.Length > 0)
            yield return new StreamResult { FinalChartCode = state.ChartCodeBuilder.ToString() };

        // Flush any incomplete DATA block buffered at end-of-stream.
        if (state.DataBlockActive && state.DataBuffer.Length > 0)
            yield return new StreamResult { FinalData = state.DataBuffer.ToString().Trim() };

        // Fallback: if the OutputNode never fired (workflow without output node),
        // surface whatever the MainAgent accumulated after the THOUGHT block.
        if (!state.OutputThoughtClosed && state.ThoughtBuffer.Length > 0)
        {
            var raw = state.ThoughtBuffer.ToString();
            int thoughtEnd = raw.IndexOf("</THOUGHT>", StringComparison.OrdinalIgnoreCase);
            var fallback = thoughtEnd >= 0
                ? raw[(thoughtEnd + "</THOUGHT>".Length)..].TrimStart('\n', '\r')
                : raw;
            if (!string.IsNullOrEmpty(fallback))
                yield return new StreamResult { AssistantChunk = fallback };
        }
    }


    private static bool TryParseJsonDocument(string text, out JsonDocument? doc)
    {
        try
        {
            doc = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            doc = null;
            return false;
        }
    }

    private static IEnumerable<StreamResult> ParseAndProcess(string text, StreamParseState state)
    {
        Console.WriteLine($"Parse and processing");
        if (!TryParseJsonDocument(text, out var doc))
            yield break;

        using (doc)
            foreach (var result in ProcessDocument(doc!.RootElement, state))
                yield return result;
    }

    private static IEnumerable<StreamResult> ProcessDocument(JsonElement root, StreamParseState state)
    {
        IEnumerable<JsonElement> elements = root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray(),
            JsonValueKind.Object => new[] { root },
            _ => Enumerable.Empty<JsonElement>()
        };

        bool IsDoneThought = false;

        foreach (var element in elements)
        {
            if (!element.TryGetProperty("type", out var typeProp))
                continue;

            var type = typeProp.GetString();

            if (!element.TryGetProperty("metadata", out var metadata))
                continue;

            if (!metadata.TryGetProperty("nodeName", out var nodeNameProp))
                continue;

            var nodeName = nodeNameProp.GetString();

            if (type == "begin" && nodeName is not null)
                state.ActiveNode = MapNodeName(nodeName);
            else if (type == "end")
                state.ActiveNode = WorkflowNodeType.Unknown;

            if (state.ActiveNode == WorkflowNodeType.Unknown)
                continue;

            if (type == "item" && element.TryGetProperty("content", out var contentProp))
            {
                var contentStr = contentProp.GetString();
                if (string.IsNullOrEmpty(contentStr))
                    continue;

                if (state.ActiveNode == WorkflowNodeType.MainAgent)
                {
                    // MainAgent events are used solely for <TITLE> extraction.
                    // All content (pre-tool intermediate text and post-tool response) is suppressed here;
                    // the OutputNode is the authoritative source for conversational content.
                    if (!state.TitleExtracted)
                    {
                        string? buffered = "";
                        int thoughtEnd = 0;
                        if (!IsDoneThought)
                        {
                            state.ThoughtBuffer.Append(contentStr);
                            buffered = state.ThoughtBuffer.ToString();

                            thoughtEnd = buffered.IndexOf("</THOUGHT>", StringComparison.OrdinalIgnoreCase);
                            if (thoughtEnd < 0)
                                continue; // Still buffering — keep going.

                            // done thoughting
                            IsDoneThought = true;
                        }


                        // Extract SQL from the thought buffer if present.
                        var sqlMatch = SqlTagPattern.Match(buffered);
                        if (sqlMatch.Success)
                            yield return new StreamResult { SqlQuery = sqlMatch.Groups[1].Value.Trim() };

                        state.TitleExtracted = true;


                        var titleMatch = TitleRegex.Match(buffered[..thoughtEnd]);
                        if (titleMatch.Success)
                            yield return new StreamResult { ConversationTitle = titleMatch.Groups[1].Value.Trim() };
                    }

                    // Always suppress MainAgent content — do not yield AssistantChunk.
                }
                else if (state.ActiveNode == WorkflowNodeType.OutputNode)
                {
                    // OutputNode carries the clean final response.
                    // Apply a defensive THOUGHT filter in case the model echoes its reasoning here too.
                    if (!state.OutputThoughtClosed && !state.SqlClosed)
                    {
                        state.OutputBuffer.Append(contentStr);
                        var buffered = state.OutputBuffer.ToString().TrimStart();

                        if (buffered.StartsWith("<THOUGHT>", StringComparison.OrdinalIgnoreCase))
                        {
                            // OutputNode also has a thought block — buffer until it closes.
                            int thoughtEnd = buffered.IndexOf("</THOUGHT>", StringComparison.OrdinalIgnoreCase);
                            if (thoughtEnd < 0) continue;

                            var afterThought = buffered[(thoughtEnd + "</THOUGHT>".Length)..].TrimStart('\n', '\r');

                            // After thought, SQL may follow — keep buffering until we know.
                            if (afterThought.Length == 0)
                                continue; // More content needed to determine what follows.

                            if (afterThought.StartsWith(OPENING_SQL_TAG, StringComparison.OrdinalIgnoreCase))
                            {
                                int sqlEnd = afterThought.IndexOf(CLOSING_SQL_TAG, StringComparison.OrdinalIgnoreCase);
                                if (sqlEnd < 0) continue; // SQL tag opened but not closed — keep buffering.

                                var sqlContent = afterThought[OPENING_SQL_TAG.Length..sqlEnd].Trim();
                                yield return new StreamResult { SqlQuery = sqlContent };
                                state.SqlClosed = true;
                                afterThought = afterThought[(sqlEnd + CLOSING_SQL_TAG.Length)..].TrimStart('\n', '\r');
                            }

                            state.OutputThoughtClosed = true;
                            foreach (var r in ProcessOutputContent(afterThought, state))
                                yield return r;
                        }
                        else if (buffered.StartsWith(OPENING_SQL_TAG, StringComparison.OrdinalIgnoreCase))
                        {
                            int sqlEnd = buffered.IndexOf(CLOSING_SQL_TAG, StringComparison.OrdinalIgnoreCase);
                            if (sqlEnd < 0) continue;

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
                            // No THOUGHT block — flush the buffer through the content pipeline.
                            state.OutputThoughtClosed = true;
                            foreach (var r in ProcessOutputContent(buffered, state))
                                yield return r;
                        }
                    }
                    else
                    {
                        foreach (var r in ProcessOutputContent(contentStr, state))
                            yield return r;
                    }
                }
                else if (state.ActiveNode == WorkflowNodeType.ChartAgent)
                {
                    state.ChartCodeBuilder.Append(contentStr);
                }
            }
        }
    }

    /// <summary>
    /// Routes OutputNode content through DATA-block extraction first, then through the
    /// code-fence filter. DATA block content is yielded as <see cref="StreamResult.FinalData"/>;
    /// surrounding text is passed to <see cref="FilterCodeBlocks"/>.
    /// Handles DATA blocks that span multiple streaming chunks via <see cref="StreamParseState"/>.
    /// </summary>
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
        int pos = 0;

        while (pos <= text.Length)
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

    private static WorkflowNodeType MapNodeName(string nodeName) =>
        nodeName switch
        {
            string n when n.Contains("Main Agent", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.MainAgent,
            string n when n.Contains("AI Agent", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.MainAgent,
            string n when n.Contains("Output", StringComparison.OrdinalIgnoreCase)     => WorkflowNodeType.OutputNode,
            string n when n.Contains("Charts.js", StringComparison.OrdinalIgnoreCase)  => WorkflowNodeType.ChartAgent,
            string n when n.Contains("chartjs", StringComparison.OrdinalIgnoreCase)    => WorkflowNodeType.ChartAgent,
            string n when n.Contains("chartsjs", StringComparison.OrdinalIgnoreCase)   => WorkflowNodeType.ChartAgent,
            string n when n.Contains("chart.js", StringComparison.OrdinalIgnoreCase)   => WorkflowNodeType.ChartAgent,
            string n when n.Contains("ECharts", StringComparison.OrdinalIgnoreCase)    => WorkflowNodeType.ChartAgent,
            string n when n.Contains("D3", StringComparison.OrdinalIgnoreCase)         => WorkflowNodeType.ChartAgent,
            string n when n.Contains("Highcharts", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.ChartAgent,
            _ => WorkflowNodeType.Unknown
        };


    // Matches <TITLE>...</TITLE> inside the thought block (case-insensitive, single-line).
    private static readonly Regex TitleRegex =
        new(@"<TITLE>(.*?)</TITLE>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    
    private static readonly Regex SqlTagPattern = new Regex(@"<SQL>(.*?)<\/SQL>", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed class StreamParseState
    {
        public WorkflowNodeType ActiveNode { get; set; } = WorkflowNodeType.Unknown;
        public bool IsInsideCodeBlock { get; set; }
        public StringBuilder ChartCodeBuilder { get; } = new();
        public StringBuilder SqlBuffer { get; } = new StringBuilder();

        // MainAgent: buffer THOUGHT block to extract <TITLE>; content is always suppressed.
        public StringBuilder ThoughtBuffer { get; } = new();
        public bool TitleExtracted { get; set; }

        // OutputNode: defensive THOUGHT filter + clean content streaming.
        public StringBuilder OutputBuffer { get; } = new();
        public bool OutputThoughtClosed { get; set; }
        public bool SqlClosed { get; set; }

        // DATA block detection across chunks.
        public StringBuilder DataBuffer { get; } = new();
        public bool DataBlockActive { get; set; }
    }
}

public enum WorkflowNodeType
{
    Unknown,
    MainAgent,
    ChartAgent,
    OutputNode,
}
