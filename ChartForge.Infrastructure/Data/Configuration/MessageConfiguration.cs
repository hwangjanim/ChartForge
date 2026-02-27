using ChartForge.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChartForge.Infrastructure.Data.Configuration;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        // --- Table ---
        builder.ToTable("Messages");

        // --- Primary Key ---
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever();
        
        // --- Properties ---
        builder.Property(m => m.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
        
        builder.Property(m => m.ChartStateId)
            .IsRequired(false);

        builder.Property(m => m.SentAtUtc)
            .IsRequired()
            .HasColumnType("datetime2");

        builder.Property(m => m.SequenceNumber)
            .IsRequired();

        // --- Indexes ---
        builder.HasIndex(m => new { m.ConversationId, m.SequenceNumber })
            .IsUnique();

        // --- Relationship ---
        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(m => m.ChartState)
            .WithOne(cs => cs.Message)
            .HasForeignKey<Message>(m => m.ChartStateId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

    }
}