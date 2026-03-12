using Microsoft.AspNetCore.Mvc;
using ChartForge.Web.Services;

namespace ChartForge.Web.Controllers;

[ApiController]
[Route("api/[controller]")] // This makes the URL: api/n8n
public class N8nController : ControllerBase
{
    private readonly ISqlExecutionService _sqlService;

    // The constructor tells .NET to provide the SqlExecutionService automatically
    public N8nController(ISqlExecutionService sqlService)
    {
        _sqlService = sqlService;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessN8nResponse([FromBody] string rawN8nOutput)
    {
        // 1. Parse the tags using your static parser
        var (cleanText, title, csvData, sql) = ThoughtBlockParser.Parse(rawN8nOutput);

        IEnumerable<IDictionary<string, object>>? queryResults = null;

        // 2. If SQL exists, run it through our safe service
        if (!string.IsNullOrEmpty(sql))
        {
            try 
            {
                queryResults = await _sqlService.ExecuteQueryAsync(sql);
            }
            catch (Exception ex)
            {
                // If the SQL fails, we still want to return the text, but maybe with an error note
                return BadRequest(new { error = ex.Message, partialText = cleanText });
            }
        }

        // 3. Return everything to your frontend
        return Ok(new 
        {
            Message = cleanText,
            Title = title,
            RawData = csvData,
            TableData = queryResults 
        });
    }
}