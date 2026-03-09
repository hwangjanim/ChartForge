using ChartForge.Core.Entities;
using ChartForge.Core.Enums;

namespace ChartForge.Core.Entities;
public class ChartState
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public ChartLibrary ChartLibrary { get; set; }
    public string ChartSourceCode { get; set; } = string.Empty;
    // public string SummaryMetricsJson {  get; set; }
    // public string SuggestedActionsJson { get; set; }
    public string VersionLabel { get; set; }
    public DateTime CreatedAtUtc { get; set; } 

    // --- Navigation Properties ---
    public int? ConversationId { get; set; }
    public Conversation Conversation { get; set; }
    public int? MessageId { get; set; }
    public Message Message { get; set; }
}