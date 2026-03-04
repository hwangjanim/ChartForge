using ChartForge.Core.Entities;

public interface IConversationService
{
    Task<List<Conversation>> GetByUserIdAsync(int userId);
    Task<Conversation> CreateAsync(int userId, string title);
    Task RenameAsync(int conversationId, string newTitle);
    Task SoftDeleteAsync(int conversationId);
}