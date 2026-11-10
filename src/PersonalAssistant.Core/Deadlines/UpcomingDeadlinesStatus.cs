namespace PersonalAssistant.Core.Deadlines;

public enum UpcomingDeadlinesStatus
{
    /// <summary>
    /// The user has no deadlines calendar link, so deadlines are not part of their brief.
    /// </summary>
    NotConfigured,

    /// <summary>
    /// The calendar could not be read, so the deadlines are unknown.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The calendar was read. An empty list means nothing is due soon.
    /// </summary>
    Available
}
