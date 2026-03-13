using System.Text.Json;
using ChartForge.Core.Models;

namespace ChartForge.Infrastructure.Services;

public class ChartAgentParser : INodeParser
{
    public IEnumerable<StreamResult> Parse(string content, StreamParseState state)
    {
        if (string.IsNullOrEmpty(content))
            yield break;

        state.ChartCodeBuilder.Append(content);
    }
}