using PersonalAssistant.Core.Deadlines;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Infrastructure.Calendars;

namespace PersonalAssistant.Infrastructure.Deadlines;

/// <summary>
/// Reads a user's college deadlines from the iCalendar export of their Moodle calendar.
/// Times are converted to the users' time zone, including summer time.
/// </summary>
public sealed class CalendarFeedDeadlineProvider(CachedCalendarFeed feed, TimeZoneInfo timeZone) : IDeadlineProvider
{
    private static readonly char[] CategorySeparators = ['|', ','];

    public async Task<UpcomingDeadlines> GetUpcomingAsync(
        UserProfile user,
        DateOnly fromDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.DeadlinesCalendarUrl))
        {
            return UpcomingDeadlines.NotConfigured;
        }

        var events = await feed.GetEventsAsync(
            user.DeadlinesCalendarUrl,
            $"deadlines calendar of {user.Name}",
            cancellationToken);

        if (events is null)
        {
            return UpcomingDeadlines.Unavailable;
        }

        var untilDate = fromDate.AddDays(UpcomingDeadlines.HorizonDays);

        var deadlines = events
            .Select(calendarEvent => new Deadline(
                calendarEvent.Summary,
                CourseFrom(calendarEvent.Categories),
                TimeZoneInfo.ConvertTime(calendarEvent.Start, timeZone)))
            .Where(deadline =>
            {
                var dueDate = DateOnly.FromDateTime(deadline.DueAt.DateTime);

                return dueDate >= fromDate && dueDate < untilDate;
            })
            .OrderBy(deadline => deadline.DueAt)
            .ToList();

        return UpcomingDeadlines.WithDeadlines(deadlines);
    }

    /// <summary>
    /// Moodle lists the program, the module code and the course name; the course name comes last.
    /// </summary>
    private static string? CourseFrom(string categories)
    {
        var parts = categories.Split(CategorySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? null : parts[^1];
    }
}
