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
        var result = new List<Dictionary<string, object>>();

        // max size is 10mb
        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration()
        {
            HasHeaderRecord = true,
            WillThrowOnMissingField = false,
            CultureInfo = CultureInfo.InvariantCulture,
        });

        // get the headers;

        



    }
}
