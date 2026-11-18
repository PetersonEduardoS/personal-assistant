namespace PersonalAssistant.Core.MorningBriefs;

/// <summary>Keeps each person's daily brief choice made in the chat.</summary>
public interface IMorningBriefPreferenceStore
{
    Task<MorningBriefPreference?> GetAsync(string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>Saves the choice, replacing any earlier one.</summary>
    Task SetAsync(string phoneNumber, MorningBriefPreference preference, CancellationToken cancellationToken = default);

    /// <summary>Removes the choice, so the hosting settings apply again.</summary>
    Task ClearAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
