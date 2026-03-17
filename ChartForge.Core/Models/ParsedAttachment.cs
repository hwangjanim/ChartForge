namespace ChartForge.Core.Models;

public record ParsedAttachment(string FileName, List<Dictionary<string, object?>> Data);