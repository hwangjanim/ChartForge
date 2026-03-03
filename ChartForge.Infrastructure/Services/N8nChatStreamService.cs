using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ChartForge.Core.Entities.Features.Chat;
using ChartForge.Core.Enums;
using Microsoft.Extensions.Configuration;

namespace ChartForge.Infrastructure.Services;

public class N8nChatStreamService : IChatStreamService
{
    private readonly HttpClient httpClient;

    public N8nChatStreamService(HttpClient httpClient) 
    {
        this.httpClient = httpClient;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(ChatRequest chatRequest, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var payload = new
        {
            // insert payload stuff
            message = chatRequest.UserPrompt,
            chartCode = chatRequest.CurrentChartCode,
            history = chatRequest.History
        };

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "") { Content = JsonContent.Create(payload)};
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        // send req and get response
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        // TODO: decide if we want to try catch and handle the exception here or let it bubble up
        response.EnsureSuccessStatusCode();

        // get streamer and reader
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        WorkflowNodeType activeNode = WorkflowNodeType.Unknown;
        bool isInsideCodeBlock = false;

        // actual streaming process and parsing happens here
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            yield return line;

            string chunk = line;
            using var doc = JsonDocument.Parse(chunk);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
                continue;

            var type = typeProp.GetString();

            if (!root.TryGetProperty("metadata", out var metadata))
                continue;

            var nodeName = metadata.GetProperty("nodeName").GetString();
            if (type == "begin" && nodeName is not null)
            {
                activeNode = MapNodeName(nodeName);
            }
                


            if (activeNode == WorkflowNodeType.Unknown)
                continue;

            if (type == "item" && root.TryGetProperty("content", out var contentProp))
            {
                var contentStr = contentProp.GetString();
                if (string.IsNullOrEmpty(contentStr))
                    continue;
                
                if (activeNode == WorkflowNodeType.MainAgent)
                {
                    // do something with main agent
                    if (contentStr.Contains("```"))
                    {
                        isInsideCodeBlock = !isInsideCodeBlock;
                        continue;
                    }

                    if (!isInsideCodeBlock)
                        yield return $"{contentStr}";
                } 
                else if (activeNode == WorkflowNodeType.ChartAgent)
                {
                    // just print the code from the chart agent
                    yield return $"{contentStr}";
                }
            }
        }

    }


    private WorkflowNodeType MapNodeName(string nodeName)
    {
        
        return nodeName switch
        {
            string n when n.Contains("Main Agent", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.MainAgent,
            string n when n.Contains("Output", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.OutputNode,
            string n when n.Contains("Chart.js", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.ChartAgent,
            string n when n.Contains("ECharts", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.ChartAgent,
            string n when n.Contains("D3", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.ChartAgent,
            string n when n.Contains("Highcharts", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.ChartAgent,
            _ => WorkflowNodeType.Unknown
            
        };
    }
}

public enum WorkflowNodeType
{
    Unknown,
    MainAgent,
    ChartAgent,
    OutputNode,

}