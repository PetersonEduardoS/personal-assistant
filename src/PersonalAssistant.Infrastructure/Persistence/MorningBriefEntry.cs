namespace PersonalAssistant.Infrastructure.Persistence;

/// <summary>
/// A stored morning brief. The pair phone number and date is unique.
/// </summary>
public sealed class MorningBriefEntry
{
    public long Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// The day of the brief in Dublin time.
    /// </summary>
    public DateOnly Date { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Empty while the brief waits for the user to answer the template.
    /// </summary>
    public DateTime? DeliveredAtUtc { get; set; }
}
