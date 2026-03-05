using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ChartForge.Core.Enums;
using ChartForge.Core.Interfaces;
using ChartForge.Core.Models;
using Microsoft.Extensions.Configuration;

namespace ChartForge.Infrastructure.Services;

public class N8nChatStreamService : IChatStreamService
{
    private readonly HttpClient httpClient;

    public N8nChatStreamService(HttpClient httpClient) 
    {
        this.httpClient = httpClient;
    }

    public async IAsyncEnumerable<StreamResult> StreamChatAsync(ChatRequest chatRequest, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // insert payload stuff
        var payload = new
        {
            chatInput = chatRequest.UserPrompt,
            currentCode = chatRequest.CurrentChartCode,
            chatHistory = chatRequest.History,
            dataSchema = ""
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "") { Content = content };
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

        // we use a string builder cuz apparently concatenating to a string is more expensive
        // since it creates a new copy of the string everytime
        // a StringBuilder on the other hand just appends characters to an internal buffer/dynamic buffer 
        // and reallocating only when the buffer is null
        var chartCodeBuilder = new StringBuilder();

        // actual streaming process and parsing happens here
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // yield return line;


            string chunk = line;
            Console.WriteLine("RAW: " + chunk);
            using var doc = JsonDocument.Parse(chunk);
            var root = doc.RootElement;

            // since sometimes the n8n sends back a json array
            // if its an Array, EnumerateArray() lets us loop over each element inside
            // if its an Object, we still want it to be loopable so we wrap it in a single-element array
            // if its neither (e.g string, number or null) we skip it
            IEnumerable<JsonElement> elements = root.ValueKind switch
            {
                JsonValueKind.Array => root.EnumerateArray(),
                JsonValueKind.Object => new[] { root },

                // TODO: maybe try to handle receiving neither array or object
                _ => Enumerable.Empty<JsonElement>()
            };

            foreach (var element in elements)
            {
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
                else if (type == "end" && nodeName is not null)
                {
                    activeNode = WorkflowNodeType.Unknown;
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
                        {
                            yield return new StreamResult
                            {

                                AssistantChunk = contentStr
                            };
                        }
                    } 
                    else if (activeNode == WorkflowNodeType.ChartAgent)
                    {
                        // build the code here
                        chartCodeBuilder.Append(contentStr);
                    }
                }

            }


        }
            // after end of stream we return the final code that was built
            if (chartCodeBuilder.Length > 0)
            {
                yield return new StreamResult
                {
                    FinalChartCode = chartCodeBuilder.ToString()
                };
            }



    }



    private WorkflowNodeType MapNodeName(string nodeName)
    {
        
        return nodeName switch
        {
            string n when n.Contains("Main Agent", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.MainAgent,
            string n when n.Contains("Output", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.OutputNode,
            string n when n.Contains("Charts.js", StringComparison.OrdinalIgnoreCase) => WorkflowNodeType.ChartAgent,
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
