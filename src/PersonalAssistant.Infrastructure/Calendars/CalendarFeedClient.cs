namespace PersonalAssistant.Infrastructure.Calendars;

/// <summary>
/// Downloads and reads an iCalendar feed. Failures are thrown; <see cref="CachedCalendarFeed"/> decides what to do.
/// </summary>
public sealed class CalendarFeedClient(HttpClient httpClient)
{
    private const string WebcalScheme = "webcal://";

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(string url, CancellationToken cancellationToken = default)
    {
        var calendar = await httpClient.GetStringAsync(ToHttps(url), cancellationToken);

        return CalendarFeedReader.Read(calendar);
    }

    private static string ToHttps(string url) =>
        url.StartsWith(WebcalScheme, StringComparison.OrdinalIgnoreCase)
            ? "https://" + url[WebcalScheme.Length..]
            : url;
}
