namespace ChartForge.Web.Services;

public static class DataSummaryService
{
    public static string GetCsvSummary(string rawCsv, int maxRows = 3)
    {
        if (string.IsNullOrWhiteSpace(rawCsv))
            return string.Empty;

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
