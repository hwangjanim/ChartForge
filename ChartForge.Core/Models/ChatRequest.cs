namespace ChartForge.Core.Models;

public class ChatRequest
{
    public int ConversationId { get; set; }
    public string UserPrompt { get; set; }
    public string? CurrentChartCode { get; set; }
    public IReadOnlyList<MessageContext> History { get; set; }
    public string DataSchema { get; set; }
}