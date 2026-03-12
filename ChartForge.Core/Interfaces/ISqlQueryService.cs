namespace ChartForge.Core.Interfaces;

public interface ISqlQueryService
{
    Task<IEnumerable<IDictionary<string, object?>>> ExecuteQueryAsync(string sql);
    Task<string> GetSchemaAsync();
}