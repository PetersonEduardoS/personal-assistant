using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WorkSchedules;
using PersonalAssistant.Infrastructure.Calendars;

namespace PersonalAssistant.Infrastructure.WorkSchedules;

/// <summary>
/// Reads a user's work shifts from the iCalendar feed of their scheduling app (Findmyshift).
/// Shift times are converted to the users' time zone, including summer time.
/// </summary>
public sealed class CalendarFeedWorkScheduleProvider(CachedCalendarFeed feed, TimeZoneInfo timeZone) : IWorkScheduleProvider
{
    public async Task<WorkSchedule> GetScheduleAsync(
        UserProfile user,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.ShiftsCalendarUrl))
        {
            return WorkSchedule.NotConfigured;
        }

        var events = await feed.GetEventsAsync(user.ShiftsCalendarUrl, $"work schedule of {user.Name}", cancellationToken);

        if (events is null)
        {
            return WorkSchedule.Unavailable;
        }

        var shifts = events
            .Select(calendarEvent => (
                calendarEvent.Summary,
                Start: TimeZoneInfo.ConvertTime(calendarEvent.Start, timeZone),
                End: TimeZoneInfo.ConvertTime(calendarEvent.End, timeZone)))
            .Where(calendarEvent => DateOnly.FromDateTime(calendarEvent.Start.DateTime) == date)
            .OrderBy(calendarEvent => calendarEvent.Start)
            .Select(calendarEvent => new WorkShift(
                TimeOnly.FromDateTime(calendarEvent.Start.DateTime),
                TimeOnly.FromDateTime(calendarEvent.End.DateTime),
                calendarEvent.Summary))
            .ToList();

        return WorkSchedule.WithShifts(shifts);
    }
}
