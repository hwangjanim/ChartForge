using ChartForge.Core.Enums;

namespace ChartForge.Core.Entities;

public class Message
{
    public int Id { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? ChartStateId { get; set; }
    public DateTime SentAtUtc { get; set; }
    public int SequenceNumber { get; set; }

    // --- Navigation Properties ---
    public int? ConversationId { get; set; }
    public Conversation Conversation { get; set; }
    public ChartState? ChartState { get; set; }
}