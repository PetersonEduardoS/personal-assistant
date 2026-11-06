using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.WorkSchedules;

/// <summary>
/// Gets a user's work shifts from their scheduling app.
/// </summary>
public interface IWorkScheduleProvider
{
    /// <summary>
    /// Returns the shifts that start on that day, in Dublin time. A failure to read the schedule
    /// does not throw: it returns <see cref="WorkSchedule.Unavailable"/>, so the brief can still be sent.
    /// </summary>
    Task<WorkSchedule> GetScheduleAsync(UserProfile user, DateOnly date, CancellationToken cancellationToken = default);
}
