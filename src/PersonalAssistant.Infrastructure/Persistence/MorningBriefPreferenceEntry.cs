namespace PersonalAssistant.Infrastructure.Persistence;

/// <summary>The daily brief choice a person made in the chat. One row per person.</summary>
public sealed class MorningBriefPreferenceEntry
{
    public string PhoneNumber { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    /// <summary>The brief time in Dublin, or null when the brief is off.</summary>
    public TimeOnly? Time { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
