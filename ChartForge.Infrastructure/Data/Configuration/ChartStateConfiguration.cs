using ChartForge.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChartForge.Infrastructure.Data.Configuration;

public class ChartStateConfiguration : IEntityTypeConfiguration<ChartState>
{
    public void Configure(EntityTypeBuilder<ChartState> builder)
    {
        // --- Table ---
        builder.ToTable("ChartStates");

        // --- Primary Key ---
        builder.HasKey(cs => cs.Id);

        
        builder.Property(cs => cs.Id)
            .ValueGeneratedNever();
        
        // --- Properties ---
        builder.Property(cs => cs.VersionNumber)
            .IsRequired();

        builder.Property(cs => cs.ChartLibrary)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        
        builder.Property(cs => cs.ChartSourceCode)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(cs => cs.SummaryMetricsJson)
            .IsRequired(false)
            .HasColumnType("nvarchar(max)");

        builder.Property(cs => cs.SuggestedActionsJson)
            .IsRequired(false)
            .HasColumnType("nvarchar(max)");
        
        builder.Property(cs => cs.VersionLabel)
            .IsRequired(false)
            .HasMaxLength(300);

        builder.Property(cs => cs.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2");

        // --- Indexes ---
        builder.HasIndex(cs => new { cs.ConversationId, cs.VersionNumber })
            .IsUnique();

        builder.HasIndex(cs => cs.ConversationId);

        // --- Relationships ---
        builder.HasOne(cs => cs.Conversation)
            .WithMany(c => c.ChartStates)
            .HasForeignKey(c => c.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(cs => cs.Message)
            .WithOne(m => m.ChartState)
            .HasForeignKey<Message>(m => m.ChartStateId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
    }
}