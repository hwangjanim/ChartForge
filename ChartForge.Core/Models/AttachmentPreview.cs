namespace ChartForge.Core.Models;

public class AttachmentPreview
{
    public string Name { get; init; } 
    public bool IsImage { get; init; }

    public bool IsParsing { get; set; }
    public string? ParseError { get; set; }
    public List<Dictionary<string, object?>>? ParsedData { get; set; }
}