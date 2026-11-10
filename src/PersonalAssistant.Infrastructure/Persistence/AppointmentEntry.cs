namespace PersonalAssistant.Infrastructure.Persistence;

/// <summary>
/// A stored appointment. The date and time are in Dublin, as the user said them.
/// </summary>
public sealed class AppointmentEntry
{
    public long Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public TimeOnly Time { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
