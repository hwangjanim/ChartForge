using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Components.Forms;

namespace ChartForge.Infrastructure.Services;

public class FileParserService
{
    public async Task<List<Dictionary<string, object?>>> ParseFileAsync(IBrowserFile file)
    {
        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        return ext switch
        {
            ".csv" => await ParseCsvAsync(file),
            // .xlsx files here
            _ => throw new NotSupportedException($"Unsupported file type: {ext}")
        };
    }

    private async Task<List<Dictionary<string, object?>>> ParseCsvAsync(IBrowserFile file)
    {
        var result = new List<Dictionary<string, object?>>();

        // max size is 10mb
        using var browserStream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        using var ms = new MemoryStream();
        await browserStream.CopyToAsync(ms);
        ms.Position = 0;

        using var reader = new StreamReader(ms);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,

        });

        // fk this shi
        // why tf is the heatmap csv uses spaces for the fking headers
        // yan tuloy di sya sync sa db IUSDUBHBJSBF
        // JUST KEEP THE "_"!!!!!!!!!
        // kasalanan mo to file upload
        await csv.ReadAsync();
        csv.ReadHeader();
        string[] headers = csv.HeaderRecord!.Select(h => h.Trim()).ToArray();

        while(csv.Read())
        {
            var row = new Dictionary<string, object?>();
            foreach (var header in headers)
            {
                var sanitizedKey = header.Replace(" ", "_");
                row[sanitizedKey] = TryCoerce(csv.GetField(header));
            }
            
            result.Add(row);
        }

        return result;
    }

    private static object? TryCoerce(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var num)) return num;
        if (bool.TryParse(value, out var b)) return b;
        return value;
    }
}
