using ChartForge.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using System.Configuration;

namespace ChartForge.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<ChartState> ChartStates { get; set; }
    public DbSet<DataState> DataStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User -> Conversations (1:many)
        modelBuilder.Entity<Conversation>()
            .HasOne(c => c.User)
            .WithMany(u => u.Conversations)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Conversation -> Messages (1:many)
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Conversation -> ChartStates (1:many)
        modelBuilder.Entity<ChartState>()
            .HasOne(cs => cs.Conversation)
            .WithMany(c => c.ChartStates)
            .HasForeignKey(cs => cs.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Conversation -> DataStates (1:many)
        modelBuilder.Entity<DataState>()
            .HasOne(ds => ds.Conversation)
            .WithMany(c => c.DataStates)
            .HasForeignKey(ds => ds.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
