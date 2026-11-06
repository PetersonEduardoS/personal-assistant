using Microsoft.Extensions.Logging;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WorkSchedules;

namespace PersonalAssistant.Infrastructure.WorkSchedules;

/// <summary>
/// Reads a user's work shifts from the iCalendar feed of their scheduling app (Findmyshift).
/// Shift times are converted to the users' time zone, including summer time.
/// </summary>
public sealed class CalendarFeedWorkScheduleProvider(
    HttpClient httpClient,
    TimeZoneInfo timeZone,
    ILogger<CalendarFeedWorkScheduleProvider> logger) : IWorkScheduleProvider
{
    private const string WebcalScheme = "webcal://";

    public async Task<WorkSchedule> GetScheduleAsync(
        UserProfile user,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.ShiftsCalendarUrl))
        {
            return WorkSchedule.NotConfigured;
        }

        try
        {
            var calendar = await httpClient.GetStringAsync(ToHttps(user.ShiftsCalendarUrl), cancellationToken);

            var shifts = CalendarFeedReader.Read(calendar)
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
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The link is a secret, so it is never written to the logs.
            logger.LogWarning(exception, "Could not read the work schedule of {UserName}", user.Name);

            return WorkSchedule.Unavailable;
        }
    }

    private static string ToHttps(string url) =>
        url.StartsWith(WebcalScheme, StringComparison.OrdinalIgnoreCase)
            ? "https://" + url[WebcalScheme.Length..]
            : url;
}
