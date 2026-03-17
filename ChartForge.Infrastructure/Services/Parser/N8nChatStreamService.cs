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
            csvPreview = chatRequest.CurrentData ?? "",
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
        var parser = new AiStreamParser();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrEmpty(line))
            {
                foreach (var result in parser.FlushBuffer(state))
                    yield return result;

                continue;
            }

            string chunk = line.StartsWith("data: ", StringComparison.Ordinal)
                ? line["data: ".Length..]
                : line;

            if (chunk == "[DONE]")
                break;

            // only enable for testing
            // Console.WriteLine("RAW: " + chunk);

            foreach (var result in parser.ProcessChunk(chunk, state))
                yield return result;
        }

        foreach (var r in parser.EndOfStreamFlush(state))
            yield return r;
    }
}


