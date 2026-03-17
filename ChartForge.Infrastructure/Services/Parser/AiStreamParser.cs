using System.Text;
using System.Text.Json;
using ChartForge.Core.Models;

namespace ChartForge.Infrastructure.Services;

public class AiStreamParser
{
    private readonly Dictionary<WorkflowNodeType, INodeParser> parsers;
    private readonly StringBuilder jsonBuffer = new StringBuilder();
    private const string CLOSING_THOUGHT_TAG = "</THOUGHT>";

    public AiStreamParser()
    {
        parsers = new Dictionary<WorkflowNodeType, INodeParser>
        {
            { WorkflowNodeType.MainAgent, new MainAgentParser() },
            { WorkflowNodeType.ChartAgent, new ChartAgentParser() },
            { WorkflowNodeType.OutputNode, new OutputNodeParser() },
        };
    }

    public IEnumerable<StreamResult> ProcessChunk(string chunk, StreamParseState state)
    {
         if (TryParseJsonDocument(chunk, out var doc))
            {
                using (doc)
                    foreach(var element in GetElements(doc.RootElement))
                    {
                        UpdateActiveNode(element, state);
                        foreach (var result in RouteToNodeParser(element, state))
                            yield return result;
                    }
            }
            else
            {
                jsonBuffer.Append(chunk);

                if (TryParseJsonDocument(jsonBuffer.ToString(), out var bufferedDoc))
                {
                    jsonBuffer.Clear();
                    using (bufferedDoc)
                        foreach(var element in GetElements(bufferedDoc.RootElement))
                        {
                            UpdateActiveNode(element, state);
                            foreach (var result in RouteToNodeParser(element, state))
                                yield return result;
                        }
                }
            }
    }


    public IEnumerable<StreamResult> FlushBuffer(StreamParseState state)
    {
        if (jsonBuffer.Length <= 0) yield break;

        if (TryParseJsonDocument(jsonBuffer.ToString(), out var doc))
        {
            jsonBuffer.Clear();
            using (doc)
                foreach (var element in GetElements(doc.RootElement))
                {
                    UpdateActiveNode(element, state);
                    foreach (var r in RouteToNodeParser(element, state))
                        yield return r;
                }
        }

    }

    public IEnumerable<StreamResult> EndOfStreamFlush(StreamParseState state)
    {
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
            int thoughtEnd = raw.IndexOf(CLOSING_THOUGHT_TAG, StringComparison.OrdinalIgnoreCase);
            var fallback = thoughtEnd >= 0
                ? raw[(thoughtEnd + CLOSING_THOUGHT_TAG.Length)..].TrimStart('\n', '\r')
                : raw;
            if (!string.IsNullOrEmpty(fallback))
                yield return new StreamResult { AssistantChunk = fallback };
        }
    }

    public IEnumerable<StreamResult> RouteToNodeParser(JsonElement element, StreamParseState state)
    {
        if (!element.TryGetProperty("type", out var typeProp))
            yield break;
        
        var type = typeProp.GetString();

        if (type != "item")
            yield break;

        if (!element.TryGetProperty("content", out var contentProp))
            yield break;

        var content = contentProp.GetString();


        if (parsers.TryGetValue(state.ActiveNode, out var parser))
        {
            if (content is not null)
            {
                foreach (var r in parser.Parse(content, state))
                    yield return r;
            }
        }
        else
        {
            // unknown node
            yield break;
        }
    }

    private static void UpdateActiveNode(JsonElement element, StreamParseState state)
    {
        if (!element.TryGetProperty("type", out var typeProp))
            return;

        var type = typeProp.GetString();

        if (type == "end")
        {
            state.ActiveNode = WorkflowNodeType.Unknown;
            return;
        }

        if (type == "begin")
        {
            if (!element.TryGetProperty("metadata", out var metadata))
                return;

            if (!metadata.TryGetProperty("nodeName", out var nodeNameProp))
                return;

            var nodeName = nodeNameProp.GetString();
            if (nodeName != null)
                state.ActiveNode = MapNodeName(nodeName);
        }
    }

    private static IEnumerable<JsonElement> GetElements(JsonElement root) =>
    root.ValueKind switch
    {
        JsonValueKind.Array => root.EnumerateArray(),
        JsonValueKind.Object => new[] { root },
        _ => Enumerable.Empty<JsonElement>()
    };

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
}