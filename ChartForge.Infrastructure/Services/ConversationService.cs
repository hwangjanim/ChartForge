using ChartForge.Core.Entities;
using ChartForge.Core.Interfaces;
using ChartForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChartForge.Infrastructure.Services;

public class ConversationService : IConversationService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ConversationService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }


    public async Task<User> GetOrCreateUserAsync(string email, string displayName, string ssoSubjectId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is not null)
            return user;

        user = new User
        {
            SsoSubjectId = ssoSubjectId,
            DisplayName = displayName,
            Email = email,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            LastLoginAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<List<Conversation>> GetByUserIdAsync(int userId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Conversations
            .Where(c => c.UserId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .ToListAsync();
    }

    public async Task<Conversation?> GetByIdAsync(int conversationId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.SequenceNumber))
            .Include(c => c.ChartStates.OrderBy(cs => cs.VersionNumber))
            .Include(c => c.DataStates.OrderBy(ds => ds.VersionNumber))
            .FirstOrDefaultAsync(c => c.Id == conversationId);
    }

    public async Task<Conversation> CreateAsync(int userId, string title)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conversation = new Conversation
        {
            UserId = userId,
            Title = title,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return conversation;
    }

    public async Task RenameAsync(int conversationId, string newTitle)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conversation = await db.Conversations.FindAsync(conversationId);
        if (conversation is null) return;
        conversation.Title = newTitle;
        await db.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(int conversationId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conversation = await db.Conversations.FindAsync(conversationId);
        if (conversation is null) return;
        conversation.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    public async Task UpdateTimestampAsync(int conversationId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var conversation = await db.Conversations.FindAsync(conversationId);
        if (conversation is null) return;
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }


    public async Task AddMessageAsync(Message message)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        // Detach navigation properties so EF doesn't try to re-insert the parent entities.
        message.Conversation = null!;
        db.Messages.Add(message);
        await db.SaveChangesAsync();
    }

    public async Task AddChartStateAsync(ChartState chartState)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        // Detach navigation properties so EF doesn't try to re-insert the parent entities.
        chartState.Conversation = null!;
        db.ChartStates.Add(chartState);
        await db.SaveChangesAsync();
    }

    public async Task AddDataStateAsync(DataState dataState)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        dataState.Conversation = null!;
        db.DataStates.Add(dataState);
        await db.SaveChangesAsync();
    }
}
