using System.Runtime.CompilerServices;

namespace ChartForge.Core.Entities.Features.Chat;

public interface IChatStreamService
{
    public IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken);
}