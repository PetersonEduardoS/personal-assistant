using Microsoft.EntityFrameworkCore;

namespace PersonalAssistant.Infrastructure.Persistence;

public sealed class AssistantDbContext(DbContextOptions<AssistantDbContext> options) : DbContext(options)
{
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    public DbSet<MorningBriefEntry> MorningBriefs => Set<MorningBriefEntry>();

    public DbSet<AppointmentEntry> Appointments => Set<AppointmentEntry>();

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

        modelBuilder.Entity<AppointmentEntry>(entity =>
        {
            entity.ToTable("Appointments");
            entity.HasKey(appointment => appointment.Id);

            entity.Property(appointment => appointment.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(appointment => appointment.Title)
                .HasMaxLength(PersonalAssistant.Core.Appointments.Appointment.MaxTitleLength)
                .IsRequired();

            entity.HasIndex(appointment => new { appointment.PhoneNumber, appointment.Date });
        });

        modelBuilder.Entity<MorningBriefEntry>(entity =>
        {
            entity.ToTable("MorningBriefs");
            entity.HasKey(brief => brief.Id);

            entity.Property(brief => brief.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(brief => brief.Content).IsRequired();

            entity.HasIndex(brief => new { brief.PhoneNumber, brief.Date }).IsUnique();
        });
    }
}
