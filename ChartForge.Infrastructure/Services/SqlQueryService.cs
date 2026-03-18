using System.Text;
using System.Text.RegularExpressions;
using ChartForge.Core.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ChartForge.Infrastructure.Services;

public class SqlQueryService : ISqlQueryService
{
    private readonly string ConnectionString;

    public SqlQueryService(IConfiguration configuration)
    {
        ConnectionString = configuration.GetConnectionString("SecondDefault")!;
    }
    public async Task<IEnumerable<IDictionary<string, object?>>> ExecuteQueryAsync(string sql)
    {
        // only accept SELECT Queries bish
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only SELECT Queries are permitted");

        await using SqlConnection conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        sql = await NormalizeSqlIdentifiers(sql, conn);

        var result = await conn.QueryAsync(sql);
        return result.Select(row => (IDictionary<string, object?>)row).ToList();
    }

    /// <summary>
    /// Normalizes bracketed SQL identifiers like [Vendor License] to match the actual
    /// DB column names (e.g. [VendorLicense]) using a fuzzy key (lowercase, no spaces/underscores).
    /// This compensates for the AI sometimes generating column names with spaces.
    /// </summary>
    private static async Task<string> NormalizeSqlIdentifiers(string sql, SqlConnection conn)
    {
        var allColumns = await conn.QueryAsync<string>(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS");

        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in allColumns)
        {
            var key = col.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            lookup.TryAdd(key, col);
        }

        // Replace [Any Bracketed Identifier] with the exact DB column name if a fuzzy match is found.
        // e.g. [Vendor License] â†’ key "vendorlicense" â†’ matches VendorLicense â†’ [VendorLicense]
        return Regex.Replace(sql, @"\[([^\]]+)\]", match =>
        {
            var inner = match.Groups[1].Value;
            var key = inner.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            return lookup.TryGetValue(key, out var exact) ? $"[{exact}]" : match.Value;
        });
    }

    public async Task<string> GetSchemaAsync()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        var rows = await conn.QueryAsync<(string Table, string Column, string Type)>(
            @"SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
              FROM INFORMATION_SCHEMA.COLUMNS
              ORDER BY TABLE_NAME, ORDINAL_POSITION");

        var sb = new StringBuilder(
            "IMPORTANT: The following are the EXACT SQL column names. Use them verbatim â€” do NOT add spaces, change casing, or invent new names.\n" +
            "Always wrap table and column names in square brackets, e.g. SELECT [ColumnName] FROM [TableName].\n\n");

        foreach (var table in rows.GroupBy(r => r.Table))
        {
            sb.AppendLine($"TableName: [{table.Key}]");
            foreach (var col in table)
                sb.AppendLine($"  - [{col.Column}] ({col.Type})");
        }

        return sb.ToString().TrimEnd();
    }
}
