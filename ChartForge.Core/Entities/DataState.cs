namespace ChartForge.Core.Entities;

public class DataState
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public string RawData { get; set; } = string.Empty;
    public string JsonData { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public int? ConversationId { get; set; }
    public Conversation Conversation { get; set; }
}
