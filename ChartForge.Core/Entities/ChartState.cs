namespace ChartForge.Core.Entities;

public class ChartState
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public string ChartSourceCode { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    // --- Navigation Properties ---
    public int? ConversationId { get; set; }
    public Conversation Conversation { get; set; }
}