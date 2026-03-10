namespace ChartForge.Web.Services;

public static class DataSummaryService
{
    /// <summary>
    /// Returns the header row plus up to <paramref name="maxRows"/> data rows from a CSV string.
    /// Safe to call with null, empty, or single-line input.
    /// </summary>
    public static string GetCsvSummary(string rawCsv, int maxRows = 3)
    {
        if (string.IsNullOrWhiteSpace(rawCsv))
            return string.Empty;

        // Read only as many lines as we need — no full-string split.
        var lines = new List<string>(maxRows + 1);
        var reader = new StringReader(rawCsv);

        string? line;
        while (lines.Count <= maxRows && (line = reader.ReadLine()) is not null)
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }
}
