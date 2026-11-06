namespace PersonalAssistant.Core.WorkSchedules;

/// <summary>
/// A work shift on the day of the brief. When the end is not after the start, the shift ends the next day.
/// </summary>
public sealed record WorkShift(TimeOnly Start, TimeOnly End, string Title)
{
    public bool EndsNextDay => End <= Start;
}
