namespace PersonalAssistant.Core.MorningBriefs;

/// <summary>
/// The morning brief prepared for one user on one day.
/// It stays undelivered while the assistant waits for the user to answer the template.
/// </summary>
public sealed record MorningBrief(string PhoneNumber, DateOnly Date, string Content, DateTimeOffset? DeliveredAt)
{
    public bool IsDelivered => DeliveredAt is not null;
}
