using Microsoft.EntityFrameworkCore;

namespace PersonalAssistant.Infrastructure.Persistence;

public sealed class AssistantDbContext(DbContextOptions<AssistantDbContext> options) : DbContext(options)
{
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    public DbSet<MorningBriefEntry> MorningBriefs => Set<MorningBriefEntry>();

    public DbSet<AppointmentEntry> Appointments => Set<AppointmentEntry>();

    public DbSet<NoteEntry> Notes => Set<NoteEntry>();

    public DbSet<ShoppingItemEntry> ShoppingItems => Set<ShoppingItemEntry>();

    public DbSet<ReceiptEntry> Receipts => Set<ReceiptEntry>();

    public DbSet<MorningBriefPreferenceEntry> MorningBriefPreferences => Set<MorningBriefPreferenceEntry>();

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

            entity.Property(appointment => appointment.Household).HasMaxLength(64);
            entity.Property(appointment => appointment.CreatedBy).HasMaxLength(100);

            entity.HasIndex(appointment => new { appointment.PhoneNumber, appointment.Date });
            entity.HasIndex(appointment => new { appointment.Household, appointment.Date });
        });

        modelBuilder.Entity<NoteEntry>(entity =>
        {
            entity.ToTable("Notes");
            entity.HasKey(note => note.Id);

            entity.Property(note => note.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(note => note.Content)
                .HasMaxLength(PersonalAssistant.Core.Notes.UserNote.MaxContentLength)
                .IsRequired();

            entity.HasIndex(note => note.PhoneNumber);
        });

        modelBuilder.Entity<ReceiptEntry>(entity =>
        {
            entity.ToTable("Receipts");
            entity.HasKey(receipt => receipt.Id);

            entity.Property(receipt => receipt.Household).HasMaxLength(64).IsRequired();
            entity.Property(receipt => receipt.Store).HasMaxLength(100).IsRequired();
            entity.Property(receipt => receipt.AddedBy).HasMaxLength(100).IsRequired();

            entity.HasMany(receipt => receipt.Lines)
                .WithOne()
                .HasForeignKey(line => line.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(receipt => new { receipt.Household, receipt.Date });
        });

        modelBuilder.Entity<ReceiptLineEntry>(entity =>
        {
            entity.ToTable("ReceiptLines");
            entity.HasKey(line => line.Id);

            entity.Property(line => line.ReceiptName).HasMaxLength(100).IsRequired();
            entity.Property(line => line.FriendlyName).HasMaxLength(100).IsRequired();
            entity.Property(line => line.Unit).HasMaxLength(8).IsRequired();
        });

        modelBuilder.Entity<ShoppingItemEntry>(entity =>
        {
            entity.ToTable("ShoppingItems");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Household).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Name)
                .HasMaxLength(PersonalAssistant.Core.Shopping.ShoppingItem.MaxNameLength)
                .IsRequired();
            entity.Property(item => item.Quantity)
                .HasMaxLength(PersonalAssistant.Core.Shopping.ShoppingItem.MaxQuantityLength);
            entity.Property(item => item.Section).HasMaxLength(32).IsRequired();
            entity.Property(item => item.AddedBy).HasMaxLength(100).IsRequired();

            entity.HasIndex(item => new { item.Household, item.BoughtAtUtc });
        });

        modelBuilder.Entity<MorningBriefPreferenceEntry>(entity =>
        {
            entity.ToTable("MorningBriefPreferences");
            entity.HasKey(preference => preference.PhoneNumber);

            entity.Property(preference => preference.PhoneNumber).HasMaxLength(32);
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
