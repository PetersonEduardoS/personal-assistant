using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Deadlines;

/// <summary>
/// Gets a user's college deadlines from their course calendar (Moodle).
/// </summary>
public interface IDeadlineProvider
{
    /// <summary>
    /// Returns the deadlines due from <paramref name="fromDate"/> for the next
    /// <see cref="UpcomingDeadlines.HorizonDays"/> days, in Dublin time and in date order.
    /// A failure does not throw: it returns <see cref="UpcomingDeadlines.Unavailable"/>.
    /// </summary>
    Task<UpcomingDeadlines> GetUpcomingAsync(UserProfile user, DateOnly fromDate, CancellationToken cancellationToken = default);
}
