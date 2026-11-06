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
}
