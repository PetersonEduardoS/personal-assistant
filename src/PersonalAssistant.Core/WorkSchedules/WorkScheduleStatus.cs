namespace PersonalAssistant.Core.WorkSchedules;

public enum WorkScheduleStatus
{
    /// <summary>
    /// The user has no work schedule link, so shifts are not part of their brief.
    /// </summary>
    NotConfigured,

    /// <summary>
    /// The schedule could not be read, so it is unknown whether the user works that day.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The schedule was read. An empty list of shifts means a day off.
    /// </summary>
    Available
}
