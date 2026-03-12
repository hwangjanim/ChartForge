using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;

namespace ChartForge.Web.Services;

public interface ISqlExecutionService
{
    Task<IEnumerable<IDictionary<string, object>>> ExecuteQueryAsync(string sql);
}

public class SqlExecutionService : ISqlExecutionService
{
    private readonly string _connectionString;
    private readonly ILogger<SqlExecutionService> _logger;

    public SqlExecutionService(IConfiguration configuration, ILogger<SqlExecutionService> logger)
    {
        // Use a dedicated "ReadOnly" connection string from your appsettings.json
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Default connection string not found.");
        _logger = logger;
    }

    public async Task<IEnumerable<IDictionary<string, object>>> ExecuteQueryAsync(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return Enumerable.Empty<IDictionary<string, object>>();

        // Basic Safety Check: Block destructive keywords
        string[] forbidden = { "DROP ", "DELETE ", "UPDATE ", "INSERT ", "TRUNCATE ", "ALTER " };
        if (forbidden.Any(word => sql.Contains(word, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Blocked potentially malicious SQL query: {Sql}", sql);
            throw new UnauthorizedAccessException("Only SELECT queries are allowed.");
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Execute and return as a list of dynamic dictionaries
            var results = await connection.QueryAsync(sql);
            return results.Select(x => (IDictionary<string, object>)x);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Database error executing AI-generated SQL.");
            throw new Exception("The generated SQL was invalid. Please try a different prompt.", ex);
        }
    }
}