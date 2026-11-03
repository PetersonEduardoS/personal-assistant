using Microsoft.EntityFrameworkCore;

namespace PersonalAssistant.Infrastructure.Persistence;

public sealed class AssistantDbContext(DbContextOptions<AssistantDbContext> options) : DbContext(options)
{
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationMessage>(entity =>
        {
            entity.ToTable("ConversationMessages");
            entity.HasKey(message => message.Id);

            entity.Property(message => message.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(message => message.Role).HasConversion<string>().HasMaxLength(16);
            entity.Property(message => message.Content).IsRequired();
            entity.Property(message => message.WhatsAppMessageId).HasMaxLength(128);

            entity.HasIndex(message => message.WhatsAppMessageId).IsUnique();
            entity.HasIndex(message => new { message.PhoneNumber, message.Id });
        });
    }
}
