namespace PersonalAssistant.Core.Users;

/// <summary>
/// A person who can talk to the assistant.
/// </summary>
public sealed class UserProfile
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ContextFilePath { get; set; }

    /// <summary>
    /// Link to the iCalendar feed with the user's work shifts (Findmyshift).
    /// It is a secret: anyone with the link can see the shifts.
    /// </summary>
    public string? ShiftsCalendarUrl { get; set; }

    /// <summary>
    /// Link to the iCalendar export of the user's college calendar (Moodle), with assignment deadlines.
    /// It is a secret: anyone with the link can see the calendar.
    /// </summary>
    public string? DeadlinesCalendarUrl { get; set; }

    /// <summary>Daily brief time in Dublin. Null disables automatic briefs for this user.</summary>
    public TimeOnly? MorningBriefTime { get; set; }

    /// <summary>Private structured routine. When set, computes the brief time instead of MorningBriefTime.</summary>
    public string? MorningBriefRoutineFilePath { get; set; }
}
