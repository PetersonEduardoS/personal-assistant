namespace PersonalAssistant.Infrastructure.Tests.WorkSchedules;

/// <summary>
/// Builds small iCalendar feeds in the same format Findmyshift uses, with made-up shifts.
/// </summary>
internal static class CalendarFeeds
{
    private const string LineBreak = "\r\n";

    public static string Calendar(params string[] events)
    {
        string[] lines = ["BEGIN:VCALENDAR", "PRODID:-//Test//Shifts//EN", "VERSION:2.0", .. events, "END:VCALENDAR"];

        return string.Join(LineBreak, lines) + LineBreak;
    }

    public static string Event(params string[] properties)
    {
        string[] lines =
        [
            "BEGIN:VEVENT",
            $"UID:{Guid.NewGuid()}@test",
            "DTSTAMP:20261001T120000Z",
            .. properties,
            "STATUS:CONFIRMED",
            "END:VEVENT"
        ];

        return string.Join(LineBreak, lines);
    }

    public static string Shift(string startUtc, string endUtc, string summary = "BAR") =>
        Event($"DTSTART:{startUtc}", $"DTEND:{endUtc}", $"SUMMARY:{summary}");

    public static string DayOff(string date, string nextDate) =>
        Event($"DTSTART;VALUE=DATE:{date}", $"DTEND;VALUE=DATE:{nextDate}", "SUMMARY:OFF");
}
