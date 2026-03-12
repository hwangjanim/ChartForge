namespace ChartForge.Core.Models;

public class ChatRequest
{
    public int ConversationId { get; set; }
    public string UserPrompt { get; set; }
    public string? CurrentChartCode { get; set; }
    public string? CurrentData { get; set; }
    public IReadOnlyList<MessageContext> History { get; set; }
    public string DataSchema { get; set; }
}

public class StreamResult
{
    public string? AssistantChunk { get; set; }
    public string? FinalChartCode { get; set; }
    public string? FinalData { get; set; }
    public string? ConversationTitle { get; set; }
    public string? SqlQuery { get; set; }
}