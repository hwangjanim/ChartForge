using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public class ModelContextProtocolService
{
    //[McpServerTool, Description("Gets the songs from the server.")]
    //public async Task<List<Song>> GetSongs(IServiceScopeFactory scopeFactory,
    //    [Description("Pagination take for the fetch")] int take,
    //    [Description("Pagination skip for the fetch")] int skip)
    //{
    //    try
    //    {
    //        using var scope = scopeFactory.CreateScope();
    //        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

    //        return await context.Songs
    //            .Take(take)
    //            .Skip(skip)
    //            .OrderBy(x => x.id)
    //            .ToListAsync();
    //    }
    //    catch (Exception ex)
    //    {
    //        return null;
    //    }
    }