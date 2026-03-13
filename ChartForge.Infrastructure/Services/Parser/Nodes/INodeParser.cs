using System.Text.Json;
using ChartForge.Core.Models;

namespace ChartForge.Infrastructure.Services;

public interface INodeParser
{
    IEnumerable<StreamResult> Parse(string content, StreamParseState state);
}