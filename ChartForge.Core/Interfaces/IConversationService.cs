using ChartForge.Core.Entities;

namespace ChartForge.Core.Interfaces;

public interface IConversationService
{
    // User
    Task<User> GetOrCreateUserAsync(string email, string displayName, string ssoSubjectId);

    // Conversations
    Task<List<Conversation>> GetByUserIdAsync(int userId);
    Task<Conversation?> GetByIdAsync(int conversationId);
    Task<Conversation> CreateAsync(int userId, string title);
    Task RenameAsync(int conversationId, string newTitle);
    Task SoftDeleteAsync(int conversationId);
    Task UpdateTimestampAsync(int conversationId);

    // Messages & ChartStates
    Task AddMessageAsync(Message message);
    Task AddChartStateAsync(ChartState chartState);
}
