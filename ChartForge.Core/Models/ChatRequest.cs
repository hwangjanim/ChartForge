namespace ChartForge.Core.Entities.Features.Chat;

public class ChatRequest
{
    public int ConversationId { get; set; }
    public string UserPrompt { get; set; }
    public string? CurrentChartCode { get; set; }
    public IReadOnlyList<MessageContext> History { get; set; }
}