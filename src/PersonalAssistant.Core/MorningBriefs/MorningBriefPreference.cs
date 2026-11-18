namespace PersonalAssistant.Core.MorningBriefs;

/// <summary>
/// What a person chose in the chat for their daily brief: a fixed time, or no brief at all.
/// When there is no choice, the hosting settings (fixed time or routine) apply.
/// A time chosen in the chat wins over the routine.
/// </summary>
public sealed record MorningBriefPreference(bool Enabled, TimeOnly? Time)
{
    public static readonly TimeOnly EarliestTime = new(5, 0);

    public static readonly TimeOnly LatestTime = new(12, 0);

    public static MorningBriefPreference Off { get; } = new(false, null);

    public static MorningBriefPreference At(TimeOnly time) => new(true, time);
}
