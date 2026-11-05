namespace PersonalAssistant.Core.MorningBriefs;

/// <summary>
/// Keeps the morning briefs, at most one per user per day.
/// </summary>
public interface IMorningBriefStore
{
    Task<MorningBrief?> GetAsync(string phoneNumber, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new undelivered brief. Returns false when the user already has one for that day,
    /// so a restart in the morning does not send the brief twice.
    /// </summary>
    Task<bool> TryAddAsync(
        string phoneNumber,
        DateOnly date,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the brief was delivered. Returns false when there is no brief for that day
    /// or it was already delivered, so two quick replies do not deliver it twice.
    /// </summary>
    Task<bool> TryMarkDeliveredAsync(string phoneNumber, DateOnly date, CancellationToken cancellationToken = default);
}
